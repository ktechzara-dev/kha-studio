"""
OptiFlow Vision Studio - Macros, Worker Subgraphs & State Machine Engine
Milestone 6 Implementation:
- Macro Definition and Composite Subgraph Execution
- Finite State Machine (FSM) for Industrial Sequence Control
- Micro-delays for Robot & PLC Handshake Synchronization
"""

from typing import List, Dict, Any, Optional
from core.project import ProgramGraph, FilterNode, FilterConnection


class FiniteStateMachine:
    """
    Industrial Finite State Machine for sequencing inspection cycles,
    robot triggers, part ejection, and error handling.
    """
    def __init__(self, initial_state: str = "IDLE", states: Optional[List[str]] = None):
        self.states = states or ["IDLE", "TRIGGERED", "ACQUIRING", "INSPECTING", "PASS", "FAIL", "FAULT"]
        self.current_state = initial_state if initial_state in self.states else self.states[0]
        # (current_state, event) -> next_state
        self._transitions: Dict[str, Dict[str, str]] = {}
        self._history: List[str] = [self.current_state]

    def add_transition(self, from_state: str, event: str, to_state: str) -> None:
        if from_state not in self._transitions:
            self._transitions[from_state] = {}
        self._transitions[from_state][event] = to_state

    def trigger_event(self, event: str) -> str:
        """Processes event trigger and advances state if valid transition exists."""
        current_map = self._transitions.get(self.current_state, {})
        if event in current_map:
            self.current_state = current_map[event]
            self._history.append(self.current_state)
            if len(self._history) > 50:
                self._history.pop(0)
        return self.current_state

    def reset(self, state: str = "IDLE") -> None:
        self.current_state = state
        self._history = [self.current_state]

    def get_status(self) -> Dict[str, Any]:
        return {
            "current_state": self.current_state,
            "states": self.states,
            "history": self._history[-10:]
        }


class MacroDefinition:
    """
    Encapsulates a reusable subgraph as a composite block.
    Maps macro input ports to internal nodes and collects outputs from MacroOutput node.
    """
    def __init__(
        self,
        macro_id: str,
        name: str,
        subgraph: ProgramGraph,
        description: str = ""
    ):
        self.macro_id = macro_id
        self.name = name
        self.subgraph = subgraph
        self.description = description

    def execute(self, inputs: Dict[str, Any], executor, image_service=None) -> Dict[str, Any]:
        """
        Executes internal subgraph using provided executor.
        Injects inputs into nodes that require macro inputs and gathers macro outputs.
        """
        # Inject inputs into nodes
        for node in self.subgraph.nodes:
            if node.filter_type == "MacroInput":
                for k, v in inputs.items():
                    node.inputs[k] = v

        sub_res = executor.run_graph(self.subgraph, use_cache=False)
        outputs = {}

        # Collect outputs from MacroOutput node if present
        for node in self.subgraph.nodes:
            if node.filter_type == "MacroOutput":
                node_res = sub_res.get("node_results", {}).get(node.id, {})
                outputs.update(node_res.get("outputs", {}))

        return {
            "success": sub_res.get("success", False),
            "outputs": outputs,
            "sub_results": sub_res.get("node_results", {})
        }
