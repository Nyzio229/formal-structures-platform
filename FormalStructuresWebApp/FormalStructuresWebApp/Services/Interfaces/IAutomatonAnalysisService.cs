using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAutomatonAnalysisService
    {
        List<string> Analyze(FiniteAutomaton automaton);
    }
}