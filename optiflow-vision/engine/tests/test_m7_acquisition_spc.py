"""
Unit Tests for OptiFlow Vision Studio Milestone 7:
- Industrial Camera Adapter (Synthetic simulator & frame grabber)
- Statistical Process Control (SPC): Cp, Cpk, Mean, StdDev, Western Electric Alarms
- Milestone 7 Filter Registrations and Execution
"""

import unittest
import numpy as np
from core.acquisition_and_spc import SyntheticCameraAdapter, SPCStatisticsEngine
from core.filters import filter_registry
from core.pipeline_executor import ExecutionContext


class TestSyntheticCamera(unittest.TestCase):
    def test_camera_lifecycle_and_grabbing(self):
        cam = SyntheticCameraAdapter(width=640, height=480)
        self.assertFalse(cam.is_connected())

        # Connect
        self.assertTrue(cam.connect())
        self.assertTrue(cam.is_connected())

        # Parameters
        self.assertTrue(cam.set_exposure_us(8000.0))
        self.assertEqual(cam.exposure_us, 8000.0)
        self.assertTrue(cam.set_gain_db(3.0))
        self.assertEqual(cam.gain_db, 3.0)

        # Grab frames
        f1 = cam.grab_frame()
        self.assertIsNotNone(f1)
        self.assertEqual(f1.shape, (480, 640, 3))
        self.assertEqual(cam.frame_id, 1)

        f2 = cam.grab_frame()
        self.assertIsNotNone(f2)
        self.assertEqual(cam.frame_id, 2)

        # Disconnect
        cam.disconnect()
        self.assertFalse(cam.is_connected())
        self.assertIsNone(cam.grab_frame())


class TestSPCEngine(unittest.TestCase):
    def test_cp_cpk_calculation(self):
        # Nominal: 50.0mm, Tolerance: 47.0 .. 53.0mm (Total spec = 6.0mm)
        spc = SPCStatisticsEngine(nominal=50.0, lsl=47.0, usl=53.0)

        # Feed 40 normal parts tightly centered at 50.0 with sigma = 0.5
        np.random.seed(42)
        samples = np.random.normal(50.0, 0.5, 40)
        for s in samples:
            spc.add_sample(s)

        stats = spc.calculate_metrics()
        self.assertEqual(stats["count"], 40)
        self.assertAlmostEqual(stats["mean"], 50.0, delta=0.2)
        self.assertAlmostEqual(stats["std_dev"], 0.5, delta=0.15)
        # Cp = 6.0 / (6 * 0.5) = 2.0 (Capable process)
        self.assertGreater(stats["cp"], 1.5)
        self.assertGreater(stats["cpk"], 1.5)
        self.assertTrue(stats["in_control"])
        self.assertEqual(len(stats["alarms"]), 0)

    def test_out_of_control_spec_breach(self):
        spc = SPCStatisticsEngine(nominal=50.0, lsl=47.0, usl=53.0)
        for val in [50.1, 49.9, 50.0, 50.2, 49.8]:
            spc.add_sample(val)

        # Add defective part outside spec
        spc.add_sample(54.5)
        stats = spc.calculate_metrics()

        self.assertFalse(stats["in_control"])
        self.assertGreaterEqual(len(stats["alarms"]), 1)
        self.assertTrue(any("SPEC_BREACH" in a for a in stats["alarms"]))


class TestM7Filters(unittest.TestCase):
    def setUp(self):
        self.ctx = ExecutionContext()

    def test_m7_filters_registered(self):
        m7_types = ["AcquireCameraFrame", "ComputeSPC"]
        for ft in m7_types:
            f = filter_registry.get(ft)
            self.assertIsNotNone(f, f"Filter {ft} must be registered")

    def test_filter_acquire_camera_frame(self):
        f_cam = filter_registry.get("AcquireCameraFrame")
        res = f_cam.executor({"exposure_us": 6000.0, "gain_db": 0.0}, self.ctx)
        self.assertIsNotNone(res["image"])
        self.assertGreater(res["width"], 0)
        self.assertGreater(res["height"], 0)
        self.assertGreater(res["frame_id"], 0)

    def test_filter_compute_spc(self):
        f_spc = filter_registry.get("ComputeSPC")
        # Reset engine first
        f_spc.executor({"reset": True}, self.ctx)

        # Feed 5 valid samples
        for v in [100.1, 99.9, 100.0, 100.2, 99.8]:
            res = f_spc.executor({"value": v, "nominal": 100.0, "lsl": 95.0, "usl": 105.0}, self.ctx)

        self.assertEqual(res["sample_count"], 5)
        self.assertAlmostEqual(res["mean"], 100.0, delta=0.2)
        self.assertTrue(res["in_control"])
        self.assertEqual(res["latest_alarm"], "NORMAL")


if __name__ == "__main__":
    unittest.main()
