using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.Automaton
{
    public class AutomatonAnalysisService : IAutomatonAnalysisService
    {
        public List<string> Analyze(FiniteAutomaton automaton)
        {
            var messages = new List<string>();

            messages.Add($"Liczba stanów: {automaton.States.Count}");
            messages.Add($"Liczba symboli alfabetu: {automaton.Alphabet.Count}");
            messages.Add($"Liczba przejść: {automaton.Transitions.Count}");
            messages.Add($"Liczba stanów akceptujących: {automaton.States.Count(s => s.IsAccepting)}");

            var isDeterministic = automaton.Transitions
                .GroupBy(t => new { t.FromState, t.Symbol })
                .All(g => g.Count() <= 1);

            messages.Add(isDeterministic
                ? "Automat jest deterministyczny."
                : "Automat nie jest deterministyczny.");

            return messages;
        }
    }
}