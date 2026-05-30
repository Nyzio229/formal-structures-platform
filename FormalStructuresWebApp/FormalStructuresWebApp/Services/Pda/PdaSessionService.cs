using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;
using System.Text.Json;

namespace FormalStructuresWebApp.Services.Pda
{
    public class PdaSessionService : IPdaSessionService
    {
        private const string SessionKey = "CurrentPda";
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PdaSessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public PushdownAutomaton GetPda()
        {
            var session = _httpContextAccessor.HttpContext!.Session;
            var json = session.GetString(SessionKey);
            if (string.IsNullOrEmpty(json))
                return new PushdownAutomaton();
            return JsonSerializer.Deserialize<PushdownAutomaton>(json)
                   ?? new PushdownAutomaton();
        }

        public void SetPda(PushdownAutomaton pda)
        {
            var session = _httpContextAccessor.HttpContext!.Session;
            session.SetString(SessionKey, JsonSerializer.Serialize(pda));
        }
    }
}