using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Models.DTOs
{
    public class GenerateStructureResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public FiniteAutomaton? Automaton { get; set; }
    }
}