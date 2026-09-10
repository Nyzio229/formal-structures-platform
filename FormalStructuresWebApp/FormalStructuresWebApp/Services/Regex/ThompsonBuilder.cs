using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Regex
{
    /// <summary>
    /// Klasyczna konstrukcja Thompsona — buduje NFA z ε-przejściami
    /// z drzewa składniowego wyrażenia regularnego. W pełni deterministyczny,
    /// podręcznikowy algorytm formalny (bez udziału LLM).
    /// </summary>
    public class ThompsonBuilder
    {
        public const string Epsilon = "ε";

        private int _counter;

        public FiniteAutomaton Build(RegexNode node, List<string> alphabet)
        {
            _counter = 0;

            var nfa = new FiniteAutomaton
            {
                Name = "Thompson NFA",
                StructureType = FormalStructureType.NonDeterministicFiniteAutomaton,
                Alphabet = alphabet,
                States = new List<State>(),
                Transitions = new List<Transition>()
            };

            var (start, accept) = BuildFragment(node, nfa);

            var startState = nfa.States.First(s => s.Name == start);
            startState.IsStart = true;

            var acceptState = nfa.States.First(s => s.Name == accept);
            acceptState.IsAccepting = true;

            return nfa;
        }

        private (string start, string accept) BuildFragment(RegexNode node, FiniteAutomaton nfa)
        {
            switch (node)
            {
                case EpsNode:
                    {
                        var s = AddState(nfa);
                        var a = AddState(nfa);
                        AddTransition(nfa, s, Epsilon, a);
                        return (s, a);
                    }

                case LiteralNode lit:
                    {
                        var s = AddState(nfa);
                        var a = AddState(nfa);
                        AddTransition(nfa, s, lit.Symbol, a);
                        return (s, a);
                    }

                case ConcatNode c:
                    {
                        var (s1, a1) = BuildFragment(c.Left, nfa);
                        var (s2, a2) = BuildFragment(c.Right, nfa);
                        AddTransition(nfa, a1, Epsilon, s2);
                        return (s1, a2);
                    }

                case UnionNode u:
                    {
                        var (s1, a1) = BuildFragment(u.Left, nfa);
                        var (s2, a2) = BuildFragment(u.Right, nfa);
                        var s = AddState(nfa);
                        var a = AddState(nfa);
                        AddTransition(nfa, s, Epsilon, s1);
                        AddTransition(nfa, s, Epsilon, s2);
                        AddTransition(nfa, a1, Epsilon, a);
                        AddTransition(nfa, a2, Epsilon, a);
                        return (s, a);
                    }

                case StarNode st:
                    {
                        var (s1, a1) = BuildFragment(st.Inner, nfa);
                        var s = AddState(nfa);
                        var a = AddState(nfa);
                        AddTransition(nfa, s, Epsilon, s1);
                        AddTransition(nfa, s, Epsilon, a);
                        AddTransition(nfa, a1, Epsilon, s1);
                        AddTransition(nfa, a1, Epsilon, a);
                        return (s, a);
                    }

                case PlusNode pl:
                    // e+ == e · e*
                    return BuildFragment(new ConcatNode(pl.Inner, new StarNode(pl.Inner)), nfa);

                case OptionalNode op:
                    // e? == e | ε
                    return BuildFragment(new UnionNode(op.Inner, new EpsNode()), nfa);

                default:
                    throw new InvalidOperationException(
                        $"Nieznany typ węzła AST regexu: {node.GetType().Name}");
            }
        }

        private string AddState(FiniteAutomaton nfa)
        {
            var name = $"n{_counter++}";
            nfa.States.Add(new State { Name = name, IsStart = false, IsAccepting = false });
            return name;
        }

        private static void AddTransition(FiniteAutomaton nfa, string from, string symbol, string to) =>
            nfa.Transitions.Add(new Transition { FromState = from, Symbol = symbol, ToState = to });
    }
}