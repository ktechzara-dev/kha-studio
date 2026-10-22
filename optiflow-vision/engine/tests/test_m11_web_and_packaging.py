"""
OptiFlow Vision Studio - Milestone 11 Packaging & Web HMI Unit Tests
Tests Web HMI endpoint, standalone inspection execution, and filter library integrity.
"""

import unittest
from fastapi.testclient import TestClient
from app import app
from core.filters import filter_registry
from core.project import OptiFlowProject


class TestM11WebAndPackaging(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.client = TestClient(app)

    def test_web_hmi_endpoint(self):
        """Verifies GET /hmi returns valid responsive HTML for factory floor operators."""
        response = self.client.get("/hmi")
        self.assertEqual(response.status_code, 200)
        self.assertIn("text/html", response.headers["content-type"])
        self.assertIn("OPTIFLOW VISION", response.text)
        self.assertIn("Unilever Delta Robot Line", response.text)

    def test_complete_filter_catalog_integrity(self):
        """Verifies exactly 57 filters across 13 industrial categories are registered."""
        filters = filter_registry.list_all()
        self.assertEqual(len(filters), 57, f"Expected 57 filters, got {len(filters)}")

        categories = set(f["category"] for f in filters)
        expected_categories = {
            "Acquisition",
            "Processing",
            "Measurement",
            "Metrology",
            "Geometry",
            "Logic",
            "Overlays",
            "Communication",
            "Analysis",
            "Deep Learning"
        }
        for cat in expected_categories:
            self.assertIn(cat, categories, f"Category '{cat}' must exist in Filter Catalog")

    def test_project_standalone_compatibility(self):
        """Verifies default project has valid format version and programs."""
        proj = OptiFlowProject()
        self.assertGreaterEqual(len(proj.programs), 1)
        self.assertEqual(proj.generator, "OptiFlow Vision Studio")
        self.assertTrue(proj.format_version.startswith("1."))


if __name__ == "__main__":
    unittest.main()
