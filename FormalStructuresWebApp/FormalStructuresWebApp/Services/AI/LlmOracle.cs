using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.AI
{
    public class LlmOracle : IAutomatonOracle
    {
        private readonly IOllamaService _ollama;
        private readonly string _description;
        public List<string> RawResponses { get; } = new();
        private readonly Dictionary<string, bool> _cache = new();

        public LlmOracle(IOllamaService ollama, string description)
        {
            _ollama = ollama;
            _description = description;
        }

        public async Task<bool> MembershipQuery(string word)
        {
            if (_cache.TryGetValue(word, out var cached))
            {
                RawResponses.Add($"[{word}] → (cache: {(cached ? "TAK" : "NIE")})");
                return cached;
            }

            var wordDisplay = word == "" ? "ε (słowo puste, nie ma żadnych znaków)" : $"'{word}'";
            var lastChar = word.Length > 0 ? $"Ostatni znak tego słowa to: '{word[^1]}'." : "To słowo puste — nie ma ostatniego znaku.";

            var prompt = $@"Opis języka: {_description}
                Analizowane słowo: {wordDisplay}
                {lastChar}

                Czy to słowo należy do języka? Odpowiedz TYLKO: TAK lub NIE.";

            var result = await _ollama.AskAsync(prompt);
            bool answer = ParseAnswer(result) ?? false;

            RawResponses.Add($"[{word}] → {(answer ? "TAK" : "NIE")}");
            _cache[word] = answer;
            return answer;
        }

        private bool? ParseAnswer(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return null;
            var normalized = result.Trim().ToUpper();
            if (normalized.Contains("TAK")) return true;
            if (normalized.Contains("NIE")) return false;
            return null;
        }

        public async Task<List<string>> ExtractAlphabetAsync()
        {
            // Najpierw spróbuj wyciągnąć alfabet z opisu za pomocą regex
            // Szuka wzorców: {0,1} lub {a,b} lub {a, b} itp.
            var match = System.Text.RegularExpressions.Regex.Match(
                _description,
                @"\{([^}]+)\}"
            );

            if (match.Success)
            {
                var symbols = match.Groups[1].Value
                    .Split(',')
                    .Select(s => s.Trim())
                    .Where(s => s.Length == 1 && char.IsLetterOrDigit(s[0]))
                    .Distinct()
                    .ToList();

                if (symbols.Count >= 2)
                {
                    Console.WriteLine($"[DEBUG] Alfabet z opisu (regex): {string.Join(",", symbols)}");
                    return symbols;
                }
            }

            // Fallback — zapytaj model tylko jeśli regex nie znalazł alfabetu
            var prompt = $@"Opis języka: {_description}
                Wypisz TYLKO symbole alfabetu oddzielone przecinkami.
                Przykład: a,b lub 0,1
                Odpowiedz WYŁĄCZNIE symbolami.";

            var result = await _ollama.AskAsync(prompt);
            var fromModel = result.Trim()
                .Split(',')
                .Select(s => s.Trim())
                .Where(s => s.Length == 1 && char.IsLetterOrDigit(s[0]))
                .Distinct()
                .ToList();

            if (fromModel.Count >= 2)
            {
                Console.WriteLine($"[DEBUG] Alfabet z modelu: {string.Join(",", fromModel)}");
                return fromModel;
            }

            // Ostateczny fallback
            Console.WriteLine("[DEBUG] Fallback alfabet: a,b");
            return new List<string> { "a", "b" };
        }
    }
}
