using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.LStar
{
    public class LStarService
    {


        public async Task<FiniteAutomaton> LearnAsync(IAutomatonOracle oracle, List<string> alphabet)
        {

            if (alphabet == null || alphabet.Count == 0)
            {
                var llmOracle = (LlmOracle)oracle;
                alphabet = await llmOracle.ExtractAlphabetAsync();
            }
            var S = new List<string> { "" };    //prefixy
            var E = new List<string> { "" };    //suffixy
            E.AddRange(alphabet);
            var table = new Dictionary<(string, string), bool>();
            
            if (alphabet == null || alphabet.Count == 0)
            {
                var llmOracle = (LlmOracle)oracle;
                alphabet = await llmOracle.ExtractAlphabetAsync();
                Console.WriteLine($"[DEBUG] Wyciągnięty alfabet: '{string.Join(",", alphabet)}'");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Alfabet z formularza: '{string.Join(",", alphabet)}'");
            }
            // Funkcja wypełniająca tabelę
            async Task Fill()
            {
                foreach (var s in S.Concat(S.SelectMany(si => alphabet.Select(a => si + a))))
                {
                    if (s.Length > 10) continue;  // ← DODAJ TEN WARUNEK
                    foreach (var e in E)
                    {
                        if ((s + e).Length > 10) continue;  // ← I TEN
                        if (!table.ContainsKey((s, e)))
                            table[(s, e)] = await oracle.MembershipQuery(s + e);
                    }
                }
            }

            await Fill();

            int maxIterations = 10;
            int iteration = 0;

            while (true)
            {
                if (iteration++ >= maxIterations)
                {
                    Console.WriteLine("[WARN] Przekroczono limit iteracji L*");
                    break;
                }

                // Sprawdź domknięcie (closedness)
                var SxA = S.SelectMany(s => alphabet.Select(a => s + a)).ToList();
                var missingPrefix = SxA.FirstOrDefault(sa =>
                    !S.Any(s => E.All(e => table.GetValueOrDefault((s, e)) == table.GetValueOrDefault((sa, e)))));

                if (missingPrefix != null)
                {
                    S.Add(missingPrefix);
                    await Fill();
                    continue;
                }

                break; // tabela domknięta — buduj automat
            }

            var automaton = BuildAutomaton(S, E, alphabet, table);
            return automaton;

        }

        private FiniteAutomaton BuildAutomaton(List<string> S, List<string> E, List<string> alphabet, Dictionary<(string, string), bool> table)
        {
            // Każdy unikalny "wiersz" w tabeli = osobny stan
            // Wiersz to ciąg wartości true/false dla wszystkich e w E
            string RowSignature(string s) =>
                string.Join(",", E.Select(e => table.GetValueOrDefault((s, e)) ? "1" : "0"));

            // Reprezentanci stanów — tylko z S (nie S·A)
            var representatives = S
                .GroupBy(RowSignature)
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

            // Nadaj nazwy stanom
            int i = 0;
            var stateNames = representatives.ToDictionary(
                r => RowSignature(r),
                r => $"q{i++}"
            );

            foreach (var rep in representatives)
            {
                var sig = RowSignature(rep);
                var name = stateNames[sig];
                automaton.States.Add(new State
                {
                    Name = name,
                    IsStart = rep == "",
                    IsAccepting = table.GetValueOrDefault((rep, "")),
                    X = 100 + automaton.States.Count * 200,
                    Y = 150
                });
            }

            // Dodaj przejścia
            foreach (var rep in representatives)
            {
                var fromSig = RowSignature(rep);
                var fromName = stateNames[fromSig];

                foreach (var symbol in alphabet)
                {
                    var successor = rep + symbol;
                    var toSig = RowSignature(successor);

                    // Znajdź reprezentanta o tym samym wierszu
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

            return automaton;
        }

        //private async Task FillTable(ObservationTable table)
        //{
        //    foreach (var s in table.S)
        //    {
        //        foreach (var e in table.E)
        //        {
        //            var word = s + e;
        //            var result = await _oracle.MembershipQuery(word);
        //            table.Set(s, e, result);
        //        }
        //    }
        //}
    }
}
