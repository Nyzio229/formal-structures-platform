document.addEventListener("DOMContentLoaded", function () {
    const dataEl = document.getElementById("pda-data");
    if (!dataEl) return;

    const pda = JSON.parse(dataEl.textContent);
    const container = document.getElementById("pdaGraph");
    if (!container) return;

    //const states = pda.states || pda.States || [];
    const transitions = pda.transitions || pda.Transitions || [];
    //const accepting = pda.acceptingStates || pda.AcceptingStates || [];
    //const startState = pda.startState || pda.StartState || "";


    const rawStates = pda.states || pda.States || [];
    const startState = (rawStates.find(s =>
        s.isStart || s.IsStart) || {}).name
        || (rawStates.find(s => s.isStart || s.IsStart) || {}).Name
        || pda.startState || pda.StartState || "";
    const accepting = rawStates
        .filter(s => s.isAccepting || s.IsAccepting)
        .map(s => s.name || s.Name);
    const states = rawStates.map(s => s.name || s.Name);

    // Grupuj przejścia między tymi samymi parami stanów
    // żeby wiele przejść pokazać jako jedną krawędź z wieloma etykietami
    const edgeMap = {};
    transitions.forEach(t => {
        const from = t.fromState || t.FromState || "";
        const to = t.toState || t.ToState || "";
        const key = `${from}||${to}`;

        const inp = t.inputSymbol || t.InputSymbol || null;
        const top = t.stackTop || t.StackTop || "";
        const push = t.pushSymbols || t.PushSymbols || [];
        const inpStr = inp ?? "ε";
        const pushStr = push.length > 0 ? push.join(" ") : "ε";
        const label = `${inpStr}, ${top} → ${pushStr}`;

        if (!edgeMap[key]) {
            edgeMap[key] = { from, to, labels: [] };
        }
        edgeMap[key].labels.push(label);
    });

    // Pozycje stałe dla 3 stanów PDA
    const fixedPositions = {
        "q_start": { x: 120, y: 300 },
        "q_loop": { x: 420, y: 300 },
        "q_accept": { x: 720, y: 300 }
    };

    // Węzły
    const nodes = states.map((s, i) => ({
        data: {
            id: s,
            label: s,
            isAccepting: accepting.includes(s),
            isStart: s === startState
        },
        position: fixedPositions[s] || { x: 120 + i * 300, y: 300 },
        classes: [
            "pda-state",
            accepting.includes(s) ? "accepting" : "",
            s === startState ? "start" : ""
        ].filter(Boolean).join(" ")
    }));

    // Krawędzie z połączonymi etykietami
    const edges = Object.entries(edgeMap).map(([key, edge], i) => ({
        data: {
            id: `e${i}`,
            source: edge.from,
            target: edge.to,
            label: edge.labels.join("\n"),
            isSelfLoop: edge.from === edge.to
        },
        classes: edge.from === edge.to ? "self-loop" : ""
    }));

    // Niewidoczny węzeł pomocniczy dla strzałki startowej
    nodes.push({
        data: { id: "__src__", label: "" },
        position: { x: -30, y: 300 },
        classes: "invisible"
    });
    edges.push({
        data: { id: "__start__", source: "__src__", target: startState, label: "" },
        classes: "start-arrow"
    });

    const cy = cytoscape({
        container,
        elements: [...nodes, ...edges],
        layout: { name: "preset" },
        userZoomingEnabled: true,
        userPanningEnabled: true,
        boxSelectionEnabled: false,
        style: [
            // Węzeł zwykły
            {
                selector: "node.pda-state",
                style: {
                    "label": "data(label)",
                    "text-valign": "center",
                    "text-halign": "center",
                    "width": 75,
                    "height": 75,
                    "background-color": "white",
                    "border-width": 2,
                    "border-color": "#333",
                    "font-size": 12,
                    "font-family": "DM Mono, monospace",
                    "color": "#111"
                }
            },
            // Stan akceptujący — zielona obwódka + jaśniejsze tło
            {
                selector: "node.accepting",
                style: {
                    "border-width": 3,
                    "border-color": "#1a6a3a",
                    "background-color": "#e8f5ee"
                }
            },
            // Stan startowy — niebieska obwódka
            {
                selector: "node.start",
                style: {
                    "border-color": "#1a4a8a",
                    "border-width": 3
                }
            },
            // Ukryj węzeł pomocniczy
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
            // Krawędź zwykła
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
            // Pętla własna na q_loop
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
            // Strzałka startowa — cienka, bez etykiety
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

    // Podwójne koło dla stanów akceptujących — rysowane na canvas overlay
    const overlay = document.getElementById("pdaOverlay");
    if (overlay) {
        const wrapper = overlay.parentElement;
        overlay.width = wrapper.offsetWidth;
        overlay.height = wrapper.offsetHeight;
        const ctx = overlay.getContext("2d");

        function drawAcceptingRings() {
            ctx.clearRect(0, 0, overlay.width, overlay.height);
            cy.nodes(".accepting").forEach(node => {
                const pos = node.renderedPosition();
                const size = node.renderedWidth();
                ctx.beginPath();
                ctx.arc(pos.x, pos.y, size / 2 - 7, 0, 2 * Math.PI);
                ctx.strokeStyle = "#1a6a3a";
                ctx.lineWidth = 2;
                ctx.stroke();
            });
        }

        cy.on("render", drawAcceptingRings);
        drawAcceptingRings();
    }

    cy.fit(cy.elements().not(".invisible"), 60);
});