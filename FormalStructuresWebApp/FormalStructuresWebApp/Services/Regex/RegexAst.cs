namespace FormalStructuresWebApp.Services.Regex
{
    /// <summary>Węzeł drzewa składniowego wyrażenia regularnego.</summary>
    public abstract class RegexNode { }

    /// <summary>Słowo puste ε.</summary>
    public class EpsNode : RegexNode { }

    /// <summary>Pojedynczy symbol terminala z alfabetu.</summary>
    public class LiteralNode : RegexNode
    {
        public string Symbol { get; }
        public LiteralNode(string symbol) => Symbol = symbol;
    }

    public class ConcatNode : RegexNode
    {
        public RegexNode Left { get; }
        public RegexNode Right { get; }
        public ConcatNode(RegexNode left, RegexNode right) { Left = left; Right = right; }
    }

    public class UnionNode : RegexNode
    {
        public RegexNode Left { get; }
        public RegexNode Right { get; }
        public UnionNode(RegexNode left, RegexNode right) { Left = left; Right = right; }
    }

    /// <summary>Domknięcie Kleenego: zero lub więcej powtórzeń.</summary>
    public class StarNode : RegexNode
    {
        public RegexNode Inner { get; }
        public StarNode(RegexNode inner) => Inner = inner;
    }

    /// <summary>Jedno lub więcej powtórzeń (e+ == e e*).</summary>
    public class PlusNode : RegexNode
    {
        public RegexNode Inner { get; }
        public PlusNode(RegexNode inner) => Inner = inner;
    }

    /// <summary>Zero lub jedno wystąpienie (e? == e | ε).</summary>
    public class OptionalNode : RegexNode
    {
        public RegexNode Inner { get; }
        public OptionalNode(RegexNode inner) => Inner = inner;
    }
}