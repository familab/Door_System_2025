"""Metrics routes and JSON endpoints."""
import json
import math
from collections import defaultdict, deque
from datetime import date, datetime
from urllib.parse import parse_qs

from ..config import config
from ..metrics_storage import month_events_to_csv, month_keys_in_range, query_events_range, query_month_events
from .state import APPLICATION_JSON


def _parse_date(value: str, default_value: date) -> date:
    try:
        return datetime.strptime(value, "%Y-%m-%d").date()
    except Exception:
        return default_value


def _parse_int(value: str, default_value: int, minimum: int = 1, maximum: int = 500) -> int:
    try:
        val = int(value)
        if val < minimum:
            return minimum
        if val > maximum:
            return maximum
        return val
    except Exception:
        return default_value


def _query_parts(raw_query: str):
    return parse_qs(raw_query or "", keep_blank_values=False)


def _resolve_range(query_map):
    now = datetime.now()
    default_start = date(now.year, 1, 1)
    default_end = date(now.year, now.month, now.day)
    start_date = _parse_date((query_map.get("start_date", [default_start.isoformat()])[0]), default_start)
    end_date = _parse_date((query_map.get("end_date", [default_end.isoformat()])[0]), default_end)
    if start_date > end_date:
        start_date, end_date = end_date, start_date
    start_ts = datetime(start_date.year, start_date.month, start_date.day, 0, 0, 0)
    end_ts = datetime(end_date.year, end_date.month, end_date.day, 23, 59, 59)
    return start_date, end_date, start_ts, end_ts


def _event_ts(event):
    try:
        return datetime.strptime(event["ts"], "%Y-%m-%d %H:%M:%S")
    except Exception:
        return None


def _status_is(event, expected):
    return str(event.get("status", "")).strip().lower() == expected.lower()


def _group_count(events, key_fn):
    counter = defaultdict(int)
    for event in events:
        key = key_fn(event)
        if key is not None:
            counter[key] += 1
    return counter


def _door_open_durations(events):
    durations = []
    open_ts = None
    for event in events:
        event_type = event.get("event_type")
        ts = _event_ts(event)
        if ts is None:
            continue
        if event_type == "Door OPEN/UNLOCKED":
            open_ts = ts
        elif event_type == "Door CLOSED/LOCKED" and open_ts is not None:
            if ts >= open_ts:
                durations.append((open_ts, (ts - open_ts).total_seconds()))
            open_ts = None
    return durations


def _filter_events(query_map):
    start_date, end_date, start_ts, end_ts = _resolve_range(query_map)
    events = query_events_range(
        start_ts.strftime("%Y-%m-%d %H:%M:%S"),
        end_ts.strftime("%Y-%m-%d %H:%M:%S"),
    )
    return start_date, end_date, events


