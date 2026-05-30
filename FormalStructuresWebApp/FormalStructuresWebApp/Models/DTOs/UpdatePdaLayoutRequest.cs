namespace FormalStructuresWebApp.Models.DTOs
{
    public class UpdatePdaLayoutRequest
    {
        public List<PdaStatePosition> States { get; set; } = new();
    }

    public class PdaStatePosition
    {
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
    }
}