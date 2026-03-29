using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.Session
{
    public class AutomatonSessionService : IAutomatonSessionService
    {
        private FiniteAutomaton _automaton = new();

        public FiniteAutomaton GetAutomaton()
        {
            return _automaton;
        }

        public void SetAutomaton(FiniteAutomaton automaton)
        {
            _automaton = automaton;
        }
    }
}