def send_metrics_page(handler, raw_query: str):
    """Render metrics dashboard page."""
    query = _query_parts(raw_query)
    start_date, end_date, _, _ = _resolve_range(query)
    months = month_keys_in_range(start_date, end_date)
    page = _parse_int(query.get("page", ["1"])[0], 1)
    per_page = _parse_int(query.get("per_page", ["1"])[0], 1, minimum=1, maximum=12)
    total_pages = max(1, int(math.ceil(len(months) / float(per_page)))) if months else 1
    page = min(page, total_pages)
    page_start = (page - 1) * per_page
    page_end = page_start + per_page
    page_months = months[page_start:page_end] if months else []
    selected_month = page_months[-1] if page_months else datetime.now().strftime("%Y-%m")
    data_query = (
        "start_date={0}&end_date={1}&page={2}&per_page={3}".format(
            start_date.isoformat(), end_date.isoformat(), page, per_page
        )
    )

    html = """<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8">
  <title>Door Metrics</title>
  <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
  <style>
    body { font-family: monospace; margin: 20px; background: #1e1e1e; color: #d4d4d4; }
    h1, h2 { color: #4ec9b0; }
    a { color: #9cdcfe; }
    .toolbar { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; margin-bottom: 16px; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(420px, 1fr)); gap: 16px; }
    .card { background: #252526; border: 1px solid #555; border-radius: 8px; padding: 12px; }
    .controls { margin-top: 8px; display: flex; gap: 8px; flex-wrap: wrap; }
    button { background:#4ec9b0;color:#1e1e1e;padding:6px 10px;border:none;border-radius:4px;cursor:pointer; }
    select,input { background:#1e1e1e;color:#d4d4d4;border:1px solid #555;padding:6px;border-radius:4px; }
    table { border-collapse: collapse; width: 100%; margin-top: 8px; }
    th, td { border: 1px solid #555; padding: 6px; text-align: left; }
    th { background: #2d2d30; color: #4ec9b0; }
  </style>
</head>
<body>
  <h1>Door Metrics</h1>
  <p><a href="/admin">Back to Admin</a> | <a href="/docs">API Docs</a></p>
  <form class="toolbar" method="GET" action="/metrics">
    <label>Start Date <input type="date" name="start_date" value="__START_DATE__"></label>
    <label>End Date <input type="date" name="end_date" value="__END_DATE__"></label>
    <label>Months/Page <input type="number" min="1" max="12" name="per_page" value="__PER_PAGE__"></label>
    <label>Page <input type="number" min="1" name="page" value="__PAGE__"></label>
    <button type="submit">Apply</button>
  </form>
  <p>Page __PAGE__ of __TOTAL_PAGES__ | Range months: __MONTH_COUNT__</p>
  <div class="toolbar">
    <label>Selected Month
      <select id="selectedMonth">__MONTH_OPTIONS__</select>
    </label>
    <button id="downloadMonthCsv">Download Month CSV</button>
    <button id="downloadMonthJson">Download Month JSON</button>
  </div>
  <div class="grid" id="chartsGrid"></div>
  <div class="card">
    <h2>Full Event Timeline</h2>
    <table>
      <thead><tr><th>Timestamp</th><th>Event</th><th>Badge</th><th>Status</th></tr></thead>
      <tbody id="timelineRows"></tbody>
    </table>
    <div class="controls">
      <button id="timelinePrev">Prev</button>
      <button id="timelineNext">Next</button>
      <span id="timelineMeta"></span>
    </div>
  </div>
  <script>
  (function() {
    const filterQuery = "__DATA_QUERY__";
    const selectedMonthEl = document.getElementById("selectedMonth");
    const graphDefs = [
      ["badge-scans-per-hour", "Badge Scans Per Hour", "bar"],
      ["door-open-duration", "Door Open Duration Over Time", "line"],
      ["top-badge-users", "Top Badge Users", "bar"],
      ["door-cycles-per-day", "Door Cycles Per Day", "line"],
      ["denied-badge-scans", "Denied Badge Scans", "line"],
      ["badge-scan-door-open-latency", "Badge Scan → Door Open Latency", "line"],
      ["manual-events", "Manual Unlock/Lock Events", "bar"],
      ["door-left-open-too-long", "Door Left Open Too Long", "line"],
      ["hourly-activity-heatmap", "Hourly Activity Heatmap", "bar"]
    ];
    const charts = {};

    function downloadText(filename, contentType, text) {
      const blob = new Blob([text], { type: contentType });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = filename;
      a.click();
      URL.revokeObjectURL(url);
    }

    function chartToSvg(chart) {
      const labels = chart.data.labels || [];
      const values = ((chart.data.datasets || [])[0] || {}).data || [];
      const width = 900;
      const height = 320;
      const maxVal = Math.max.apply(null, [1].concat(values));
      const stepX = labels.length > 1 ? (width - 80) / (labels.length - 1) : (width - 80);
      let points = "";
      for (let i = 0; i < values.length; i++) {
        const x = 40 + (i * stepX);
        const y = height - 30 - ((values[i] / maxVal) * (height - 70));
        points += x + "," + y + " ";
      }
      return [
        '<svg xmlns="http://www.w3.org/2000/svg" width="' + width + '" height="' + height + '">',
        '<rect width="100%" height="100%" fill="#1e1e1e" />',
        '<polyline fill="none" stroke="#4ec9b0" stroke-width="2" points="' + points.trim() + '" />',
        '</svg>'
      ].join("");
    }

    function addCard(def) {
      const id = def[0];
      const title = def[1];
      const card = document.createElement("div");
      card.className = "card";
      card.innerHTML = '<h2>' + title + '</h2>' +
        '<canvas id="c-' + id + '" height="140"></canvas>' +
        '<div class="controls">' +
        '<button data-k="png">PNG</button><button data-k="svg">SVG</button>' +
        '</div>';
      const controls = card.querySelectorAll("button");
      controls.forEach(function(btn) {
        btn.addEventListener("click", function() {
          const chart = charts[id];
          if (!chart) return;
          const month = selectedMonthEl.value || "month";
          if (btn.dataset.k === "png") {
            const a = document.createElement("a");
            a.href = chart.toBase64Image();
            a.download = id + "-" + month + ".png";
            a.click();
            return;
          }
          const svg = chartToSvg(chart);
          downloadText(id + "-" + month + ".svg", "image/svg+xml", svg);
        });
      });
      document.getElementById("chartsGrid").appendChild(card);
    }

    async function loadChart(def) {
      const id = def[0];
      const title = def[1];
      const chartType = def[2];
      const res = await fetch("/api/metrics/" + id + "?" + filterQuery, { credentials: "same-origin" });
      const payload = await res.json();
      const ctx = document.getElementById("c-" + id).getContext("2d");
      charts[id] = new Chart(ctx, {
        type: chartType,
        data: {
          labels: payload.labels || [],
          datasets: [{ label: title, data: payload.values || [], borderColor: "#4ec9b0", backgroundColor: "rgba(78,201,176,0.35)" }]
        },
        options: { responsive: true, maintainAspectRatio: false }
      });
    }

    let timelinePage = 1;
    const timelineSize = 50;
    async function loadTimeline() {
      const res = await fetch("/api/metrics/full-event-timeline?" + filterQuery + "&page=" + timelinePage + "&page_size=" + timelineSize, { credentials: "same-origin" });
      const payload = await res.json();
      const rows = document.getElementById("timelineRows");
      rows.innerHTML = "";
      (payload.items || []).forEach(function(item) {
        const tr = document.createElement("tr");
        tr.innerHTML = "<td>" + item.ts + "</td><td>" + item.event_type + "</td><td>" + (item.badge_id || "") + "</td><td>" + item.status + "</td>";
        rows.appendChild(tr);
      });
      document.getElementById("timelineMeta").textContent = "Page " + payload.page + " of " + payload.total_pages + " (" + payload.total + " items)";
      document.getElementById("timelinePrev").disabled = payload.page <= 1;
      document.getElementById("timelineNext").disabled = payload.page >= payload.total_pages;
    }

    document.getElementById("timelinePrev").addEventListener("click", function() {
      timelinePage = Math.max(1, timelinePage - 1);
      loadTimeline();
    });
    document.getElementById("timelineNext").addEventListener("click", function() {
      timelinePage += 1;
      loadTimeline();
    });

    document.getElementById("downloadMonthCsv").addEventListener("click", function() {
      const month = selectedMonthEl.value;
      location.href = "/api/metrics/export?month=" + month + "&format=csv";
    });
    document.getElementById("downloadMonthJson").addEventListener("click", function() {
      const month = selectedMonthEl.value;
      location.href = "/api/metrics/export?month=" + month + "&format=json";
    });

    graphDefs.forEach(addCard);
    Promise.all(graphDefs.map(loadChart)).then(loadTimeline);
  })();
  </script>
</body>
</html>
"""
    month_options = "".join(
        [
            '<option value="{0}"{1}>{0}</option>'.format(
                month_key, " selected" if month_key == selected_month else ""
            )
            for month_key in (page_months or [selected_month])
        ]
    )
    html = (
        html.replace("__START_DATE__", start_date.isoformat())
        .replace("__END_DATE__", end_date.isoformat())
        .replace("__PER_PAGE__", str(per_page))
        .replace("__PAGE__", str(page))
        .replace("__TOTAL_PAGES__", str(total_pages))
        .replace("__MONTH_COUNT__", str(len(months)))
        .replace("__DATA_QUERY__", data_query)
        .replace("__MONTH_OPTIONS__", month_options)
    )
    handler.send_response(200)
    handler.send_header("Content-type", "text/html; charset=utf-8")
    handler.end_headers()
    handler.wfile.write(html.encode("utf-8"))


