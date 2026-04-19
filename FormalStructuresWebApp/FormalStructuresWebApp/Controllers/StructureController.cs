using FormalStructuresWebApp.Models.ViewModels;
using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.LStar;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers
{
    public class StructuresController : Controller
    {
        private readonly IAiGenerationService _aiGenerationService;
        private readonly IAutomatonValidationService _validationService;
        private readonly IAutomatonAnalysisService _analysisService;
        private readonly IAutomatonSessionService _sessionService;
        private readonly IOllamaService _ollamaService;
        private readonly LStarService _lstarService;

        public StructuresController(
            IAiGenerationService aiGenerationService,
            IAutomatonValidationService validationService,
            IAutomatonAnalysisService analysisService,
            IAutomatonSessionService sessionService,
            IOllamaService ollamaService,
            LStarService lstarService)
        {
            _aiGenerationService = aiGenerationService;
            _validationService = validationService;
            _analysisService = analysisService;
            _sessionService = sessionService;
            _ollamaService = ollamaService;
            _lstarService = lstarService;
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

        //---------------------------- GENERATE stary ------------------------------------

        //[HttpPost]
        //public async Task<IActionResult> Generate(StructureEditorViewModel model)
        //{
        //    if (string.IsNullOrWhiteSpace(model.Description))
        //    {
        //        ModelState.AddModelError(nameof(model.Description), "Opis nie może być pusty.");
        //        return View(model);
        //    }

        //    var generationResult = await _aiGenerationService.GenerateAutomatonFromDescriptionAsync(model.Description);

        //    if (!generationResult.Success || generationResult.Automaton == null)
        //    {
        //        ModelState.AddModelError(string.Empty, generationResult.Message);
        //        return View(model);
        //    }

        //    model.Automaton = generationResult.Automaton;
        //    _sessionService.SetAutomaton(generationResult.Automaton);
        //    model.ValidationResult = _validationService.Validate(generationResult.Automaton);
        //    model.AnalysisMessages = _analysisService.Analyze(generationResult.Automaton);

        //    return View(model);
        //}


        //---------------------------- GENERATE nowy ------------------------------------
        [HttpPost]
        public async Task<IActionResult> Generate(StructureEditorViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError(nameof(model.Description), "Opis nie może być pusty.");
                return View(model);
            }

            // Parsuj alfabet z formularza (np. "0,1" → ["0","1"])
            var alphabet = model.Alphabet.Split(',').Select(a => a.Trim()).ToList();

            // Stwórz oracle korzystający z Llamy
            var oracle = new LlmOracle(_ollamaService, model.Description);

            // Uruchom algorytm L*
            var automaton = await _lstarService.LearnAsync(oracle, alphabet);

            model.Automaton = automaton;
            model.RawOllamaResponses = oracle.RawResponses;
            _sessionService.SetAutomaton(automaton);
            model.ValidationResult = _validationService.Validate(automaton);
            model.AnalysisMessages = _analysisService.Analyze(automaton);

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