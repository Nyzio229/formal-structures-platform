using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services
{
    public class AiGenerationService : IAiGenerationService
    {
        public Task<GenerateStructureResponse> GenerateAutomatonFromDescriptionAsync(string description)
        {
            var automaton = new FiniteAutomaton
            {
                Name = "Generated DFA",
                StructureType = FormalStructureType.DeterministicFiniteAutomaton,
                Alphabet = new List<string> { "a", "b" },
                States = new List<State>
                {
                    new State { Name = "q0", IsStart = true, IsAccepting = false, X = 100, Y = 150 },
                    new State { Name = "q1", IsStart = false, IsAccepting = false, X = 300, Y = 80 },
                    new State { Name = "q2", IsStart = false, IsAccepting = true, X = 500, Y = 150 }
                },
                Transitions = new List<Transition>
                {
                    new Transition { FromState = "q0", Symbol = "a", ToState = "q1" },
                    new Transition { FromState = "q0", Symbol = "b", ToState = "q0" },
                    new Transition { FromState = "q1", Symbol = "a", ToState = "q1" },
                    new Transition { FromState = "q1", Symbol = "b", ToState = "q2" },
                    new Transition { FromState = "q2", Symbol = "a", ToState = "q1" },
                    new Transition { FromState = "q2", Symbol = "b", ToState = "q0" }
                }
            };

            return Task.FromResult(new GenerateStructureResponse
            {
                Success = true,
                Message = "Automat został wygenerowany na podstawie opisu. (testowy, bez modelu)",
                Automaton = automaton
            });
        }
    }
}