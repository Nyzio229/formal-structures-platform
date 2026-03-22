namespace FormalStructuresWebApp.Models.DTOs
{
    public class UpdateLayoutRequest
    {
        public List<StatePositionDto> States { get; set; } = new();
    }

    public class StatePositionDto
    {
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
    }
}