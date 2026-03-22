document.addEventListener("DOMContentLoaded", function () {
    const graphContainer = document.getElementById("automatonGraph");
    const graphWrapper = document.getElementById("automatonGraphWrapper");
    const overlayCanvas = document.getElementById("automatonOverlay");
    const dataElement = document.getElementById("automaton-data");

    if (!graphContainer || !graphWrapper || !overlayCanvas || !dataElement) {
        console.error("Brakuje jednego z elementów: graphContainer, graphWrapper, overlayCanvas albo dataElement.");
        return;
    }

    const automaton = JSON.parse(dataElement.textContent);
    console.log("Automaton JSON:", automaton);

    const states = automaton.states || automaton.States || [];
    const transitions = automaton.transitions || automaton.Transitions || [];

    const nodes = states.map(state => ({
        data: {
            id: state.name || state.Name,
            label: state.name || state.Name,
            isStart: state.isStart ?? state.IsStart,
            isAccepting: state.isAccepting ?? state.IsAccepting
        },
        position: {
            x: state.x ?? state.X ?? 100,
            y: state.y ?? state.Y ?? 100
        },
        classes: "main-state"
    }));

    const edges = transitions.map((transition, index) => ({
        data: {
            id: `e${index}`,
            source: transition.fromState || transition.FromState,
            target: transition.toState || transition.ToState,
            label: transition.symbol || transition.Symbol
        }
    }));

    const cy = cytoscape({
        container: graphContainer,
        elements: [...nodes, ...edges],
        layout: {
            name: "preset"
        },
        style: [
            {
                selector: "node.main-state",
                style: {
                    "label": "data(label)",
                    "text-valign": "center",
                    "text-halign": "center",
                    "width": 55,
                    "height": 55,
                    "background-color": "white",
                    "border-width": 2,
                    "border-color": "#333",
                    "color": "#111",
                    "font-size": 14
                }
            },
            {
                selector: "edge",
                style: {
                    "curve-style": "bezier",
                    "target-arrow-shape": "triangle",
                    "label": "data(label)",
                    "text-rotation": "autorotate",
                    "text-margin-y": -12,
                    "text-background-opacity": 1,
                    "text-background-color": "white",
                    "text-background-padding": 2,
                    "font-size": 12,
                    "width": 2,
                    "line-color": "#555",
                    "target-arrow-color": "#555"
                }
            }
        ]
    });

    function resizeOverlayCanvas() {
        const rect = graphWrapper.getBoundingClientRect();
        overlayCanvas.width = Math.round(rect.width);
        overlayCanvas.height = Math.round(rect.height);
    }

    function drawAcceptingStateInnerCircles() {
        resizeOverlayCanvas();

        const ctx = overlayCanvas.getContext("2d");
        if (!ctx) {
            console.error("Nie udało się pobrać kontekstu 2D canvas.");
            return;
        }

        ctx.clearRect(0, 0, overlayCanvas.width, overlayCanvas.height);

        cy.nodes().forEach(node => {
            if (!node.data("isAccepting")) {
                return;
            }

            const renderedPosition = node.renderedPosition();
            const renderedWidth = node.renderedWidth();

            const radius = renderedWidth / 2;
            const innerRadius = radius - 11;

            ctx.beginPath();
            ctx.arc(renderedPosition.x, renderedPosition.y, innerRadius, 0, 2 * Math.PI);
            ctx.strokeStyle = "#333";
            ctx.lineWidth = 4;
            ctx.stroke();
        });
    }

    cy.on("render", drawAcceptingStateInnerCircles);
    cy.on("zoom", drawAcceptingStateInnerCircles);
    cy.on("pan", drawAcceptingStateInnerCircles);
    cy.on("drag", "node", drawAcceptingStateInnerCircles);
    cy.on("free", "node", drawAcceptingStateInnerCircles);
    cy.on("position", "node", drawAcceptingStateInnerCircles);

    window.addEventListener("resize", drawAcceptingStateInnerCircles);

    drawAcceptingStateInnerCircles();

    window.automatonCy = cy;
});