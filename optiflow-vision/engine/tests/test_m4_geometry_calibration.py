"""
Unit Tests for OptiFlow Vision Studio Milestone 4:
- 1D Sub-pixel Edge Detection along line profiles
- Caliper 1D Edge Pair thickness measurement
- Taubin Robust Circle Fitting & Huber Line Fitting
- 2D Point-to-Point, Point-to-Line and Line-Angle Metrology
- Camera Lens Undistortion & Planar Homography (Pixel <-> Real-World Millimeter)
"""

import math
import unittest
import numpy as np
import cv2

from core.geometry_and_calibration import (
    sample_1d_profile,
    detect_1d_edges_subpixel,
    fit_line_robust,
    fit_circle_taubin,
    measure_point_to_point,
    measure_point_to_line,
    measure_line_to_line_angle,
    LensDistortionModel,
    compute_homography_from_fiducials,
    transform_points_with_homography,
)
from core.filters import filter_registry
from core.pipeline_executor import ExecutionContext


class Test1DEdgeDetection(unittest.TestCase):
    def test_subpixel_step_edge(self):
        # Create a synthetic image with a sharp vertical transition at x = 50.4
        img = np.zeros((100, 100), dtype=np.uint8)
        img[:, :50] = 30
        img[:, 50:] = 220

        p1 = (10.0, 50.0)
        p2 = (90.0, 50.0)
        dists, intensities, coords = sample_1d_profile(img, p1, p2, corridor_width=5)
        edges = detect_1d_edges_subpixel(dists, intensities, coords, min_gradient=15.0, polarity="DARK_TO_BRIGHT")

        self.assertGreaterEqual(len(edges), 1)
        best_edge = edges[0]
        # Ray starts at x=10, step is at x=50, expected distance around 40px
        self.assertAlmostEqual(best_edge["x"], 50.0, delta=1.5)
        self.assertEqual(best_edge["polarity"], "DARK_TO_BRIGHT")

    def test_caliper_edge_pair(self):
        # Synthetic bar between x=30 and x=70 (width = 40px)
        img = np.zeros((100, 100), dtype=np.uint8)
        img[:, 30:70] = 230

        p1 = (10.0, 50.0)
        p2 = (90.0, 50.0)
        dists, intensities, coords = sample_1d_profile(img, p1, p2, corridor_width=5)
        edges = detect_1d_edges_subpixel(dists, intensities, coords, min_gradient=15.0, polarity="ANY")

        self.assertGreaterEqual(len(edges), 2)
        e1 = edges[0]
        e2 = edges[-1]
        measured_width = math.hypot(e2["x"] - e1["x"], e2["y"] - e1["y"])
        self.assertAlmostEqual(measured_width, 40.0, delta=1.5)


class TestRobustShapeFitting(unittest.TestCase):
    def test_taubin_circle_fitting(self):
        # Generate 36 points on circle R=50 at (100, 100) + Gaussian noise
        cx_true, cy_true, r_true = 100.0, 100.0, 50.0
        angles = np.linspace(0, 2 * math.pi, 36, endpoint=False)
        pts = []
        for a in angles:
            x = cx_true + r_true * math.cos(a) + np.random.normal(0, 0.2)
            y = cy_true + r_true * math.sin(a) + np.random.normal(0, 0.2)
            pts.append((x, y))

        res = fit_circle_taubin(pts)
        self.assertIsNotNone(res)
        self.assertAlmostEqual(res["cx"], cx_true, delta=0.5)
        self.assertAlmostEqual(res["cy"], cy_true, delta=0.5)
        self.assertAlmostEqual(res["radius"], r_true, delta=0.5)
        self.assertLess(res["roundness_error"], 1.0)

    def test_huber_line_fitting_with_outliers(self):
        # Line y = 2x + 10 from x=0..50
        pts = []
        for x in range(0, 50, 2):
            pts.append((float(x), float(2 * x + 10)))

        # Add 3 aggressive outlier points (burrs/dust)
        pts.append((10.0, 200.0))
        pts.append((25.0, -100.0))
        pts.append((40.0, 300.0))

        res = fit_line_robust(pts, max_reproj_error=3.0)
        self.assertIsNotNone(res)
        # Expected line angle: atan(2) ~= 63.43 degrees
        self.assertAlmostEqual(res["angle_deg"], 63.43, delta=2.0)
        self.assertGreaterEqual(res["inliers_count"], 24)


