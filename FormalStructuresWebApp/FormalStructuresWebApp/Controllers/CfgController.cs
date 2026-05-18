using FormalStructuresWebApp.Models.ViewModels;
using FormalStructuresWebApp.Services.CFG;
using FormalStructuresWebApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers
{
    public class CfgController : Controller
    {
        private readonly IOllamaService _ollamaService;

        public CfgController(IOllamaService ollamaService)
        {
            _ollamaService = ollamaService;
        }

        [HttpGet]
        public IActionResult Generate() => View(new CfgViewModel());

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

            model.Result = result;
            return View(model);
        }
    }
}