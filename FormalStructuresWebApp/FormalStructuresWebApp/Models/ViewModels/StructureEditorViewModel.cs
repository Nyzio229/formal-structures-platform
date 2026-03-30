using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Models.DTOs;

namespace FormalStructuresWebApp.Models.ViewModels
{
    public class StructureEditorViewModel
    {
        public string Description { get; set; } = string.Empty;
        public FiniteAutomaton? Automaton { get; set; }
        public ValidationResultDto? ValidationResult { get; set; }
        public List<string> AnalysisMessages { get; set; } = new();
        public List<string> RawOllamaResponses { get; set; } = new(); // DODAJ
    }
}