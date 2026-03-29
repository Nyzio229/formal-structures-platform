using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.Automaton
{
    public class AutomatonValidationService : IAutomatonValidationService
    {
        public ValidationResultDto Validate(FiniteAutomaton automaton)
        {
            var errors = new List<string>();

            if (automaton.States.Count == 0)
                errors.Add("Automat musi zawierać co najmniej jeden stan.");

            if (automaton.Alphabet.Count == 0)
                errors.Add("Alfabet nie może być pusty.");

            if (automaton.States.Count(s => s.IsStart) != 1)
                errors.Add("Automat musi mieć dokładnie jeden stan początkowy.");

            var stateNames = automaton.States.Select(s => s.Name).ToHashSet();

            foreach (var transition in automaton.Transitions)
            {
                if (!stateNames.Contains(transition.FromState))
                    errors.Add($"Przejście odwołuje się do nieistniejącego stanu źródłowego: {transition.FromState}");

                if (!stateNames.Contains(transition.ToState))
                    errors.Add($"Przejście odwołuje się do nieistniejącego stanu docelowego: {transition.ToState}");

                if (!automaton.Alphabet.Contains(transition.Symbol))
                    errors.Add($"Symbol '{transition.Symbol}' nie należy do alfabetu.");
            }

            return new ValidationResultDto
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }
    }
}