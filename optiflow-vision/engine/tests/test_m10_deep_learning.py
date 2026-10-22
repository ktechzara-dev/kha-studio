"""
OptiFlow Vision Studio - Milestone 10 Deep Learning & ONNX Inference Unit Tests
Tests ONNX session wrappers, tensor preprocessing, NMS box filtering,
YOLO detection decoding, defect classification, and anomaly score computation.
"""

import unittest
import numpy as np
import cv2

from core.deep_learning import (
    preprocess_image_tensor,
    non_max_suppression,
    ONNXInferenceSession,
    postprocess_classification,
    postprocess_yolo_detections,
    draw_yolo_detections_overlay,
    compute_anomaly_map,
)
from core.filters import filter_registry


class TestDeepLearningCore(unittest.TestCase):
    def setUp(self):
        # 1920x1080 synthetic industrial test image
        self.img = np.full((1080, 1920, 3), 128, dtype=np.uint8)
        cv2.circle(self.img, (960, 540), 200, (255, 255, 255), -1)

    def test_preprocess_image_tensor(self):
        tensor, scale, (pad_x, pad_y) = preprocess_image_tensor(
            self.img, target_size=(640, 640), channel_first=True
        )
        self.assertEqual(tensor.shape, (1, 3, 640, 640))
        self.assertEqual(tensor.dtype, np.float32)
        self.assertTrue(scale > 0.0)
        self.assertGreaterEqual(pad_x, 0)
        self.assertGreaterEqual(pad_y, 0)
        # Check normalized range
        self.assertLessEqual(np.max(tensor), 1.0)
        self.assertGreaterEqual(np.min(tensor), 0.0)

    def test_non_max_suppression(self):
        # Two overlapping boxes and one distinct box
        boxes = np.array([
            [100, 100, 200, 200],  # Box 0: score 0.95
            [105, 102, 198, 205],  # Box 1: score 0.85 (heavily overlaps Box 0)
            [500, 500, 600, 600]   # Box 2: score 0.70 (distinct)
        ], dtype=np.float32)
        scores = np.array([0.95, 0.85, 0.70], dtype=np.float32)

        keep = non_max_suppression(boxes, scores, iou_threshold=0.5)
        self.assertIn(0, keep)
        self.assertNotIn(1, keep)  # Suppressed by Box 0
        self.assertIn(2, keep)
        self.assertEqual(len(keep), 2)

    def test_postprocess_classification(self):
        logits = np.array([1.2, 5.8, 0.4])
        class_names = ["PASS", "SCRATCH_DEFECT", "STAIN_DEFECT"]
        res = postprocess_classification(logits, class_names=class_names)

        self.assertEqual(res["label"], "SCRATCH_DEFECT")
        self.assertEqual(res["class_id"], 1)
        self.assertGreater(res["confidence"], 0.9)
        self.assertTrue(res["is_defective"])
        self.assertIn("SCRATCH_DEFECT", res["probabilities"])

    def test_postprocess_yolo_detections_and_overlay(self):
        # Mock YOLO raw output (1, 6, 100)
        # 6 rows: [cx, cy, w, h, class0_score, class1_score]
        raw_preds = np.zeros((1, 6, 100), dtype=np.float32)
        # Object 1
        raw_preds[0, 0, 0] = 320.0
        raw_preds[0, 1, 0] = 240.0
        raw_preds[0, 2, 0] = 100.0
        raw_preds[0, 3, 0] = 100.0
        raw_preds[0, 4, 0] = 0.91
        raw_preds[0, 5, 0] = 0.05
        # Object 2 (low score)
        raw_preds[0, 0, 1] = 50.0
        raw_preds[0, 1, 1] = 50.0
        raw_preds[0, 2, 1] = 20.0
        raw_preds[0, 3, 1] = 20.0
        raw_preds[0, 4, 1] = 0.10
        raw_preds[0, 5, 1] = 0.05

        dets = postprocess_yolo_detections(
            raw_preds,
            conf_threshold=0.5,
            iou_threshold=0.45,
            class_names=["bottle", "cap"],
            scale=1.0,
            pad=(0, 0),
            orig_shape=(480, 640)
        )
        self.assertEqual(len(dets), 1)
        self.assertEqual(dets[0]["class_name"], "bottle")
        self.assertAlmostEqual(dets[0]["confidence"], 0.91, places=2)

        # Overlay drawing test
        canvas = np.zeros((480, 640, 3), dtype=np.uint8)
        overlaid = draw_yolo_detections_overlay(canvas, dets)
        self.assertEqual(overlaid.shape, (480, 640, 3))
        # Non-black pixels exist
        self.assertTrue(np.any(overlaid > 0))

    def test_compute_anomaly_map(self):
        orig = np.full((100, 100, 3), 100, dtype=np.uint8)
        rec = orig.copy()
        # Inject artificial flaw
        rec[40:60, 40:60] = 240

        score, heatmap = compute_anomaly_map(orig, rec)
        self.assertGreater(score, 10.0)
        self.assertEqual(heatmap.shape, (100, 100, 3))


class TestM10FiltersExecution(unittest.TestCase):
    def setUp(self):
        from core.pipeline_executor import ExecutionContext
        self.ctx = ExecutionContext()
        self.sample_img = np.full((480, 640, 3), 150, dtype=np.uint8)

    def test_m10_filter_registration(self):
        types = [f["filter_type"] for f in filter_registry.list_all()]
        self.assertIn("ONNXModelInference", types)
        self.assertIn("ClassifyDefect", types)
        self.assertIn("DetectObjectsYOLO", types)
        self.assertIn("DetectAnomaly", types)
        # Total filter count is now 57
        self.assertEqual(len(types), 57)

    def test_filter_classify_defect(self):
        f = filter_registry.get("ClassifyDefect")
        self.assertIsNotNone(f)
        res = f.executor({"image": self.sample_img, "model_path": "mock_classifier.onnx"}, self.ctx)
        self.assertIn("label", res)
        self.assertIn("confidence", res)
        self.assertIn("is_pass", res)

    def test_filter_detect_objects_yolo(self):
        f = filter_registry.get("DetectObjectsYOLO")
        self.assertIsNotNone(f)
        res = f.executor({"image": self.sample_img, "model_path": "mock_yolo.onnx"}, self.ctx)
        self.assertIn("boxes", res)
        self.assertIn("object_count", res)
        self.assertIn("annotated_image", res)
        self.assertGreaterEqual(res["object_count"], 1)

    def test_filter_detect_anomaly(self):
        f = filter_registry.get("DetectAnomaly")
        self.assertIsNotNone(f)
        res = f.executor({"image": self.sample_img, "model_path": "mock_anomaly.onnx"}, self.ctx)
        self.assertIn("anomaly_score", res)
        self.assertIn("heatmap", res)
        self.assertIsInstance(res["is_anomaly"], bool)


if __name__ == "__main__":
    unittest.main()
