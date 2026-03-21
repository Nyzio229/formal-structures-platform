using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Models.DTOs
{
    public class ValidateStructureRequest
    {
        public FiniteAutomaton Automaton { get; set; } = new();
    }
}