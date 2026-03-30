using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.AI
{
    public class LlmOracle : IAutomatonOracle
    {
        private readonly IOllamaService _ollama;
        private readonly string _description;
        public List<string> RawResponses { get; } = new(); 

        public LlmOracle(IOllamaService ollama, string description)
        {
            _ollama = ollama;
            _description = description;
        }

        public async Task<bool> MembershipQuery(string word)
        {
            var prompt = $@"
                Opis języka:
                {_description}

                Czy słowo '{word}' należy do języka?

                Odpowiedz tylko: TAK lub NIE.
                ";

            try
            {
                var result = await _ollama.AskAsync(prompt);
                RawResponses.Add($"[{word}] → {result.Trim()}");

                if (string.IsNullOrWhiteSpace(result))
                    throw new Exception("Pusta odpowiedź z LLM");

                var normalized = result.Trim().ToUpper();

                if (normalized.Contains("TAK"))
                    return true;

                if (normalized.Contains("NIE"))
                    return false;

                throw new Exception($"Niepoprawna odpowiedź LLM: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Oracle error: {ex.Message}");

                // ważne: NIE udawaj wyniku
                throw;
            }
        }
    }
}