def _write_json(handler, payload, status_code=200):
    handler.send_response(status_code)
    handler.send_header("Content-type", APPLICATION_JSON)
    handler.end_headers()
    handler.wfile.write(json.dumps(payload).encode("utf-8"))


def _graph_badge_scans_per_hour(events):
    data = [0] * 24
    for event in events:
        if event.get("event_type") != "Badge Scan":
            continue
        ts = _event_ts(event)
        if ts is None:
            continue
        data[ts.hour] += 1
    return {"labels": [str(i).zfill(2) for i in range(24)], "values": data}


def _graph_door_open_duration(events):
    durations = _door_open_durations(events)
    bucket = defaultdict(list)
    for ts, value in durations:
        bucket[ts.date().isoformat()].append(value)
    labels = sorted(bucket.keys())
    values = [round(sum(bucket[k]) / float(len(bucket[k])), 3) for k in labels]
    return {"labels": labels, "values": values}


def _graph_top_badge_users(events):
    counts = defaultdict(int)
    for event in events:
        if event.get("event_type") == "Badge Scan" and _status_is(event, "Granted"):
            badge_id = event.get("badge_id") or "unknown"
            counts[badge_id] += 1
    ordered = sorted(counts.items(), key=lambda item: (-item[1], item[0]))[:10]
    return {"labels": [x[0] for x in ordered], "values": [x[1] for x in ordered]}


