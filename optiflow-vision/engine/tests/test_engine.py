"""
OptiFlow Vision Studio - Engine & Project Serialization Test Suite
Verifies .vsproj schema, atomic I/O, ImageService, pixel probe, histogram, and FastAPI endpoints.
"""

import os
import sys
import tempfile
import unittest

# Add engine to path
ENGINE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, ENGINE_DIR)

from core.project import OptiFlowProject, ProjectMetadata, ProgramGraph, FilterNode, migrate_project_schema, CURRENT_SCHEMA_VERSION
from core.image_service import ImageService
from core.types import Point2D, Rectangle2D
from fastapi.testclient import TestClient
from app import app


class TestProjectSchema(unittest.TestCase):
    def test_project_lifecycle(self):
        """Tests project creation, node addition, serialization, and atomic file saving."""
        project = OptiFlowProject(
            metadata=ProjectMetadata(name="Test Packaging Inspection", author="Antigravity Test")
        )

        # Add sample filter node
        node = FilterNode(
            id="node_cam_1",
            filter_type="CameraAcquisition",
            label="Line 1 GigE Camera",
            position_x=120.0,
            position_y=180.0,
            inputs={"exposure_us": 1200, "trigger": "CONTINUOUS"}
        )
        project.programs[0].nodes.append(node)

        # JSON Roundtrip
        json_str = project.to_json()
        loaded = OptiFlowProject.from_json(json_str)

        self.assertEqual(loaded.format_version, CURRENT_SCHEMA_VERSION)
        self.assertEqual(loaded.metadata.name, "Test Packaging Inspection")
        self.assertEqual(len(loaded.programs[0].nodes), 1)
        self.assertEqual(loaded.programs[0].nodes[0].id, "node_cam_1")

        # Atomic File Save & Load
        with tempfile.TemporaryDirectory() as tmp_dir:
            file_path = os.path.join(tmp_dir, "test_proj.vsproj")
            project.save_to_file(file_path)

            self.assertTrue(os.path.exists(file_path))
            from_file = OptiFlowProject.load_from_file(file_path)
            self.assertEqual(from_file.metadata.name, "Test Packaging Inspection")
            self.assertEqual(from_file.programs[0].nodes[0].label, "Line 1 GigE Camera")

    def test_schema_migration(self):
        """Tests that incomplete legacy projects are upgraded safely."""
        legacy_data = {
            "format_version": "0.1.0",
            "metadata": {"name": "Old Project"}
        }
        migrated = migrate_project_schema(legacy_data)
        self.assertEqual(migrated["format_version"], CURRENT_SCHEMA_VERSION)
        self.assertIn("programs", migrated)
        self.assertIn("settings", migrated)


class TestImageService(unittest.TestCase):
    def setUp(self):
        self.svc = ImageService()

    def test_synthetic_samples_and_meta(self):
        """Verifies procedural sample generation and metadata extraction."""
        images = self.svc.list_images()
        self.assertGreaterEqual(len(images), 3)

        ids = [img.id for img in images]
        self.assertIn("sample_bottle", ids)
        self.assertIn("sample_pcb", ids)
        self.assertIn("sample_calib", ids)

        bottle_meta = self.svc.get_meta("sample_bottle")
        self.assertIsNotNone(bottle_meta)
        self.assertEqual(bottle_meta.width, 1936)
        self.assertEqual(bottle_meta.height, 1216)
        self.assertEqual(bottle_meta.channels, 1)

    def test_pixel_probe(self):
        """Tests coordinate pixel probe and 7x7 neighborhood extraction."""
        probe = self.svc.probe_pixel("sample_bottle", 450, 600)
        self.assertTrue(probe.in_bounds)
        self.assertEqual(probe.x, 450)
        self.assertEqual(probe.y, 600)
        self.assertGreater(len(probe.channels), 0)
        self.assertEqual(len(probe.neighborhood_7x7), 7)
        self.assertEqual(len(probe.neighborhood_7x7[0]), 7)

        # Out of bounds probe
        probe_oob = self.svc.probe_pixel("sample_bottle", -10, 9999)
        self.assertFalse(probe_oob.in_bounds)

    def test_histogram_computation(self):
        """Tests 256-bin histogram computation and statistical distribution."""
        hist = self.svc.compute_histogram("sample_bottle")
        self.assertIsNotNone(hist)
        self.assertEqual(len(hist.bins), 256)
        self.assertGreaterEqual(hist.max_val, hist.min_val)
        self.assertGreaterEqual(hist.mean_val, 0)
        self.assertLessEqual(hist.mean_val, 255)

    def test_binary_image_stream_and_downscale(self):
        """Tests binary stream encoding and auto-downscale for multi-tier pyramids."""
        # Full resolution JPEG
        full_bytes = self.svc.get_image_bytes("sample_bottle", format_ext=".jpg", quality=85)
        self.assertIsNotNone(full_bytes)
        self.assertGreater(len(full_bytes), 1000)

        # Downscaled thumbnail (max_dimension = 400)
        thumb_bytes = self.svc.get_image_bytes("sample_bottle", format_ext=".jpg", max_dimension=400)
        self.assertIsNotNone(thumb_bytes)
        self.assertLess(len(thumb_bytes), len(full_bytes))


class TestFastApiEndpoints(unittest.TestCase):
    def setUp(self):
        self.client = TestClient(app)

    def test_health_endpoint(self):
        resp = self.client.get("/api/health")
        self.assertEqual(resp.status_code, 200)
        data = resp.json()
        self.assertEqual(data["status"], "online")
        self.assertEqual(data["service"], "OptiFlow Vision Engine")

    def test_project_api(self):
        # New project
        resp = self.client.post("/api/project/new?name=API_Test_Project")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.json()["project"]["metadata"]["name"], "API_Test_Project")

        # Get current
        resp = self.client.get("/api/project/current")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.json()["project"]["metadata"]["name"], "API_Test_Project")

        # Save project to temp file
        with tempfile.TemporaryDirectory() as tmp_dir:
            save_path = os.path.join(tmp_dir, "saved.vsproj")
            resp = self.client.post("/api/project/save", json={"file_path": save_path})
            self.assertEqual(resp.status_code, 200)
            self.assertTrue(os.path.exists(save_path))

            # Open project from file
            resp = self.client.post("/api/project/open", json={"file_path": save_path})
            self.assertEqual(resp.status_code, 200)
            self.assertEqual(resp.json()["project"]["metadata"]["name"], "API_Test_Project")

    def test_image_endpoints(self):
        # List images
        resp = self.client.get("/api/images")
        self.assertEqual(resp.status_code, 200)
        self.assertGreaterEqual(len(resp.json()["images"]), 3)

        # Binary preview
        resp = self.client.get("/api/image/preview/sample_bottle?max_dim=800")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(resp.headers["content-type"], "image/jpeg")

        # Pixel probe
        resp = self.client.get("/api/image/probe?image_id=sample_bottle&x=500&y=400")
        self.assertEqual(resp.status_code, 200)
        self.assertTrue(resp.json()["in_bounds"])

        # Histogram
        resp = self.client.get("/api/image/histogram/sample_bottle")
        self.assertEqual(resp.status_code, 200)
        self.assertEqual(len(resp.json()["bins"]), 256)


if __name__ == "__main__":
    unittest.main()
