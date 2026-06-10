using FormalStructuresWebApp.Models.ViewModels;
using FormalStructuresWebApp.Services.CFG;
using FormalStructuresWebApp.Services.Interfaces;
using FormalStructuresWebApp.Services.Pda;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers
{
    public class CfgController : Controller
    {
        private readonly IOllamaService _ollamaService;
        private readonly IPdaSessionService _pdaSessionService;

        public CfgController(IOllamaService ollamaService, IPdaSessionService pdaSessionService)
        {
            _ollamaService = ollamaService;
            _pdaSessionService = pdaSessionService;
        }

        [HttpGet]
        public IActionResult Generate() => View(new CfgViewModel());

        [HttpGet]
        public IActionResult PdaEditor()
        {
            var pda = _pdaSessionService.GetPda();
            return View(pda);
        }

        [HttpPost]
        public async Task<IActionResult> Generate(CfgViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Description))
            {
                ModelState.AddModelError(nameof(model.Description), "Opis nie może być pusty.");
                return View(model);
            }

            var alphabet = string.IsNullOrWhiteSpace(model.Alphabet)
                ? new List<string>()
                : model.Alphabet.Split(',').Select(a => a.Trim()).ToList();

            var service = new CfgLearningService(_ollamaService);
            var result = await service.LearnAsync(model.Description, alphabet);

            if (result.Success && result.Pda != null)
            {
                _pdaSessionService.SetPda(result.Pda);
            }

            model.Result = result;
            return View(model);
        }

        [HttpGet]
        public IActionResult PdaIdentify()
        {
            var pda = _pdaSessionService.GetPda();
            return View("PdaEditor", pda);
        }

    }
}