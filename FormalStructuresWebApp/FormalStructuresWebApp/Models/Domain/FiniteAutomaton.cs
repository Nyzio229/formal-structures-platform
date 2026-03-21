namespace FormalStructuresWebApp.Models.Domain
{
    public class FiniteAutomaton
    {
        public string Name { get; set; } = "New Automaton";
        public FormalStructureType StructureType { get; set; } = FormalStructureType.DeterministicFiniteAutomaton;

        public List<State> States { get; set; } = new();
        public List<string> Alphabet { get; set; } = new();
        public List<Transition> Transitions { get; set; } = new();

        public string StartState => States.FirstOrDefault(x => x.IsStart)?.Name ?? string.Empty;

        public List<string> AcceptingStates =>
            States.Where(x => x.IsAccepting).Select(x => x.Name).ToList();
    }
}
