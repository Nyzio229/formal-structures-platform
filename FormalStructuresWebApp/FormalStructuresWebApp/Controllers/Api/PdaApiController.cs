using Microsoft.AspNetCore.Mvc;
using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.Pda;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers.Api
{
    [ApiController]
    [Route("api/pda")]
    public class PdaApiController : ControllerBase
    {
        private readonly IPdaSessionService _session;
        private readonly IPdaEditorService _editor;
        private readonly IOllamaService _ollama;

        public PdaApiController(
            IPdaSessionService session,
            IPdaEditorService editor,
            IOllamaService ollama)
        {
            _session = session;
            _editor = editor;
            _ollama = ollama;
        }

        //public IActionResult Index()
        //{
        //    return View();
        //}

        [HttpGet("current")]
        public IActionResult GetCurrent() => Ok(_session.GetPda());

        [HttpPost("add-state")]
        public IActionResult AddState([FromBody] AddPdaStateRequest req)
        {
            var pda = _session.GetPda();
            _editor.AddState(pda, req.Name, req.IsStart, req.IsAccepting, req.X, req.Y);
            _session.SetPda(pda);
            return Ok(pda);
        }

        [HttpPost("remove-state")]
        public IActionResult RemoveState([FromBody] RemovePdaStateRequest req)
        {
            var pda = _session.GetPda();
            _editor.RemoveState(pda, req.StateName);
            _session.SetPda(pda);
            return Ok(pda);
        }

        [HttpPost("add-transition")]
        public IActionResult AddTransition([FromBody] AddPdaTransitionRequest req)
        {
            var pda = _session.GetPda();
            _editor.AddTransition(pda,
                req.FromState, req.InputSymbol,
                req.StackTop, req.PushSymbols, req.ToState);
            _session.SetPda(pda);
            return Ok(pda);
        }

        [HttpPost("remove-transition")]
        public IActionResult RemoveTransition([FromBody] AddPdaTransitionRequest req)
        {
            var pda = _session.GetPda();
            _editor.RemoveTransition(pda,
                req.FromState, req.InputSymbol, req.StackTop, req.ToState);
            _session.SetPda(pda);
            return Ok(pda);
        }

        [HttpPost("update-layout")]
        public IActionResult UpdateLayout([FromBody] UpdatePdaLayoutRequest req)
        {
            var pda = _session.GetPda();
            var positions = req.States.Select(s => (s.Name, s.X, s.Y)).ToList();
            _editor.UpdateStatePositions(pda, positions);
            _session.SetPda(pda);
            return Ok(pda);
        }

        [HttpPost("identify-language")]
        public async Task<IActionResult> IdentifyLanguage()
        {
            var pda = _session.GetPda();

            if (pda.States.Count == 0)
                return BadRequest(new
                {
                    error = "Automat jest pusty. Dodaj stany i przejścia."
                });

            // Faza 1: analiza algorytmiczna
            var analyzer = new PdaAnalysisService();
            var analysis = analyzer.Analyze(pda);

            var accepted = analysis.AcceptsEmptyWord
                ? new[] { "ε" }.Concat(analysis.AcceptedWords).ToList()
                : analysis.AcceptedWords;

            // Faza 2: model dostaje gotowe fakty
            var transitions = pda.Transitions.Select(t =>
            {
                var inp = t.InputSymbol ?? "ε";
                var push = t.PushSymbols.Any()
                    ? string.Join(" ", t.PushSymbols) : "ε";
                return $"  δ({t.FromState}, {inp}, {t.StackTop}) → ({t.ToState}, {push})";
            });

            var prompt = $@"Poniżej są ZWERYFIKOWANE ALGORYTMICZNIE fakty o niedeterministycznym automacie ze stosem (PDA).

                === FAKTY (wyznaczone algorytmicznie) ===
                {string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p))}

                === SŁOWA AKCEPTOWANE (zweryfikowane symulacją) ===
                {(accepted.Any() ? string.Join(", ", accepted) : "(brak w zakresie testowym)")}

                === SŁOWA ODRZUCANE ===
                {(analysis.RejectedWords.Any() ? string.Join(", ", analysis.RejectedWords) : "(brak w zakresie testowym)")}

                === FUNKCJA PRZEJŚĆ ===
                {string.Join("\n", transitions)}

                === ZADANIE ===
                Na podstawie powyższych faktów opisz jaki język bezkontekstowy rozpoznaje ten PDA:
                1. Zwięzły opis słowny (1–2 zdania)
                2. Gramatykę CFG lub wzorzec (np. aⁿbⁿ, palindromy)
                3. Krótkie uzasadnienie oparte na podanych przykładach

                Odpowiadaj po polsku. Opieraj się wyłącznie na podanych danych.";

            try
            {
                var modelResult = await _ollama.AskAsync(prompt);
                var full =
                    "── Analiza algorytmiczna ──────────────────\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    "\n\nSłowa akceptowane: " +
                    (accepted.Any() ? string.Join(", ", accepted) : "brak") +
                    "\nSłowa odrzucane:   " +
                    (analysis.RejectedWords.Any()
                        ? string.Join(", ", analysis.RejectedWords) : "brak") +
                    "\n\n── Interpretacja modelu ───────────────────\n" +
                    modelResult;

                return Ok(new { description = full });
            }
            catch (Exception ex)
            {
                var fallback =
                    "── Analiza algorytmiczna (model niedostępny) ──\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    "\n\nSłowa akceptowane: " +
                    (accepted.Any() ? string.Join(", ", accepted) : "brak") +
                    "\nSłowa odrzucane: " +
                    (analysis.RejectedWords.Any()
                        ? string.Join(", ", analysis.RejectedWords) : "brak");

                return Ok(new
                {
                    description = fallback,
                    warning = $"Model niedostępny: {ex.Message}"
                });
            }
        }
    }
}
