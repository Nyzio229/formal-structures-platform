using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.Pda
{
    public class PdaEditorService : IPdaEditorService
    {
        public void AddState(PushdownAutomaton pda, string name,
            bool isStart, bool isAccepting, double x, double y)
        {
            if (pda.States.Any(s => s.Name == name)) return;

            if (isStart)
                pda.States.ForEach(s => s.IsStart = false);

            pda.States.Add(new Models.Domain.PdaState
            {
                Name = name,
                IsStart = isStart,
                IsAccepting = isAccepting,
                X = x,
                Y = y
            });
        }

        public void RemoveState(PushdownAutomaton pda, string name)
        {
            pda.States.RemoveAll(s => s.Name == name);
            pda.Transitions.RemoveAll(t =>
                t.FromState == name || t.ToState == name);
        }

        public void AddTransition(PushdownAutomaton pda, string from,
            string? inputSymbol, string stackTop,
            List<string> pushSymbols, string to)
        {
            bool exists = pda.Transitions.Any(t =>
                t.FromState == from &&
                t.InputSymbol == inputSymbol &&
                t.StackTop == stackTop &&
                t.ToState == to &&
                t.PushSymbols.SequenceEqual(pushSymbols));

            if (exists) return;

            // Aktualizuj alfabet wejściowy i stosu
            if (inputSymbol != null && !pda.InputAlphabet.Contains(inputSymbol))
                pda.InputAlphabet.Add(inputSymbol);

            if (!string.IsNullOrEmpty(stackTop) && !pda.StackAlphabet.Contains(stackTop))
                pda.StackAlphabet.Add(stackTop);

            foreach (var sym in pushSymbols)
                if (!pda.StackAlphabet.Contains(sym))
                    pda.StackAlphabet.Add(sym);

            pda.Transitions.Add(new PdaTransition
            {
                FromState = from,
                InputSymbol = inputSymbol,
                StackTop = stackTop,
                PushSymbols = pushSymbols,
                ToState = to
            });
        }

        public void RemoveTransition(PushdownAutomaton pda, string from,
            string? inputSymbol, string stackTop, string to)
        {
            pda.Transitions.RemoveAll(t =>
                t.FromState == from &&
                t.InputSymbol == inputSymbol &&
                t.StackTop == stackTop &&
                t.ToState == to);
        }

        public void UpdateStatePositions(PushdownAutomaton pda,
            List<(string Name, double X, double Y)> positions)
        {
            foreach (var (name, x, y) in positions)
            {
                var state = pda.States.FirstOrDefault(s => s.Name == name);
                if (state != null) { state.X = x; state.Y = y; }
            }
        }
    }
}