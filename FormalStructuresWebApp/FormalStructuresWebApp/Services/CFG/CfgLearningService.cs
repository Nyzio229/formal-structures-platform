using FormalStructuresWebApp.Models.Domain;
using FormalStructuresWebApp.Services.Interfaces;

namespace FormalStructuresWebApp.Services.CFG
{
    public class CfgLearningResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public ContextFreeGrammar? OriginalGrammar { get; set; }
        public ContextFreeGrammar? CnfGrammar { get; set; }
        public PushdownAutomaton? Pda { get; set; }
        public Dictionary<string, bool> WordVerifications { get; set; } = new();
        public string RawLlmResponse { get; set; } = "";
    }

    public class CfgLearningService
    {
        private readonly IOllamaService _ollama;
        private readonly CfgParser _parser = new();
        private readonly CnfConverter _cnfConverter = new();
        private readonly CykAlgorithm _cyk = new();
        private readonly GrammarToPdaBuilder _pdaBuilder = new();

        public CfgLearningService(IOllamaService ollama)
        {
            _ollama = ollama;
        }

        public async Task<CfgLearningResult> LearnAsync(
            string description, List<string> alphabet)
        {
            var result = new CfgLearningResult();

            // KROK 1: LLM generuje gramatykę CFG
            var prompt = BuildGrammarPrompt(description, alphabet);
            var llmResponse = await _ollama.AskAsync(prompt);
            result.RawLlmResponse = llmResponse;

            // KROK 2: Parsuj gramatykę
            ContextFreeGrammar grammar;
            try
            {
                grammar = _parser.Parse(llmResponse);
                if (!grammar.Productions.Any())
                    throw new Exception("Parser nie znalazł żadnych produkcji.");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Błąd parsowania gramatyki: {ex.Message}";
                return result;
            }

            result.OriginalGrammar = grammar;

            // KROK 3: Konwersja do CNF (algorytm formalny)
            var cnf = _cnfConverter.Convert(grammar);
            result.CnfGrammar = cnf;

            // KROK 4: Weryfikacja CYK na przykładowych słowach
            var testWords = GenerateTestWords(alphabet, maxLen: 4);
            result.WordVerifications = _cyk.VerifyWords(cnf, testWords);

            // KROK 5: Budowa PDA (algorytm formalny)
            var pda = _pdaBuilder.Build(grammar);
            result.Pda = pda;

            result.Success = true;
            return result;
        }

        private string BuildGrammarPrompt(string description, List<string> alphabet)
        {
            var alphabetStr = alphabet.Any()
                ? string.Join(", ", alphabet)
                : "wywnioskuj z opisu";

            return $@"Twoim zadaniem jest zapisanie gramatyki bezkontekstowej (CFG).

                OPIS JĘZYKA: {description}
                ALFABET TERMINALI: {alphabetStr}

                ZASADY — przestrzegaj ściśle:
                - Każda produkcja w osobnej linii, format: A -> prawa_strona
                - Alternatywy oddzielaj: |
                - Symbol pusty: ε
                - Nieterminale: TYLKO wielkie litery lub wielkie litery z cyfrą (S, A, B, S1, A1)
                - Terminale: TYLKO małe litery lub cyfry (a, b, 0, 1)
                - NIE łącz terminali z nieterminalami w nazwach (nie pisz aA, bB itp.)

                PRZYKŁAD dla języka aⁿbⁿ (n≥1):
                S -> a S b | a b

                Napisz TYLKO produkcje gramatyki, zero wyjaśnień.";
        }

        private List<string> GenerateTestWords(List<string> alphabet, int maxLen)
        {
            var words = new List<string> { "" }; // słowo puste
            var queue = new Queue<string>();
            queue.Enqueue("");

            while (queue.Count > 0)
            {
                var w = queue.Dequeue();
                if (w.Length >= maxLen) continue;
                foreach (var sym in alphabet)
                {
                    var next = w + sym;
                    words.Add(next);
                    queue.Enqueue(next);
                }
            }
            return words;
        }
    }
}