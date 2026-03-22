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

        public StructuresApiController(
            IAutomatonSessionService sessionService,
            IAutomatonEditorService editorService)
        {
            _sessionService = sessionService;
            _editorService = editorService;
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
    }
}