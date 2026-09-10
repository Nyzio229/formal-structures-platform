using System.Diagnostics;
using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Common;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.LStar
{
    /// <summary>
    /// Implementacja algorytmu L* (Angluin) do nauki DFA na podstawie
    /// zapytań o przynależność (membership queries).
    ///
    /// W stosunku do poprzedniej wersji dodano dwa brakujące, kluczowe
    /// elementy prawdziwego L*:
    ///   1) sprawdzanie SPÓJNOŚCI tabeli (consistency check) — nie tylko
    ///      domkniętości (closedness),
    ///   2) pętlę ZAPYTAŃ O RÓWNOWAŻNOŚĆ (equivalence query) z przetwarzaniem
    ///      kontrprzykładu — bez niej algorytm akceptował pierwszą z brzegu
    ///      domkniętą tabelę, co przy zaszumionym oracle (LLM) prowadziło
    ///      do automatów z rażąco za małą liczbą stanów (np. 1 stan dla
    ///      języka opartego o długość mod 3).
    ///
    /// Ponieważ nie mamy dostępu do prawdziwego, wszechwiedzącego nauczyciela
    /// (teacher), rolę equivalence oracle pełni ten sam oracle, który
    /// odpowiada na membership queries (najczęściej LLM). To przybliżenie —
    /// nie gwarantuje matematycznej poprawności tak jak klasyczny L* — ale
    /// eliminuje najpoważniejszy błąd: całkowity brak weryfikacji hipotezy
    /// przed jej zwróceniem.
    /// </summary>
    public class LStarService
    {
        // ── Parametry algorytmu ──────────────────────────────────────────
        private const int MaxTableRefinementIterations = 30; // limit pętli closedness+consistency
        private const int MaxEquivalenceRounds = 3;           // ile rund szukania kontrprzykładu
        private const int EquivalenceSearchMaxWordLen = 5;    // do jakiej długości szukamy kontrprzykładów
        private const int EquivalenceMaxQueriesPerRound = 20; // limit zapytań do oracle na rundę (koszt/czas)
        private const int MaxSymbolLen = 25;                  // defensywny limit długości słowa w tabeli (ochrona przed patologicznym wzrostem, nie powinien być normalnie osiągany)

        // Twarde budżety chroniące przed spiralą wywołaną szumem oracle
        // (zaobserwowaną empirycznie: 21 stanów zamiast ~3-4 dla prostego
        // języka, oraz kompletne timeouty na 300s dla dwóch innych języków).
        // Bez tych limitów poprawny, "podręcznikowy" L* z niedoskonałym
        // nauczycielem (LLM) może wpaść w nieskończoną (lub bardzo długą)
        // pogoń za sprzecznościami, które w rzeczywistości są pomyłkami
        // oracle, a nie prawdziwą strukturą języka.
        private const int MaxStates = 12;
        private static readonly TimeSpan MaxTotalLearningTime = TimeSpan.FromSeconds(90);

        public async Task<FiniteAutomaton> LearnAsync(IAutomatonOracle oracle, List<string> alphabet)
        {
            var sw = Stopwatch.StartNew();

            if (alphabet == null || alphabet.Count == 0)
            {
                alphabet = await ExtractAlphabet(oracle);
            }

            Console.WriteLine($"[DEBUG] Alfabet: '{string.Join(",", alphabet)}'");

            var S = new List<string> { "" };   // prefiksy
            var E = new List<string> { "" };   // sufiksy (dystynktory)
            E.AddRange(alphabet);

            var table = new Dictionary<(string, string), bool>();

            await FillTable(oracle, S, E, alphabet, table, sw);
            await MakeClosedAndConsistent(oracle, S, E, alphabet, table, sw);

            FiniteAutomaton hypothesis = BuildAutomaton(S, E, alphabet, table);

            // ── Pętla equivalence query + przetwarzanie kontrprzykładu ──
            for (int round = 0; round < MaxEquivalenceRounds; round++)
            {
                if (BudgetExceeded(sw, S))
                {
                    Console.WriteLine($"[WARN] L*: budżet wyczerpany przed rundą {round} " +
                        $"equivalence query (czas={sw.Elapsed.TotalSeconds:F1}s, stanów={S.Count}) " +
                        "— zwracam bieżącą hipotezę.");
                    return hypothesis;
                }

                var counterexample = await FindCounterexampleAsync(oracle, hypothesis, alphabet, sw);

                if (counterexample == null)
                {
                    Console.WriteLine($"[DEBUG] L*: hipoteza zaakceptowana po rundzie {round} " +
                        $"(brak kontrprzykładu w zakresie testowym).");
                    return hypothesis;
                }

                Console.WriteLine($"[DEBUG] L*: znaleziono kontrprzykład '{counterexample}' " +
                    $"w rundzie {round} — dodaję prefiksy do S i uczę ponownie.");

                // Standardowe przetwarzanie kontrprzykładu w L*: wszystkie jego
                // prefiksy trafiają do S, co wymusza rozróżnienie stanów,
                // których poprzednia hipoteza błędnie nie odróżniała.
                foreach (var prefix in AllPrefixes(counterexample))
                {
                    if (!S.Contains(prefix))
                        S.Add(prefix);
                }

                await FillTable(oracle, S, E, alphabet, table, sw);
                await MakeClosedAndConsistent(oracle, S, E, alphabet, table, sw);

                var newHypothesis = BuildAutomaton(S, E, alphabet, table);

                // Jeżeli refinement nie zmienił liczby stanów, a mimo to
                // ciągle "znajdujemy" kontrprzykłady, to prawdopodobnie
                // gonimy szum oracle (nawet po głosowaniu większościowym),
                // a nie prawdziwą strukturę — kończymy zamiast bić się
                // w kółko ze sprzecznymi odpowiedziami i zjadać cały budżet
                // czasowy na weryfikację, która i tak się nie "domknie".
                if (newHypothesis.States.Count == hypothesis.States.Count)
                {
                    Console.WriteLine("[WARN] L*: liczba stanów nie zmieniła się mimo kontrprzykładu " +
                        "— prawdopodobny szum oracle, przerywam refinement.");
                    return newHypothesis;
                }

                hypothesis = newHypothesis;
            }

            Console.WriteLine("[WARN] L*: osiągnięto limit rund equivalence query — " +
                "zwracam ostatnią hipotezę (mogła nie zostać w pełni zweryfikowana).");
            return hypothesis;
        }

        private static bool BudgetExceeded(Stopwatch sw, List<string> S) =>
            sw.Elapsed >= MaxTotalLearningTime || S.Count >= MaxStates;

        private async Task<List<string>> ExtractAlphabet(IAutomatonOracle oracle)
        {
            if (oracle is LlmOracle llmOracle)
            {
                var alphabet = await llmOracle.ExtractAlphabetAsync();
                Console.WriteLine($"[DEBUG] Wyciągnięty alfabet: '{string.Join(",", alphabet)}'");
                return alphabet;
            }

            throw new InvalidOperationException(
                "Alfabet nie został podany, a oracle nie potrafi go wyekstrahować.");
        }

        // ── Wypełnianie tabeli obserwacji ────────────────────────────────
        private async Task FillTable(
            IAutomatonOracle oracle,
            List<string> S,
            List<string> E,
            List<string> alphabet,
            Dictionary<(string, string), bool> table,
            Stopwatch sw)
        {
            var rows = S.Concat(S.SelectMany(s => alphabet.Select(a => s + a))).Distinct();

            foreach (var s in rows)
            {
                if (sw.Elapsed >= MaxTotalLearningTime)
                {
                    Console.WriteLine("[WARN] L*: budżet czasowy wyczerpany w trakcie " +
                        "wypełniania tabeli — pomijam pozostałe wiersze (bezpieczne, " +
                        "brakujące wpisy domyślnie liczą się jako odrzucenie).");
                    return;
                }

                if (s.Length > MaxSymbolLen)
                {
                    Console.WriteLine($"[WARN] L*: pominięto wiersz '{s}' — przekroczono " +
                        $"defensywny limit długości ({MaxSymbolLen}). To może oznaczać, " +
                        "że tabela rośnie patologicznie (sprawdź oracle).");
                    continue;
                }

                foreach (var e in E)
                {
                    if (!table.ContainsKey((s, e)))
                        table[(s, e)] = await oracle.MembershipQuery(s + e);
                }
            }
        }

        // ── Domykanie: closedness + consistency w jednej pętli ───────────
        private async Task MakeClosedAndConsistent(
            IAutomatonOracle oracle,
            List<string> S,
            List<string> E,
            List<string> alphabet,
            Dictionary<(string, string), bool> table,
            Stopwatch sw)
        {
            int iteration = 0;

            while (iteration++ < MaxTableRefinementIterations)
            {
                if (BudgetExceeded(sw, S))
                {
                    Console.WriteLine($"[WARN] L*: budżet wyczerpany w trakcie domykania " +
                        $"tabeli (czas={sw.Elapsed.TotalSeconds:F1}s, stanów={S.Count}) — " +
                        "kontynuuję z bieżącym stanem tabeli, bez dalszego dodawania stanów.");
                    return;
                }

                var missingPrefix = FindClosednessViolation(S, E, alphabet, table);
                if (missingPrefix != null)
                {
                    S.Add(missingPrefix);
                    await FillTable(oracle, S, E, alphabet, table, sw);
                    continue;
                }

                var newSuffix = FindConsistencyViolation(S, E, alphabet, table);
                if (newSuffix != null)
                {
                    E.Add(newSuffix);
                    await FillTable(oracle, S, E, alphabet, table, sw);
                    continue;
                }

                // Tabela jednocześnie domknięta i spójna — koniec.
                return;
            }

            Console.WriteLine("[WARN] L*: przekroczono limit iteracji domykania " +
                "(closedness/consistency) — kontynuuję z bieżącym stanem tabeli.");
        }

        // Domkniętość: każdy wiersz z S·A musi mieć odpowiednik (ten sam
        // podpis) wśród wierszy S. Jeśli nie — brakujący prefiks trzeba
        // dodać do S jako nowy, osobny stan.
        private string? FindClosednessViolation(
            List<string> S, List<string> E, List<string> alphabet,
            Dictionary<(string, string), bool> table)
        {
            string RowSig(string s) => RowSignature(s, E, table);
            var sRowSignatures = S.Select(RowSig).ToHashSet();

            foreach (var s in S)
            {
                foreach (var a in alphabet)
                {
                    var sa = s + a;
                    if (!sRowSignatures.Contains(RowSig(sa)))
                        return sa;
                }
            }

            return null;
        }

        // Spójność: jeśli dwa stany reprezentowane przez s1 i s2 w S mają
        // identyczny wiersz (są, na ile wiemy, tym samym stanem), to ich
        // następniki s1·a i s2·a też muszą mieć identyczny wiersz dla
        // każdego symbolu a. Jeśli nie — znaleźliśmy dowód, że s1 i s2 W
        // RZECZYWISTOŚCI są różnymi stanami, i musimy dodać do E nowy
        // sufiks (a + różnicujący sufiks), który to ujawni.
        //
        // Bez tego kroku algorytm może scalić dwa faktycznie różne stany
        // w jeden — dokładnie to prowadziło do automatów z drastycznie
        // za małą liczbą stanów (np. 1 stan zamiast 3 dla długości mod 3).
        private string? FindConsistencyViolation(
            List<string> S, List<string> E, List<string> alphabet,
            Dictionary<(string, string), bool> table)
        {
            string RowSig(string s) => RowSignature(s, E, table);

            for (int i = 0; i < S.Count; i++)
            {
                for (int j = i + 1; j < S.Count; j++)
                {
                    var s1 = S[i];
                    var s2 = S[j];

                    if (RowSig(s1) != RowSig(s2))
                        continue; // s1 i s2 już wiadomo, że są różne — nic do sprawdzenia

                    foreach (var a in alphabet)
                    {
                        var r1 = s1 + a;
                        var r2 = s2 + a;

                        foreach (var e in E)
                        {
                            var v1 = table.GetValueOrDefault((r1, e));
                            var v2 = table.GetValueOrDefault((r2, e));

                            if (v1 != v2)
                                return a + e; // nowy dystynktor
                        }
                    }
                }
            }

            return null;
        }

        private static string RowSignature(
            string s, List<string> E, Dictionary<(string, string), bool> table) =>
            string.Join(",", E.Select(e => table.GetValueOrDefault((s, e)) ? "1" : "0"));

        // ── Budowa automatu z domkniętej i spójnej tabeli ─────────────────
        private FiniteAutomaton BuildAutomaton(
            List<string> S, List<string> E, List<string> alphabet,
            Dictionary<(string, string), bool> table)
        {
            string RowSig(string s) => RowSignature(s, E, table);

            // Reprezentanci stanów — unikalne wiersze spośród S
            var representatives = S
                .GroupBy(RowSig)
                .Select(g => g.First())
                .ToList();

            var automaton = new FiniteAutomaton
            {
                Name = "L* Generated DFA",
                StructureType = FormalStructureType.DeterministicFiniteAutomaton,
                Alphabet = alphabet,
                States = new List<State>(),
                Transitions = new List<Transition>()
            };

            int i = 0;
            var stateNames = representatives.ToDictionary(
                r => RowSig(r),
                r => $"q{i++}");

            foreach (var rep in representatives)
            {
                var sig = RowSig(rep);
                automaton.States.Add(new State
                {
                    Name = stateNames[sig],
                    IsStart = rep == "",
                    IsAccepting = table.GetValueOrDefault((rep, "")),
                    X = 100 + automaton.States.Count * 200,
                    Y = 150
                });
            }

            foreach (var rep in representatives)
            {
                var fromName = stateNames[RowSig(rep)];

                foreach (var symbol in alphabet)
                {
                    var successor = rep + symbol;
                    var toSig = RowSig(successor);

                    if (stateNames.TryGetValue(toSig, out var toName))
                    {
                        automaton.Transitions.Add(new Transition
                        {
                            FromState = fromName,
                            Symbol = symbol,
                            ToState = toName
                        });
                    }
                }
            }

            if (DfaCompletion.CompleteWithTrapState(automaton, alphabet))
            {
                Console.WriteLine("[WARN] L*: tabela nie była w pełni domknięta — uzupełniono " +
                    "jawnym stanem pułapką, żeby zwrócić całkowity (kompletny) automat.");
            }

            return automaton;
        }

        // ── Equivalence query (przybliżona) ───────────────────────────────
        // Prawdziwe L* zakłada nauczyciela, który potrafi stwierdzić
        // "hipoteza == język docelowy" i zwrócić kontrprzykład, jeśli nie.
        // Nie mamy takiego nauczyciela — mamy tylko ten sam (zaszumiony)
        // oracle co przy membership query. Dlatego przeszukujemy słowa
        // rosnącej długości i porównujemy odpowiedź oracle z symulacją
        // hipotezy; pierwsza niezgodność to kontrprzykład.
        //
        // To nie daje formalnej gwarancji poprawności (oracle sam może się
        // mylić), ale usuwa najpoważniejszą lukę: brak JAKIEJKOLWIEK
        // weryfikacji hipotezy poza wąskim zestawem słów użytych do
        // zbudowania tabeli.
        private async Task<string?> FindCounterexampleAsync(
            IAutomatonOracle oracle, FiniteAutomaton hypothesis, List<string> alphabet, Stopwatch sw)
        {
            var delta = hypothesis.Transitions
                .GroupBy(t => (t.FromState, t.Symbol))
                .ToDictionary(g => g.Key, g => g.First().ToState);
            var acceptingStates = hypothesis.AcceptingStates.ToHashSet();
            var start = hypothesis.StartState;

            int queries = 0;

            foreach (var word in GenerateWordsByLength(alphabet, EquivalenceSearchMaxWordLen))
            {
                if (queries >= EquivalenceMaxQueriesPerRound || sw.Elapsed >= MaxTotalLearningTime)
                    break;

                bool hypothesisAccepts = SimulateHypothesis(word, start, delta, acceptingStates);
                bool oracleAccepts = await oracle.MembershipQuery(word);
                queries++;

                if (hypothesisAccepts != oracleAccepts)
                    return word;
            }

            return null;
        }

        private bool SimulateHypothesis(
            string word, string start,
            Dictionary<(string, string), string> delta,
            HashSet<string> acceptingStates)
        {
            var state = start;

            foreach (var ch in word)
            {
                var symbol = ch.ToString();
                if (!delta.TryGetValue((state, symbol), out var next))
                    return false; // brak przejścia = automat "umiera" = odrzucenie

                state = next;
            }

            return acceptingStates.Contains(state);
        }

        private static IEnumerable<string> GenerateWordsByLength(List<string> alphabet, int maxLen)
        {
            yield return "";

            if (!alphabet.Any()) yield break;

            var queue = new Queue<string>();
            foreach (var a in alphabet)
                queue.Enqueue(a);

            while (queue.Count > 0)
            {
                var word = queue.Dequeue();
                yield return word;

                if (word.Length < maxLen)
                    foreach (var a in alphabet)
                        queue.Enqueue(word + a);
            }
        }

        private static IEnumerable<string> AllPrefixes(string word)
        {
            yield return "";
            for (int i = 1; i <= word.Length; i++)
                yield return word.Substring(0, i);
        }
    }
}