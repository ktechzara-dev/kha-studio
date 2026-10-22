"""
Tests for OptiFlow Vision Studio Milestone 3:
- Safe AST Formula Evaluator (arithmetic, functions, security isolation)
- Stateful Registers across pipeline runs (RegisterStore, RegisterRead, RegisterWrite)
- Branch / Node Output Hashing & Caching
- Step Mode Execution (execute_next_step & reset_step)
- Conditional Branching & Nil Sentinel Coalescing
- Pipeline Yield & Latency Statistics Collector
"""

import unittest
from fastapi.testclient import TestClient

from app import app
from core.formula_evaluator import safe_evaluate_formula, SafeFormulaSecurityError
from core.pipeline_executor import (
    RegisterStore,
    RunStatisticsCollector,
    PipelineExecutor,
    ExecutionContext
)
from core.filters import filter_registry
from core.project import ProgramGraph, FilterNode, FilterConnection


class TestSafeFormulaEvaluator(unittest.TestCase):
    def test_basic_arithmetic(self):
        res = safe_evaluate_formula("2 * (3 + 4) - 5 / 2")
        self.assertEqual(res, 11.5)

    def test_variable_context(self):
        context = {"x": 3.0, "y": 4.0, "scale": 10.0}
        res = safe_evaluate_formula("hypot(x, y) * scale", context)
        self.assertEqual(res, 50.0)

        # Distance formula
        res2 = safe_evaluate_formula("sqrt((x2 - x1)**2 + (y2 - y1)**2)", {"x1": 0, "y1": 0, "x2": 6, "y2": 8})
        self.assertEqual(res2, 10.0)

    def test_logical_and_conditional_expressions(self):
        res = safe_evaluate_formula("1 if (width > 100 and area >= 500) else 0", {"width": 120, "area": 600})
        self.assertEqual(res, 1)

        res2 = safe_evaluate_formula("1 if (width > 100 and area >= 500) else 0", {"width": 80, "area": 600})
        self.assertEqual(res2, 0)

    def test_math_functions(self):
        self.assertEqual(safe_evaluate_formula("abs(-42)"), 42)
        self.assertEqual(safe_evaluate_formula("round(3.1415926, 2)"), 3.14)
        self.assertEqual(safe_evaluate_formula("min(10, 20, 5)"), 5)
        self.assertEqual(safe_evaluate_formula("max(10, 20, 5)"), 20)

    def test_security_isolation_blocks_dangerous_code(self):
        dangerous_expressions = [
            "__import__('os').system('dir')",
            "open('passwords.txt', 'w')",
            "eval('1+1')",
            "exec('a = 1')",
            "[x for x in ().__class__.__bases__[0].__subclasses__()]",
        ]
        for expr in dangerous_expressions:
            with self.assertRaises(SafeFormulaSecurityError, msg=f"Should block: {expr}"):
                safe_evaluate_formula(expr)


class TestStatefulRegisters(unittest.TestCase):
    def test_register_persistence_and_reset(self):
        store = RegisterStore()
        self.assertIsNone(store.get("count"))
        self.assertEqual(store.get("count", default=0), 0)

        store.set("count", 1)
        self.assertEqual(store.get("count"), 1)

        store.set("part_id", "SN-98214")
        self.assertEqual(store.get("part_id"), "SN-98214")

        all_regs = store.get_all()
        self.assertIn("count", all_regs)
        self.assertIn("part_id", all_regs)

        store.reset()
        self.assertEqual(len(store.get_all()), 0)
        self.assertIsNone(store.get("count"))


class TestRunStatistics(unittest.TestCase):
    def test_yield_and_timing_tracking(self):
        stats = RunStatisticsCollector(max_history=50)
        
        # 3 passed runs, 1 failed run
        stats.record_run(total_time_ms=12.5, is_success=True)
        stats.record_run(total_time_ms=15.0, is_success=True)
        stats.record_run(total_time_ms=14.5, is_success=False)
        stats.record_run(total_time_ms=10.0, is_success=True)

        data = stats.get_summary()
        self.assertEqual(data["total_runs"], 4)
        self.assertEqual(data["pass_runs"], 3)
        self.assertEqual(data["fail_runs"], 1)
        self.assertEqual(data["yield_percent"], 75.0)
        self.assertEqual(data["min_time_ms"], 10.0)
        self.assertEqual(data["max_time_ms"], 15.0)
        self.assertAlmostEqual(data["mean_time_ms"], 13.0, places=1)


