namespace FormalStructuresWebApp.Models.Domain
{
    public class PushdownAutomaton
    {
        public string Name { get; set; } = "New PDA";
        public List<string> States { get; set; } = new();
        public List<string> InputAlphabet { get; set; } = new();
        public List<string> StackAlphabet { get; set; } = new();
        public List<PdaTransition> Transitions { get; set; } = new();
        public string StartState { get; set; } = "";
        public string StartStackSymbol { get; set; } = "Z";
        public List<string> AcceptingStates { get; set; } = new();
        // Akceptacja przez pusty stos lub stany akceptujące
        public PdaAcceptanceMode AcceptanceMode { get; set; }
            = PdaAcceptanceMode.EmptyStack;
    }

    public class PdaTransition
    {
        public string FromState { get; set; } = "";
        // null/ε = przejście epsilon
        public string? InputSymbol { get; set; }
        public string StackTop { get; set; } = "";
        // co wkładamy na stos (pusta lista = pop)
        public List<string> PushSymbols { get; set; } = new();
        public string ToState { get; set; } = "";
    }

    public enum PdaAcceptanceMode { EmptyStack, AcceptingStates }
}