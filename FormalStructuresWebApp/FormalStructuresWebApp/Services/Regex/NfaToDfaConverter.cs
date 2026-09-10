using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Regex
{
    /// <summary>
    /// Klasyczna konstrukcja podzbiorowa (subset construction) —
    /// determinizuje NFA (z ε-przejściami) do DFA. Podręcznikowy, w pełni
    /// deterministyczny algorytm formalny, bez udziału LLM.
    /// Wynikowy DFA może nie być totalny (brakujące przejścia dla martwych
    /// gałęzi) — dopełnienie stanem pułapką robi się osobno (DfaCompletion).
    /// </summary>
    public class NfaToDfaConverter
    {
        public FiniteAutomaton Convert(FiniteAutomaton nfa, List<string> alphabet)
        {
            var epsilonTargets = nfa.Transitions
                .Where(t => t.Symbol == ThompsonBuilder.Epsilon)
                .GroupBy(t => t.FromState)
                .ToDictionary(g => g.Key, g => g.Select(t => t.ToState).ToList());

            HashSet<string> EpsilonClosure(IEnumerable<string> states)
            {
                var result = new HashSet<string>(states);
                var stack = new Stack<string>(result);
                while (stack.Count > 0)
                {
                    var s = stack.Pop();
                    if (!epsilonTargets.TryGetValue(s, out var targets)) continue;
                    foreach (var t in targets)
                        if (result.Add(t))
                            stack.Push(t);
                }
                return result;
            }

            var symbolTransitions = nfa.Transitions
                .Where(t => t.Symbol != ThompsonBuilder.Epsilon)
                .ToLookup(t => (t.FromState, t.Symbol));

            HashSet<string> Move(HashSet<string> states, string symbol)
            {
                var result = new HashSet<string>();
                foreach (var s in states)
                    foreach (var t in symbolTransitions[(s, symbol)])
                        result.Add(t.ToState);
                return result;
            }

            string Key(HashSet<string> set) => string.Join(",", set.OrderBy(x => x));

            var nfaAcceptStates = nfa.States.Where(s => s.IsAccepting).Select(s => s.Name).ToHashSet();

            var dfa = new FiniteAutomaton
            {
                Name = "Subset-constructed DFA",
                StructureType = FormalStructureType.DeterministicFiniteAutomaton,
                Alphabet = alphabet,
                States = new List<State>(),
                Transitions = new List<Transition>()
            };

            var dfaStateNames = new Dictionary<string, string>();
            var pending = new Queue<HashSet<string>>();
            int counter = 0;

            string RegisterAndEnqueueIfNew(HashSet<string> set)
            {
                var key = Key(set);
                if (!dfaStateNames.TryGetValue(key, out var name))
                {
                    name = $"q{counter++}";
                    dfaStateNames[key] = name;
                    pending.Enqueue(set);
                }
                return dfaStateNames[key];
            }

            var startClosure = EpsilonClosure(new[] { nfa.StartState });
            var startName = RegisterAndEnqueueIfNew(startClosure);
            var visited = new HashSet<string>();

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                var currentKey = Key(current);
                if (!visited.Add(currentKey)) continue;

                var currentName = dfaStateNames[currentKey];

                dfa.States.Add(new State
                {
                    Name = currentName,
                    IsStart = currentName == startName,
                    IsAccepting = current.Any(nfaAcceptStates.Contains),
                    X = 100 + dfa.States.Count * 200,
                    Y = 150
                });

                foreach (var symbol in alphabet)
                {
                    var moved = Move(current, symbol);
                    if (moved.Count == 0) continue; // brak przejścia — dopełni to DfaCompletion

                    var closure = EpsilonClosure(moved);
                    var toName = RegisterAndEnqueueIfNew(closure);

                    dfa.Transitions.Add(new Transition
                    {
                        FromState = currentName,
                        Symbol = symbol,
                        ToState = toName
                    });
                }
            }

            return dfa;
        }
    }
}