class TestM3FiltersAndPipeline(unittest.TestCase):
    def setUp(self):
        self.client = TestClient(app)

    def test_m3_filters_registration(self):
        m3_filters = [
            "EvaluateFormula",
            "RegisterRead",
            "RegisterWrite",
            "ConditionalBranch",
            "CoalesceNil"
        ]
        for ftype in m3_filters:
            f = filter_registry.get(ftype)
            self.assertIsNotNone(f, f"Filter {ftype} must be registered")

    def test_conditional_branch_filter(self):
        cond_filter = filter_registry.get("ConditionalBranch")
        ctx = ExecutionContext()

        # True path
        res_t = cond_filter.executor({
            "condition": True,
            "if_true": 100,
            "if_false": 200
        }, ctx)
        self.assertEqual(res_t["result"], 100)
        self.assertEqual(res_t["branch_taken"], "TRUE")

        # False path
        res_f = cond_filter.executor({
            "condition": False,
            "if_true": 100,
            "if_false": 200
        }, ctx)
        self.assertEqual(res_f["result"], 200)
        self.assertEqual(res_f["branch_taken"], "FALSE")

    def test_coalesce_nil_filter(self):
        coal_filter = filter_registry.get("CoalesceNil")
        ctx = ExecutionContext()

        # With None
        res_none = coal_filter.executor({"value": None, "fallback": 42}, ctx)
        self.assertEqual(res_none["result"], 42)
        self.assertTrue(res_none["was_nil"])

        # With valid value
        res_valid = coal_filter.executor({"value": 99, "fallback": 42}, ctx)
        self.assertEqual(res_valid["result"], 99)
        self.assertFalse(res_valid["was_nil"])

    def test_evaluate_formula_filter(self):
        f_eval = filter_registry.get("EvaluateFormula")
        ctx = ExecutionContext()
        res = f_eval.executor({
            "expression": "hypot(a, b)",
            "a": 3,
            "b": 4
        }, ctx)
        self.assertEqual(res["result"], 5.0)

    def test_step_mode_and_stats_api(self):
        # 1. Reset step mode
        resp_reset = self.client.post("/api/pipeline/reset-step")
        self.assertEqual(resp_reset.status_code, 200)

        # 2. Execute step
        resp_step1 = self.client.post("/api/pipeline/step")
        self.assertEqual(resp_step1.status_code, 200)
        data1 = resp_step1.json()
        self.assertIn("completed", data1)
        self.assertIn("current_node_id", data1)

        # 3. Check stats API
        resp_stats = self.client.get("/api/pipeline/stats")
        self.assertEqual(resp_stats.status_code, 200)
        stats = resp_stats.json()
        self.assertIn("total_runs", stats)
        self.assertIn("yield_percent", stats)

    def test_register_api(self):
        # Reset registers
        r_reset = self.client.post("/api/pipeline/registers/reset")
        self.assertEqual(r_reset.status_code, 200)

        # Get registers
        r_get = self.client.get("/api/pipeline/registers")
        self.assertEqual(r_get.status_code, 200)
        self.assertEqual(r_get.json()["registers"], {})

    def test_branch_caching_behavior(self):
        executor = PipelineExecutor()
        graph = ProgramGraph()
        node = FilterNode(
            id="eval_1",
            label="Formula 1",
            filter_type="EvaluateFormula",
            inputs={"formula": "10 * 10"}
        )
        graph.nodes.append(node)

        # First execution -> Computed
        res1 = executor.run_graph(graph, use_cache=True)
        self.assertTrue(res1["success"])
        node_res1 = res1["node_results"]["eval_1"]
        self.assertFalse(node_res1["from_cache"])
        self.assertEqual(node_res1["outputs"]["result"], 100)

        # Second execution with same inputs -> Cache hit
        res2 = executor.run_graph(graph, use_cache=True)
        self.assertTrue(res2["success"])
        node_res2 = res2["node_results"]["eval_1"]
        self.assertTrue(node_res2["from_cache"])
        self.assertEqual(node_res2["time_ms"], 0.0)
        self.assertEqual(node_res2["outputs"]["result"], 100)


if __name__ == "__main__":
    unittest.main()
