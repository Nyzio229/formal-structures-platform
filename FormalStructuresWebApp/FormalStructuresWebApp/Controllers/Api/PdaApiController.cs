using Microsoft.AspNetCore.Mvc;

namespace FormalStructuresWebApp.Controllers.Api
{
    public class PdaApiController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
