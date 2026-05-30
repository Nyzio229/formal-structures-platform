namespace FormalStructuresWebApp.Models.Domain
{
    public class PushdownAutomaton
    {
        public string Name { get; set; } = "New PDA";
        public List<PdaState> States { get; set; } = new();
        public List<string> InputAlphabet { get; set; } = new();
        public List<string> StackAlphabet { get; set; } = new();
        public List<PdaTransition> Transitions { get; set; } = new();
        public string StartState => States.FirstOrDefault(s => s.IsStart)?.Name ?? "";
        public string StartStackSymbol { get; set; } = "Z";
        public List<string> AcceptingStates =>
            States.Where(s => s.IsAccepting).Select(s => s.Name).ToList();
        public PdaAcceptanceMode AcceptanceMode { get; set; }
            = PdaAcceptanceMode.EmptyStack;
    }

    public class PdaTransition
    {
        public string FromState { get; set; } = "";
        public string? InputSymbol { get; set; }
        public string StackTop { get; set; } = "";
        public List<string> PushSymbols { get; set; } = new();
        public string ToState { get; set; } = "";
    }

    public enum PdaAcceptanceMode { EmptyStack, AcceptingStates }
}