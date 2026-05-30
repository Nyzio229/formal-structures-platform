using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Pda
{
    public class PdaLanguageAnalysisResult
    {
        public bool AcceptsEmptyWord { get; set; }
        public List<string> AcceptedWords { get; set; } = new();
        public List<string> RejectedWords { get; set; } = new();
        public List<string> DetectedPatterns { get; set; } = new();
        public List<string> ReachableStates { get; set; } = new();
    }

    public class PdaAnalysisService
    {
        private const int MaxWordLen = 6;
        private const int MaxExamples = 5;
        private const int MaxConfigs = 8000;
        private const int MaxStackDepth = 15;

        public PdaLanguageAnalysisResult Analyze(PushdownAutomaton pda)
        {
            var result = new PdaLanguageAnalysisResult();

            result.ReachableStates = GetReachableStates(pda);
            result.AcceptsEmptyWord = SimulateWord(pda, new List<string>());

            // BFS po słowach rosnącej długości
            var words = GenerateWords(pda.InputAlphabet, MaxWordLen);
            foreach (var word in words)
            {
                bool accepted = SimulateWord(pda, word);
                var display = word.Count == 0 ? "ε" : string.Join("", word);

                if (accepted && result.AcceptedWords.Count < MaxExamples)
                    result.AcceptedWords.Add(display);
                else if (!accepted && result.RejectedWords.Count < MaxExamples)
                    result.RejectedWords.Add(display);

                if (result.AcceptedWords.Count >= MaxExamples &&
                    result.RejectedWords.Count >= MaxExamples)
                    break;
            }

            result.DetectedPatterns = DetectPatterns(pda, result);
            return result;
        }

        // Symulacja PDA na słowie — BFS po konfiguracjach (stan, pozycja, stos)
        private bool SimulateWord(PushdownAutomaton pda, List<string> symbols)
        {
            // Konfiguracja: (stan, pozycja w słowie, stos jako lista — góra na indeksie 0)
            var initial = (
                state: pda.StartState,
                pos: 0,
                stack: new List<string> { pda.StartStackSymbol }
            );

            var queue = new Queue<(string state, int pos, List<string> stack)>();
            var visited = new HashSet<string>();
            queue.Enqueue(initial);

            int steps = 0;
            while (queue.Count > 0 && steps++ < MaxConfigs)
            {
                var (state, pos, stack) = queue.Dequeue();

                // Sprawdź akceptację
                bool inputDone = pos == symbols.Count;
                if (inputDone)
                {
                    if (pda.AcceptanceMode == PdaAcceptanceMode.EmptyStack
                        && stack.Count == 0)
                        return true;
                    if (pda.AcceptanceMode == PdaAcceptanceMode.AcceptingStates
                        && pda.AcceptingStates.Contains(state))
                        return true;
                }

                if (stack.Count == 0) continue;
                var stackTop = stack[0];

                foreach (var t in pda.Transitions
                    .Where(t => t.FromState == state && t.StackTop == stackTop))
                {
                    int newPos = pos;

                    // Sprawdź symbol wejściowy
                    if (t.InputSymbol != null)
                    {
                        if (pos >= symbols.Count || symbols[pos] != t.InputSymbol)
                            continue;
                        newPos = pos + 1;
                    }

                    // Nowy stos: pop góry, push nowych symboli (PushSymbols już odwrócone)
                    var newStack = new List<string>(
                        Enumerable.Reverse(t.PushSymbols));
                    newStack.AddRange(stack.Skip(1));

                    if (newStack.Count > MaxStackDepth) continue;

                    var key = $"{t.ToState}|{newPos}|{string.Join(",", newStack)}";
                    if (!visited.Add(key)) continue;

                    queue.Enqueue((t.ToState, newPos, newStack));
                }
            }

            return false;
        }

        // BFS po grafie stanów (ignoruje symbole wejściowe/stosu)
        private List<string> GetReachableStates(PushdownAutomaton pda)
        {
            var visited = new HashSet<string> { pda.StartState };
            var queue = new Queue<string>();
            queue.Enqueue(pda.StartState);

            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                foreach (var t in pda.Transitions.Where(t => t.FromState == s))
                    if (visited.Add(t.ToState))
                        queue.Enqueue(t.ToState);
            }

            return visited.ToList();
        }

        // BFS generujący słowa w kolejności rosnącej długości
        private IEnumerable<List<string>> GenerateWords(
            List<string> alphabet, int maxLen)
        {
            yield return new List<string>(); // słowo puste

            var queue = new Queue<List<string>>();
            foreach (var sym in alphabet)
                queue.Enqueue(new List<string> { sym });

            while (queue.Count > 0)
            {
                var word = queue.Dequeue();
                yield return word;
                if (word.Count < maxLen)
                    foreach (var sym in alphabet)
                        queue.Enqueue(word.Concat(new[] { sym }).ToList());
            }
        }

        private List<string> DetectPatterns(
            PushdownAutomaton pda, PdaLanguageAnalysisResult result)
        {
            var patterns = new List<string>();

            if (!result.AcceptedWords.Any() && !result.AcceptsEmptyWord)
                patterns.Add("Język wydaje się PUSTY — brak zaakceptowanych słów w zakresie testowym.");
            else
                patterns.Add($"Język jest NIEPUSTY.");

            patterns.Add(result.AcceptsEmptyWord
                ? "Język zawiera słowo puste (ε)."
                : "Język NIE zawiera słowa pustego (ε).");

            patterns.Add($"Tryb akceptacji: {(pda.AcceptanceMode == PdaAcceptanceMode.EmptyStack ? "przez pusty stos" : "przez stany akceptujące")}.");
            patterns.Add($"Liczba stanów: {pda.States.Count}, osiągalnych: {result.ReachableStates.Count}.");
            patterns.Add($"Liczba przejść: {pda.Transitions.Count}.");
            patterns.Add($"Alfabet wejściowy: {{{string.Join(", ", pda.InputAlphabet)}}}.");
            patterns.Add($"Alfabet stosu: {{{string.Join(", ", pda.StackAlphabet)}}}.");

            // Sprawdź czy są przejścia epsilon (charakterystyczne dla PDA z gramatyki)
            var epsTransitions = pda.Transitions.Count(t => t.InputSymbol == null);
            if (epsTransitions > 0)
                patterns.Add($"Automat używa {epsTransitions} przejść epsilon (niedeterministyczny).");

            return patterns;
        }
    }
}