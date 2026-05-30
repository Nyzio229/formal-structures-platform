document.addEventListener("DOMContentLoaded", function () {
    const dataEl = document.getElementById("pda-data");
    const container = document.getElementById("pdaGraph");
    if (!dataEl || !container) return;

    const initialPda = JSON.parse(dataEl.textContent);

    let selectedNodeId = null;
    let selectedEdgeData = null;
    let addTransMode = false;
    let transSourceId = null;

    // ── Pomocnicze ───────────────────────────────────────────────
    function stateName(s) { return s.name || s.Name || s; }
    function stateX(s) { return s.x ?? s.X ?? 200; }
    function stateY(s) { return s.y ?? s.Y ?? 200; }
    function isStart(s) { return !!(s.isStart || s.IsStart); }
    function isAccepting(s) { return !!(s.isAccepting || s.IsAccepting); }

    // ── Buduj elementy Cytoscape ─────────────────────────────────
    function buildElements(states, transitions) {
        // Oblicz startState lokalnie z przekazanych stanów
        const startStateObj = states.find(s => isStart(s));
        const startStateName = startStateObj ? stateName(startStateObj) : null;

        const edgeMap = {};
        (transitions || []).forEach(t => {
            const from = t.fromState || t.FromState || "";
            const to = t.toState || t.ToState || "";
            const inp = t.inputSymbol ?? t.InputSymbol ?? null;
            const top = t.stackTop || t.StackTop || "";
            const push = t.pushSymbols || t.PushSymbols || [];
            const inpStr = inp ?? "ε";
            const pushStr = push.length > 0 ? push.join(" ") : "ε";
            const label = `${inpStr}, ${top} → ${pushStr}`;
            const key = `${from}||${to}`;
            if (!edgeMap[key]) edgeMap[key] = { from, to, labels: [], raw: [] };
            edgeMap[key].labels.push(label);
            edgeMap[key].raw.push({ from, to, inp, top, push });
        });

        const nodes = (states || []).map(s => ({
            data: {
                id: stateName(s),
                label: stateName(s),
                isAccepting: isAccepting(s),
                isStart: isStart(s)
            },
            position: { x: stateX(s), y: stateY(s) },
            classes: [
                "pda-state",
                isAccepting(s) ? "accepting" : "",
                isStart(s) ? "start" : ""
            ].filter(Boolean).join(" ")
        }));

        const edges = Object.entries(edgeMap).map(([key, e], i) => ({
            data: {
                id: `e${i}`,
                source: e.from,
                target: e.to,
                label: e.labels.join("\n"),
                rawTrans: JSON.stringify(e.raw)
            },
            classes: e.from === e.to ? "self-loop" : ""
        }));

        // Strzałka startowa — tylko jeśli istnieje stan startowy
        if (startStateName) {
            nodes.push({
                data: { id: "__src__", label: "" },
                position: {
                    x: stateX(startStateObj) - 90,
                    y: stateY(startStateObj)
                },
                classes: "invisible"
            });
            edges.push({
                data: {
                    id: "__start__",
                    source: "__src__",
                    target: startStateName,
                    label: ""
                },
                classes: "start-arrow"
            });
        }

        return [...nodes, ...edges];
    }

    // ── Inicjalizacja Cytoscape ──────────────────────────────────
    const initialStates = initialPda.states || initialPda.States || [];
    const initialTrans = initialPda.transitions || initialPda.Transitions || [];

    const cy = cytoscape({
        container,
        elements: buildElements(initialStates, initialTrans),
        layout: { name: "preset" },
        style: [
            {
                selector: "node.pda-state",
                style: {
                    "label": "data(label)",
                    "text-valign": "center",
                    "text-halign": "center",
                    "width": 70,
                    "height": 70,
                    "background-color": "white",
                    "border-width": 2,
                    "border-color": "#333",
                    "font-size": 12,
                    "font-family": "DM Mono, monospace",
                    "color": "#111"
                }
            },
            {
                selector: "node.accepting",
                style: {
                    "border-color": "#1a6a3a",
                    "border-width": 3,
                    "background-color": "#e8f5ee"
                }
            },
            {
                selector: "node.start",
                style: {
                    "border-color": "#1a4a8a",
                    "border-width": 3
                }
            },
            {
                selector: "node:selected",
                style: {
                    "border-color": "#0d6efd",
                    "border-width": 4
                }
            },
            {
                selector: "node.invisible",
                style: {
                    "width": 1,
                    "height": 1,
                    "background-color": "transparent",
                    "border-width": 0,
                    "label": ""
                }
            },
            {
                selector: "edge",
                style: {
                    "curve-style": "bezier",
                    "target-arrow-shape": "triangle",
                    "target-arrow-color": "#555",
                    "line-color": "#555",
                    "width": 2,
                    "label": "data(label)",
                    "text-wrap": "wrap",
                    "text-max-width": "200px",
                    "font-family": "DM Mono, monospace",
                    "font-size": 10,
                    "text-background-opacity": 1,
                    "text-background-color": "white",
                    "text-background-padding": "3px",
                    "text-border-opacity": 1,
                    "text-border-color": "#d4cfc6",
                    "text-border-width": 1,
                    "text-margin-y": -12
                }
            },
            {
                selector: "edge.self-loop",
                style: {
                    "curve-style": "loop",
                    "loop-direction": "90deg",
                    "loop-sweep": "-60deg",
                    "control-point-step-size": 100,
                    "text-margin-y": -80,
                    "text-margin-x": 20,
                    "text-max-width": "220px"
                }
            },
            {
                selector: "edge:selected",
                style: {
                    "line-color": "#dc3545",
                    "target-arrow-color": "#dc3545"
                }
            },
            {
                selector: "edge.start-arrow",
                style: {
                    "curve-style": "straight",
                    "target-arrow-shape": "triangle",
                    "target-arrow-color": "#333",
                    "line-color": "#333",
                    "width": 2,
                    "label": ""
                }
            }
        ]
    });

    // ── Podwójne koło dla akceptujących ─────────────────────────
    const overlay = document.getElementById("pdaOverlay");
    if (overlay) {
        const wrap = overlay.parentElement;
        overlay.width = wrap.offsetWidth;
        overlay.height = wrap.offsetHeight;
        const ctx = overlay.getContext("2d");

        function drawRings() {
            ctx.clearRect(0, 0, overlay.width, overlay.height);
            cy.nodes(".accepting").forEach(n => {
                const pos = n.renderedPosition();
                const sz = n.renderedWidth();
                ctx.beginPath();
                ctx.arc(pos.x, pos.y, sz / 2 - 7, 0, 2 * Math.PI);
                ctx.strokeStyle = "#1a6a3a";
                ctx.lineWidth = 2;
                ctx.stroke();
            });
        }

        cy.on("render", drawRings);
        drawRings();
    }

    // ── Zaznaczanie ──────────────────────────────────────────────
    cy.on("tap", "node.pda-state", e => {
        if (addTransMode) {
            handleTransitionClick(e.target.id());
            return;
        }
        selectedNodeId = e.target.id();
        selectedEdgeData = null;
        updateSelectionInfo();
    });

    cy.on("tap", "edge", e => {
        if (addTransMode) return;
        selectedEdgeData = e.target.data();
        selectedNodeId = null;
        updateSelectionInfo();
    });

    cy.on("tap", e => {
        if (e.target === cy) {
            selectedNodeId = selectedEdgeData = null;
            updateSelectionInfo();
        }
    });

    function updateSelectionInfo() {
        const el = document.getElementById("selectionInfo");
        if (!el) return;
        if (selectedNodeId)
            el.textContent = `Stan: ${selectedNodeId}`;
        else if (selectedEdgeData)
            el.textContent = `Przejście: ${selectedEdgeData.label}`;
        else
            el.textContent = "Nic nie zaznaczono.";
    }

    // ── Dodaj stan ───────────────────────────────────────────────
    document.getElementById("addStateBtn")?.addEventListener("click", async () => {
        const name = prompt("Nazwa stanu:");
        if (!name || !name.trim()) return;

        const isStartState = confirm("Stan początkowy?");
        const isAcc = confirm("Stan akceptujący?");
        const pos = {
            x: 150 + Math.random() * 400,
            y: 150 + Math.random() * 200
        };

        const res = await fetch("/api/pda/add-state", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                name,
                isStart: isStartState,
                isAccepting: isAcc,
                x: pos.x,
                y: pos.y
            })
        });

        if (res.ok) refreshGraph(await res.json());
    });

    // ── Usuń stan ────────────────────────────────────────────────
    document.getElementById("removeStateBtn")?.addEventListener("click", async () => {
        if (!selectedNodeId) { alert("Zaznacz stan do usunięcia."); return; }

        const res = await fetch("/api/pda/remove-state", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ stateName: selectedNodeId })
        });

        if (res.ok) {
            selectedNodeId = null;
            updateSelectionInfo();
            refreshGraph(await res.json());
        }
    });

    // ── Tryb dodawania przejścia ─────────────────────────────────
    document.getElementById("addTransBtn")?.addEventListener("click", () => {
        addTransMode = !addTransMode;
        transSourceId = null;
        const btn = document.getElementById("addTransBtn");
        if (addTransMode) {
            btn.textContent = "⇒ Kliknij stan źródłowy...";
            btn.classList.add("active");
        } else {
            btn.textContent = "⇒ Dodaj przejście";
            btn.classList.remove("active");
        }
    });

    function handleTransitionClick(nodeId) {
        if (!transSourceId) {
            transSourceId = nodeId;
            const btn = document.getElementById("addTransBtn");
            btn.textContent = `⇒ Źródło: ${nodeId} — kliknij cel`;
        } else {
            const targetId = nodeId;
            showTransitionDialog(transSourceId, targetId);
            transSourceId = null;
            addTransMode = false;
            const btn = document.getElementById("addTransBtn");
            btn.textContent = "⇒ Dodaj przejście";
            btn.classList.remove("active");
        }
    }

    // ── Dialog przejścia ─────────────────────────────────────────
    function showTransitionDialog(from, to) {
        const dialog = document.getElementById("transDialog");
        if (!dialog) return;

        document.getElementById("dialogFrom").textContent = from;
        document.getElementById("dialogTo").textContent = to;
        document.getElementById("transInput").value = "";
        document.getElementById("transStackTop").value = "";
        document.getElementById("transPush").value = "";
        dialog.style.display = "flex";

        document.getElementById("dialogConfirm").onclick = async () => {
            const inp = document.getElementById("transInput").value.trim();
            const top = document.getElementById("transStackTop").value.trim();
            const push = document.getElementById("transPush").value.trim();

            if (!top) { alert("Podaj symbol szczytu stosu."); return; }

            const inputSymbol = (inp === "" || inp === "ε") ? null : inp;
            const pushSymbols = (push === "" || push === "ε")
                ? []
                : push.split(/\s+/).filter(Boolean);

            const res = await fetch("/api/pda/add-transition", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    fromState: from,
                    inputSymbol,
                    stackTop: top,
                    pushSymbols,
                    toState: to
                })
            });

            if (res.ok) refreshGraph(await res.json());
            dialog.style.display = "none";
        };

        document.getElementById("dialogCancel").onclick = () => {
            dialog.style.display = "none";
        };
    }

    // ── Usuń przejście ───────────────────────────────────────────
    document.getElementById("removeTransBtn")?.addEventListener("click", async () => {
        if (!selectedEdgeData) { alert("Zaznacz przejście do usunięcia."); return; }

        let raw = [];
        try { raw = JSON.parse(selectedEdgeData.rawTrans || "[]"); } catch { }
        if (!raw.length) return;

        const t = raw[0];
        const res = await fetch("/api/pda/remove-transition", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
                fromState: t.from,
                inputSymbol: t.inp,
                stackTop: t.top,
                pushSymbols: t.push,
                toState: t.to
            })
        });

        if (res.ok) {
            selectedEdgeData = null;
            updateSelectionInfo();
            refreshGraph(await res.json());
        }
    });

    // ── Zapisz układ ─────────────────────────────────────────────
    document.getElementById("saveLayoutBtn")?.addEventListener("click", async () => {
        const positions = cy.nodes(".pda-state").map(n => ({
            name: n.id(),
            x: Math.round(n.position("x")),
            y: Math.round(n.position("y"))
        }));

        const res = await fetch("/api/pda/update-layout", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ states: positions })
        });

        if (!res.ok) alert("Błąd zapisu układu.");
    });

    // ── Odśwież graf ─────────────────────────────────────────────
    function refreshGraph(newPda) {
        const newStates = newPda.states || newPda.States || [];
        const newTrans = newPda.transitions || newPda.Transitions || [];

        cy.elements().remove();
        cy.add(buildElements(newStates, newTrans));
        updateStats(newPda);
    }

    // ── Statystyki ───────────────────────────────────────────────
    function updateStats(p) {
        const s = p.states || p.States || [];
        const t = p.transitions || p.Transitions || [];
        const a = p.inputAlphabet || p.InputAlphabet || [];

        const statsStates = document.getElementById("statsStates");
        const statsTrans = document.getElementById("statsTransitions");
        const statsAlph = document.getElementById("statsAlphabet");

        if (statsStates) statsStates.textContent =
            s.map(x => stateName(x)).join(", ") || "—";
        if (statsTrans) statsTrans.textContent = t.length || "—";
        if (statsAlph) statsAlph.textContent =
            a.length ? "{" + a.join(", ") + "}" : "—";
    }

    updateStats(initialPda);
});