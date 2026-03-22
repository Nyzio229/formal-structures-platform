using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services
{
    public class AutomatonEditorService : IAutomatonEditorService
    {
        public void AddState(FiniteAutomaton automaton, string stateName, bool isStart, bool isAccepting, double x, double y)
        {
            if (string.IsNullOrWhiteSpace(stateName))
                return;

            if (automaton.States.Any(s => s.Name == stateName))
                return;

            if (isStart)
            {
                foreach (var state in automaton.States)
                    state.IsStart = false;
            }

            automaton.States.Add(new State
            {
                Name = stateName,
                IsStart = isStart,
                IsAccepting = isAccepting,
                X = x,
                Y = y
            });
        }

        public void RemoveState(FiniteAutomaton automaton, string stateName)
        {
            var state = automaton.States.FirstOrDefault(s => s.Name == stateName);
            if (state == null)
                return;

            automaton.States.Remove(state);
            automaton.Transitions.RemoveAll(t => t.FromState == stateName || t.ToState == stateName);
        }

        public void AddTransition(FiniteAutomaton automaton, string fromState, string symbol, string toState)
        {
            if (string.IsNullOrWhiteSpace(fromState) ||
                string.IsNullOrWhiteSpace(symbol) ||
                string.IsNullOrWhiteSpace(toState))
                return;

            if (!automaton.States.Any(s => s.Name == fromState))
                return;

            if (!automaton.States.Any(s => s.Name == toState))
                return;

            if (!automaton.Alphabet.Contains(symbol))
                automaton.Alphabet.Add(symbol);

            var exists = automaton.Transitions.Any(t =>
                t.FromState == fromState &&
                t.Symbol == symbol &&
                t.ToState == toState);

            if (exists)
                return;

            automaton.Transitions.Add(new Transition
            {
                FromState = fromState,
                Symbol = symbol,
                ToState = toState
            });
        }

        public void RemoveTransition(FiniteAutomaton automaton, string fromState, string symbol, string toState)
        {
            var transition = automaton.Transitions.FirstOrDefault(t =>
                t.FromState == fromState &&
                t.Symbol == symbol &&
                t.ToState == toState);

            if (transition != null)
                automaton.Transitions.Remove(transition);
        }

        public void UpdateStatePositions(FiniteAutomaton automaton, List<(string Name, double X, double Y)> positions)
        {
            foreach (var pos in positions)
            {
                var state = automaton.States.FirstOrDefault(s => s.Name == pos.Name);
                if (state != null)
                {
                    state.X = pos.X;
                    state.Y = pos.Y;
                }
            }
        }
    }
}