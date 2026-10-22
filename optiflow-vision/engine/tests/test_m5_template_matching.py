"""
Unit Tests for OptiFlow Vision Studio Milestone 5:
- 2D Local Coordinate System & ROI Fixture Alignment
- Multi-tier Pyramid Normalized Cross-Correlation (NCC) Matching
- Rotational Invariant Template Matching
- Golden Template Differential Defect Inspection
- Milestone 5 Filter Registrations & Execution
"""

import math
import unittest
import numpy as np
import cv2

from core.template_matching import (
    CoordinateSystem2D,
    match_template_ncc_pyramid,
    match_template_with_rotation,
    compare_golden_template,
)
from core.filters import filter_registry
from core.pipeline_executor import ExecutionContext


class TestCoordinateSystem2D(unittest.TestCase):
    def test_point_transforms_at_90_degrees(self):
        # Origin at (100, 200), rotated +90 degrees (Y-axis points left, X-axis points down)
        cs = CoordinateSystem2D(origin_x=100.0, origin_y=200.0, angle_deg=90.0)

        # Local point (10, 0) -> World
        # world_x = 100 + 10*cos(90) - 0 = 100
        # world_y = 200 + 10*sin(90) + 0 = 210
        wx, wy = cs.point_to_world(10.0, 0.0)
        self.assertAlmostEqual(wx, 100.0, places=2)
        self.assertAlmostEqual(wy, 210.0, places=2)

        # Round-trip back to local
        lx, ly = cs.point_to_local(wx, wy)
        self.assertAlmostEqual(lx, 10.0, places=2)
        self.assertAlmostEqual(ly, 0.0, places=2)

    def test_roi_rectangle_alignment(self):
        cs = CoordinateSystem2D(origin_x=50.0, origin_y=50.0, angle_deg=0.0)
        aligned = cs.align_rectangle({"x": 10.0, "y": 20.0, "width": 40.0, "height": 60.0})
        self.assertEqual(aligned["width"], 40.0)
        self.assertEqual(aligned["height"], 60.0)
        self.assertEqual(aligned["center_x"], 80.0)  # 50 + 10 + 20
        self.assertEqual(aligned["center_y"], 100.0) # 50 + 20 + 30


class TestTemplateMatching(unittest.TestCase):
    def setUp(self):
        # Create base canvas with distinct target
        self.img = np.zeros((200, 200), dtype=np.uint8) + 40
        # Draw distinctive cross pattern at (120, 80)
        cv2.circle(self.img, (120, 80), 20, 220, -1)
        cv2.rectangle(self.img, (115, 65), (125, 95), 50, -1)

        # Extract template centered at (120, 80), size 40x40
        self.tpl = self.img[60:100, 100:140].copy()

    def test_pyramid_ncc_matching(self):
        matches = match_template_ncc_pyramid(self.img, self.tpl, min_score=0.85)
        self.assertGreaterEqual(len(matches), 1)
        m = matches[0]
        self.assertAlmostEqual(m["x"], 120.0, delta=1.5)
        self.assertAlmostEqual(m["y"], 80.0, delta=1.5)
        self.assertGreaterEqual(m["score"], 0.95)

    def test_rotation_template_matching(self):
        # Rotate canvas by +15 degrees
        M = cv2.getRotationMatrix2D((120, 80), 15.0, 1.0)
        rotated_img = cv2.warpAffine(self.img, M, (200, 200))

        match = match_template_with_rotation(
            rotated_img, self.tpl, min_score=0.75, min_angle_deg=-25.0, max_angle_deg=25.0, angle_step_deg=5.0
        )
        self.assertIsNotNone(match)
        # Expected orientation near 15 degrees
        self.assertAlmostEqual(match["angle_deg"], 15.0, delta=5.0)
        self.assertAlmostEqual(match["x"], 120.0, delta=4.0)
        self.assertAlmostEqual(match["y"], 80.0, delta=4.0)


class TestGoldenTemplateInspection(unittest.TestCase):
    def test_flaw_detection(self):
        golden = np.zeros((150, 150), dtype=np.uint8) + 50
        cv2.circle(golden, (75, 75), 30, 180, -1)

        # Live part has a surface scratch / defect at (40, 40)
        live = golden.copy()
        cv2.rectangle(live, (35, 35), (45, 45), 255, -1)  # 10x10 = 100 px defect

        res = compare_golden_template(live, golden, diff_threshold=40, min_defect_area=20)
        self.assertTrue(res["has_defects"])
        self.assertGreaterEqual(res["defect_count"], 1)
        self.assertGreaterEqual(res["max_defect_area"], 50)


class TestM5Filters(unittest.TestCase):
    def setUp(self):
        self.ctx = ExecutionContext()

    def test_m5_filters_registered(self):
        types = [
            "MatchTemplateNCC",
            "MatchTemplateRotation",
            "CreateCoordinateSystem",
            "AlignROI",
            "CompareGoldenTemplate"
        ]
        for ft in types:
            f = filter_registry.get(ft)
            self.assertIsNotNone(f, f"Filter {ft} must be registered")

    def test_filter_create_coordinate_system_and_align_roi(self):
        f_cs = filter_registry.get("CreateCoordinateSystem")
        res_cs = f_cs.executor({"origin_x": 200, "origin_y": 150, "angle_deg": 45}, self.ctx)
        cs_dict = res_cs["coord_system"]
        self.assertEqual(cs_dict["origin_x"], 200.0)
        self.assertEqual(cs_dict["angle_deg"], 45.0)

        f_align = filter_registry.get("AlignROI")
        res_align = f_align.executor({
            "coord_system": cs_dict,
            "roi_x": 0,
            "roi_y": 0,
            "roi_w": 60,
            "roi_h": 40
        }, self.ctx)
        self.assertIn("world_cx", res_align)
        self.assertEqual(res_align["world_w"], 60.0)


if __name__ == "__main__":
    unittest.main()
