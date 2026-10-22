"""
Unit Tests for OptiFlow Vision Studio Milestone 6:
- Finite State Machine (FSM) event-driven sequencing
- Macro Definition and Nested Subgraph Execution
- Milestone 6 Filter Registration & Execution (StateMachineStep, DelayMs, MacroInput, MacroOutput)
"""

import unittest
from core.macro_and_statemachine import FiniteStateMachine, MacroDefinition
from core.project import ProgramGraph, FilterNode, FilterConnection
from core.pipeline_executor import PipelineExecutor, ExecutionContext
from core.filters import filter_registry


class TestFiniteStateMachine(unittest.TestCase):
    def test_state_transitions(self):
        fsm = FiniteStateMachine(initial_state="IDLE")
        fsm.add_transition("IDLE", "START_TRIGGER", "RUNNING")
        fsm.add_transition("RUNNING", "PASS_EVAL", "PASS")
        fsm.add_transition("PASS", "RESET", "IDLE")

        self.assertEqual(fsm.current_state, "IDLE")
        
        # Trigger valid transition
        fsm.trigger_event("START_TRIGGER")
        self.assertEqual(fsm.current_state, "RUNNING")

        # Unknown event should not change state
        fsm.trigger_event("UNKNOWN_NOISE")
        self.assertEqual(fsm.current_state, "RUNNING")

        # Advance to PASS
        fsm.trigger_event("PASS_EVAL")
        self.assertEqual(fsm.current_state, "PASS")

        # Reset back to IDLE
        fsm.reset()
        self.assertEqual(fsm.current_state, "IDLE")


class TestMacroExecution(unittest.TestCase):
    def test_nested_macro_subgraph(self):
        # Build a small macro subgraph: MacroInput -> MathMultiply (times 2) -> MacroOutput
        subgraph = ProgramGraph(
            id="macro_scale",
            name="Scale By 2",
            type="MACRO",
            nodes=[
                FilterNode(
                    id="m_in",
                    label="Macro In",
                    filter_type="MacroInput",
                    inputs={"data": 0.0}
                ),
                FilterNode(
                    id="m_mul",
                    label="Multiply x2",
                    filter_type="MathMultiply",
                    inputs={"a": 0.0, "b": 2.0}
                ),
                FilterNode(
                    id="m_out",
                    label="Macro Out",
                    filter_type="MacroOutput",
                    inputs={"result": 0.0}
                )
            ],
            connections=[
                FilterConnection(
                    id="c1",
                    source_node_id="m_in",
                    source_port="data",
                    target_node_id="m_mul",
                    target_port="a"
                ),
                FilterConnection(
                    id="c2",
                    source_node_id="m_mul",
                    source_port="result",
                    target_node_id="m_out",
                    target_port="result"
                )
            ]
        )

        macro = MacroDefinition(macro_id="macro_scale", name="Scale By 2", subgraph=subgraph)
        executor = PipelineExecutor()

        res = macro.execute({"data": 25.0}, executor)
        self.assertTrue(res["success"])
        self.assertEqual(res["outputs"]["result"], 50.0)


class TestM6Filters(unittest.TestCase):
    def setUp(self):
        self.ctx = ExecutionContext()

    def test_m6_filters_registered(self):
        m6_types = ["StateMachineStep", "DelayMs", "MacroInput", "MacroOutput"]
        for ft in m6_types:
            f = filter_registry.get(ft)
            self.assertIsNotNone(f, f"Filter {ft} must be registered")

    def test_filter_delay_ms(self):
        f_delay = filter_registry.get("DelayMs")
        res = f_delay.executor({"duration_ms": 5.0}, self.ctx)
        self.assertTrue(res["done"])
        self.assertGreaterEqual(res["elapsed_ms"], 4.0)

    def test_filter_statemachine_step(self):
        f_fsm = filter_registry.get("StateMachineStep")
        # Reset to IDLE
        res1 = f_fsm.executor({"reset": True}, self.ctx)
        self.assertEqual(res1["current_state"], "IDLE")
        self.assertTrue(res1["is_idle"])

        # Trigger event -> ACQUIRING
        res2 = f_fsm.executor({"event": "TRIGGER"}, self.ctx)
        self.assertEqual(res2["current_state"], "ACQUIRING")
        self.assertFalse(res2["is_idle"])


if __name__ == "__main__":
    unittest.main()
