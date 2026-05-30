namespace FormalStructuresWebApp.Models.Domain
{
    public class PdaState
    {
        public string Name { get; set; } = "";
        public bool IsStart { get; set; }
        public bool IsAccepting { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}
