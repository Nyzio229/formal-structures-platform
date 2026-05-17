using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Automaton;
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

            // FAZA 1: analiza algorytmiczna
            var analyzer = new AutomatonLanguageAnalyzer();
            var analysis = analyzer.Analyze(automaton);

            // Jeśli język pusty/uniwersalny — odpowiadamy bez modelu
            if (analysis.IsLanguageEmpty)
                return Ok(new
                {
                    description = "✓ Analiza algorytmiczna: automat nie akceptuje żadnego słowa — język jest pusty.",
                    algorithmic = true
                });

            if (analysis.IsLanguageUniversal)
                return Ok(new
                {
                    description = "✓ Analiza algorytmiczna: automat akceptuje każde słowo nad podanym alfabetem — język jest uniwersalny (Σ*).",
                    algorithmic = true
                });

            // FAZA 2: model dostaje gotowe fakty, nie musi liczyć
            var acceptedDisplay = analysis.AcceptsEmptyWord
                ? new[] { "ε" }.Concat(analysis.AcceptedWords).ToList()
                : analysis.AcceptedWords;

            var prompt = $@"Poniżej znajdują się ZWERYFIKOWANE ALGORYTMICZNIE fakty dotyczące deterministycznego automatu skończonego (DFA). Nie kwestionuj tych danych — są w 100% poprawne.

                === ALFABET ===
                {{ {string.Join(", ", automaton.Alphabet)} }}

                === FAKTY O JĘZYKU (wyznaczone algorytmicznie) ===
                {string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p))}

                === PRZYKŁADY SŁÓW AKCEPTOWANYCH ===
                {(acceptedDisplay.Any() ? string.Join(", ", acceptedDisplay) : "(brak w zakresie do długości 6)")}

                === PRZYKŁADY SŁÓW ODRZUCANYCH ===
                {(analysis.RejectedWords.Any() ? string.Join(", ", analysis.RejectedWords) : "(brak w zakresie do długości 6)")}

                === TABELA PRZEJŚĆ ===
                {analysis.TransitionTableText}

                === TWOJE ZADANIE ===
                Na podstawie powyższych faktów:
                1. Podaj zwięzły opis słowny języka (1–2 zdania)
                2. Podaj wyrażenie regularne (jeśli język jest regularny i da się zwięźle zapisać)
                3. Krótko uzasadnij swój opis, odwołując się do podanych przykładów

                Odpowiadaj po polsku. Nie wymyślaj faktów — opieraj się wyłącznie na danych powyżej.";

            try
            {
                var modelDescription = await _ollamaService.AskAsync(prompt);
                var fullResponse =
                    $"── Analiza algorytmiczna ──────────────────\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    $"\n\nSłowa akceptowane: {(acceptedDisplay.Any() ? string.Join(", ", acceptedDisplay) : "brak")}" +
                    $"\nSłowa odrzucane:   {(analysis.RejectedWords.Any() ? string.Join(", ", analysis.RejectedWords) : "brak")}" +
                    $"\n\n── Interpretacja modelu ───────────────────\n" +
                    modelDescription;

                return Ok(new { description = fullResponse });
            }
            catch (Exception ex)
            {
                // Jeśli model niedostępny — zwróć samą analizę algorytmiczną
                var fallback =
                    $"── Analiza algorytmiczna (model niedostępny) ──\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    $"\n\nSłowa akceptowane: {(acceptedDisplay.Any() ? string.Join(", ", acceptedDisplay) : "brak")}" +
                    $"\nSłowa odrzucane:   {(analysis.RejectedWords.Any() ? string.Join(", ", analysis.RejectedWords) : "brak")}";

                return Ok(new { description = fallback, warning = $"Model niedostępny: {ex.Message}" });
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