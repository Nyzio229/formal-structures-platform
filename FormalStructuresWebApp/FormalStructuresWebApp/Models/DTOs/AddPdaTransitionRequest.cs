namespace FormalStructuresWebApp.Models.DTOs
{
    public class AddPdaTransitionRequest
    {
        public string FromState { get; set; } = "";
        public string? InputSymbol { get; set; }
        public string StackTop { get; set; } = "";
        public List<string> PushSymbols { get; set; } = new();
        public string ToState { get; set; } = "";
    }
}
