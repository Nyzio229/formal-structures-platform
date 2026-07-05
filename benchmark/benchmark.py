"""
Benchmark: FSP vs ChatGPT-4o vs Ollama-raw
Uruchomienie: python benchmark.py
Wyniki: results/benchmark_results.json + results/*.png
"""

import json
import time
import itertools
import random
import statistics
import os
from dataclasses import dataclass, field, asdict
from typing import Callable
import requests

# ── pip install openai httpx matplotlib pandas tabulate ──────────────
import openai
import matplotlib.pyplot as plt
import pandas as pd
from tabulate import tabulate

# ════════════════════════════════════════════════════════════════════
# 1. ZBIÓR JĘZYKÓW BENCHMARKOWYCH
# ════════════════════════════════════════════════════════════════════

@dataclass
class BenchmarkLanguage:
    id: str
    description_pl: str           # opis słowny (prompt dla modelu)
    alphabet: list[str]
    membership: Callable[[str], bool]  # ground truth
    category: str                  # trivial / regular / cfg
    max_test_len: int = 6


LANGUAGES: list[BenchmarkLanguage] = [

    # ── TRYWIALNE ────────────────────────────────────────────────
    BenchmarkLanguage(
        id="ends_b",
        description_pl="Język słów nad alfabetem {a, b} kończących się na literę b",
        alphabet=["a", "b"],
        membership=lambda w: len(w) > 0 and w[-1] == "b",
        category="trivial",
    ),
    BenchmarkLanguage(
        id="contains_a",
        description_pl="Język słów nad {a, b} zawierających przynajmniej jedno a",
        alphabet=["a", "b"],
        membership=lambda w: "a" in w,
        category="trivial",
    ),
    BenchmarkLanguage(
        id="starts_ab",
        description_pl="Język słów nad {a, b} zaczynających się od ab",
        alphabet=["a", "b"],
        membership=lambda w: w.startswith("ab"),
        category="trivial",
    ),

    # ── REGULARNE ────────────────────────────────────────────────
    BenchmarkLanguage(
        id="even_a",
        description_pl="Język słów nad {a, b} zawierających parzystą liczbę liter a "
                       "(zero też jest parzyste)",
        alphabet=["a", "b"],
        membership=lambda w: w.count("a") % 2 == 0,
        category="regular",
    ),
    BenchmarkLanguage(
        id="div3_binary",
        description_pl="Język liczb binarnych (cyfry 0 i 1) reprezentujących liczby "
                       "podzielne przez 3 (włącznie z 0)",
        alphabet=["0", "1"],
        membership=lambda w: len(w) > 0 and int(w, 2) % 3 == 0,
        category="regular",
    ),
    BenchmarkLanguage(
        id="no_aa",
        description_pl="Język słów nad {a, b} niezawierających podciągu aa",
        alphabet=["a", "b"],
        membership=lambda w: "aa" not in w,
        category="regular",
    ),
    BenchmarkLanguage(
        id="ends_ab",
        description_pl="Język słów nad {a, b} kończących się na podciąg ab",
        alphabet=["a", "b"],
        membership=lambda w: w.endswith("ab"),
        category="regular",
    ),
    BenchmarkLanguage(
        id="len_mod3",
        description_pl="Język słów nad {a, b} których długość jest podzielna przez 3",
        alphabet=["a", "b"],
        membership=lambda w: len(w) % 3 == 0,
        category="regular",
    ),

    # ── BEZKONTEKSTOWE (CFG/PDA) ─────────────────────────────────
    BenchmarkLanguage(
        id="anbn",
        description_pl="Język słów postaci a^n b^n dla n >= 1 "
                       "(tyle samo liter a co b, najpierw a potem b)",
        alphabet=["a", "b"],
        membership=lambda w: (
            len(w) >= 2 and
            len(w) % 2 == 0 and
            w == "a" * (len(w) // 2) + "b" * (len(w) // 2)
        ),
        category="cfg",
        max_test_len=8,
    ),
    BenchmarkLanguage(
        id="balanced_parens",
        description_pl="Język poprawnie zagnieżdżonych nawiasów okrągłych "
                       "nad alfabetem {'(', ')'}",
        alphabet=["(", ")"],
        membership=lambda w: _is_balanced(w),
        category="cfg",
        max_test_len=8,
    ),
    BenchmarkLanguage(
        id="palindrome_ab",
        description_pl="Język palindromów nad {a, b} — słów które czytają się "
                       "tak samo od lewej i od prawej",
        alphabet=["a", "b"],
        membership=lambda w: w == w[::-1],
        category="cfg",
        max_test_len=7,
    ),
    BenchmarkLanguage(
        id="equal_ab",
        description_pl="Język słów nad {a, b} zawierających tyle samo liter a co b "
                       "(w dowolnej kolejności)",
        alphabet=["a", "b"],
        membership=lambda w: w.count("a") == w.count("b"),
        category="cfg",
        max_test_len=8,
    ),
]


def _is_balanced(w: str) -> bool:
    depth = 0
    for ch in w:
        if ch == "(": depth += 1
        elif ch == ")":
            depth -= 1
            if depth < 0: return False
    return depth == 0


# ════════════════════════════════════════════════════════════════════
# 2. GENEROWANIE SŁÓW TESTOWYCH (BFS)
# ════════════════════════════════════════════════════════════════════

def generate_test_words(alphabet: list[str], max_len: int) -> list[str]:
    words = [""]
    queue = [""]
    while queue:
        w = queue.pop(0)
        if len(w) >= max_len:
            continue
        for sym in alphabet:
            nw = w + sym
            words.append(nw)
            queue.append(nw)
    return words


# ════════════════════════════════════════════════════════════════════
# 3. PARSOWANIE AUTOMATÓW Z TEKSTU LLM
# ════════════════════════════════════════════════════════════════════

@dataclass
class ParsedDFA:
    states: list[str]
    alphabet: list[str]
    start: str
    accepting: list[str]
    # transitions[(state, symbol)] = next_state
    transitions: dict[tuple[str, str], str]
    raw_text: str = ""
    parse_error: str = ""

    def simulate(self, word: str) -> bool | None:
        """None = nie da się zasymulować (niekompletny automat)"""
        current = self.start
        for ch in word:
            key = (current, ch)
            if key not in self.transitions:
                return None   # niekompletny DFA
            current = self.transitions[key]
        return current in self.accepting


def parse_dfa_from_llm_text(text: str, alphabet: list[str]) -> ParsedDFA:
    """
    Próbuje wydobyć DFA z dowolnie sformatowanej odpowiedzi LLM.
    Szuka sekcji z tabelą przejść, stanami akceptującymi, stanem startowym.
    """
    import re

    lines = text.strip().split("\n")
    transitions: dict[tuple[str, str], str] = {}
    states_found: set[str] = set()
    start_state: str = ""
    accepting: list[str] = []

    # Szukaj linii w stylu: q0 --a--> q1  /  delta(q0, a) = q1  /  q0, a -> q1
    patterns = [
        # delta(q0, a) = q1
        re.compile(r"delta\s*\(\s*(\w+)\s*,\s*(\S+)\s*\)\s*=\s*(\w+)", re.I),
        # q0, a -> q1  lub  q0 a q1
        re.compile(r"(\w+)\s*,\s*(\S+)\s*[-=]>\s*(\w+)"),
        # q0 --a--> q1
        re.compile(r"(\w+)\s*-+(\S+)-+>\s*(\w+)"),
        # | q0 | a | q1 |  (tabela markdown)
        re.compile(r"\|\s*(\w+)\s*\|\s*(\S+)\s*\|\s*(\w+)\s*\|"),
    ]

    for line in lines:
        line_clean = line.strip()

        # Wykryj stan startowy
        if re.search(r"start(ow[ay]|ing)?\s*(stan|state)?.*?[:=]\s*(\w+)", line_clean, re.I):
            m = re.search(r":\s*(\w+)", line_clean)
            if m: start_state = m.group(1)

        # Wykryj stany akceptujące
        if re.search(r"akceptuj[aą]\w*|accept\w*|final", line_clean, re.I):
            found = re.findall(r"\b(q\d+|\w\d*)\b", line_clean)
            accepting.extend([s for s in found if re.match(r"q\d+", s)])

        # Wykryj przejścia
        for pat in patterns:
            m = pat.search(line_clean)
            if m:
                frm, sym, to = m.group(1), m.group(2), m.group(3)
                if sym in alphabet:
                    transitions[(frm, sym)] = to
                    states_found.update([frm, to])

    # Jeśli nie znalazł stanu startowego — spróbuj "q0"
    if not start_state and "q0" in states_found:
        start_state = "q0"

    error = ""
    if not transitions:
        error = "Nie znaleziono żadnych przejść"
    elif not start_state:
        error = "Nie znaleziono stanu startowego"

    return ParsedDFA(
        states=list(states_found),
        alphabet=alphabet,
        start=start_state,
        accepting=list(set(accepting)),
        transitions=transitions,
        raw_text=text,
        parse_error=error,
    )


# ════════════════════════════════════════════════════════════════════
# 4. KLIENTY LLM
# ════════════════════════════════════════════════════════════════════

OPENAI_API_KEY = os.environ.get("OPENAI_API_KEY", "")
OLLAMA_BASE    = os.environ.get("OLLAMA_BASE", "http://localhost:11434")
OLLAMA_MODEL   = os.environ.get("OLLAMA_MODEL", "mistral")
FSP_BASE       = os.environ.get("FSP_BASE", "https://localhost:7160")

# -- System prompt identyczny dla A i B --
SYSTEM_PROMPT_DFA = """Jesteś ekspertem od teorii języków formalnych i automatów.
Gdy użytkownik poda opis języka regularnego, odpowiedz WYŁĄCZNIE definicją DFA.
Format odpowiedzi (obowiązkowy):

Stan startowy: q0
Stany akceptujące: q1, q2   (lub lista)
Przejścia:
delta(q0, a) = q1
delta(q0, b) = q0
...itd dla każdej pary (stan, symbol)

Nie pisz nic poza tym."""

SYSTEM_PROMPT_PDA = """Jesteś ekspertem od teorii języków formalnych.
Gdy użytkownik poda opis języka bezkontekstowego, odpowiedz WYŁĄCZNIE
gramatyką bezkontekstową (CFG) w formacie BNF:

S -> a S b | a b

Każda produkcja w osobnej linii. Nieterminale: wielkie litery.
Terminale: małe litery. Symbol pusty: eps. Nic poza produkcjami."""


def ask_chatgpt(description: str, is_cfg: bool = False) -> tuple[str, float]:
    """Zwraca (odpowiedź, czas_sekundy)"""
    if not OPENAI_API_KEY:
        return ("BRAK KLUCZA OPENAI_API_KEY", 0.0)

    client = openai.OpenAI(api_key=OPENAI_API_KEY)
    system = SYSTEM_PROMPT_PDA if is_cfg else SYSTEM_PROMPT_DFA

    t0 = time.time()
    resp = client.chat.completions.create(
        model="gpt-4o",
        messages=[
            {"role": "system", "content": system},
            {"role": "user",   "content": description},
        ],
        temperature=0,
        max_tokens=800,
    )
    elapsed = time.time() - t0
    return resp.choices[0].message.content or "", elapsed


def ask_ollama_raw(description: str, is_cfg: bool = False) -> tuple[str, float]:
    """Ollama bez pipeline — surowy prompt jak u ChatGPT"""
    system = SYSTEM_PROMPT_PDA if is_cfg else SYSTEM_PROMPT_DFA
    prompt = f"{system}\n\n{description}"

    t0 = time.time()
    resp = requests.post(
        f"{OLLAMA_BASE}/api/generate",
        json={"model": OLLAMA_MODEL, "prompt": prompt, "stream": False},
        timeout=120,
    )
    elapsed = time.time() - t0
    if resp.status_code != 200:
        return (f"HTTP {resp.status_code}", elapsed)
    return resp.json().get("response", ""), elapsed


def ask_fsp_generate_dfa(description: str, alphabet: list[str]) -> tuple[dict, float]:
    """Wywołuje Twoją aplikację FSP — endpoint generowania DFA"""
    import ssl, urllib3
    urllib3.disable_warnings()

    t0 = time.time()
    try:
        resp = requests.post(
            f"{FSP_BASE}/Structures/Generate",
            data={
                "Description": description,
                "Alphabet":    ",".join(alphabet),
            },
            timeout=300,
            verify=False,  # localhost self-signed cert
        )
        elapsed = time.time() - t0
        # Pobierz wygenerowany automat z sesji
        auto_resp = requests.get(
            f"{FSP_BASE}/api/structures/current",
            timeout=30, verify=False,
        )
        return auto_resp.json(), elapsed
    except Exception as e:
        return {"error": str(e)}, time.time() - t0


def simulate_automaton_from_fsp(automaton_json: dict, word: str) -> bool | None:
    """Symulacja DFA zwróconego przez FSP"""
    states     = automaton_json.get("states", [])
    trans_list = automaton_json.get("transitions", [])
    start      = next((s["name"] for s in states if s.get("isStart")), None)
    accepting  = {s["name"] for s in states if s.get("isAccepting")}

    if not start:
        return None

    trans = {(t["fromState"], t["symbol"]): t["toState"] for t in trans_list}

    current = start
    for ch in word:
        key = (current, ch)
        if key not in trans:
            return None
        current = trans[key]
    return current in accepting


# ════════════════════════════════════════════════════════════════════
# 5. OBLICZANIE METRYK
# ════════════════════════════════════════════════════════════════════

@dataclass
class LanguageResult:
    lang_id: str
    category: str
    variant: str          # "chatgpt" | "ollama_raw" | "fsp"
    accuracy: float       # 0.0–1.0 na zbiorze testowym
    parsed_ok: bool       # czy automat udało się sparsować
    complete: bool        # czy automat jest kompletny (brak None w symulacji)
    state_count: int      # liczba stanów
    duration_sec: float
    error_msg: str = ""


def compute_accuracy(
    simulate_fn: Callable[[str], bool | None],
    test_words: list[str],
    ground_truth: Callable[[str], bool],
) -> tuple[float, bool]:
    """Zwraca (accuracy, is_complete)"""
    correct = 0
    total   = len(test_words)
    complete = True

    for w in test_words:
        result = simulate_fn(w)
        if result is None:
            complete = False
            # Traktuj jako błąd (zakładamy odrzucenie)
            result = False
        if result == ground_truth(w):
            correct += 1

    return correct / total if total > 0 else 0.0, complete


# ════════════════════════════════════════════════════════════════════
# 6. GŁÓWNA PĘTLA BENCHMARKU
# ════════════════════════════════════════════════════════════════════

def run_benchmark(
    languages: list[BenchmarkLanguage],
    run_chatgpt: bool = True,
    run_ollama: bool  = True,
    run_fsp: bool     = True,
) -> list[LanguageResult]:

    results: list[LanguageResult] = []

    for lang in languages:
        print(f"\n{'='*60}")
        print(f"Język: {lang.id} [{lang.category}]")
        print(f"Opis:  {lang.description_pl[:60]}...")

        test_words = generate_test_words(lang.alphabet, lang.max_test_len)
        is_cfg = lang.category == "cfg"
        print(f"Słów testowych: {len(test_words)}")

        # ── Wariant A: ChatGPT ────────────────────────────────
        if run_chatgpt:
            print("  → ChatGPT-4o...", end=" ", flush=True)
            raw, dur = ask_chatgpt(lang.description_pl, is_cfg=is_cfg)
            dfa  = parse_dfa_from_llm_text(raw, lang.alphabet)
            ok   = not bool(dfa.parse_error) and bool(dfa.transitions)
            acc, complete = (
                compute_accuracy(dfa.simulate, test_words, lang.membership)
                if ok else (0.0, False)
            )
            print(f"accuracy={acc:.2f}  parsed={'✓' if ok else '✗'}  {dur:.1f}s")
            results.append(LanguageResult(
                lang_id=lang.id, category=lang.category, variant="chatgpt",
                accuracy=acc, parsed_ok=ok, complete=complete,
                state_count=len(dfa.states), duration_sec=dur,
                error_msg=dfa.parse_error,
            ))

        # ── Wariant B: Ollama raw ─────────────────────────────
        if run_ollama:
            print("  → Ollama raw...", end=" ", flush=True)
            raw, dur = ask_ollama_raw(lang.description_pl, is_cfg=is_cfg)
            dfa  = parse_dfa_from_llm_text(raw, lang.alphabet)
            ok   = not bool(dfa.parse_error) and bool(dfa.transitions)
            acc, complete = (
                compute_accuracy(dfa.simulate, test_words, lang.membership)
                if ok else (0.0, False)
            )
            print(f"accuracy={acc:.2f}  parsed={'✓' if ok else '✗'}  {dur:.1f}s")
            results.append(LanguageResult(
                lang_id=lang.id, category=lang.category, variant="ollama_raw",
                accuracy=acc, parsed_ok=ok, complete=complete,
                state_count=len(dfa.states), duration_sec=dur,
                error_msg=dfa.parse_error,
            ))

        # ── Wariant C: FSP ────────────────────────────────────
        if run_fsp:
            print("  → FSP...", end=" ", flush=True)
            auto_json, dur = ask_fsp_generate_dfa(lang.description_pl, lang.alphabet)
            ok = "error" not in auto_json and auto_json.get("states")
            if ok:
                acc, complete = compute_accuracy(
                    lambda w: simulate_automaton_from_fsp(auto_json, w),
                    test_words, lang.membership,
                )
                sc = len(auto_json.get("states", []))
            else:
                acc, complete, sc = 0.0, False, 0
            print(f"accuracy={acc:.2f}  {'✓' if ok else '✗'}  {dur:.1f}s")
            results.append(LanguageResult(
                lang_id=lang.id, category=lang.category, variant="fsp",
                accuracy=acc, parsed_ok=ok, complete=complete,
                state_count=sc, duration_sec=dur,
                error_msg=auto_json.get("error", ""),
            ))

    return results


# ════════════════════════════════════════════════════════════════════
# 7. RAPORTY I WYKRESY
# ════════════════════════════════════════════════════════════════════

def save_results(results: list[LanguageResult]):
    os.makedirs("results", exist_ok=True)
    with open("results/benchmark_results.json", "w", encoding="utf-8") as f:
        json.dump([asdict(r) for r in results], f, ensure_ascii=False, indent=2)
    print("\nZapisano: results/benchmark_results.json")


def print_table(results: list[LanguageResult]):
    rows = []
    for r in results:
        rows.append({
            "Język":     r.lang_id,
            "Kategoria": r.category,
            "Wariant":   r.variant,
            "Accuracy":  f"{r.accuracy:.2f}",
            "Parsed":    "✓" if r.parsed_ok else "✗",
            "Kompletny": "✓" if r.complete  else "✗",
            "Stany":     r.state_count,
            "Czas [s]":  f"{r.duration_sec:.1f}",
        })
    df = pd.DataFrame(rows)
    print("\n" + tabulate(df, headers="keys", tablefmt="grid", showindex=False))
    df.to_csv("results/benchmark_table.csv", index=False)


def plot_accuracy_by_category(results: list[LanguageResult]):
    categories = ["trivial", "regular", "cfg"]
    variants   = ["chatgpt", "ollama_raw", "fsp"]
    colors     = {"chatgpt": "#2196F3", "ollama_raw": "#FF9800", "fsp": "#4CAF50"}
    labels     = {"chatgpt": "ChatGPT-4o", "ollama_raw": "Ollama (raw)", "fsp": "FSP (Twoja aplikacja)"}

    fig, axes = plt.subplots(1, 3, figsize=(14, 5), sharey=True)
    fig.suptitle(
        "Dokładność klasyfikacji słów (accuracy)\nwedług kategorii języka",
        fontsize=13, fontweight="bold"
    )

    for ax, cat in zip(axes, categories):
        cat_results = [r for r in results if r.category == cat]
        lang_ids = sorted(set(r.lang_id for r in cat_results))

        x = range(len(lang_ids))
        bar_width = 0.25

        for i, variant in enumerate(variants):
            accs = []
            for lid in lang_ids:
                vr = next((r for r in cat_results
                           if r.lang_id == lid and r.variant == variant), None)
                accs.append(vr.accuracy if vr else 0.0)

            offset = (i - 1) * bar_width
            bars = ax.bar([xi + offset for xi in x], accs,
                          bar_width, label=labels[variant],
                          color=colors[variant], alpha=0.85)

        ax.set_title(cat.upper(), fontweight="bold")
        ax.set_xticks(x)
        ax.set_xticklabels(lang_ids, rotation=30, ha="right", fontsize=8)
        ax.set_ylim(0, 1.05)
        ax.set_ylabel("Accuracy" if cat == "trivial" else "")
        ax.axhline(1.0, color="grey", linestyle="--", linewidth=0.8, alpha=0.5)
        ax.legend(fontsize=7)

    plt.tight_layout()
    plt.savefig("results/accuracy_by_category.png", dpi=150)
    print("Zapisano: results/accuracy_by_category.png")


def plot_parse_success_rate(results: list[LanguageResult]):
    variants = ["chatgpt", "ollama_raw", "fsp"]
    labels   = ["ChatGPT-4o", "Ollama (raw)", "FSP"]
    colors   = ["#2196F3", "#FF9800", "#4CAF50"]

    rates = []
    for v in variants:
        vr = [r for r in results if r.variant == v]
        rate = sum(r.parsed_ok for r in vr) / len(vr) if vr else 0
        rates.append(rate)

    fig, ax = plt.subplots(figsize=(7, 4))
    bars = ax.bar(labels, rates, color=colors, alpha=0.85, width=0.5)
    ax.set_title("Odsetek poprawnie sparsowanych automatów\n(czy wynik da się odczytać jako DFA/PDA)", fontsize=11)
    ax.set_ylim(0, 1.1)
    ax.set_ylabel("Odsetek sukcesów parsowania")
    for bar, rate in zip(bars, rates):
        ax.text(bar.get_x() + bar.get_width() / 2,
                bar.get_height() + 0.02,
                f"{rate:.0%}", ha="center", fontsize=11, fontweight="bold")
    ax.axhline(1.0, color="grey", linestyle="--", linewidth=0.8)
    plt.tight_layout()
    plt.savefig("results/parse_success_rate.png", dpi=150)
    print("Zapisano: results/parse_success_rate.png")


def plot_avg_accuracy_per_variant(results: list[LanguageResult]):
    variants = ["chatgpt", "ollama_raw", "fsp"]
    labels   = ["ChatGPT-4o", "Ollama (raw)", "FSP"]
    colors   = ["#2196F3", "#FF9800", "#4CAF50"]

    avgs  = []
    stds  = []
    for v in variants:
        accs = [r.accuracy for r in results if r.variant == v]
        avgs.append(statistics.mean(accs) if accs else 0)
        stds.append(statistics.stdev(accs) if len(accs) > 1 else 0)

    fig, ax = plt.subplots(figsize=(7, 4))
    bars = ax.bar(labels, avgs, yerr=stds, color=colors, alpha=0.85,
                  width=0.5, capsize=6)
    ax.set_title("Średnia dokładność klasyfikacji słów\n(wszystkie języki)", fontsize=11)
    ax.set_ylim(0, 1.15)
    ax.set_ylabel("Średnia accuracy ± odchylenie std")
    for bar, avg in zip(bars, avgs):
        ax.text(bar.get_x() + bar.get_width() / 2,
                bar.get_height() + 0.04,
                f"{avg:.2f}", ha="center", fontsize=11, fontweight="bold")
    plt.tight_layout()
    plt.savefig("results/avg_accuracy.png", dpi=150)
    print("Zapisano: results/avg_accuracy.png")


def generate_latex_table(results: list[LanguageResult]) -> str:
    """Generuje tabelę LaTeX do wklejenia w pracę."""
    lang_ids  = sorted(set(r.lang_id for r in results))
    variants  = ["chatgpt", "ollama_raw", "fsp"]
    var_names = {"chatgpt": "ChatGPT-4o", "ollama_raw": "Ollama (raw)", "fsp": "FSP (nasza)"}

    lines = [
        r"\begin{table}[h]",
        r"\centering",
        r"\caption{Porównanie dokładności (accuracy) klasyfikacji słów}",
        r"\begin{tabular}{l|ccc}",
        r"\hline",
        r"\textbf{Język} & \textbf{ChatGPT-4o} & \textbf{Ollama raw} & \textbf{FSP} \\",
        r"\hline",
    ]

    for lid in lang_ids:
        row_vals = []
        for v in variants:
            r = next((x for x in results if x.lang_id == lid and x.variant == v), None)
            val = f"{r.accuracy:.2f}" if r else "--"
            if r and v == "fsp" and r.accuracy > 0.99:
                val = r"\textbf{" + val + "}"
            row_vals.append(val)
        lines.append(f"{lid} & {' & '.join(row_vals)} \\\\")

    lines += [
        r"\hline",
        r"\end{tabular}",
        r"\end{table}",
    ]
    latex = "\n".join(lines)

    with open("results/table_latex.tex", "w") as f:
        f.write(latex)
    print("Zapisano: results/table_latex.tex")
    return latex


# ════════════════════════════════════════════════════════════════════
# 8. ENTRY POINT
# ════════════════════════════════════════════════════════════════════

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="FSP Benchmark")
    parser.add_argument("--no-chatgpt", action="store_true",
                        help="Pomiń testy ChatGPT (wymaga OPENAI_API_KEY)")
    parser.add_argument("--no-ollama",  action="store_true")
    parser.add_argument("--no-fsp",     action="store_true")
    parser.add_argument("--only",       type=str, default=None,
                        help="Testuj tylko jeden język, np. --only anbn")
    args = parser.parse_args()

    langs = LANGUAGES
    if args.only:
        langs = [l for l in LANGUAGES if l.id == args.only]
        if not langs:
            print(f"Nie znaleziono języka: {args.only}")
            exit(1)

    results = run_benchmark(
        langs,
        run_chatgpt=not args.no_chatgpt,
        run_ollama =not args.no_ollama,
        run_fsp    =not args.no_fsp,
    )

    save_results(results)
    print_table(results)
    plot_accuracy_by_category(results)
    plot_parse_success_rate(results)
    plot_avg_accuracy_per_variant(results)
    generate_latex_table(results)

    print("\nGotowe. Wyniki w folderze results/")