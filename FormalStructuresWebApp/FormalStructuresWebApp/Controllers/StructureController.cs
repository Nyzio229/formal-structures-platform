using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Models.ViewModels;
using FormalStructuresWebApp.Services.AI;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.LStar;
using FormalStructuresWebApp.Services.Regex;
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
        private readonly RegexDfaLearningService _regexDfaService;

        public StructuresController(
            IAiGenerationService aiGenerationService,
            IAutomatonValidationService validationService,
            IAutomatonAnalysisService analysisService,
            IAutomatonSessionService sessionService,
            IOllamaService ollamaService,
            LStarService lstarService,
            RegexDfaLearningService regexDfaService)
        {
            _aiGenerationService = aiGenerationService;
            _validationService = validationService;
            _analysisService = analysisService;
            _sessionService = sessionService;
            _ollamaService = ollamaService;
            _lstarService = lstarService;
            _regexDfaService = regexDfaService;
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
            var alphabet = string.IsNullOrWhiteSpace(model.Alphabet)
                ? new List<string>()  // pusty = wyciągnij z opisu
                : model.Alphabet.Split(',').Select(a => a.Trim()).ToList();

            var rawResponses = new List<string>();

            // Jeśli alfabet nie podany — wyciągnij go raz (użyjemy tego samego
            // oracle niżej, jeśli trzeba będzie spaść do L*, żeby nie pytać LLM
            // o alfabet dwa razy).
            LlmOracle? extractionOracle = null;
            if (!alphabet.Any())
            {
                extractionOracle = new LlmOracle(_ollamaService, model.Description);
                alphabet = await extractionOracle.ExtractAlphabetAsync();
            }

            FiniteAutomaton? automaton = null;
            var genInfo = new GenerationInfo();

            // KROK 1: spróbuj szybszej, jednostrzałowej ścieżki regex → klasyczna
            // konstrukcja (Thompson → determinizacja → minimalizacja). Analogicznie
            // do tego, co już dobrze działa dla CFG — LLM generuje raz, resztę
            // robią algorytmy formalne, więc jest mniej okazji do pomyłki niż
            // przy L*, gdzie LLM jest odpytywany dziesiątki razy jako wyrocznia.
            if (alphabet.Any())
            {
                automaton = await _regexDfaService.TryLearnAsync(model.Description, alphabet);
                genInfo.RawRegex = _regexDfaService.RawRegex;
                genInfo.SanityCheckMismatches = _regexDfaService.LastSanityCheckMismatches >= 0
                    ? _regexDfaService.LastSanityCheckMismatches
                    : null;
                genInfo.Candidates = _regexDfaService.Candidates;

                if (automaton != null)
                {
                    genInfo.Source = "regex";
                    rawResponses.Add($"[regex→DFA] wyrażenie: {_regexDfaService.RawRegex}");
                }
                else
                {
                    genInfo.RegexFallbackReason = _regexDfaService.FallbackReason;
                    rawResponses.Add($"[regex→DFA] fallback do L*: {_regexDfaService.FallbackReason}");
                }
            }

            // KROK 2: fallback — pełne L*, jeśli ścieżka regexowa się nie powiodła
            // (regex niesparsowalny albo nie przeszedł szybkiej weryfikacji).
            if (automaton == null)
            {
                var oracle = extractionOracle ?? new LlmOracle(_ollamaService, model.Description);
                automaton = await _lstarService.LearnAsync(oracle, alphabet);
                rawResponses.AddRange(oracle.RawResponses);
                genInfo.Source = "lstar";
            }

            automaton.GenerationInfo = genInfo;

            model.Automaton = automaton;
            model.RawOllamaResponses = rawResponses;
            _sessionService.SetAutomaton(automaton);
            model.ValidationResult = _validationService.Validate(automaton);
            model.AnalysisMessages = _analysisService.Analyze(automaton);

            return View(model);
        }

        [HttpGet]
        public IActionResult Identify(string? sample = null)
        {
            FiniteAutomaton? automaton;

            if (sample == "ends_b")
            {
                // Przykład ze strony głównej: automat akceptujący słowa
                // kończące się na 'b' — wcześniej był to tylko statyczny SVG,
                // bez rzeczywistego obiektu automatu, więc "Edytuj graf
                // automatu" prowadziło do pustego edytora (a nawet to nie
                // działało z powodu literówki w linku: "Indentify").
                automaton = BuildEndsWithBSample();
                _sessionService.SetAutomaton(automaton);
            }
            else
            {
                automaton = _sessionService.GetAutomaton();
            }

            var model = new StructureEditorViewModel
            {
                Automaton = automaton
            };
            return View(model);
        }

        private static FiniteAutomaton BuildEndsWithBSample()
        {
            return new FiniteAutomaton
            {
                Name = "Przykład: słowa kończące się na b",
                StructureType = FormalStructureType.DeterministicFiniteAutomaton,
                Alphabet = new List<string> { "a", "b" },
                States = new List<State>
                {
                    new State { Name = "q0", IsStart = true,  IsAccepting = false, X = 80,  Y = 90 },
                    new State { Name = "q1", IsStart = false, IsAccepting = true,  X = 340, Y = 90 },
                },
                Transitions = new List<Transition>
                {
                    new Transition { FromState = "q0", Symbol = "a", ToState = "q0" },
                    new Transition { FromState = "q0", Symbol = "b", ToState = "q1" },
                    new Transition { FromState = "q1", Symbol = "a", ToState = "q0" },
                    new Transition { FromState = "q1", Symbol = "b", ToState = "q1" },
                },
            };
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