def _graph_door_cycles_per_day(events):
    counts = _group_count(events, lambda e: _event_ts(e).date().isoformat() if e.get("event_type") == "Door OPEN/UNLOCKED" and _event_ts(e) else None)
    labels = sorted(counts.keys())
    return {"labels": labels, "values": [counts[x] for x in labels]}


def _graph_denied_badge_scans(events):
    counts = _group_count(
        events,
        lambda e: _event_ts(e).date().isoformat()
        if e.get("event_type") == "Badge Scan" and _status_is(e, "Denied") and _event_ts(e)
        else None,
    )
    labels = sorted(counts.keys())
    return {"labels": labels, "values": [counts[x] for x in labels]}


def _graph_badge_scan_latency(events):
    scans_by_badge = defaultdict(deque)
    pairs = []
    for event in events:
        ts = _event_ts(event)
        if ts is None:
            continue
        event_type = event.get("event_type")
        badge_id = event.get("badge_id")
        if event_type == "Badge Scan" and _status_is(event, "Granted") and badge_id:
            scans_by_badge[badge_id].append(ts)
            continue
        if event_type == "Door OPEN/UNLOCKED" and badge_id:
            q = scans_by_badge.get(badge_id)
            if q and len(q) > 0:
                scan_ts = q.popleft()
                if ts >= scan_ts:
                    pairs.append((ts, (ts - scan_ts).total_seconds()))
    labels = [p[0].strftime("%Y-%m-%d %H:%M:%S") for p in pairs]
    values = [round(p[1], 3) for p in pairs]
    return {"labels": labels, "values": values}


def _graph_manual_events(events):
    unlock_counts = defaultdict(int)
    lock_counts = defaultdict(int)
    for event in events:
        ts = _event_ts(event)
        if ts is None:
            continue
        day_key = ts.date().isoformat()
        et = event.get("event_type")
        if et == "Manual Unlock (1 hour)":
            unlock_counts[day_key] += 1
        elif et == "Manual Lock":
            lock_counts[day_key] += 1
    labels = sorted(set(list(unlock_counts.keys()) + list(lock_counts.keys())))
    values = [unlock_counts[d] + lock_counts[d] for d in labels]
    return {"labels": labels, "values": values}


