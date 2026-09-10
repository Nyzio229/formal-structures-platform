namespace FormalStructuresWebApp.Models.Domain
{
    /// <summary>
    /// Metadane o TYM, JAK automat został wygenerowany — do debugowania
    /// i benchmarkowania (np. z benchmark.py, przez odczyt pola
    /// "generationInfo" w JSON-ie zwracanym przez /api/structures/current).
    /// To nie jest część formalnej definicji automatu, tylko informacja
    /// diagnostyczna o przebiegu generowania.
    /// </summary>
    public class GenerationInfo
    {
        /// <summary>"regex" (szybka ścieżka jednostrzałowa) albo "lstar" (fallback).</summary>
        public string Source { get; set; } = "";

        /// <summary>Surowe wyrażenie regularne wygenerowane przez LLM (jeśli próbowano tej ścieżki).</summary>
        public string? RawRegex { get; set; }

        /// <summary>Powód spadku do L*, jeśli ścieżka regexowa się nie powiodła.</summary>
        public string? RegexFallbackReason { get; set; }

        /// <summary>Liczba rozbieżności wykrytych podczas szybkiej weryfikacji wyrywkowej regexu.</summary>
        public int? SanityCheckMismatches { get; set; }

        /// <summary>Wszyscy kandydaci regexu wygenerowani i ocenieni (samospójność) — do debugowania.</summary>
        public List<RegexCandidateInfo>? Candidates { get; set; }
    }

    /// <summary>Pojedynczy kandydat regexu oceniony w ramach samospójności (self-consistency).</summary>
    public class RegexCandidateInfo
    {
        public string Regex { get; set; } = "";
        public int? Mismatches { get; set; }
        public int? StateCount { get; set; }
        public string? ParseError { get; set; }
    }
}