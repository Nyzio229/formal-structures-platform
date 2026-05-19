using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.CFG
{
    /// <summary>
    /// Buduje PDA z gramatyki CFG metodą "top-down" (predyktywną).
    ///
    /// Klasyczna konstrukcja:
    /// - 3 stany: q_start, q_loop, q_accept
    /// - q_start → q_loop: push symbolu startowego na stos
    /// - q_loop dla każdej produkcji A → α:
    ///     przejście ε na A ze stosu → push α (odwrócone)
    /// - q_loop dla każdego terminala a:
    ///     przejście a na a ze stosu → pop (push nic)
    /// - q_loop → q_accept gdy stos pusty (Z pop)
    ///
    /// Akceptacja przez pusty stos.
    /// </summary>
    public class GrammarToPdaBuilder
    {
        public PushdownAutomaton Build(ContextFreeGrammar grammar)
        {
            var pda = new PushdownAutomaton
            {
                Name = "PDA dla gramatyki CFG",
                StartState = "q_start",
                StartStackSymbol = "Z",
                AcceptanceMode = PdaAcceptanceMode.EmptyStack
            };

            pda.States = new List<string> { "q_start", "q_loop", "q_accept" };
            pda.AcceptingStates = new List<string> { "q_accept" };

            // Nieterminale = symbole które pojawiają się po lewej stronie produkcji
            var nonTerminals = grammar.Productions
                .Select(p => p.Left)
                .Distinct()
                .ToHashSet();

            // Terminale = symbole z prawej strony które NIE są nieterminalami
            var terminals = grammar.Productions
                .SelectMany(p => p.Right)
                .Where(s => !nonTerminals.Contains(s))
                .Distinct()
                .ToList();

            pda.InputAlphabet = terminals;
            pda.StackAlphabet = nonTerminals.Concat(terminals).Append("Z").ToList();

            // Przejście inicjalizujące
            pda.Transitions.Add(new PdaTransition
            {
                FromState = "q_start",
                InputSymbol = null,
                StackTop = "Z",
                PushSymbols = new List<string> { grammar.StartSymbol, "Z" },
                ToState = "q_loop"
            });

            // Przejścia dla produkcji — deduplikacja przez HashSet
            var seenProductions = new HashSet<string>();

            foreach (var prod in grammar.Productions)
            {
                // Klucz unikalności: lewa strona + prawa strona
                var key = prod.Left + "→" + string.Join(" ", prod.Right);
                if (!seenProductions.Add(key)) continue;  // pomiń duplikat

                var pushSymbols = prod.Right.Count == 0
                    ? new List<string>()
                    : Enumerable.Reverse(prod.Right).ToList();

                pda.Transitions.Add(new PdaTransition
                {
                    FromState = "q_loop",
                    InputSymbol = null,
                    StackTop = prod.Left,
                    PushSymbols = pushSymbols,
                    ToState = "q_loop"
                });
            }

            // Przejścia dla terminali — match i pop
            foreach (var term in terminals)
            {
                pda.Transitions.Add(new PdaTransition
                {
                    FromState = "q_loop",
                    InputSymbol = term,   // czyta 'a' z wejścia
                    StackTop = term,      // gdy 'a' na szczycie stosu
                    PushSymbols = new List<string>(),  // pop
                    ToState = "q_loop"
                });
            }

            // Przejście finalne
            pda.Transitions.Add(new PdaTransition
            {
                FromState = "q_loop",
                InputSymbol = null,
                StackTop = "Z",
                PushSymbols = new List<string>(),
                ToState = "q_accept"
            });

            return pda;
        }
    }
}