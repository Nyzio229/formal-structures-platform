using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.Automaton
{
    public class LanguageAnalysisResult
    {
        // Fakty ustalone algorytmicznie
        public bool IsLanguageEmpty { get; set; }
        public bool IsLanguageUniversal { get; set; }
        public bool AcceptsEmptyWord { get; set; }
        public bool IsComplete { get; set; }
        public List<string> ReachableStates { get; set; } = new();
        public List<string> UnreachableStates { get; set; } = new();
        public List<string> TrapStates { get; set; } = new();
        public List<string> AcceptedWords { get; set; } = new();
        public List<string> RejectedWords { get; set; } = new();
        public List<string> DetectedPatterns { get; set; } = new();
        public string TransitionTableText { get; set; } = "";
    }

    public class AutomatonLanguageAnalyzer
    {
        private const int MaxWordLength = 6;
        private const int MaxExamples = 5;

        public LanguageAnalysisResult Analyze(FiniteAutomaton automaton)
        {
            var result = new LanguageAnalysisResult();

            var stateNames = automaton.States.Select(s => s.Name).ToHashSet();
            var acceptingStates = automaton.States
                .Where(s => s.IsAccepting)
                .Select(s => s.Name)
                .ToHashSet();
            var alphabet = automaton.Alphabet;
            var startState = automaton.StartState;

            // Słownik przejść: (stan, symbol) -> stan
            var delta = automaton.Transitions
                .GroupBy(t => (t.FromState, t.Symbol))
                .ToDictionary(g => g.Key, g => g.First().ToState);

            // 1. Stany osiągalne (BFS od stanu początkowego)
            var reachable = GetReachableStates(startState, alphabet, delta);
            result.ReachableStates = reachable.ToList();
            result.UnreachableStates = stateNames.Except(reachable).ToList();

            // 2. Stany pułapki — osiągalne, nieakceptujące, z których nie da się dojść do akceptującego
            var coaccessible = GetCoaccessibleStates(stateNames, acceptingStates, automaton.Transitions);
            result.TrapStates = reachable
                .Where(s => !acceptingStates.Contains(s) && !coaccessible.Contains(s))
                .ToList();

            // 3. Czy DFA jest kompletny (każdy osiągalny stan ma przejście dla każdego symbolu)
            result.IsComplete = reachable.All(s =>
                alphabet.All(a => delta.ContainsKey((s, a))));

            // 4. Czy akceptuje słowo puste
            result.AcceptsEmptyWord = acceptingStates.Contains(startState);

            // 5. BFS — znajdź przykłady słów akceptowanych i odrzucanych
            BfsWords(startState, alphabet, delta, acceptingStates,
                result.AcceptedWords, result.RejectedWords);

            // 6. Czy język pusty lub uniwersalny
            result.IsLanguageEmpty = result.AcceptedWords.Count == 0
                && !result.AcceptsEmptyWord
                && reachable.All(s => !acceptingStates.Contains(s));

            result.IsLanguageUniversal = reachable
                .Where(s => !result.TrapStates.Contains(s))
                .All(s => acceptingStates.Contains(s))
                && result.IsComplete;

            // 7. Wykryj proste wzorce
            result.DetectedPatterns = DetectPatterns(
                automaton, reachable, acceptingStates, delta, alphabet, result);

            // 8. Tabela przejść jako tekst
            result.TransitionTableText = BuildTransitionTable(
                reachable, alphabet, delta, acceptingStates, startState);

            return result;
        }

        // BFS po grafie automatu — zbiera słowa akceptowane i odrzucane
        private void BfsWords(
            string startState,
            List<string> alphabet,
            Dictionary<(string, string), string> delta,
            HashSet<string> acceptingStates,
            List<string> accepted,
            List<string> rejected)
        {
            // kolejka: (stan, słowo)
            var queue = new Queue<(string state, string word)>();
            var visited = new HashSet<string>(); // stan+słowo jako klucz

            queue.Enqueue((startState, ""));
            visited.Add(startState + "|");

            while (queue.Count > 0 && (accepted.Count < MaxExamples || rejected.Count < MaxExamples))
            {
                var (state, word) = queue.Dequeue();

                // Klasyfikuj bieżące słowo (poza pustym, bo to osobna flaga)
                if (word.Length > 0)
                {
                    if (acceptingStates.Contains(state) && accepted.Count < MaxExamples)
                        accepted.Add(word == "" ? "ε" : word);
                    else if (!acceptingStates.Contains(state) && rejected.Count < MaxExamples)
                        rejected.Add(word == "" ? "ε" : word);
                }

                if (word.Length >= MaxWordLength) continue;

                foreach (var symbol in alphabet)
                {
                    if (!delta.TryGetValue((state, symbol), out var nextState)) continue;

                    var key = nextState + "|" + word + symbol;
                    if (visited.Contains(key)) continue;
                    visited.Add(key);
                    queue.Enqueue((nextState, word + symbol));
                }
            }
        }

        // Stany osiągalne od startu (BFS)
        private HashSet<string> GetReachableStates(
            string start,
            List<string> alphabet,
            Dictionary<(string, string), string> delta)
        {
            var visited = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                foreach (var a in alphabet)
                {
                    if (delta.TryGetValue((s, a), out var next) && visited.Add(next))
                        queue.Enqueue(next);
                }
            }
            return visited;
        }

        // Stany ko-osiągalne — z których można dojść do jakiegoś stanu akceptującego
        private HashSet<string> GetCoaccessibleStates(
            HashSet<string> allStates,
            HashSet<string> acceptingStates,
            List<Transition> transitions)
        {
            // Odwróć graf przejść
            var reverseAdj = allStates.ToDictionary(s => s, s => new List<string>());
            foreach (var t in transitions)
                if (reverseAdj.ContainsKey(t.ToState))
                    reverseAdj[t.ToState].Add(t.FromState);

            // BFS wstecz od stanów akceptujących
            var visited = new HashSet<string>(acceptingStates);
            var queue = new Queue<string>(acceptingStates);

            while (queue.Count > 0)
            {
                var s = queue.Dequeue();
                foreach (var prev in reverseAdj[s])
                    if (visited.Add(prev))
                        queue.Enqueue(prev);
            }
            return visited;
        }

        // Wykrywanie prostych wzorców językowych
        private List<string> DetectPatterns(
            FiniteAutomaton automaton,
            HashSet<string> reachable,
            HashSet<string> acceptingStates,
            Dictionary<(string, string), string> delta,
            List<string> alphabet,
            LanguageAnalysisResult result)
        {
            var patterns = new List<string>();
            var start = automaton.StartState;

            if (result.IsLanguageEmpty)
            {
                patterns.Add("Język jest PUSTY — automat nie akceptuje żadnego słowa.");
                return patterns;
            }

            if (result.IsLanguageUniversal)
            {
                patterns.Add("Język jest UNIWERSALNY — automat akceptuje każde słowo nad alfabetem.");
                return patterns;
            }

            if (result.AcceptsEmptyWord)
                patterns.Add("Język zawiera słowo puste (ε).");
            else
                patterns.Add("Język NIE zawiera słowa pustego (ε).");

            // Sprawdź czy to język skończony (brak cykli w osiągalnej części wiodącej do akceptujących)
            if (IsFiniteLanguage(reachable, acceptingStates, delta, alphabet))
                patterns.Add("Język wydaje się być SKOŃCZONY (brak pętli prowadzących do akceptacji).");
            else
                patterns.Add("Język jest NIESKOŃCZONY.");

            // Sprawdź wzorzec: akceptacja zależy od ostatniego symbolu
            foreach (var sym in alphabet)
            {
                bool allEndWithSymAccepted = reachable.All(s =>
                {
                    if (!delta.TryGetValue((s, sym), out var next)) return true;
                    return acceptingStates.Contains(next);
                });
                if (allEndWithSymAccepted && acceptingStates.Count > 0)
                    patterns.Add($"Możliwy wzorzec: każde słowo kończące się na '{sym}' jest akceptowane.");
            }

            // Sprawdź parzystość — czy automat ma 2 stany i akceptuje co drugi
            if (automaton.States.Count == 2 && alphabet.Count == 1)
            {
                patterns.Add($"Możliwy wzorzec parzystości (2 stany, 1 symbol alfabetu).");
            }

            // Liczba stanów akceptujących vs wszystkich
            var reachableAccepting = reachable.Intersect(acceptingStates).Count();
            patterns.Add($"Spośród {reachable.Count} osiągalnych stanów, {reachableAccepting} jest akceptujących.");

            if (result.TrapStates.Count > 0)
                patterns.Add($"Wykryto {result.TrapStates.Count} stan(y) pułapki: {string.Join(", ", result.TrapStates)}.");

            if (result.UnreachableStates.Count > 0)
                patterns.Add($"Wykryto {result.UnreachableStates.Count} nieosiągalny(-e) stan(y): {string.Join(", ", result.UnreachableStates)}.");

            return patterns;
        }

        private bool IsFiniteLanguage(
            HashSet<string> reachable,
            HashSet<string> acceptingStates,
            Dictionary<(string, string), string> delta,
            List<string> alphabet)
        {
            // Uproszczone: sprawdź czy istnieje cykl w podgrafie stanów ko-osiągalnych
            // Używamy DFS z kolorowaniem
            var colors = reachable.ToDictionary(s => s, s => 0); // 0=biały, 1=szary, 2=czarny

            bool HasCycle(string s)
            {
                colors[s] = 1;
                foreach (var a in alphabet)
                {
                    if (!delta.TryGetValue((s, a), out var next)) continue;
                    if (!reachable.Contains(next)) continue;
                    if (colors[next] == 1) return true; // krawędź wsteczna = cykl
                    if (colors[next] == 0 && HasCycle(next)) return true;
                }
                colors[s] = 2;
                return false;
            }

            return !reachable.Any(s => colors[s] == 0 && HasCycle(s));
        }

        private string BuildTransitionTable(
            HashSet<string> reachable,
            List<string> alphabet,
            Dictionary<(string, string), string> delta,
            HashSet<string> acceptingStates,
            string startState)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Stan".PadRight(12));
            foreach (var a in alphabet)
                sb.Append(a.PadRight(10));
            sb.AppendLine();
            sb.AppendLine(new string('-', 12 + alphabet.Count * 10));

            foreach (var s in reachable.OrderBy(x => x))
            {
                var marker = "";
                if (s == startState && acceptingStates.Contains(s)) marker = "[→✓]";
                else if (s == startState) marker = "[→]";
                else if (acceptingStates.Contains(s)) marker = "[✓]";

                sb.Append($"{s}{marker}".PadRight(12));
                foreach (var a in alphabet)
                {
                    var target = delta.TryGetValue((s, a), out var t) ? t : "—";
                    sb.Append(target.PadRight(10));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}