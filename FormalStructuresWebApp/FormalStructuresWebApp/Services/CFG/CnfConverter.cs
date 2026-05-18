using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.CFG
{
    /// <summary>
    /// Konwersja CFG → Chomsky Normal Form (CNF).
    /// CNF wymaga że każda produkcja ma postać:
    ///   A → BC  (dwa nieterminale)
    ///   A → a   (jeden terminal)
    ///   S → ε   (tylko dla symbolu startowego)
    ///
    /// Algorytm (klasyczny, 5 kroków):
    /// 1. START: nowy symbol startowy
    /// 2. TERM:  zamień terminale w długich produkcjach na nieterminale
    /// 3. BIN:   rozbij produkcje z prawą stroną > 2 symbole
    /// 4. DEL:   usuń produkcje epsilon (poza startem)
    /// 5. UNIT:  usuń produkcje jednostkowe (A → B)
    /// </summary>
    public class CnfConverter
    {
        private int _counter = 0;
        private string FreshNT() => $"X{_counter++}";

        public ContextFreeGrammar Convert(ContextFreeGrammar grammar)
        {
            var prods = grammar.Productions
                .Select(p => new CfgProduction
                {
                    Left = p.Left,
                    Right = new List<string>(p.Right)
                }).ToList();

            var start = grammar.StartSymbol;

            // Krok 1: START
            var newStart = start + "0";
            prods.Insert(0, new CfgProduction
            {
                Left = newStart,
                Right = new List<string> { start }
            });
            start = newStart;

            // Krok 2: TERM — zamień terminale w prod. długości >= 2
            var terminals = prods
                .SelectMany(p => p.Right)
                .Where(s => IsTerminal(s, prods))
                .Distinct().ToList();

            var termMap = new Dictionary<string, string>();
            foreach (var t in terminals)
            {
                var nt = "T_" + t;
                termMap[t] = nt;
            }

            prods = prods.Select(p =>
            {
                if (p.Right.Count < 2) return p;
                return new CfgProduction
                {
                    Left = p.Left,
                    Right = p.Right.Select(s =>
                        IsTerminal(s, prods) ? termMap[s] : s).ToList()
                };
            }).ToList();

            // Dodaj produkcje T_a → a
            foreach (var (t, nt) in termMap)
                prods.Add(new CfgProduction { Left = nt, Right = new List<string> { t } });

            // Krok 3: BIN — rozbij produkcje z prawą stroną > 2
            var binProds = new List<CfgProduction>();
            foreach (var p in prods)
            {
                if (p.Right.Count <= 2) { binProds.Add(p); continue; }

                var symbols = new List<string>(p.Right);
                var current = p.Left;
                while (symbols.Count > 2)
                {
                    var fresh = FreshNT();
                    binProds.Add(new CfgProduction
                    {
                        Left = current,
                        Right = new List<string> { symbols[0], fresh }
                    });
                    symbols.RemoveAt(0);
                    current = fresh;
                }
                binProds.Add(new CfgProduction
                {
                    Left = current,
                    Right = symbols
                });
            }
            prods = binProds;

            // Krok 4: DEL — usuń produkcje epsilon (A → ε)
            prods = EliminateEpsilon(prods, start);

            // Krok 5: UNIT — usuń produkcje jednostkowe (A → B)
            prods = EliminateUnit(prods);

            return new ContextFreeGrammar
            {
                StartSymbol = start,
                Productions = prods
            };
        }

        private bool IsTerminal(string sym, List<CfgProduction> prods) =>
            !prods.Any(p => p.Left == sym);

        private List<CfgProduction> EliminateEpsilon(
            List<CfgProduction> prods, string start)
        {
            // Znajdź nullable (produkcje epsilon)
            var nullable = new HashSet<string>();
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var p in prods)
                {
                    if (nullable.Contains(p.Left)) continue;
                    if (p.Right.Count == 0 || p.Right.All(s => nullable.Contains(s)))
                    {
                        nullable.Add(p.Left);
                        changed = true;
                    }
                }
            }

            var result = new List<CfgProduction>();
            foreach (var p in prods)
            {
                if (p.Right.Count == 0)
                {
                    // Zachowaj tylko S0 → ε
                    if (p.Left == start) result.Add(p);
                    continue;
                }

                // Dodaj wszystkie kombinacje z pominięciem nullable symboli
                var combos = GetCombinations(p.Right, nullable);
                foreach (var combo in combos)
                {
                    if (combo.Count == 0 && p.Left != start) continue;
                    result.Add(new CfgProduction { Left = p.Left, Right = combo });
                }
            }

            return result.GroupBy(p => (p.Left, string.Join(",", p.Right)))
                         .Select(g => g.First()).ToList();
        }

        private List<List<string>> GetCombinations(
            List<string> symbols, HashSet<string> nullable)
        {
            var results = new List<List<string>> { new() };
            foreach (var sym in symbols)
            {
                var newResults = new List<List<string>>();
                foreach (var existing in results)
                {
                    // Zawsze dodaj symbol
                    newResults.Add(new List<string>(existing) { sym });
                    // Jeśli nullable — dodaj też wersję bez
                    if (nullable.Contains(sym))
                        newResults.Add(new List<string>(existing));
                }
                results = newResults;
            }
            return results;
        }

        private List<CfgProduction> EliminateUnit(List<CfgProduction> prods)
        {
            // Dla każdego nieterminala A oblicz unit-closure (A →* B)
            var nonTerminals = prods.Select(p => p.Left).Distinct().ToList();
            var result = new List<CfgProduction>();

            foreach (var nt in nonTerminals)
            {
                var reachable = new HashSet<string> { nt };
                var queue = new Queue<string>();
                queue.Enqueue(nt);

                while (queue.Count > 0)
                {
                    var curr = queue.Dequeue();
                    foreach (var p in prods.Where(p =>
                        p.Left == curr && p.Right.Count == 1 &&
                        nonTerminals.Contains(p.Right[0])))
                    {
                        if (reachable.Add(p.Right[0]))
                            queue.Enqueue(p.Right[0]);
                    }
                }

                // Dodaj wszystkie produkcje niebędące jednostkowymi
                foreach (var reachNt in reachable)
                {
                    foreach (var p in prods.Where(p =>
                        p.Left == reachNt &&
                        !(p.Right.Count == 1 && nonTerminals.Contains(p.Right[0]))))
                    {
                        result.Add(new CfgProduction
                        {
                            Left = nt,
                            Right = new List<string>(p.Right)
                        });
                    }
                }
            }

            return result.GroupBy(p => (p.Left, string.Join(",", p.Right)))
                         .Select(g => g.First()).ToList();
        }
    }
}