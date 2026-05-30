using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IPdaEditorService
    {
        void AddState(PushdownAutomaton pda, string name,
            bool isStart, bool isAccepting, double x, double y);
        void RemoveState(PushdownAutomaton pda, string name);
        void AddTransition(PushdownAutomaton pda, string from,
            string? inputSymbol, string stackTop,
            List<string> pushSymbols, string to);
        void RemoveTransition(PushdownAutomaton pda, string from,
            string? inputSymbol, string stackTop, string to);
        void UpdateStatePositions(PushdownAutomaton pda,
            List<(string Name, double X, double Y)> positions);
    }
}