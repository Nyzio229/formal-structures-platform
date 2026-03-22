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
        private readonly IAutomatonSessionService _sessionService;

        public StructuresController(
            IAiGenerationService aiGenerationService,
            IAutomatonValidationService validationService,
            IAutomatonAnalysisService analysisService,
            IAutomatonSessionService sessionService)
        {
            _aiGenerationService = aiGenerationService;
            _validationService = validationService;
            _analysisService = analysisService;
            _sessionService = sessionService;
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
            _sessionService.SetAutomaton(generationResult.Automaton);
            model.ValidationResult = _validationService.Validate(generationResult.Automaton);
            model.AnalysisMessages = _analysisService.Analyze(generationResult.Automaton);

            return View(model);
        }

        [HttpGet]
        public IActionResult Editor()
        {
            var automaton = _sessionService.GetAutomaton();

            var model = new StructureEditorViewModel
            {
                Automaton = automaton
            };

            return View(model);
        }
    }
}