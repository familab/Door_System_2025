"""OpenAPI (Swagger) spec generator for the Door Controller API."""
from typing import Dict, Optional
from .config import config
from .logging_utils import logger
from .version import __version__

GRAPH_DATA_DESCRIPTION = "Graph data"


def get_openapi_spec(host: Optional[str] = None) -> Dict:
    """Return an OpenAPI 3.0 spec as a Python dict.

    If `host` is provided (for example the HTTP Host header from the request), it
    will be used to construct the `servers` URL. `host` may be a hostname, hostname:port,
    or a full URL (including scheme). When not provided, falls back to configuration
    or localhost.
    """
    port = config.get("HEALTH_SERVER_PORT", 8080)

    logger.debug(f"get_openapi_spec called with host={host!r}, port={port}")

    if host:
        # If a full URL is provided, use it directly
        if host.startswith('http://') or host.startswith('https://'):
            server_url = host
        else:
            # If host already includes a port, don't append one
            if ':' in host:
                server_url = f"http://{host}"
            else:
                server_url = f"http://{host}:{port}"
    else:
        cfg_host = config.get("HEALTH_SERVER_HOST", "localhost")
        server_url = f"http://{cfg_host}:{port}"

    logger.debug(f"Calculated server_url={server_url!r}")

    spec = {
        "openapi": "3.0.0",
        "info": {
            "title": "Door Controller API",
            "version": __version__,
            "description": "API endpoints for managing and inspecting the Door Controller"
        },
        "servers": [{"url": server_url}],
        "components": {
            "securitySchemes": {
                "basicAuth": {
                    "type": "http",
                    "scheme": "basic"
                }
            }
        },
        "security": [{"basicAuth": []}],
        "paths": {
            "/api/refresh_badges": {
                "post": {
                    "summary": "Trigger a manual badge list refresh from Google Sheets",
                    "description": "Manually refresh the badge list. Requires Basic Auth.",
                    "responses": {
                        "200": {"description": "Refresh completed (JSON)"},
                        "500": {"description": "Internal server error"},
                        "503": {"description": "Service unavailable (no refresh callback)"}
                    },
                    "security": [{"basicAuth": []}]
                }
            },
            "/api/toggle": {
                "post": {
                    "summary": "Toggle door lock state",
                    "description": "Uses the existing manual unlock/lock implementation and returns the new state.",
                    "responses": {
                        "200": {"description": "Door state toggled"},
                        "500": {"description": "Internal server error"},
                        "503": {"description": "Door toggle callback unavailable"}
                    },
                    "security": [{"basicAuth": []}]
                }
            },
            "/metrics": {
                "get": {
                    "summary": "Metrics dashboard page",
                    "description": "HTML page with chart rendering, pagination and date-range filters.",
                    "parameters": [
                        {"name": "start_date", "in": "query", "schema": {"type": "string", "format": "date"}},
                        {"name": "end_date", "in": "query", "schema": {"type": "string", "format": "date"}},
                        {"name": "page", "in": "query", "schema": {"type": "integer", "minimum": 1}},
                        {"name": "per_page", "in": "query", "schema": {"type": "integer", "minimum": 1}}
                    ],
                    "responses": {"200": {"description": "HTML metrics page"}},
                    "security": [{"basicAuth": []}]
                }
            },
            "/api/metrics/badge-scans-per-hour": {"get": {"summary": "Badge Scans Per Hour", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/door-open-duration": {"get": {"summary": "Door Open Duration Over Time", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/top-badge-users": {"get": {"summary": "Top Badge Users", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/door-cycles-per-day": {"get": {"summary": "Door Cycles Per Day", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/denied-badge-scans": {"get": {"summary": "Denied Badge Scans", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/badge-scan-door-open-latency": {"get": {"summary": "Badge Scan to Door Open Latency", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/manual-events": {"get": {"summary": "Manual Unlock/Lock Events", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/door-left-open-too-long": {"get": {"summary": "Door Left Open Too Long", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/hourly-activity-heatmap": {"get": {"summary": "Hourly Activity Heatmap", "responses": {"200": {"description": GRAPH_DATA_DESCRIPTION}}, "security": [{"basicAuth": []}]}},
            "/api/metrics/full-event-timeline": {
                "get": {
                    "summary": "Full Event Timeline",
                    "parameters": [
                        {"name": "page", "in": "query", "schema": {"type": "integer", "minimum": 1}},
                        {"name": "page_size", "in": "query", "schema": {"type": "integer", "minimum": 1}}
                    ],
                    "responses": {"200": {"description": "Paginated timeline data"}},
                    "security": [{"basicAuth": []}]
                }
            },
            "/api/metrics/export": {
                "get": {
                    "summary": "Export month data",
                    "parameters": [
                        {"name": "month", "in": "query", "required": True, "schema": {"type": "string", "example": "2026-02"}},
                        {"name": "format", "in": "query", "required": True, "schema": {"type": "string", "enum": ["csv", "json"]}}
                    ],
                    "responses": {
                        "200": {"description": "Monthly export file"},
                        "400": {"description": "Invalid query parameters"}
                    },
                    "security": [{"basicAuth": []}]
                }
            }
        }
    }

    return spec
