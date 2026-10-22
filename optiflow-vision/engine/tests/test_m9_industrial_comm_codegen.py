"""
Unit Tests for OptiFlow Vision Studio Milestone 9:
- Modbus TCP Master Communication Simulator
- Raw TCP Socket Industrial Packet Dispatcher
- Standalone Python Code Generator from Inspection DAG
- Export Code REST API endpoint
"""

import unittest
import numpy as np
from fastapi.testclient import TestClient

from app import app
from core.industrial_comm_and_codegen import (
    ModbusTCPSimulator,
    send_raw_tcp_packet,
    PipelineCodeGenerator,
)
from core.project import ProgramGraph, FilterNode, FilterConnection
from core.filters import filter_registry
from core.pipeline_executor import ExecutionContext


class TestModbusTCP(unittest.TestCase):
    def test_modbus_register_write(self):
        modbus = ModbusTCPSimulator()
        res = modbus.write_holding_register(105, 42)
        self.assertTrue(res["success"])
        self.assertEqual(res["register_address"], 105)
        self.assertEqual(res["value"], 42)
        self.assertEqual(res["byte_length"], 12)
        self.assertIn("hex_packet", res)
        self.assertEqual(modbus.holding_registers[105], 42)

    def test_robot_pose_registers(self):
        modbus = ModbusTCPSimulator()
        # Robot X=420.5mm, Y=185.3mm, Angle=15.0 deg
        res = modbus.write_robot_pose_registers(200, 420.5, 185.3, 15.0)
        self.assertEqual(res["start_address"], 200)
        self.assertEqual(modbus.holding_registers[200], 4205)
        self.assertEqual(modbus.holding_registers[201], 1853)
        self.assertEqual(modbus.holding_registers[202], 150)


class TestTCPSocket(unittest.TestCase):
    def test_tcp_packet_generation(self):
        # Even if remote port is closed, function should gracefully return bytes_sent without crashing
        res = send_raw_tcp_packet("127.0.0.1", 9999, "Vision101A05LB800\r", timeout_sec=0.2)
        self.assertGreater(res["bytes_sent"], 0)
        self.assertEqual(res["payload"], "Vision101A05LB800\r")


class TestPipelineCodeGenerator(unittest.TestCase):
    def test_generate_and_execute_standalone_python_script(self):
        graph = ProgramGraph(
            id="prog_codegen",
            name="Codegen Inspection Test",
            type="MAIN_LOOP",
            nodes=[
                FilterNode(
                    id="node_gray",
                    label="Chuyển Xám",
                    filter_type="ConvertToGrayscale"
                ),
                FilterNode(
                    id="node_otsu",
                    label="Phân Ngưỡng Otsu",
                    filter_type="ThresholdOtsu"
                ),
                FilterNode(
                    id="node_blobs",
                    label="Trích Xuất Đốm",
                    filter_type="ExtractBlobs"
                ),
                FilterNode(
                    id="node_decision",
                    label="Quyết Định Đạt/Lỗi",
                    filter_type="PassFailDecision"
                )
            ]
        )

        code_str = PipelineCodeGenerator.generate_python_script(graph)
        self.assertIn("def run_inspection(image_bgr: np.ndarray) -> dict:", code_str)
        self.assertIn("cv2.cvtColor", code_str)
        self.assertIn("cv2.threshold", code_str)

        # Test execution of generated Python code in sandbox namespace
        namespace = {}
        exec(code_str, namespace)
        self.assertIn("run_inspection", namespace)

        # Execute generated function on a dummy image frame
        sample_frame = np.ones((100, 100, 3), dtype=np.uint8) * 128
        run_fn = namespace["run_inspection"]
        result = run_fn(sample_frame)

        self.assertIn("node_gray", result)
        self.assertIn("node_otsu", result)
        self.assertIn("verdict", result)
        self.assertIn("execution_time_ms", result)


class TestM9APIAndFilters(unittest.TestCase):
    def setUp(self):
        self.client = TestClient(app)
        self.ctx = ExecutionContext()

    def test_export_code_api(self):
        resp = self.client.post("/api/pipeline/export-code", json={})
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertTrue(data["success"])
        self.assertEqual(data["format"], "python")
        self.assertIn("def run_inspection", data["code"])

    def test_m9_filter_registration(self):
        m9_types = ["SendModbusRegister", "SendTCPSocket"]
        for ft in m9_types:
            f = filter_registry.get(ft)
            self.assertIsNotNone(f, f"Filter {ft} must be registered")

    def test_filter_send_modbus(self):
        f = filter_registry.get("SendModbusRegister")
        res = f.executor({"register_address": 300, "value": 99}, self.ctx)
        self.assertTrue(res["success"])
        self.assertEqual(res["register_address"], 300)
        self.assertEqual(res["value"], 99)


if __name__ == "__main__":
    unittest.main()
