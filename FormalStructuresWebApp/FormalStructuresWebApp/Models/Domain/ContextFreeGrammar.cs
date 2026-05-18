namespace FormalStructuresWebApp.Models.Domain
{
    public class ContextFreeGrammar
    {
        public string StartSymbol { get; set; } = "S";
        public List<CfgProduction> Productions { get; set; } = new();

        public List<string> NonTerminals =>
            Productions.Select(p => p.Left).Distinct().ToList();

        public List<string> Terminals =>
            Productions
                .SelectMany(p => p.Right)
                .Where(s => !Productions.Any(p => p.Left == s))
                .Distinct()
                .ToList();
    }

    public class CfgProduction
    {
        // Lewa strona: jeden nieterminal
        public string Left { get; set; } = "";
        // Prawa strona: lista symboli (terminale i nieterminale)
        // Pusta lista = produkcja epsilon
        public List<string> Right { get; set; } = new();

        public override string ToString() =>
            $"{Left} → {(Right.Any() ? string.Join(" ", Right) : "ε")}";
    }
}