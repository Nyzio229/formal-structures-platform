using FormalStructuresWebApp.Models.ViewModels;
using FormalStructuresWebApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers
{
    public class StructuresController : Controller
    {
        private readonly IAiGenerationService _aiGenerationService;
        private readonly IAutomatonValidationService _validationService;
        private readonly IAutomatonAnalysisService _analysisService;

        public StructuresController(
            IAiGenerationService aiGenerationService,
            IAutomatonValidationService validationService,
            IAutomatonAnalysisService analysisService)
        {
            _aiGenerationService = aiGenerationService;
            _validationService = validationService;
            _analysisService = analysisService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Generate()
        {
            return View(new StructureEditorViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> Generate(StructureEditorViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError(nameof(model.Description), "Opis nie może być pusty.");
                return View(model);
            }

            var generationResult = await _aiGenerationService.GenerateAutomatonFromDescriptionAsync(model.Description);

            if (!generationResult.Success || generationResult.Automaton == null)
            {
                ModelState.AddModelError(string.Empty, generationResult.Message);
                return View(model);
            }

            model.Automaton = generationResult.Automaton;
            model.ValidationResult = _validationService.Validate(generationResult.Automaton);
            model.AnalysisMessages = _analysisService.Analyze(generationResult.Automaton);

            return View(model);
        }
    }
}