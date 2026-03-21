using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Models.DTOs;

namespace FormalStructuresWebApp.Services.Interfaces
{
    public interface IAutomatonValidationService
    {
        ValidationResultDto Validate(FiniteAutomaton automaton);
    }
}