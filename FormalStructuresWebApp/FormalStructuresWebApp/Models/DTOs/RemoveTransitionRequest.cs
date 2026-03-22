namespace FormalStructuresWebApp.Models.DTOs
{
    public class RemoveTransitionRequest
    {
        public string FromState { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string ToState { get; set; } = string.Empty;
    }
}