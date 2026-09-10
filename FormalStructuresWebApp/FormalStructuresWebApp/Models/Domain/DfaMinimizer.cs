using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Regex
{
    /// <summary>
    /// Minimalizacja DFA metodą iteracyjnego podziału na klasy równoważności
    /// (algorytm Moore'a). Zakłada, że wejściowy DFA jest już CAŁKOWITY
    /// (patrz Services/Common/DfaCompletion.cs) — dzięki temu każdy stan ma
    /// zdefiniowane przejście dla każdego symbolu, co upraszcza podział.
    ///
    /// Podręcznikowy, w pełni deterministyczny algorytm formalny — żadnego
    /// udziału LLM na tym etapie.
    /// </summary>
    public class DfaMinimizer
    {
        public FiniteAutomaton Minimize(FiniteAutomaton dfa, List<string> alphabet)
        {
            var states = dfa.States.Select(s => s.Name).ToList();
            var transitionMap = dfa.Transitions.ToDictionary(t => (t.FromState, t.Symbol), t => t.ToState);
            var accepting = dfa.AcceptingStates.ToHashSet();

            // Podział początkowy: akceptujące / nieakceptujące.
            var partition = states
                .GroupBy(s => accepting.Contains(s))
                .Select(g => g.ToHashSet())
                .ToList();

            bool changed = true;
            while (changed)
            {
                changed = false;
                var newPartition = new List<HashSet<string>>();

                foreach (var group in partition)
                {
                    var subgroups = new Dictionary<string, HashSet<string>>();

                    foreach (var state in group)
                    {
                        var signature = string.Join(",", alphabet.Select(a =>
                        {
                            var target = transitionMap.GetValueOrDefault((state, a));
                            var idx = target == null
                                ? -1
                                : partition.FindIndex(p => p.Contains(target));
                            return idx.ToString();
                        }));

                        if (!subgroups.TryGetValue(signature, out var sub))
                        {
                            sub = new HashSet<string>();
                            subgroups[signature] = sub;
                        }
                        sub.Add(state);
                    }

                    if (subgroups.Count > 1) changed = true;
                    newPartition.AddRange(subgroups.Values);
                }

                partition = newPartition;
            }

            var groupNames = new Dictionary<HashSet<string>, string>();
            int i = 0;
            foreach (var g in partition)
                groupNames[g] = $"m{i++}";

            HashSet<string> GroupOf(string state) => partition.First(p => p.Contains(state));

            var minimized = new FiniteAutomaton
            {
                Name = "Minimized DFA",
                StructureType = FormalStructureType.DeterministicFiniteAutomaton,
                Alphabet = alphabet,
                States = new List<State>(),
                Transitions = new List<Transition>()
            };

            foreach (var g in partition)
            {
                minimized.States.Add(new State
                {
                    Name = groupNames[g],
                    IsStart = g.Contains(dfa.StartState),
                    IsAccepting = g.Any(accepting.Contains),
                    X = 100 + minimized.States.Count * 200,
                    Y = 150
                });
            }

            var addedTransitions = new HashSet<(string, string)>();
            foreach (var g in partition)
            {
                var repState = g.First();
                var fromName = groupNames[g];

                foreach (var a in alphabet)
                {
                    var target = transitionMap.GetValueOrDefault((repState, a));
                    if (target == null) continue;

                    var toName = groupNames[GroupOf(target)];
                    if (addedTransitions.Add((fromName, a)))
                    {
                        minimized.Transitions.Add(new Transition
                        {
                            FromState = fromName,
                            Symbol = a,
                            ToState = toName
                        });
                    }
                }
            }

            return minimized;
        }
    }
}