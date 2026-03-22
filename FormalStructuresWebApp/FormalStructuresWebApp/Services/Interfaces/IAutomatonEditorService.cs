using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAutomatonEditorService
    {
        void AddState(FiniteAutomaton automaton, string stateName, bool isStart, bool isAccepting, double x, double y);
        void RemoveState(FiniteAutomaton automaton, string stateName);
        void AddTransition(FiniteAutomaton automaton, string fromState, string symbol, string toState);
        void RemoveTransition(FiniteAutomaton automaton, string fromState, string symbol, string toState);
        void UpdateStatePositions(FiniteAutomaton automaton, List<(string Name, double X, double Y)> positions);
    }
}