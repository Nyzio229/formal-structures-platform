using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/structures")]
    public class StructuresApiController : ControllerBase
    {
        private readonly IAiGenerationService _aiGenerationService;
        private readonly IAutomatonValidationService _validationService;
        private readonly IAutomatonAnalysisService _analysisService;

        public StructuresApiController(
            IAiGenerationService aiGenerationService,
            IAutomatonValidationService validationService,
            IAutomatonAnalysisService analysisService)
        {
            _aiGenerationService = aiGenerationService;
            _validationService = validationService;
            _analysisService = analysisService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] GenerateStructureRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
                return BadRequest("Opis nie może być pusty.");

            var result = await _aiGenerationService.GenerateAutomatonFromDescriptionAsync(request.Description);
            return Ok(result);
        }

        [HttpPost("validate")]
        public IActionResult Validate([FromBody] ValidateStructureRequest request)
        {
            var result = _validationService.Validate(request.Automaton);
            return Ok(result);
        }

        [HttpPost("analyze")]
        public IActionResult Analyze([FromBody] ValidateStructureRequest request)
        {
            var result = _analysisService.Analyze(request.Automaton);
            return Ok(result);
        }
    }
}