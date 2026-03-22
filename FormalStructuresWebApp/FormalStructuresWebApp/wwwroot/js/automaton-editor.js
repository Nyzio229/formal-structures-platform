document.addEventListener("DOMContentLoaded", function () {
    const graphContainer = document.getElementById("automatonGraph");
    const graphWrapper = document.getElementById("automatonGraphWrapper");
    const overlayCanvas = document.getElementById("automatonOverlay");
    const dataElement = document.getElementById("automaton-data");

    const addStateButton = document.getElementById("addStateButton");
    const removeSelectedStateButton = document.getElementById("removeSelectedStateButton");
    const removeSelectedEdgeButton = document.getElementById("removeSelectedEdgeButton");
    const saveLayoutButton = document.getElementById("saveLayoutButton");
    const startAddTransitionModeButton = document.getElementById("startAddTransitionModeButton");
    const transitionSymbolInput = document.getElementById("transitionSymbolInput");
    const selectionInfo = document.getElementById("selectionInfo");

    if (!graphContainer || !graphWrapper || !overlayCanvas || !dataElement) {
        return;
    }

    const automaton = JSON.parse(dataElement.textContent);
    const states = automaton.states || automaton.States || [];
    const transitions = automaton.transitions || automaton.Transitions || [];

    let selectedNodeId = null;
    let selectedEdgeData = null;
    let addTransitionMode = false;
    let transitionSourceNodeId = null;

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
                selector: "node:selected",
                style: {
                    "border-color": "#0d6efd",
                    "border-width": 4
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
            },
            {
                selector: "edge:selected",
                style: {
                    "line-color": "#dc3545",
                    "target-arrow-color": "#dc3545",
                    "width": 4
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
            const innerRadius = radius - 8;

            ctx.beginPath();
            ctx.arc(renderedPosition.x, renderedPosition.y, innerRadius, 0, 2 * Math.PI);
            ctx.strokeStyle = "#333";
            ctx.lineWidth = 2;
            ctx.stroke();
        });
    }

    function refreshSelectionInfo() {
        if (selectedNodeId) {
            selectionInfo.innerHTML = `<strong>Stan:</strong> ${selectedNodeId}`;
            return;
        }

        if (selectedEdgeData) {
            selectionInfo.innerHTML = `<strong>Przejście:</strong> ${selectedEdgeData.source} --(${selectedEdgeData.label})-> ${selectedEdgeData.target}`;
            return;
        }

        selectionInfo.textContent = "Nic nie zaznaczono.";
    }

    function clearSelection() {
        selectedNodeId = null;
        selectedEdgeData = null;
        cy.elements().unselect();
        refreshSelectionInfo();
    }

    async function postJson(url, payload) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`Błąd żądania: ${response.status}`);
        }

        return await response.json();
    }

    cy.on("tap", "node", function (evt) {
        const node = evt.target;

        if (addTransitionMode) {
            if (!transitionSourceNodeId) {
                transitionSourceNodeId = node.id();
                selectionInfo.innerHTML = `<strong>Dodawanie przejścia:</strong> wybrano stan źródłowy ${transitionSourceNodeId}. Kliknij stan docelowy.`;
                return;
            }

            const targetNodeId = node.id();
            const symbol = transitionSymbolInput.value.trim();

            if (!symbol) {
                alert("Podaj symbol przejścia.");
                return;
            }

            postJson("/api/structures/add-transition", {
                fromState: transitionSourceNodeId,
                symbol: symbol,
                toState: targetNodeId
            })
                .then(() => {
                    location.reload();
                })
                .catch(error => {
                    console.error(error);
                    alert("Nie udało się dodać przejścia.");
                });

            return;
        }

        selectedNodeId = node.id();
        selectedEdgeData = null;
        refreshSelectionInfo();
    });

    cy.on("tap", "edge", function (evt) {
        selectedEdgeData = {
            source: evt.target.data("source"),
            target: evt.target.data("target"),
            label: evt.target.data("label")
        };
        selectedNodeId = null;
        refreshSelectionInfo();
    });

    cy.on("tap", function (evt) {
        if (evt.target === cy) {
            clearSelection();
        }
    });

    addStateButton.addEventListener("click", async function () {
        const name = prompt("Podaj nazwę nowego stanu:");
        if (!name) {
            return;
        }

        const isStart = confirm("Czy to ma być stan początkowy?");
        const isAccepting = confirm("Czy to ma być stan akceptujący?");

        try {
            await postJson("/api/structures/add-state", {
                name: name,
                isStart: isStart,
                isAccepting: isAccepting,
                x: 200,
                y: 200
            });

            location.reload();
        } catch (error) {
            console.error(error);
            alert("Nie udało się dodać stanu.");
        }
    });

    removeSelectedStateButton.addEventListener("click", async function () {
        if (!selectedNodeId) {
            alert("Najpierw zaznacz stan.");
            return;
        }

        try {
            await postJson("/api/structures/remove-state", {
                stateName: selectedNodeId
            });

            location.reload();
        } catch (error) {
            console.error(error);
            alert("Nie udało się usunąć stanu.");
        }
    });

    removeSelectedEdgeButton.addEventListener("click", async function () {
        if (!selectedEdgeData) {
            alert("Najpierw zaznacz przejście.");
            return;
        }

        try {
            await postJson("/api/structures/remove-transition", {
                fromState: selectedEdgeData.source,
                symbol: selectedEdgeData.label,
                toState: selectedEdgeData.target
            });

            location.reload();
        } catch (error) {
            console.error(error);
            alert("Nie udało się usunąć przejścia.");
        }
    });

    saveLayoutButton.addEventListener("click", async function () {
        const statePositions = cy.nodes().map(node => ({
            name: node.id(),
            x: node.position("x"),
            y: node.position("y")
        }));

        try {
            await postJson("/api/structures/update-layout", {
                states: statePositions
            });

            alert("Układ zapisany.");
        } catch (error) {
            console.error(error);
            alert("Nie udało się zapisać układu.");
        }
    });

    startAddTransitionModeButton.addEventListener("click", function () {
        addTransitionMode = true;
        transitionSourceNodeId = null;
        selectionInfo.innerHTML = "<strong>Tryb dodawania przejścia:</strong> kliknij stan źródłowy.";
    });

    cy.on("render", drawAcceptingStateInnerCircles);
    cy.on("zoom", drawAcceptingStateInnerCircles);
    cy.on("pan", drawAcceptingStateInnerCircles);
    cy.on("drag", "node", drawAcceptingStateInnerCircles);
    cy.on("free", "node", drawAcceptingStateInnerCircles);
    cy.on("position", "node", drawAcceptingStateInnerCircles);

    window.addEventListener("resize", drawAcceptingStateInnerCircles);

    drawAcceptingStateInnerCircles();
    refreshSelectionInfo();

    window.automatonCy = cy;
});