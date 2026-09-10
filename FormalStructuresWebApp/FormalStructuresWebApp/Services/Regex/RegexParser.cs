namespace FormalStructuresWebApp.Services.Regex
{
    public class RegexParseException : Exception
    {
        public RegexParseException(string message) : base(message) { }
    }

    /// <summary>
    /// Parser rekurencyjno-zstępujący dla prostego języka wyrażeń regularnych:
    ///   union   := concat ('|' concat)*
    ///   concat  := repeat*                (zero powtórzeń = ε)
    ///   repeat  := atom ('*' | '+' | '?')*
    ///   atom    := literał | '(' union ')' | 'ε' | '\' dowolny_znak
    ///
    /// Literały to symbole z podanego alfabetu (dopasowywane najdłuższym
    /// prefiksem — istotne, gdyby symbole alfabetu były wieloznakowe).
    /// Symbol poprzedzony backslashem (\() jest traktowany jako DOSŁOWNY
    /// znak, nawet jeśli koliduje z operatorem regexu (np. gdy sam alfabet
    /// zawiera nawiasy) — to jedyny sposób na jednoznaczne rozróżnienie
    /// "nawias grupujący" od "nawias jako litera alfabetu".
    /// </summary>
    public class RegexParser
    {
        private readonly List<string> _tokens;
        private int _pos;

        public RegexParser(string pattern, List<string> alphabet)
        {
            _tokens = Tokenize(pattern ?? "", alphabet);
            _pos = 0;
        }

        public RegexNode Parse()
        {
            if (_tokens.Count == 0)
                return new EpsNode();

            var node = ParseUnion();

            if (_pos != _tokens.Count)
                throw new RegexParseException(
                    $"Nieoczekiwany token '{_tokens[_pos]}' na pozycji {_pos} (za dużo znaków w wyrażeniu).");

            return node;
        }

        private RegexNode ParseUnion()
        {
            var left = ParseConcat();
            while (Peek() == "|")
            {
                Advance();
                var right = ParseConcat();
                left = new UnionNode(left, right);
            }
            return left;
        }

        private RegexNode ParseConcat()
        {
            RegexNode? result = null;
            while (Peek() != null && Peek() != "|" && Peek() != ")")
            {
                var next = ParseRepeat();
                result = result == null ? next : new ConcatNode(result, next);
            }
            return result ?? new EpsNode();
        }

        private RegexNode ParseRepeat()
        {
            var atom = ParseAtom();
            while (Peek() == "*" || Peek() == "+" || Peek() == "?")
            {
                var op = Advance();
                atom = op switch
                {
                    "*" => new StarNode(atom),
                    "+" => new PlusNode(atom),
                    "?" => new OptionalNode(atom),
                    _ => atom
                };
            }
            return atom;
        }

        private RegexNode ParseAtom()
        {
            var tok = Peek();
            if (tok == null)
                throw new RegexParseException("Nieoczekiwany koniec wyrażenia.");

            if (tok == "(")
            {
                Advance();
                var inner = ParseUnion();
                if (Peek() != ")")
                    throw new RegexParseException("Brakujący nawias zamykający ')'.");
                Advance();
                return inner;
            }

            if (tok == "ε" || tok.Equals("eps", StringComparison.OrdinalIgnoreCase)
                            || tok.Equals("epsilon", StringComparison.OrdinalIgnoreCase))
            {
                Advance();
                return new EpsNode();
            }

            if (tok is "|" or "*" or "+" or "?" or ")")
                throw new RegexParseException(
                    $"Nieoczekiwany operator '{tok}' w miejscu, gdzie oczekiwano symbolu.");

            Advance();
            return new LiteralNode(tok);
        }

        private string? Peek() => _pos < _tokens.Count ? _tokens[_pos] : null;
        private string Advance() => _tokens[_pos++];

        private static List<string> Tokenize(string pattern, List<string> alphabet)
        {
            var sortedAlphabet = alphabet.OrderByDescending(a => a.Length).ToList();
            var tokens = new List<string>();
            int i = 0;

            while (i < pattern.Length)
            {
                var c = pattern[i];

                if (char.IsWhiteSpace(c)) { i++; continue; }

                // Znak poprzedzony backslashem = dosłowny literał (ucieczka
                // przed konfliktem z operatorami regexu, np. \( gdy "(" jest
                // symbolem alfabetu, nie operatorem grupowania).
                if (c == '\\' && i + 1 < pattern.Length)
                {
                    tokens.Add(pattern[i + 1].ToString());
                    i += 2;
                    continue;
                }

                if ("|*+?()".IndexOf(c) >= 0)
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                var matched = sortedAlphabet.FirstOrDefault(a =>
                    a.Length > 0 && i + a.Length <= pattern.Length &&
                    pattern.Substring(i, a.Length) == a);

                if (matched != null)
                {
                    tokens.Add(matched);
                    i += matched.Length;
                    continue;
                }

                if (c == 'ε')
                {
                    tokens.Add("ε");
                    i++;
                    continue;
                }

                // Fallback: pojedynczy znak jako literał, gdyby LLM użył
                // symbolu spoza zadeklarowanego alfabetu.
                tokens.Add(c.ToString());
                i++;
            }

            return tokens;
        }
    }
}