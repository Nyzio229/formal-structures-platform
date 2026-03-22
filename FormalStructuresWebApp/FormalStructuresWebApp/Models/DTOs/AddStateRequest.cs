namespace FormalStructuresWebApp.Models.DTOs
{
    public class AddStateRequest
    {
        public string Name { get; set; } = string.Empty;
        public bool IsStart { get; set; }
        public bool IsAccepting { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}