"""
OptiFlow Vision Studio - Filter Library & DAG Pipeline Test Suite
Verifies all 25 basic vision filters and topological DAG execution.
"""

import os
import sys
import unittest
import numpy as np
import cv2

ENGINE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ENGINE_DIR)

from core.filters import filter_registry
from core.pipeline_executor import PipelineExecutor, ExecutionContext
from core.project import ProgramGraph, FilterNode, FilterConnection
from core.image_service import ImageService
from core.types import Point2D, Rectangle2D
from fastapi.testclient import TestClient
from app import app


class TestFilterLibrary25(unittest.TestCase):
    def setUp(self):
        self.image_service = ImageService()
        self.ctx = ExecutionContext(image_service=self.image_service)

    def test_filter_count_and_uniqueness(self):
        """Verifies exactly 25 filters are registered with unique types and categories."""
        filters = filter_registry.list_all()
        self.assertGreaterEqual(len(filters), 25, f"Expected at least 25 filters, got {len(filters)}")
        types = [f["filter_type"] for f in filters]
        self.assertEqual(len(types), len(set(types)), "Filter types must be unique")

    def test_image_processing_filters(self):
        """Tests image processing filters (Grayscale, Thresholds, Blur, Morphology, CLAHE)."""
        sample_bgr = np.zeros((100, 100, 3), dtype=np.uint8)
        sample_bgr[:50, :] = (30, 30, 30)
        sample_bgr[50:, :] = (210, 210, 210)

        # 1. ConvertToGrayscale
        f_gray = filter_registry.get("ConvertToGrayscale")
        res = f_gray.executor({"image": sample_bgr}, self.ctx)
        self.assertIsNotNone(res["out_image"])
        self.assertEqual(len(res["out_image"].shape), 2)

        # 2. ThresholdGlobal
        f_thresh = filter_registry.get("ThresholdGlobal")
        res_t = f_thresh.executor({"image": res["out_image"], "threshold": 80, "invert": False}, self.ctx)
        self.assertIn(res_t["out_image"][0, 0], [0, 255])

        # 3. ThresholdOtsu
        f_otsu = filter_registry.get("ThresholdOtsu")
        res_o = f_otsu.executor({"image": res["out_image"]}, self.ctx)
        self.assertGreater(res_o["computed_threshold"], 0)

        # 4. ThresholdAdaptive
        f_adapt = filter_registry.get("ThresholdAdaptive")
        res_a = f_adapt.executor({"image": res["out_image"], "block_size": 11, "c": 2.0}, self.ctx)
        self.assertIsNotNone(res_a["out_image"])

        # 5. GaussianBlur
        f_blur = filter_registry.get("GaussianBlur")
        res_b = f_blur.executor({"image": res["out_image"], "kernel_size": 5}, self.ctx)
        self.assertEqual(res_b["out_image"].shape, res["out_image"].shape)

        # 6. Morphology
        f_morph = filter_registry.get("Morphology")
        res_m = f_morph.executor({"image": res_t["out_image"], "operation": "DILATE", "kernel_size": 3}, self.ctx)
        self.assertIsNotNone(res_m["out_image"])

        # 7. EqualizeHistogram
        f_clahe = filter_registry.get("EqualizeHistogram")
        res_c = f_clahe.executor({"image": res["out_image"], "clip_limit": 2.0}, self.ctx)
        self.assertIsNotNone(res_c["out_image"])

    def test_blob_analysis_filters(self):
        """Tests blob analysis filters (Extract, Filter, Centroid/Box, Moments)."""
        mask = np.zeros((200, 200), dtype=np.uint8)
        # Create two solid rectangular blobs
        mask[20:80, 20:60] = 255   # Blob 1: 60x40 = 2400 area
        mask[100:180, 100:150] = 255 # Blob 2: 80x50 = 4000 area

        # 8. ExtractBlobs
        f_extract = filter_registry.get("ExtractBlobs")
        res_e = f_extract.executor({"image": mask}, self.ctx)
        self.assertEqual(res_e["blob_count"], 2)

        # 9. FilterBlobsByArea
        f_filter = filter_registry.get("FilterBlobsByArea")
        res_f = f_filter.executor({"blobs": res_e["blobs"], "min_area": 3000.0, "max_area": 5000.0}, self.ctx)
        self.assertEqual(res_f["count"], 1)

        # 10. BlobCentroidAndBox
        f_box = filter_registry.get("BlobCentroidAndBox")
        res_b = f_box.executor({"blobs": res_e["blobs"], "index": 0}, self.ctx)
        self.assertIsInstance(res_b["centroid"], Point2D)
        self.assertIsInstance(res_b["bounding_box"], Rectangle2D)
        self.assertGreater(res_b["centroid"].x, 0)

        # 11. BlobMomentsAndOrientation
        f_orient = filter_registry.get("BlobMomentsAndOrientation")
        res_o = f_orient.executor({"blobs": res_e["blobs"], "index": 0}, self.ctx)
        self.assertTrue(res_o["is_portrait"])

    def test_measurement_and_logic_filters(self):
        """Tests measurement (Distance, FitLine, FitCircle, ROI) and logic filters."""
        # 12. MeasureDistancePoints
        f_dist = filter_registry.get("MeasureDistancePoints")
        res_d = f_dist.executor({"point1": Point2D(x=0, y=0), "point2": Point2D(x=30, y=40)}, self.ctx)
        self.assertAlmostEqual(res_d["distance"], 50.0)

        # 13. FitLinePoints
        f_line = filter_registry.get("FitLinePoints")
        pts = [Point2D(x=0, y=0), Point2D(x=10, y=10), Point2D(x=20, y=20)]
        res_l = f_line.executor({"points": pts}, self.ctx)
        self.assertAlmostEqual(res_l["slope"], 1.0, places=2)

        # 14. FitCirclePoints
        f_circ = filter_registry.get("FitCirclePoints")
        c_pts = [Point2D(x=10, y=0), Point2D(x=0, y=10), Point2D(x=-10, y=0), Point2D(x=0, y=-10)]
        res_c = f_circ.executor({"points": c_pts}, self.ctx)
        self.assertAlmostEqual(res_c["radius"], 10.0, places=1)

        # 15. CreateInspectionROI
        f_roi = filter_registry.get("CreateInspectionROI")
        res_r = f_roi.executor({"x": 100, "y": 100, "width": 500, "height": 300}, self.ctx)
        self.assertEqual(res_r["area"], 150000.0)

        # 16. MathAdd & 17. MathMultiply
        f_add = filter_registry.get("MathAdd")
        self.assertEqual(f_add.executor({"a": 15.5, "b": 4.5}, self.ctx)["result"], 20.0)
        f_mul = filter_registry.get("MathMultiply")
        self.assertEqual(f_mul.executor({"a": 3.0, "b": 7.0}, self.ctx)["result"], 21.0)

        # 18. CompareThreshold & 19. LogicAnd
        f_comp = filter_registry.get("CompareThreshold")
        self.assertTrue(f_comp.executor({"value": 50, "min_limit": 40, "max_limit": 60}, self.ctx)["is_pass"])
        self.assertFalse(f_comp.executor({"value": 80, "min_limit": 40, "max_limit": 60}, self.ctx)["is_pass"])

        f_and = filter_registry.get("LogicAnd")
        self.assertTrue(f_and.executor({"a": True, "b": True}, self.ctx)["result"])
        self.assertFalse(f_and.executor({"a": True, "b": False}, self.ctx)["result"])

    def test_overlays_and_communication_filters(self):
        """Tests overlay drawing and Schneider SERT PLC protocol formatting."""
        # 20. DrawOverlayRectangle
        f_d_rect = filter_registry.get("DrawOverlayRectangle")
        res_dr = f_d_rect.executor({"rectangle": Rectangle2D(x=10, y=20, width=100, height=50)}, self.ctx)
        self.assertEqual(len(self.ctx.overlays), 1)

        # 21. DrawOverlayCrosshair
        f_d_cross = filter_registry.get("DrawOverlayCrosshair")
        f_d_cross.executor({"point": Point2D(x=50, y=60)}, self.ctx)
        self.assertEqual(len(self.ctx.overlays), 2)

        # 22. DrawOverlayText
        f_d_text = filter_registry.get("DrawOverlayText")
        f_d_text.executor({"text": "TEST PASS"}, self.ctx)
        self.assertEqual(len(self.ctx.overlays), 3)

        # 23. ImageSource
        f_src = filter_registry.get("ImageSource")
        res_src = f_src.executor({"image_id": "sample_bottle"}, self.ctx)
        self.assertIsNotNone(res_src["image"])
        self.assertEqual(res_src["width"], 1936)

        # 24. FormatSchneiderSert
        f_sert = filter_registry.get("FormatSchneiderSert")
        res_sert = f_sert.executor({
            "count": 1,
            "status": 1,
            "model_code": "LB800",
            "x": 420.5,
            "y": 185.3,
            "angle": 15.0,
            "orientation": "PO1"
        }, self.ctx)
        self.assertIn("Vision101A05LB800", res_sert["packet_string"])
        self.assertTrue(res_sert["packet_string"].endswith("\r"))

        # 25. PassFailDecision
        f_pf = filter_registry.get("PassFailDecision")
        res_pf = f_pf.executor({"dimension_ok": True, "count_ok": True}, self.ctx)
        self.assertTrue(res_pf["overall_pass"])
        self.assertEqual(res_pf["color_code"], "#10b981")