class Test2DMetrology(unittest.TestCase):
    def test_point_to_point(self):
        # 3-4-5 right triangle
        res = measure_point_to_point((0.0, 0.0), (3.0, 4.0))
        self.assertEqual(res["distance"], 5.0)
        self.assertEqual(res["delta_x"], 3.0)
        self.assertEqual(res["delta_y"], 4.0)

    def test_point_to_line(self):
        # Line along x-axis (x0=0, y0=0, vx=1, vy=0), test point at (10, 7)
        res = measure_point_to_line((10.0, 7.0), (0.0, 0.0), (1.0, 0.0))
        self.assertEqual(res["distance"], 7.0)
        self.assertEqual(res["foot_x"], 10.0)
        self.assertEqual(res["foot_y"], 0.0)

    def test_line_angle(self):
        # Perpendicular lines (1, 0) and (0, 1)
        res = measure_line_to_line_angle((1.0, 0.0), (0.0, 1.0))
        self.assertEqual(res["angle_deg"], 90.0)
        self.assertTrue(res["is_perpendicular"])
        self.assertFalse(res["is_parallel"])

        # Parallel lines
        res2 = measure_line_to_line_angle((2.0, 3.0), (4.0, 6.0))
        self.assertEqual(res2["angle_deg"], 0.0)
        self.assertTrue(res2["is_parallel"])


class TestCameraCalibrationAndHomography(unittest.TestCase):
    def test_planar_homography_projection(self):
        # 4 corners of an inspection conveyor target
        # Pixels -> Real-world millimeters
        pixel_pts = [(100.0, 100.0), (500.0, 100.0), (500.0, 400.0), (100.0, 400.0)]
        world_pts = [(0.0, 0.0), (200.0, 0.0), (200.0, 150.0), (0.0, 150.0)]

        H = compute_homography_from_fiducials(pixel_pts, world_pts)
        self.assertIsNotNone(H)

        # Center pixel (300, 250) should map to real-world center (100, 75) mm
        transformed = transform_points_with_homography([(300.0, 250.0)], H)
        self.assertEqual(len(transformed), 1)
        tx, ty = transformed[0]
        self.assertAlmostEqual(tx, 100.0, delta=0.5)
        self.assertAlmostEqual(ty, 75.0, delta=0.5)

    def test_undistort_model(self):
        img = np.ones((480, 640, 3), dtype=np.uint8) * 128
        K = np.array([[600.0, 0, 320.0], [0, 600.0, 240.0], [0, 0, 1.0]], dtype=np.float32)
        D = np.array([-0.1, 0.02, 0.0, 0.0, 0.0], dtype=np.float32)

        model = LensDistortionModel(K, D, (480, 640))
        rectified = model.undistort(img)
        self.assertEqual(rectified.shape, img.shape)


class TestM4FiltersExecution(unittest.TestCase):
    def setUp(self):
        self.ctx = ExecutionContext()

    def test_m4_filter_registration(self):
        m4_filter_types = [
            "Measure1DEdge",
            "Measure1DEdgePair",
            "FitLine2D",
            "FitCircle2D",
            "MeasurePointToPoint",
            "MeasurePointToLine",
            "MeasureLineAngle",
            "UndistortImage",
            "CalibrateHomography",
            "TransformPointsWorld"
        ]
        for ft in m4_filter_types:
            f = filter_registry.get(ft)
            self.assertIsNotNone(f, f"Filter {ft} must be registered in library")

    def test_filter_measure_point_to_point(self):
        f = filter_registry.get("MeasurePointToPoint")
        res = f.executor({"x1": 0, "y1": 0, "x2": 60, "y2": 80}, self.ctx)
        self.assertEqual(res["distance"], 100.0)
        self.assertEqual(res["delta_x"], 60.0)
        self.assertEqual(res["delta_y"], 80.0)

    def test_filter_fit_circle_2d(self):
        f = filter_registry.get("FitCircle2D")
        pts = [(10, 0), (0, 10), (-10, 0), (0, -10)]
        res = f.executor({"points": pts}, self.ctx)
        self.assertTrue(res["found"])
        self.assertAlmostEqual(res["radius"], 10.0, delta=0.2)
        self.assertAlmostEqual(res["cx"], 0.0, delta=0.2)
        self.assertAlmostEqual(res["cy"], 0.0, delta=0.2)


if __name__ == "__main__":
    unittest.main()
