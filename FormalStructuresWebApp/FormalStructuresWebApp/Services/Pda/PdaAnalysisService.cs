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
        public List<string> StackBehaviorNotes { get; set; } = new();
        public string TransitionTableText { get; set; } = "";
    }

    public class PdaAnalysisService
    {
        private const int MaxWordLen = 8;
        private const int MaxExamples = 8;
        private const int MaxConfigs = 50000;
        private const int MaxStackDepth = 30;

        public PdaLanguageAnalysisResult Analyze(PushdownAutomaton pda)
        {
            var result = new PdaLanguageAnalysisResult();

            result.ReachableStates = GetReachableStates(pda);
            result.AcceptsEmptyWord = SimulateWord(pda, new List<string>());

            // BFS po słowach rosnącej długości
            foreach (var word in GenerateWords(pda.InputAlphabet, MaxWordLen))
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

            result.StackBehaviorNotes = AnalyzeStackBehavior(pda);
            result.DetectedPatterns = DetectPatterns(pda, result);
            result.TransitionTableText = BuildTransitionTable(pda);

            return result;
        }

        // ── Symulacja BFS po konfiguracjach ──────────────────────
        private bool SimulateWord(PushdownAutomaton pda, List<string> symbols)
        {
            if (string.IsNullOrEmpty(pda.StartState)) return false;

            var initial = (
                state: pda.StartState,
                pos: 0,
                stack: new List<string> { pda.StartStackSymbol }
            );

            var queue = new Queue<(string state, int pos, List<string> stack)>();
            var visited = new HashSet<string>();

            queue.Enqueue(initial);
            var initKey = MakeKey(initial.state, initial.pos, initial.stack);
            visited.Add(initKey);

            int steps = 0;
            while (queue.Count > 0 && steps++ < MaxConfigs)
            {
                var (state, pos, stack) = queue.Dequeue();

                // Sprawdź akceptację w bieżącej konfiguracji
                if (pos == symbols.Count)
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

                    if (t.InputSymbol != null)
                    {
                        if (pos >= symbols.Count || symbols[pos] != t.InputSymbol)
                            continue;
                        newPos = pos + 1;
                    }

                    // Nowy stos: zdejmij szczyt, połóż PushSymbols
                    // PushSymbols są zapisane w kolejności "od góry do dołu"
                    var newStack = new List<string>(t.PushSymbols);
                    newStack.AddRange(stack.Skip(1));

                    if (newStack.Count > MaxStackDepth) continue;

                    var key = MakeKey(t.ToState, newPos, newStack);
                    if (!visited.Add(key)) continue;

                    queue.Enqueue((t.ToState, newPos, newStack));
                }
            }

            return false;
        }

        private static string MakeKey(string state, int pos, List<string> stack)
            => $"{state}|{pos}|{string.Join(",", stack)}";

        // ── Analiza zachowania stosu ──────────────────────────────
        // To jest kluczowe — daje modelowi informację o strukturze PDA
        private List<string> AnalyzeStackBehavior(PushdownAutomaton pda)
        {
            var notes = new List<string>();
            var trans = pda.Transitions;

            // Jakie symbole są pushowane
            var pushed = trans
                .SelectMany(t => t.PushSymbols)
                .Distinct().OrderBy(s => s).ToList();

            // Jakie symbole są popowane (stackTop bez odpowiedniego push)
            var popped = trans
                .Where(t => t.PushSymbols.Count == 0)
                .Select(t => t.StackTop)
                .Distinct().ToList();

            // Przejścia które pushują więcej niż jeden symbol
            var pushMore = trans
                .Where(t => t.PushSymbols.Count > 1)
                .ToList();

            // Przejścia które tylko popują (PushSymbols puste)
            var onlyPop = trans
                .Where(t => t.PushSymbols.Count == 0)
                .ToList();

            // Przejścia epsilon vs czytające
            var epsTrans = trans.Count(t => t.InputSymbol == null);
            var readingTrans = trans.Count(t => t.InputSymbol != null);

            notes.Add($"Symbole pushowane na stos: " +
                (pushed.Any() ? string.Join(", ", pushed) : "(brak)"));
            notes.Add($"Przejść pushujących wiele symboli: {pushMore.Count}");
            notes.Add($"Przejść tylko popujących (push=ε): {onlyPop.Count}");
            notes.Add($"Przejść epsilon (nie czyta wejścia): {epsTrans}");
            notes.Add($"Przejść czytających symbol wejściowy: {readingTrans}");

            // Sprawdź czy push i pop są symetryczne (sugestia języka nawiasowego)
            var pushSymbols = trans
                .Where(t => t.PushSymbols.Count > 0)
                .SelectMany(t => t.PushSymbols)
                .ToHashSet();
            var popSymbols = trans
                .Where(t => t.PushSymbols.Count == 0 && t.StackTop != pda.StartStackSymbol)
                .Select(t => t.StackTop)
                .ToHashSet();

            if (pushSymbols.Any() && popSymbols.Any())
            {
                var overlap = pushSymbols.Intersect(popSymbols).ToList();
                if (overlap.Any())
                    notes.Add($"Symbole pushowane i popowane symetrycznie: " +
                        string.Join(", ", overlap) +
                        " (sugestia: język z zagnieżdżoną strukturą)");
            }

            // Sprawdź wzorzec: push przy czytaniu 'a', pop przy czytaniu 'b'
            var pushOnRead = trans
                .Where(t => t.InputSymbol != null && t.PushSymbols.Count > 0)
                .GroupBy(t => t.InputSymbol)
                .ToDictionary(g => g.Key!, g => g.ToList());

            var popOnRead = trans
                .Where(t => t.InputSymbol != null && t.PushSymbols.Count == 0)
                .GroupBy(t => t.InputSymbol)
                .ToDictionary(g => g.Key!, g => g.ToList());

            foreach (var (sym, pushList) in pushOnRead)
                notes.Add($"Symbol '{sym}' powoduje push na stos " +
                    $"({pushList.Count} przejść)");

            foreach (var (sym, popList) in popOnRead)
                notes.Add($"Symbol '{sym}' powoduje pop ze stosu " +
                    $"({popList.Count} przejść)");

            return notes;
        }

        // ── Wykrywanie wzorców ────────────────────────────────────
        private List<string> DetectPatterns(
            PushdownAutomaton pda, PdaLanguageAnalysisResult result)
        {
            var patterns = new List<string>();
            var accepted = result.AcceptedWords;
            var alphabet = pda.InputAlphabet;

            // Język pusty
            if (!accepted.Any() && !result.AcceptsEmptyWord)
            {
                patterns.Add("OSTRZEŻENIE: brak zaakceptowanych słów w zakresie " +
                    $"testowym (słowa do długości {MaxWordLen}). " +
                    "Możliwy język pusty lub automat wymaga dłuższych słów.");
                return patterns;
            }

            patterns.Add(result.AcceptsEmptyWord
                ? "Język zawiera słowo puste (ε)."
                : "Język NIE zawiera słowa pustego (ε).");

            // Długości zaakceptowanych słów
            var lengths = accepted
                .Where(w => w != "ε")
                .Select(w => w.Length)
                .OrderBy(l => l)
                .ToList();

            if (lengths.Any())
            {
                patterns.Add($"Długości zaakceptowanych słów: " +
                    string.Join(", ", lengths));

                // Sprawdź czy długości są parzyste
                if (lengths.All(l => l % 2 == 0))
                    patterns.Add("Wszystkie zaakceptowane słowa mają PARZYSTĄ długość.");

                // Sprawdź czy długości tworzą ciąg arytmetyczny
                if (lengths.Count >= 3)
                {
                    var diffs = lengths.Zip(lengths.Skip(1), (a, b) => b - a).ToList();
                    if (diffs.Distinct().Count() == 1)
                        patterns.Add($"Długości zaakceptowanych słów tworzą ciąg " +
                            $"arytmetyczny z różnicą {diffs[0]}.");
                }
            }

            // Sprawdź proporcje symboli w zaakceptowanych słowach
            if (alphabet.Count == 2)
            {
                var sym0 = alphabet[0];
                var sym1 = alphabet[1];

                var equalCount = accepted
                    .Where(w => w != "ε")
                    .Count(w => w.Count(c => c.ToString() == sym0)
                              == w.Count(c => c.ToString() == sym1));

                if (equalCount == accepted.Count(w => w != "ε") && equalCount > 0)
                    patterns.Add($"We wszystkich zaakceptowanych słowach liczba " +
                        $"'{sym0}' = liczba '{sym1}' " +
                        $"(silna sugestia języka zrównoważonego).");

                // Sprawdź czy wszystkie słowa zaczynają się od sym0
                var allStartSym0 = accepted
                    .Where(w => w != "ε")
                    .All(w => w.StartsWith(sym0));
                if (allStartSym0 && accepted.Count > 1)
                    patterns.Add($"Wszystkie zaakceptowane słowa zaczynają się " +
                        $"od '{sym0}'.");

                // Sprawdź czy to aⁿbⁿ — słowa postaci a*b*
                var isAnBn = accepted
                    .Where(w => w != "ε")
                    .All(w => {
                        var s = w;
                        int i = 0;
                        while (i < s.Length && s[i].ToString() == sym0) i++;
                        while (i < s.Length && s[i].ToString() == sym1) i++;
                        return i == s.Length &&
                            w.Count(c => c.ToString() == sym0) ==
                            w.Count(c => c.ToString() == sym1);
                    });

                if (isAnBn && accepted.Count(w => w != "ε") >= 2)
                    patterns.Add($"WZORZEC: słowa postaci {sym0}ⁿ{sym1}ⁿ " +
                        $"(n≥1) — klasyczny język aⁿbⁿ.");
            }

            // Sprawdź palindromy
            var palindromes = accepted
                .Where(w => w != "ε" && w.Length > 1)
                .Count(w => w == new string(w.Reverse().ToArray()));
            if (palindromes > 0 && palindromes == accepted.Count(w => w != "ε" && w.Length > 1))
                patterns.Add("WZORZEC: wszystkie zaakceptowane słowa są palindromami.");

            patterns.Add($"Tryb akceptacji: " +
                (pda.AcceptanceMode == PdaAcceptanceMode.EmptyStack
                    ? "przez pusty stos"
                    : "przez stany akceptujące") + ".");
            patterns.Add($"Liczba stanów: {pda.States.Count}, " +
                $"osiągalnych: {result.ReachableStates.Count}.");
            patterns.Add($"Alfabet wejściowy: " +
                $"{{{string.Join(", ", pda.InputAlphabet)}}}.");

            return patterns;
        }

        // ── Tabela przejść ────────────────────────────────────────
        private string BuildTransitionTable(PushdownAutomaton pda)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("δ(stan, wejście, szczyt_stosu) → (stan', push)");
            sb.AppendLine(new string('─', 55));

            foreach (var t in pda.Transitions)
            {
                var inp = t.InputSymbol ?? "ε";
                var push = t.PushSymbols.Any()
                    ? string.Join(" ", t.PushSymbols) : "ε";
                sb.AppendLine(
                    $"  δ({t.FromState}, {inp}, {t.StackTop}) " +
                    $"→ ({t.ToState}, {push})");
            }

            return sb.ToString();
        }

        // ── BFS po stanach ────────────────────────────────────────
        private List<string> GetReachableStates(PushdownAutomaton pda)
        {
            if (string.IsNullOrEmpty(pda.StartState))
                return new List<string>();

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

        // ── Generowanie słów BFS ──────────────────────────────────
        private IEnumerable<List<string>> GenerateWords(
            List<string> alphabet, int maxLen)
        {
            yield return new List<string>();

            if (!alphabet.Any()) yield break;

            var queue = new Queue<List<string>>();
            foreach (var sym in alphabet)
                queue.Enqueue(new List<string> { sym });

            while (queue.Count > 0)
            {
                var word = queue.Dequeue();
                yield return word;
                if (word.Count < maxLen)
                    foreach (var sym in alphabet)
                        queue.Enqueue(
                            word.Concat(new[] { sym }).ToList());
            }
        }
    }
}