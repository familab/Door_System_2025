"""Tests for OpenAPI spec generator."""
import unittest
from lib.openapi import get_openapi_spec
from lib.version import __version__


class TestOpenAPISpec(unittest.TestCase):
    def test_version_matches_package_version(self):
        spec = get_openapi_spec()
        self.assertIn("info", spec)
        self.assertEqual(spec["info"]["version"], __version__)

    def test_server_url_uses_host_header(self):
        spec = get_openapi_spec(host="example.com:1234")
        self.assertIn("servers", spec)
        self.assertTrue(any("example.com" in s["url"] for s in spec["servers"]))

    def test_metrics_and_toggle_paths_present(self):
        spec = get_openapi_spec()
        paths = spec.get("paths", {})
        self.assertIn("/api/toggle", paths)
        self.assertIn("/metrics", paths)
        self.assertIn("/api/metrics/badge-scans-per-hour", paths)
        self.assertIn("/api/metrics/full-event-timeline", paths)
        self.assertIn("/api/metrics/export", paths)

if __name__ == '__main__':
    unittest.main()
