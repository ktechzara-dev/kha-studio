"""
OptiFlow Vision Studio - Pipeline Graph Executor (Milestone 3)
Deterministic DAG topological execution engine with:
- Intelligent branch output caching
- Step mode execution
- Conditional execution & branch skipping
- Nil-safe fallback handling
- Stateful register persistence across continuous loops
- Multi-run statistical profiling & yield calculation
"""

import time
import math
import hashlib
from typing import Dict, Any, List, Optional, Tuple
from collections import deque

from core.project import ProgramGraph, FilterNode, FilterConnection
from core.filters import filter_registry
from core.types import OverlayItem


class RegisterStore:
    """
    Stateful memory registers preserved across continuous inspection loop iterations.
    """
    def __init__(self):
        self._registers: Dict[str, Any] = {}

    def get(self, name: str, default: Any = None) -> Any:
        return self._registers.get(name, default)

    def set(self, name: str, value: Any) -> None:
        self._registers[name] = value

    def reset(self) -> None:
        self._registers.clear()

    def get_all(self) -> Dict[str, Any]:
        return dict(self._registers)


# Global persistent registers
global_registers = RegisterStore()


class ExecutionContext:
    def __init__(self, image_service=None, registers: Optional[RegisterStore] = None):
        self.image_service = image_service
        self.registers = registers or global_registers
        self.overlays: List[OverlayItem] = []
        self.node_outputs: Dict[str, Dict[str, Any]] = {}
        self.node_timing_ms: Dict[str, float] = {}
        self.node_status: Dict[str, str] = {}  # SUCCESS, ERROR, SKIPPED
        self.active_image_id: Optional[str] = None


class RunStatisticsCollector:
    """
    Tracks execution history, timing distributions, pass/fail counts, and yield rate.
    """
    def __init__(self, max_history: int = 100):
        self.max_history = max_history
        self.total_runs: int = 0
        self.pass_runs: int = 0
        self.fail_runs: int = 0
        self.run_times_ms: List[float] = []

    def record_run(self, total_time_ms: float, is_success: bool) -> None:
        self.total_runs += 1
        if is_success:
            self.pass_runs += 1
        else:
            self.fail_runs += 1

        self.run_times_ms.append(round(total_time_ms, 2))
        if len(self.run_times_ms) > self.max_history:
            self.run_times_ms.pop(0)

    def get_summary(self) -> Dict[str, Any]:
        count = len(self.run_times_ms)
        if count == 0:
            return {
                "total_runs": 0,
                "pass_runs": 0,
                "fail_runs": 0,
                "yield_percent": 100.0,
                "min_time_ms": 0.0,
                "max_time_ms": 0.0,
                "mean_time_ms": 0.0,
                "history": []
            }

        mean_val = sum(self.run_times_ms) / count
        min_val = min(self.run_times_ms)
        max_val = max(self.run_times_ms)
        yield_pct = (self.pass_runs / max(1, self.total_runs)) * 100.0

        return {
            "total_runs": self.total_runs,
            "pass_runs": self.pass_runs,
            "fail_runs": self.fail_runs,
            "yield_percent": round(yield_pct, 1),
            "min_time_ms": round(min_val, 2),
            "max_time_ms": round(max_val, 2),
            "mean_time_ms": round(mean_val, 2),
            "recent_times_ms": self.run_times_ms[-20:]
        }

    def reset(self) -> None:
        self.total_runs = 0
        self.pass_runs = 0
        self.fail_runs = 0
        self.run_times_ms.clear()


