using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Models.ViewModels
{
    public class EditAutomatonViewModel
    {
        public FiniteAutomaton Automaton { get; set; } = new();

        public string NewStateName { get; set; } = string.Empty;
        public bool NewStateIsStart { get; set; }
        public bool NewStateIsAccepting { get; set; }

        public string TransitionFromState { get; set; } = string.Empty;
        public string TransitionSymbol { get; set; } = string.Empty;
        public string TransitionToState { get; set; } = string.Empty;

        public string InputWord { get; set; } = string.Empty;
        public bool? SimulationAccepted { get; set; }
        public List<string> SimulationSteps { get; set; } = new();
    }
}