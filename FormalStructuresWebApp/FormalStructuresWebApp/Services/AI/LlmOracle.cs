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
            var wordDisplay = word == "" ? "ε (słowo puste)" : $"'{word}'";

            var prompt = $@"Rozważamy język formalny nad alfabetem {{0,1}}.
Opis języka: {_description}

Czy słowo {wordDisplay} należy do tego języka?
Odpowiedz WYŁĄCZNIE słowem TAK lub NIE — zero innych słów, zero wyjaśnień.";

            var result = await _ollama.AskAsync(prompt);
            RawResponses.Add($"[{word}] → {result.Trim()}");

            if (string.IsNullOrWhiteSpace(result))
                throw new Exception("Pusta odpowiedź z LLM");

            var normalized = result.Trim().ToUpper();

            if (normalized.Contains("TAK")) return true;
            if (normalized.Contains("NIE")) return false;

            // Fallback zamiast wyjątku — loguj i zwróć false
            Console.WriteLine($"[WARN] Niepoprawna odpowiedź LLM dla '{word}': {result}");
            return false;
        }
    }
}
