using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAutomatonSessionService
    {
        FiniteAutomaton GetAutomaton();
        void SetAutomaton(FiniteAutomaton automaton);
    }
}