class PipelineExecutor:
    def __init__(self, image_service=None):
        self.image_service = image_service
        self.stats = RunStatisticsCollector()
        self._output_cache: Dict[str, Tuple[str, Dict[str, Any]]] = {}  # node_id -> (input_hash, outputs)

        # Step mode state
        self._step_context: Optional[ExecutionContext] = None
        self._step_ordered_nodes: List[FilterNode] = []
        self._step_index: int = 0
        self._step_incoming_conns: Dict[str, List[FilterConnection]] = {}
        self._step_node_results: Dict[str, Any] = {}

    def clear_cache(self) -> None:
        self._output_cache.clear()

    def _compute_input_hash(self, filter_type: str, inputs: Dict[str, Any]) -> str:
        """
        Generates deterministic hash of resolved inputs for node caching.
        Skips large numpy arrays by hashing their shape, dtype, and corner pixels.
        """
        hasher = hashlib.md5()
        hasher.update(filter_type.encode('utf-8'))
        for k in sorted(inputs.keys()):
            v = inputs[k]
            hasher.update(k.encode('utf-8'))
            if hasattr(v, "shape"):  # np.ndarray
                h, w = v.shape[:2]
                hasher.update(f"img_{w}_{h}_{v.dtype}_{v[0,0] if v.size > 0 else 0}".encode('utf-8'))
            else:
                hasher.update(str(v).encode('utf-8'))
        return hasher.hexdigest()

    def topological_sort(self, graph: ProgramGraph) -> List[FilterNode]:
        """
        Kahn's algorithm for deterministic topological DAG ordering.
        Detects cycles and guarantees dependent nodes execute after upstream parents.
        """
        node_map = {n.id: n for n in graph.nodes}
        in_degree = {n.id: 0 for n in graph.nodes}
        adj_list = {n.id: [] for n in graph.nodes}

        for conn in graph.connections:
            if conn.source_node_id in adj_list and conn.target_node_id in in_degree:
                adj_list[conn.source_node_id].append(conn.target_node_id)
                in_degree[conn.target_node_id] += 1

        queue = deque([nid for nid, deg in in_degree.items() if deg == 0])
        ordered_nodes = []

        while queue:
            curr_id = queue.popleft()
            if curr_id in node_map:
                ordered_nodes.append(node_map[curr_id])

            for neighbor_id in adj_list.get(curr_id, []):
                in_degree[neighbor_id] -= 1
                if in_degree[neighbor_id] == 0:
                    queue.append(neighbor_id)

        # Append unconnected or cyclic nodes gracefully
        if len(ordered_nodes) < len(graph.nodes):
            for n in graph.nodes:
                if n not in ordered_nodes:
                    ordered_nodes.append(n)

        return ordered_nodes

    def run_graph(self, graph: ProgramGraph, use_cache: bool = True) -> Dict[str, Any]:
        """
        Executes all nodes in topological dependency order with caching,
        conditional branch skipping, Nil safety, and error isolation.
        """
        t_start = time.perf_counter()
        ctx = ExecutionContext(image_service=self.image_service)
        ordered_nodes = self.topological_sort(graph)

        node_results: Dict[str, Any] = {}
        overall_success = True

        incoming_conns: Dict[str, List[FilterConnection]] = {n.id: [] for n in graph.nodes}
        for conn in graph.connections:
            if conn.target_node_id in incoming_conns:
                incoming_conns[conn.target_node_id].append(conn)

        for node in ordered_nodes:
            # 1. Disabled Nodes Check
            if node.is_disabled:
                ctx.node_status[node.id] = "SKIPPED"
                node_results[node.id] = {
                    "status": "SKIPPED",
                    "time_ms": 0.0,
                    "from_cache": False,
                    "outputs": {},
                    "error": None
                }
                continue

            # 2. Check for Skipped Upstream Dependencies
            has_skipped_parent = False
            for conn in incoming_conns.get(node.id, []):
                if ctx.node_status.get(conn.source_node_id) == "SKIPPED":
                    has_skipped_parent = True
                    break

            if has_skipped_parent:
                ctx.node_status[node.id] = "SKIPPED"
                node_results[node.id] = {
                    "status": "SKIPPED",
                    "time_ms": 0.0,
                    "from_cache": False,
                    "outputs": {},
                    "error": "Upstream dependency was skipped"
                }
                continue

            filter_def = filter_registry.get(node.filter_type)
            if not filter_def:
                ctx.node_status[node.id] = "ERROR"
                node_results[node.id] = {
                    "status": "ERROR",
                    "time_ms": 0.0,
                    "from_cache": False,
                    "outputs": {},
                    "error": f"Unknown filter type: {node.filter_type}"
                }
                overall_success = False
                continue

            # 3. Gather & Resolve Inputs
            resolved_inputs: Dict[str, Any] = {}
            for port_name, port_def in filter_def.inputs.items():
                if port_name in node.inputs:
                    resolved_inputs[port_name] = node.inputs[port_name]
                else:
                    resolved_inputs[port_name] = port_def.default_value

            for conn in incoming_conns.get(node.id, []):
                src_outputs = ctx.node_outputs.get(conn.source_node_id, {})
                if conn.source_port in src_outputs:
                    resolved_inputs[conn.target_port] = src_outputs[conn.source_port]

            # 4. Conditional Enable Check (if filter has a 'condition' input that evaluated to False)
            if "condition" in resolved_inputs and resolved_inputs["condition"] is False:
                ctx.node_status[node.id] = "SKIPPED"
                node_results[node.id] = {
                    "status": "SKIPPED",
                    "time_ms": 0.0,
                    "from_cache": False,
                    "outputs": {},
                    "error": "Condition evaluated to False"
                }
                continue

            # 5. Output Cache Invalidation / Hit Check
            input_hash = self._compute_input_hash(node.filter_type, resolved_inputs)
            if use_cache and node.id in self._output_cache:
                cached_hash, cached_outputs = self._output_cache[node.id]
                if cached_hash == input_hash:
                    ctx.node_outputs[node.id] = cached_outputs
                    ctx.node_timing_ms[node.id] = 0.0
                    ctx.node_status[node.id] = "SUCCESS"
                    node_results[node.id] = {
                        "status": "SUCCESS",
                        "time_ms": 0.0,
                        "from_cache": True,
                        "outputs": self._sanitize_outputs_for_json(cached_outputs),
                        "error": None
                    }
                    continue

            # 6. High-Precision Filter Execution
            t_node_0 = time.perf_counter()
            try:
                outputs = filter_def.executor(resolved_inputs, ctx)
                t_node_ms = (time.perf_counter() - t_node_0) * 1000.0

                ctx.node_outputs[node.id] = outputs
                ctx.node_timing_ms[node.id] = t_node_ms
                ctx.node_status[node.id] = "SUCCESS"
                self._output_cache[node.id] = (input_hash, outputs)

                node_results[node.id] = {
                    "status": "SUCCESS",
                    "time_ms": round(t_node_ms, 2),
                    "from_cache": False,
                    "outputs": self._sanitize_outputs_for_json(outputs),
                    "error": None
                }
            except Exception as ex:
                t_node_ms = (time.perf_counter() - t_node_0) * 1000.0
                ctx.node_status[node.id] = "ERROR"
                node_results[node.id] = {
                    "status": "ERROR",
                    "time_ms": round(t_node_ms, 2),
                    "from_cache": False,
                    "outputs": {},
                    "error": str(ex)
                }
                overall_success = False

        total_time_ms = (time.perf_counter() - t_start) * 1000.0
        self.stats.record_run(total_time_ms, overall_success)

        return {
            "success": overall_success,
            "total_time_ms": round(total_time_ms, 2),
            "node_count": len(graph.nodes),
            "node_results": node_results,
            "overlays": [o.model_dump() for o in ctx.overlays],
            "stats": self.stats.get_summary(),
            "registers": global_registers.get_all()
        }

    # =========================================================================
    # STEP EXECUTION MODE (F10)
    # =========================================================================

    def reset_step_execution(self, graph: ProgramGraph) -> None:
        """Initializes or resets step execution to node index 0."""
        self._step_context = ExecutionContext(image_service=self.image_service)
        self._step_ordered_nodes = self.topological_sort(graph)
        self._step_index = 0
        self._step_node_results = {}

        self._step_incoming_conns = {n.id: [] for n in graph.nodes}
        for conn in graph.connections:
            if conn.target_node_id in self._step_incoming_conns:
                self._step_incoming_conns[conn.target_node_id].append(conn)

    def execute_next_step(self, graph: ProgramGraph) -> Dict[str, Any]:
        """
        Executes exactly one node at the current step pointer.
        Returns the single node's result and overall progress.
        """
        if self._step_context is None or len(self._step_ordered_nodes) != len(graph.nodes):
            self.reset_step_execution(graph)

        if self._step_index >= len(self._step_ordered_nodes):
            return {
                "completed": True,
                "current_node_id": None,
                "step_index": self._step_index,
                "total_steps": len(self._step_ordered_nodes),
                "node_results": self._step_node_results,
                "overlays": [o.model_dump() for o in self._step_context.overlays],
                "registers": global_registers.get_all()
            }

        node = self._step_ordered_nodes[self._step_index]
        filter_def = filter_registry.get(node.filter_type)

        if node.is_disabled or not filter_def:
            res = {
                "status": "SKIPPED" if node.is_disabled else "ERROR",
                "time_ms": 0.0,
                "outputs": {},
                "error": None if node.is_disabled else f"Unknown filter: {node.filter_type}"
            }
        else:
            resolved_inputs: Dict[str, Any] = {}
            for port_name, port_def in filter_def.inputs.items():
                if port_name in node.inputs:
                    resolved_inputs[port_name] = node.inputs[port_name]
                else:
                    resolved_inputs[port_name] = port_def.default_value

            for conn in self._step_incoming_conns.get(node.id, []):
                src_outputs = self._step_context.node_outputs.get(conn.source_node_id, {})
                if conn.source_port in src_outputs:
                    resolved_inputs[conn.target_port] = src_outputs[conn.source_port]

            t0 = time.perf_counter()
            try:
                outputs = filter_def.executor(resolved_inputs, self._step_context)
                t_ms = (time.perf_counter() - t0) * 1000.0
                self._step_context.node_outputs[node.id] = outputs
                res = {
                    "status": "SUCCESS",
                    "time_ms": round(t_ms, 2),
                    "outputs": self._sanitize_outputs_for_json(outputs),
                    "error": None
                }
            except Exception as ex:
                t_ms = (time.perf_counter() - t0) * 1000.0
                res = {
                    "status": "ERROR",
                    "time_ms": round(t_ms, 2),
                    "outputs": {},
                    "error": str(ex)
                }

        self._step_node_results[node.id] = res
        curr_id = node.id
        self._step_index += 1
        is_done = self._step_index >= len(self._step_ordered_nodes)

        return {
            "completed": is_done,
            "current_node_id": curr_id,
            "step_index": self._step_index,
            "total_steps": len(self._step_ordered_nodes),
            "current_result": res,
            "node_results": self._step_node_results,
            "overlays": [o.model_dump() for o in self._step_context.overlays],
            "registers": global_registers.get_all()
        }

    def _sanitize_outputs_for_json(self, outputs: Dict[str, Any]) -> Dict[str, Any]:
        json_outputs = {}
        for k, v in outputs.items():
            if hasattr(v, "shape"):  # np.ndarray
                h, w = v.shape[:2]
                json_outputs[k] = f"<Image: {w}x{h}>"
            elif hasattr(v, "model_dump"):
                json_outputs[k] = v.model_dump()
            elif v is None:
                json_outputs[k] = None
            else:
                json_outputs[k] = v
        return json_outputs
