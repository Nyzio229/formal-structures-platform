using System.Diagnostics;
using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Common;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.Regex
{
    /// <summary>
    /// Alternatywna, "jednostrzałowa" ścieżka generowania DFA — analogiczna
    /// do tego, co już dobrze sprawdza się dla CFG (Services/CFG/CfgLearningService.cs):
    /// LLM jest pytany generatywnie (o wyrażenie regularne), a cała reszta to
    /// klasyczne, deterministyczne algorytmy formalne (Thompson, subset
    /// construction, minimalizacja). To odwrotność L*, gdzie LLM jest
    /// odpytywany dziesiątki/setki razy jako wyrocznia o pojedyncze słowa —
    /// każde takie zapytanie to osobna okazja do pomyłki, a błędy się
    /// kumulują w strukturze automatu. Tu błąd może pochodzić tylko
    /// z JEDNEGO miejsca: samej treści wygenerowanego regexu.
    ///
    /// SAMOSPÓJNOŚĆ (self-consistency): zamiast pytać model o regex raz,
    /// generujemy kilku NIEZALEŻNYCH kandydatów (osobne zapytania — domyślna,
    /// niezerowa temperatura Ollamy daje realną różnorodność między nimi) i
    /// oceniamy każdego względem TEJ SAMEJ, raz pobranej próbki odpowiedzi
    /// oracle. Wygrywa kandydat z najmniejszą liczbą rozbieżności — to
    /// znacznie zwiększa szansę, że choć jeden z kilku "rzutów" trafi
    /// w poprawną semantykę, zamiast polegać na jednym, nieskorygowanym
    /// zgadnięciu. Jeśli nawet najlepszy kandydat nadal wypada podejrzanie
    /// — metoda zwraca null, a wywołujący (StructureController) spada do
    /// pełnego L* jako bezpiecznika.
    /// </summary>
    public class RegexDfaLearningService
    {
        private const int RegexCandidateCount = 3;    // ile niezależnych regexów generujemy do wyboru
        private const int SanityCheckWordCount = 40;
        private const int SanityCheckMaxWordLen = 6; // dopasowane WPROST do domyślnego max_test_len w benchmark.py —
                                                     // próbkowanie z WIĘKSZEJ przestrzeni niż realny test tylko
                                                     // rozrzedza pokrycie akurat tam, gdzie jest najbardziej istotne
        private const int MaxToleratedMismatches = 2; // tolerancja dla NAJLEPSZEGO z kandydatów (2/40 = 5%, dużo
                                                      // ostrzejsze niż poprzednie 2/16 = 12.5%)

        // Ta ścieżka robi do RegexCandidateCount + (SanityCheckWordCount × 3 głosy)
        // SEKWENCYJNYCH zapytań do Ollamy (nawet 120+) bez żadnego wcześniejszego
        // ograniczenia czasowego. Z szybszym modelem to nie był problem — z
        // wolniejszym (np. phi4-mini: pojedyncze zapytanie potrafiło trwać >100s
        // w obserwowanych przebiegach) całość łatwo przekracza zewnętrzny limit
        // czasu HTTP (300s w benchmark.py), kończąc się "cichym" timeoutem bez
        // żadnej diagnostyki. Budżet niżej gwarantuje, że ZAWSZE zdążymy albo
        // zwrócić wynik, albo jawnie spaść do L*, w rozsądnym czasie.
        private static readonly TimeSpan MaxTotalTime = TimeSpan.FromSeconds(120);

        private readonly IOllamaService _ollama;
        private readonly ThompsonBuilder _thompson = new();
        private readonly NfaToDfaConverter _determinizer = new();
        private readonly DfaMinimizer _minimizer = new();

        public string RawRegex { get; private set; } = "";
        public string FallbackReason { get; private set; } = "";
        public int LastSanityCheckMismatches { get; private set; } = -1; // -1 = nie doszło do weryfikacji
        public List<RegexCandidateInfo> Candidates { get; private set; } = new();

        public RegexDfaLearningService(IOllamaService ollama)
        {
            _ollama = ollama;
        }

        public async Task<FiniteAutomaton?> TryLearnAsync(string description, List<string> alphabet)
        {
            RawRegex = "";
            FallbackReason = "";
            LastSanityCheckMismatches = -1;
            Candidates = new List<RegexCandidateInfo>();

            if (alphabet == null || !alphabet.Any())
            {
                FallbackReason = "Brak alfabetu — nie da się tokenizować wyrażenia regularnego.";
                return null;
            }

            // Próbka słów i odpowiedzi oracle pobrana RAZ, dzielona między
            // wszystkich kandydatów — dzięki temu porównanie jest uczciwe
            // ("jabłka do jabłek") i nie mnożymy kosztu zapytań × liczba
            // kandydatów. Głosowanie większościowe (votes: 3) sprowadza
            // błąd samego "sędziego" do rozsądnego poziomu.
            var sw = Stopwatch.StartNew();
            var sampleWords = SampleWords(alphabet, SanityCheckWordCount, SanityCheckMaxWordLen);
            var judge = new LlmOracle(_ollama, description, votes: 3);
            var sampleAnswers = new Dictionary<string, bool>();
            foreach (var w in sampleWords)
            {
                if (sw.Elapsed >= MaxTotalTime)
                {
                    FallbackReason = $"Budżet czasowy wyczerpany podczas zbierania próbki " +
                        $"({sampleAnswers.Count}/{sampleWords.Count} słów, {sw.Elapsed.TotalSeconds:F0}s) — " +
                        "prawdopodobnie wolny model. Spadam do L*.";
                    return null;
                }
                sampleAnswers[w] = await judge.MembershipQuery(w);
            }

            FiniteAutomaton? best = null;
            int bestMismatches = int.MaxValue;
            string bestRegex = "";

            for (int i = 0; i < RegexCandidateCount; i++)
            {
                if (sw.Elapsed >= MaxTotalTime)
                {
                    Console.WriteLine($"[WARN] Regex→DFA: budżet czasowy wyczerpany po {i} " +
                        $"z {RegexCandidateCount} kandydatów ({sw.Elapsed.TotalSeconds:F0}s) — " +
                        "kończę z tym, co już oceniono, zamiast czekać na kolejnych.");
                    break;
                }

                var response = await _ollama.AskAsync(BuildRegexPrompt(description, alphabet));
                var candidateRegex = ExtractRegexLine(response);

                RegexNode ast;
                try
                {
                    ast = new RegexParser(candidateRegex, alphabet).Parse();
                }
                catch (RegexParseException ex)
                {
                    Candidates.Add(new RegexCandidateInfo
                    {
                        Regex = candidateRegex,
                        ParseError = ex.Message
                    });
                    continue;
                }

                FiniteAutomaton minimal;
                try
                {
                    var nfa = _thompson.Build(ast, alphabet);
                    var dfa = _determinizer.Convert(nfa, alphabet);
                    DfaCompletion.CompleteWithTrapState(dfa, alphabet);
                    minimal = _minimizer.Minimize(dfa, alphabet);
                }
                catch (Exception ex)
                {
                    // Nie powinno się zdarzyć przy poprawnym AST — defensywnie,
                    // żeby błąd w tej ścieżce nigdy nie wywalił całego żądania.
                    Candidates.Add(new RegexCandidateInfo
                    {
                        Regex = candidateRegex,
                        ParseError = $"Błąd budowy automatu: {ex.Message}"
                    });
                    continue;
                }

                var mismatches = CountMismatches(minimal, sampleAnswers);
                Candidates.Add(new RegexCandidateInfo
                {
                    Regex = candidateRegex,
                    Mismatches = mismatches,
                    StateCount = minimal.States.Count
                });

                // Wygrywa najmniej rozbieżności; przy remisie preferujemy
                // PROSTSZY automat (mniej stanów) — mniej podatny na to,
                // że "przypadkiem" zgadł akurat te konkretne słowa próbki.
                var isBetter = mismatches < bestMismatches ||
                    (mismatches == bestMismatches && best != null && minimal.States.Count < best.States.Count);

                if (isBetter)
                {
                    best = minimal;
                    bestMismatches = mismatches;
                    bestRegex = candidateRegex;
                }
            }

            if (best == null)
            {
                FallbackReason = sw.Elapsed >= MaxTotalTime
                    ? $"Budżet czasowy wyczerpany ({sw.Elapsed.TotalSeconds:F0}s) zanim jakikolwiek " +
                      "kandydat został w pełni oceniony — prawdopodobnie wolny model."
                    : $"Żaden z {RegexCandidateCount} wygenerowanych kandydatów nie sparsował się poprawnie.";
                return null;
            }

            RawRegex = bestRegex;
            LastSanityCheckMismatches = bestMismatches;

            if (bestMismatches > MaxToleratedMismatches)
            {
                FallbackReason = $"Najlepszy z {RegexCandidateCount} kandydatów ('{bestRegex}') nadal ma " +
                    $"{bestMismatches} rozbieżności na {sampleWords.Count} sprawdzonych słów.";
                return null;
            }

            Console.WriteLine($"[DEBUG] Regex→DFA: zaakceptowano '{bestRegex}' spośród " +
                $"{RegexCandidateCount} kandydatów ({best.States.Count} stanów, {bestMismatches} rozbieżności).");

            return best;
        }

        private static int CountMismatches(FiniteAutomaton dfa, Dictionary<string, bool> sampleAnswers)
        {
            int mismatches = 0;
            foreach (var (word, expected) in sampleAnswers)
            {
                if (Simulate(dfa, word) != expected) mismatches++;
            }
            return mismatches;
        }

        private static bool Simulate(FiniteAutomaton dfa, string word)
        {
            var transitions = dfa.Transitions.ToDictionary(t => (t.FromState, t.Symbol), t => t.ToState);
            var accepting = dfa.AcceptingStates.ToHashSet();
            var state = dfa.StartState;

            foreach (var ch in word)
            {
                if (!transitions.TryGetValue((state, ch.ToString()), out var next))
                    return false;
                state = next;
            }

            return accepting.Contains(state);
        }

        private static List<string> SampleWords(List<string> alphabet, int count, int maxLen)
        {
            // WAŻNE: nie bierzemy po prostu pierwszych `count` słów w kolejności
            // BFS — to głównie krótkie prefiksy, które słabo (albo wcale) nie
            // odróżniają języków "pozycyjnych" (np. "zaczyna się od ab" od
            // błędnie wygenerowanego "zawiera ab" — na krótkich prefiksach
            // dają identyczne odpowiedzi). Generujemy WSZYSTKIE słowa do
            // maxLen, a próbkę losujemy — to dużo skuteczniej wyłapuje
            // regexy poprawne tylko "z grubsza".
            var all = new List<string> { "" };
            var queue = new Queue<string>();
            queue.Enqueue("");
            const int cap = 4000; // zabezpieczenie przed eksplozją przy dużym alfabecie

            while (queue.Count > 0 && all.Count < cap)
            {
                var w = queue.Dequeue();
                if (w.Length >= maxLen) continue;
                foreach (var a in alphabet)
                {
                    var next = w + a;
                    all.Add(next);
                    queue.Enqueue(next);
                    if (all.Count >= cap) break;
                }
            }

            if (all.Count <= count)
                return all;

            var rnd = new Random(12345); // stały seed — powtarzalna próbka między wywołaniami
            return all.OrderBy(_ => rnd.Next()).Take(count).ToList();
        }

        private string BuildRegexPrompt(string description, List<string> alphabet)
        {
            const string metaChars = "|*+?()\\";
            var needsEscaping = alphabet.Where(a => a.Length == 1 && metaChars.Contains(a[0])).ToList();

            var alphabetStr = string.Join(", ", alphabet.Select(a => $"'{a}'"));

            var escapeNote = needsEscaping.Any()
                ? $"\nUWAGA: alfabet zawiera symbol(e) będące też operatorami regexu " +
                  $"({string.Join(", ", needsEscaping.Select(a => $"'{a}'"))}). Żeby użyć ich jako " +
                  $"DOSŁOWNYCH znaków (nie jako operatorów), poprzedź je backslashem, " +
                  $"np. \\{needsEscaping.First()} oznacza dosłowny znak '{needsEscaping.First()}'."
                : "";

            return $@"Twoim zadaniem jest zapisanie wyrażenia regularnego opisującego podany język.

                OPIS JĘZYKA: {description}
                ALFABET: {alphabetStr}

                SKŁADNIA — używaj WYŁĄCZNIE:
                - konkatenacja: symbole zapisane obok siebie, np. ab
                - alternatywa: |
                - domknięcie Kleenego (zero lub więcej): *
                - jedno lub więcej: +
                - zero lub jeden: ?
                - grupowanie: ( )
                - słowo puste: ε{escapeNote}

                NIE używaj żadnej innej składni (bez klas znaków [...], bez ^, $, bez \d itp.) —
                tylko symbole z podanego alfabetu i operatory wymienione wyżej.

                Odpowiedz WYŁĄCZNIE jednym wyrażeniem regularnym w jednej linii,
                bez żadnych wyjaśnień, bez formatowania markdown, bez ""```"".";
        }

        private static string ExtractRegexLine(string response)
        {
            var cleaned = response
                .Replace("```regex", "")
                .Replace("```", "")
                .Trim();

            var firstLine = cleaned
                .Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

            return firstLine ?? "";
        }
    }
}