def _graph_door_left_open(events):
    threshold = max(30, int(config.get("DOOR_UNLOCK_BADGE_DURATION", 5)) * 2)
    durations = _door_open_durations(events)
    over = defaultdict(int)
    for ts, value in durations:
        if value > threshold:
            over[ts.date().isoformat()] += 1
    labels = sorted(over.keys())
    return {"labels": labels, "values": [over[d] for d in labels], "threshold_seconds": threshold}


def _graph_heatmap(events):
    grid = [[0 for _h in range(24)] for _d in range(7)]
    for event in events:
        ts = _event_ts(event)
        if ts is None:
            continue
        grid[ts.weekday()][ts.hour] += 1
    labels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]
    # Flatten to keep Chart.js rendering lightweight.
    flat_labels = []
    flat_values = []
    for day_index, day_name in enumerate(labels):
        for hour in range(24):
            flat_labels.append("{0}-{1:02d}".format(day_name, hour))
            flat_values.append(grid[day_index][hour])
    return {"labels": flat_labels, "values": flat_values}


def _timeline(events, query):
    page = _parse_int(query.get("page", ["1"])[0], 1)
    page_size = _parse_int(query.get("page_size", ["50"])[0], 50, minimum=1, maximum=500)
    total = len(events)
    total_pages = max(1, int(math.ceil(total / float(page_size)))) if total else 1
    page = min(page, total_pages)
    start_idx = (page - 1) * page_size
    end_idx = start_idx + page_size
    items = events[start_idx:end_idx]
    return {
        "page": page,
        "page_size": page_size,
        "total": total,
        "total_pages": total_pages,
        "items": items,
    }


_GRAPH_HANDLERS = {
    "badge-scans-per-hour": _graph_badge_scans_per_hour,
    "door-open-duration": _graph_door_open_duration,
    "top-badge-users": _graph_top_badge_users,
    "door-cycles-per-day": _graph_door_cycles_per_day,
    "denied-badge-scans": _graph_denied_badge_scans,
    "badge-scan-door-open-latency": _graph_badge_scan_latency,
    "manual-events": _graph_manual_events,
    "door-left-open-too-long": _graph_door_left_open,
    "hourly-activity-heatmap": _graph_heatmap,
}


def handle_metrics_api_get(handler, path: str, raw_query: str) -> bool:
    """Handle GET /api/metrics/* endpoints."""
    query = _query_parts(raw_query)
    if path == "/api/metrics/export":
        month_key = query.get("month", [""])[0]
        fmt = query.get("format", ["json"])[0].lower()
        if not month_key:
            _write_json(handler, {"error": "month is required"}, status_code=400)
            return True
        events = query_month_events(month_key)
        if fmt == "csv":
            payload = month_events_to_csv(events)
            handler.send_response(200)
            handler.send_header("Content-type", "text/csv; charset=utf-8")
            handler.send_header("Content-Disposition", 'attachment; filename="metrics-{0}.csv"'.format(month_key))
            handler.end_headers()
            handler.wfile.write(payload.encode("utf-8"))
            return True
        if fmt == "json":
            handler.send_response(200)
            handler.send_header("Content-type", "application/json; charset=utf-8")
            handler.send_header("Content-Disposition", 'attachment; filename="metrics-{0}.json"'.format(month_key))
            handler.end_headers()
            handler.wfile.write(json.dumps(events).encode("utf-8"))
            return True
        _write_json(handler, {"error": "format must be csv or json"}, status_code=400)
        return True

    if path == "/api/metrics/full-event-timeline":
        _start, _end, events = _filter_events(query)
        _write_json(handler, _timeline(events, query))
        return True

    graph_key = path.replace("/api/metrics/", "", 1)
    graph_fn = _GRAPH_HANDLERS.get(graph_key)
    if graph_fn is None:
        return False
    _start, _end, events = _filter_events(query)
    _write_json(handler, graph_fn(events))
    return True
