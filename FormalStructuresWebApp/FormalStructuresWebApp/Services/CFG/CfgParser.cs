using FormalStructuresWebApp.Models.Domain;

namespace FormalStructuresWebApp.Services.CFG
{
    /// <summary>
    /// Parsuje gramatykę CFG z tekstu BNF zwróconego przez model.
    /// Format wejściowy:
    ///   S -> a S b | ε
    ///   S -> A B
    ///   A -> a
    /// </summary>
    public class CfgParser
    {
        public ContextFreeGrammar Parse(string bnfText)
        {
            var grammar = new ContextFreeGrammar();
            var lines = bnfText
                .Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Contains("->") || l.Contains("→"))
                .ToList();

            foreach (var line in lines)
            {
                // Obsługuj zarówno -> jak i →
                var separator = line.Contains("→") ? "→" : "->";
                var parts = line.Split(separator, 2);
                if (parts.Length < 2) continue;

                var left = parts[0].Trim();
                var rightSide = parts[1].Trim();

                // Alternatywy oddzielone |
                var alternatives = rightSide.Split('|');
                foreach (var alt in alternatives)
                {
                    var production = new CfgProduction { Left = left };
                    var symbols = alt.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var sym in symbols)
                    {
                        if (sym == "ε" || sym == "eps" || sym == "epsilon")
                            continue; // produkcja epsilon = pusta lista
                        production.Right.Add(sym);
                    }

                    grammar.Productions.Add(production);
                }
            }

            // Pierwszy nieterminal z lewej strony = start
            if (grammar.Productions.Any())
                grammar.StartSymbol = grammar.Productions.First().Left;

            return grammar;
        }
    }
}