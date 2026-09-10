using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Common
{
    /// <summary>
    /// Gwarantuje, że DFA jest CAŁKOWITY — ma przejście dla każdej pary
    /// stan×symbol. Brakujące przejścia kierowane są do jawnego,
    /// nieakceptującego stanu pułapki (samopętla na każdym symbolu).
    ///
    /// Używane zarówno przez L* (gdy tabela nie została w pełni domknięta
    /// z powodu budżetu czasowego), jak i przez konstrukcję z wyrażenia
    /// regularnego (subset construction z NFA nie gwarantuje totalności,
    /// jeśli NFA miał "martwe" ścieżki).
    /// </summary>
    public static class DfaCompletion
    {
        public static bool CompleteWithTrapState(FiniteAutomaton automaton, List<string> alphabet)
        {
            var existingNames = automaton.States.Select(s => s.Name).ToHashSet();
            var haveTransition = automaton.Transitions
                .Select(t => (t.FromState, t.Symbol))
                .ToHashSet();

            var missing = automaton.States
                .SelectMany(s => alphabet.Select(a => (s.Name, a)))
                .Where(pair => !haveTransition.Contains(pair))
                .ToList();

            if (!missing.Any())
                return false; // automat już był całkowity

            var trapName = "trap";
            while (existingNames.Contains(trapName)) trapName += "_";

            foreach (var (fromState, symbol) in missing)
            {
                automaton.Transitions.Add(new Transition
                {
                    FromState = fromState,
                    Symbol = symbol,
                    ToState = trapName
                });
            }

            automaton.States.Add(new State
            {
                Name = trapName,
                IsStart = false,
                IsAccepting = false,
                X = 100 + automaton.States.Count * 200,
                Y = 350
            });

            foreach (var symbol in alphabet)
            {
                automaton.Transitions.Add(new Transition
                {
                    FromState = trapName,
                    Symbol = symbol,
                    ToState = trapName
                });
            }

            return true;
        }
    }
}