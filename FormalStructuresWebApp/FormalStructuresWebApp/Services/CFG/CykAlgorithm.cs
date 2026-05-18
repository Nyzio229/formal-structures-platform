using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.CFG
{
    /// <summary>
    /// Algorytm CYK (Cocke-Younger-Kasami).
    /// Sprawdza czy słowo należy do języka gramatyki w CNF.
    /// Złożoność: O(n³ · |G|)
    /// </summary>
    public class CykAlgorithm
    {
        public bool Accepts(ContextFreeGrammar cnfGrammar, string word)
        {
            if (word == "" || word == "ε")
            {
                // Słowo puste: sprawdź czy S →* ε
                return cnfGrammar.Productions.Any(p =>
                    p.Left == cnfGrammar.StartSymbol && p.Right.Count == 0);
            }

            var symbols = word.Select(c => c.ToString()).ToArray();
            int n = symbols.Length;

            // table[i][j] = zbiór nieterminali które generują symbols[i..j]
            var table = new HashSet<string>[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    table[i, j] = new HashSet<string>();

            // Inicjalizacja: pojedyncze symbole
            for (int i = 0; i < n; i++)
            {
                foreach (var prod in cnfGrammar.Productions
                    .Where(p => p.Right.Count == 1 && p.Right[0] == symbols[i]))
                {
                    table[i, i].Add(prod.Left);
                }
            }

            // Wypełnianie tablicy
            for (int len = 2; len <= n; len++)
            {
                for (int i = 0; i <= n - len; i++)
                {
                    int j = i + len - 1;
                    for (int k = i; k < j; k++)
                    {
                        foreach (var prod in cnfGrammar.Productions
                            .Where(p => p.Right.Count == 2))
                        {
                            if (table[i, k].Contains(prod.Right[0]) &&
                                table[k + 1, j].Contains(prod.Right[1]))
                            {
                                table[i, j].Add(prod.Left);
                            }
                        }
                    }
                }
            }

            return table[0, n - 1].Contains(cnfGrammar.StartSymbol);
        }

        // Weryfikacja zestawu przykładów — zwraca jakie słowa pasują
        public Dictionary<string, bool> VerifyWords(
            ContextFreeGrammar cnfGrammar, IEnumerable<string> words)
        {
            return words.ToDictionary(w => w, w => Accepts(cnfGrammar, w));
        }
    }
}