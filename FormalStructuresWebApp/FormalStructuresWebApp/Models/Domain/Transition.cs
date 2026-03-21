namespace FormalStructuresWebApp.Models.Domain
{
    public class Transition
    {
        public string FromState { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string ToState { get; set; } = string.Empty;
    }
}
