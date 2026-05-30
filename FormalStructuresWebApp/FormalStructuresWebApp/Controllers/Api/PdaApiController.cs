using Microsoft.AspNetCore.Mvc;
using FormalStructuresWebApp.Models.DTOs;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.Pda;
using Microsoft.AspNetCore.Mvc;
using FormalStructuresWebApp.Models.Domain;

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
                return BadRequest(new { error = "Automat jest pusty." });

            var analyzer = new PdaAnalysisService();
            var analysis = analyzer.Analyze(pda);

            var accepted = analysis.AcceptsEmptyWord
                ? new[] { "ε" }.Concat(analysis.AcceptedWords).ToList()
                : analysis.AcceptedWords;

            // Zbuduj szczegółowy prompt z faktami algorytmicznymi
            var prompt = $@"Jesteś ekspertem od teorii języków formalnych.
                Poniżej znajdują się ZWERYFIKOWANE ALGORYTMICZNIE dane o niedeterministycznym
                automacie ze stosem (NPDA). Wszystkie fakty są w 100% poprawne.

                ════════════════════════════════════════
                DANE STRUKTURALNE
                ════════════════════════════════════════
                Alfabet wejściowy : {{{string.Join(", ", pda.InputAlphabet)}}}
                Alfabet stosu     : {{{string.Join(", ", pda.StackAlphabet)}}}
                Symbol startowy stosu: {pda.StartStackSymbol}
                Tryb akceptacji   : {(pda.AcceptanceMode == PdaAcceptanceMode.EmptyStack ? "pusty stos" : "stany akceptujące")}
                Liczba stanów     : {pda.States.Count}
                Liczba przejść    : {pda.Transitions.Count}

                ════════════════════════════════════════
                ZACHOWANIE STOSU (analiza strukturalna)
                ════════════════════════════════════════
                {string.Join("\n", analysis.StackBehaviorNotes.Select(n => "• " + n))}

                ════════════════════════════════════════
                WYKRYTE WZORCE
                ════════════════════════════════════════
                {string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p))}

                ════════════════════════════════════════
                SŁOWA ZAAKCEPTOWANE (symulacja BFS)
                ════════════════════════════════════════
                {(accepted.Any() ? string.Join(", ", accepted) : "(brak w zakresie do długości 8)")}

                ════════════════════════════════════════
                SŁOWA ODRZUCONE (symulacja BFS)
                ════════════════════════════════════════
                {(analysis.RejectedWords.Any() ? string.Join(", ", analysis.RejectedWords) : "(brak)")}

                ════════════════════════════════════════
                FUNKCJA PRZEJŚĆ
                ════════════════════════════════════════
                {analysis.TransitionTableText}

                ════════════════════════════════════════
                TWOJE ZADANIE
                ════════════════════════════════════════
                Korzystając WYŁĄCZNIE z powyższych danych, podaj:

                1. NAZWA JĘZYKA: jednozdaniowa nazwa (np. ""język aⁿbⁿ"", ""palindromy nad {{a,b}}"")
                2. OPIS FORMALNY: opis matematyczny (np. L = {{aⁿbⁿ | n≥1}})
                3. GRAMATYKA CFG: napisz gramatykę bezkontekstową dla tego języka
                4. UZASADNIENIE: w 2–3 zdaniach wyjaśnij dlaczego struktura automatu
                   (szczególnie zachowanie stosu) wskazuje na ten język

                Jeśli dane są niewystarczające lub sprzeczne — napisz to wprost zamiast zgadywać.
                Odpowiadaj po polsku.";

            try
            {
                var modelResult = await _ollama.AskAsync(prompt);

                var full =
                    "── Analiza algorytmiczna ──────────────────────────\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    "\n\n── Zachowanie stosu ───────────────────────────────\n" +
                    string.Join("\n", analysis.StackBehaviorNotes.Select(n => "• " + n)) +
                    "\n\nSłowa akceptowane : " +
                    (accepted.Any() ? string.Join(", ", accepted) : "brak") +
                    "\nSłowa odrzucane   : " +
                    (analysis.RejectedWords.Any()
                        ? string.Join(", ", analysis.RejectedWords) : "brak") +
                    "\n\n── Interpretacja modelu ───────────────────────────\n" +
                    modelResult;

                return Ok(new { description = full });
            }
            catch (Exception ex)
            {
                // Fallback bez modelu — sama analiza algorytmiczna
                var fallback =
                    "── Analiza algorytmiczna (model niedostępny) ──────\n" +
                    string.Join("\n", analysis.DetectedPatterns.Select(p => "• " + p)) +
                    "\n\n── Zachowanie stosu ───────────────────────────────\n" +
                    string.Join("\n", analysis.StackBehaviorNotes.Select(n => "• " + n)) +
                    "\n\nSłowa akceptowane : " +
                    (accepted.Any() ? string.Join(", ", accepted) : "brak") +
                    "\nSłowa odrzucane   : " +
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
