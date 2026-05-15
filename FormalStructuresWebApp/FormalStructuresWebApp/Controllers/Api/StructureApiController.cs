using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/structures")]
    public class StructuresApiController : ControllerBase
    {
        private readonly IAutomatonSessionService _sessionService;
        private readonly IAutomatonEditorService _editorService;
        private readonly IOllamaService _ollamaService;

        public StructuresApiController(
            IAutomatonSessionService sessionService,
            IAutomatonEditorService editorService,
            IOllamaService ollamaService)
        {
            _sessionService = sessionService;
            _editorService = editorService;
            _ollamaService = ollamaService;
        }

        [HttpPost("identify-language")]
        public async Task<IActionResult> IdentifyLanguage()
        {
            var automaton = _sessionService.GetAutomaton();

            if (automaton == null || !automaton.States.Any())
                return BadRequest(new { error = "Automat jest pusty. Dodaj stany i przejścia przed identyfikacją języka." });

            var stateLines = automaton.States.Select(s =>
            {
                var flags = new List<string>();
                if (s.IsStart) flags.Add("początkowy");
                if (s.IsAccepting) flags.Add("akceptujący");
                var flagStr = flags.Any() ? $" [{string.Join(", ", flags)}]" : "";
                return $"  - {s.Name}{flagStr}";
            });

            var transitionLines = automaton.Transitions.Select(t =>
                $"  - δ({t.FromState}, {t.Symbol}) = {t.ToState}");

            var prompt = $@"Masz dany deterministyczny automat skończony (DFA) opisany poniżej.

                Alfabet: {{{string.Join(", ", automaton.Alphabet)}}}

                Stany:
                {string.Join("\n", stateLines)}

                Funkcja przejść:
                {string.Join("\n", transitionLines)}

                Stan początkowy: {automaton.StartState}
                Stany akceptujące: {string.Join(", ", automaton.AcceptingStates)}

                Przeanalizuj ten automat i opisz dokładnie jaki język formalny rozpoznaje.
                Podaj:
                1. Zwięzły opis słowny języka (jedno lub dwa zdania)
                2. Wyrażenie regularne opisujące język (jeśli możliwe)
                3. Kilka przykładów słów należących do języka i kilka nienależących

                Odpowiadaj po polsku.";

            try
            {
                var result = await _ollamaService.AskAsync(prompt);
                return Ok(new { description = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Błąd połączenia z modelem: {ex.Message}" });
            }
        }

        [HttpGet("current")]
        public IActionResult GetCurrent()
        {
            var automaton = _sessionService.GetAutomaton();
            return Ok(automaton);
        }

        [HttpPost("add-state")]
        public IActionResult AddState([FromBody] AddStateRequest request)
        {
            var automaton = _sessionService.GetAutomaton();

            _editorService.AddState(
                automaton,
                request.Name,
                request.IsStart,
                request.IsAccepting,
                request.X,
                request.Y);

            _sessionService.SetAutomaton(automaton);

            return Ok(automaton);
        }

        [HttpPost("remove-state")]
        public IActionResult RemoveState([FromBody] RemoveStateRequest request)
        {
            var automaton = _sessionService.GetAutomaton();

            _editorService.RemoveState(automaton, request.StateName);
            _sessionService.SetAutomaton(automaton);

            return Ok(automaton);
        }

        [HttpPost("add-transition")]
        public IActionResult AddTransition([FromBody] AddTransitionRequest request)
        {
            var automaton = _sessionService.GetAutomaton();

            _editorService.AddTransition(
                automaton,
                request.FromState,
                request.Symbol,
                request.ToState);

            _sessionService.SetAutomaton(automaton);

            return Ok(automaton);
        }

        [HttpPost("remove-transition")]
        public IActionResult RemoveTransition([FromBody] RemoveTransitionRequest request)
        {
            var automaton = _sessionService.GetAutomaton();

            _editorService.RemoveTransition(
                automaton,
                request.FromState,
                request.Symbol,
                request.ToState);

            _sessionService.SetAutomaton(automaton);

            return Ok(automaton);
        }

        [HttpPost("update-layout")]
        public IActionResult UpdateLayout([FromBody] UpdateLayoutRequest request)
        {
            var automaton = _sessionService.GetAutomaton();

            var positions = request.States
                .Select(s => (s.Name, s.X, s.Y))
                .ToList();

            _editorService.UpdateStatePositions(automaton, positions);
            _sessionService.SetAutomaton(automaton);

            return Ok(automaton);
        }


        [HttpPost("set-alphabet")]
        public IActionResult SetAlphabet([FromBody] SetAlphabetRequest request)
        {
            var automaton = _sessionService.GetAutomaton();
            automaton.Alphabet = request.Symbols
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
            _sessionService.SetAutomaton(automaton);
            return Ok(automaton);
        }

    }
}