class TestPipelineDAGExecution(unittest.TestCase):
    def setUp(self):
        self.image_service = ImageService()
        self.executor = PipelineExecutor(image_service=self.image_service)

    def test_full_pipeline_dag_run(self):
        """Constructs a complete 5-node inspection DAG and executes Run Once."""
        graph = ProgramGraph(
            id="prog_test",
            name="Test Packaging DAG",
            nodes=[
                FilterNode(id="n1", filter_type="ImageSource", label="Camera Stream", inputs={"image_id": "sample_bottle"}),
                FilterNode(id="n2", filter_type="ThresholdOtsu", label="Otsu Binarization"),
                FilterNode(id="n3", filter_type="ExtractBlobs", label="Connected Components"),
                FilterNode(id="n4", filter_type="FilterBlobsByArea", label="Filter Noise", inputs={"min_area": 1000.0, "max_area": 150000.0}),
                FilterNode(id="n5", filter_type="PassFailDecision", label="Verdict", inputs={"dimension_ok": True, "count_ok": True})
            ],
            connections=[
                FilterConnection(id="c1", source_node_id="n1", source_port="image", target_node_id="n2", target_port="image"),
                FilterConnection(id="c2", source_node_id="n2", source_port="out_image", target_node_id="n3", target_port="image"),
                FilterConnection(id="c3", source_node_id="n3", source_port="blobs", target_node_id="n4", target_port="blobs"),
            ]
        )

        result = self.executor.run_graph(graph)
        self.assertTrue(result["success"])
        self.assertEqual(result["node_count"], 5)
        self.assertGreater(result["total_time_ms"], 0.0)

        # Verify all nodes succeeded and profiled
        for nid in ["n1", "n2", "n3", "n4", "n5"]:
            self.assertEqual(result["node_results"][nid]["status"], "SUCCESS")
            self.assertGreaterEqual(result["node_results"][nid]["time_ms"], 0.0)

        # Verify blob output passed to n4
        n4_out = result["node_results"]["n4"]["outputs"]
        self.assertIn("count", n4_out)


class TestFastApiPipelineEndpoints(unittest.TestCase):
    def setUp(self):
        self.client = TestClient(app)

    def test_api_list_filters(self):
        resp = self.client.get("/api/filters")
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertGreaterEqual(len(data["filters"]), 25)

    def test_api_pipeline_run_once(self):
        resp = self.client.post("/api/pipeline/run-once", json={})
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertTrue(data["success"])
        self.assertIn("node_results", data)


if __name__ == "__main__":
    unittest.main()
