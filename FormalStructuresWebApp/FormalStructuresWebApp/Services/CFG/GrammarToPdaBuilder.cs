using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.CFG
{
    public class GrammarToPdaBuilder
    {
        public PushdownAutomaton Build(ContextFreeGrammar grammar)
        {
            var pda = new PushdownAutomaton
            {
                Name = "PDA dla gramatyki CFG",
                StartStackSymbol = "Z",
                AcceptanceMode = PdaAcceptanceMode.EmptyStack
            };

            pda.States = new List<PdaState>
            {
                new() { Name = "q_start", IsStart   = true,  X = 120, Y = 250 },
                new() { Name = "q_loop",                      X = 420, Y = 250 },
                new() { Name = "q_accept", IsAccepting = true, X = 720, Y = 250 }
            };

            var nonTerminals = grammar.Productions
                .Select(p => p.Left).Distinct().ToHashSet();

            var terminals = grammar.Productions
                .SelectMany(p => p.Right)
                .Where(s => !nonTerminals.Contains(s))
                .Distinct().ToList();

            pda.InputAlphabet = terminals;
            pda.StackAlphabet = nonTerminals.Concat(terminals).Append("Z").ToList();

            pda.Transitions.Add(new PdaTransition
            {
                FromState = "q_start",
                InputSymbol = null,
                StackTop = "Z",
                PushSymbols = new List<string> { grammar.StartSymbol, "Z" },
                ToState = "q_loop"
            });

            var seen = new HashSet<string>();
            foreach (var prod in grammar.Productions)
            {
                var key = prod.Left + "→" + string.Join(" ", prod.Right);
                if (!seen.Add(key)) continue;

                pda.Transitions.Add(new PdaTransition
                {
                    FromState = "q_loop",
                    InputSymbol = null,
                    StackTop = prod.Left,
                    PushSymbols = prod.Right.Count == 0
                        ? new List<string>()
                        : Enumerable.Reverse(prod.Right).ToList(),
                    ToState = "q_loop"
                });
            }

            foreach (var term in terminals)
            {
                pda.Transitions.Add(new PdaTransition
                {
                    FromState = "q_loop",
                    InputSymbol = term,
                    StackTop = term,
                    PushSymbols = new List<string>(),
                    ToState = "q_loop"
                });
            }

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