"""SQLite-backed monthly metrics storage and cross-month query helpers."""
import csv
import os
import re
import sqlite3
from datetime import date, datetime
from io import StringIO
from typing import Dict, List, Optional, Sequence, Tuple

from .config import config

EVENTS_TABLE_SQL = """
CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    ts TEXT NOT NULL,
    event_type TEXT NOT NULL,
    badge_id TEXT,
    status TEXT NOT NULL,
    raw_message TEXT NOT NULL
);
"""

EVENTS_INDEX_SQL = (
    "CREATE INDEX IF NOT EXISTS idx_events_ts ON events(ts);",
    "CREATE INDEX IF NOT EXISTS idx_events_event_type ON events(event_type);",
    "CREATE INDEX IF NOT EXISTS idx_events_badge_id ON events(badge_id);",
)

_ACTION_LINE_RE = re.compile(
    r"^(?P<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) - [^-]+ - [A-Z]+ - (?P<message>.*)$"
)
_BADGE_PART = " - Badge: "
_STATUS_PART = " - Status: "


def get_metrics_base_path() -> str:
    """Return configured base path for metrics db files."""
    return str(config.get("METRICS_DB_PATH", "logs/metrics"))


def _month_key_for_datetime(ts: datetime) -> str:
    return ts.strftime("%Y-%m")


def get_month_db_path(month_key: str, base_path: Optional[str] = None) -> str:
    """Return monthly db path in year/year-month.db format."""
    base = base_path or get_metrics_base_path()
    year = month_key.split("-")[0]
    return os.path.join(base, year, "{0}.db".format(month_key))


def ensure_month_db(month_key: str, base_path: Optional[str] = None) -> str:
    """Create month db/schema if missing and return its path."""
    db_path = get_month_db_path(month_key, base_path=base_path)
    db_dir = os.path.dirname(db_path)
    if db_dir:
        os.makedirs(db_dir, exist_ok=True)
    conn = sqlite3.connect(db_path)
    try:
        conn.execute(EVENTS_TABLE_SQL)
        for stmt in EVENTS_INDEX_SQL:
            conn.execute(stmt)
        conn.commit()
    finally:
        conn.close()
    return db_path


def _parse_action_message(message: str) -> Optional[Dict[str, str]]:
    badge_id = None
    event_type = None
    status = "Unknown"

    if _BADGE_PART in message and _STATUS_PART in message:
        left, right = message.split(_BADGE_PART, 1)
        badge_part, status_part = right.rsplit(_STATUS_PART, 1)
        event_type = left.strip()
        badge_id = badge_part.strip() or None
        status = status_part.strip() or "Unknown"
    elif _STATUS_PART in message:
        left, status_part = message.rsplit(_STATUS_PART, 1)
        event_type = left.strip()
        status = status_part.strip() or "Unknown"

    if not event_type:
        return None

    return {
        "event_type": event_type,
        "badge_id": badge_id,
        "status": status,
    }


def parse_action_log_line(line: str) -> Optional[Dict[str, str]]:
    """Parse action log line into normalized event dict."""
    raw = line.strip()
    if not raw:
        return None

    match = _ACTION_LINE_RE.match(raw)
    if not match:
        return None

    parsed = _parse_action_message(match.group("message"))
    if parsed is None:
        return None

    parsed["ts"] = match.group("ts")
    parsed["raw_message"] = raw
    return parsed


def ingest_action_log_file(path: str, base_path: Optional[str] = None) -> int:
    """
    Parse a dated action log file and persist events into monthly sqlite dbs.

    Returns:
        Number of inserted records.
    """
    if not os.path.exists(path):
        return 0

    inserted = 0
    conns: Dict[str, sqlite3.Connection] = {}
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as handle:
            for line in handle:
                parsed = parse_action_log_line(line)
                if parsed is None:
                    continue
                ts = datetime.strptime(parsed["ts"], "%Y-%m-%d %H:%M:%S")
                month_key = _month_key_for_datetime(ts)
                db_path = ensure_month_db(month_key, base_path=base_path)
                conn = conns.get(db_path)
                if conn is None:
                    conn = sqlite3.connect(db_path)
                    conns[db_path] = conn
                conn.execute(
                    """
                    INSERT INTO events (ts, event_type, badge_id, status, raw_message)
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    (
                        parsed["ts"],
                        parsed["event_type"],
                        parsed.get("badge_id"),
                        parsed["status"],
                        parsed["raw_message"],
                    ),
                )
                inserted += 1
        for conn in conns.values():
            conn.commit()
    finally:
        for conn in conns.values():
            conn.close()
    return inserted


def month_keys_in_range(start_date: date, end_date: date) -> List[str]:
    """Return inclusive YYYY-MM keys spanning start_date..end_date."""
    if end_date < start_date:
        return []
    months: List[str] = []
    cur = date(start_date.year, start_date.month, 1)
    final = date(end_date.year, end_date.month, 1)
    while cur <= final:
        months.append("{0:04d}-{1:02d}".format(cur.year, cur.month))
        if cur.month == 12:
            cur = date(cur.year + 1, 1, 1)
        else:
            cur = date(cur.year, cur.month + 1, 1)
    return months


def db_paths_in_range(start_date: date, end_date: date, base_path: Optional[str] = None) -> List[str]:
    """Return existing db paths in range; create current-month db when missing."""
    paths: List[str] = []
    now_key = datetime.now().strftime("%Y-%m")
    for month_key in month_keys_in_range(start_date, end_date):
        db_path = get_month_db_path(month_key, base_path=base_path)
        if os.path.exists(db_path):
            paths.append(db_path)
            continue
        if month_key == now_key:
            ensure_month_db(month_key, base_path=base_path)
            paths.append(db_path)
    return paths


def attach_databases(conn: sqlite3.Connection, db_paths: Sequence[str]) -> List[str]:
    """Attach db files and return aliases in attachment order."""
    aliases: List[str] = []
    for idx, path in enumerate(db_paths):
        alias = "m{0}".format(idx)
        conn.execute("ATTACH DATABASE ? AS {0}".format(alias), (path,))
        aliases.append(alias)
    return aliases


def build_union_all_query(aliases: Sequence[str], where_clause: str = "") -> str:
    """Build SELECT ... UNION ALL query body over attached monthly db aliases."""
    if not aliases:
        return (
            "SELECT ts, event_type, badge_id, status, raw_message "
            "FROM (SELECT 1 AS x) WHERE 1=0"
        )
    select_parts = [
        "SELECT ts, event_type, badge_id, status, raw_message FROM {0}.events {1}".format(
            alias, where_clause
        )
        for alias in aliases
    ]
    return " UNION ALL ".join(select_parts)


def _event_row(row: Tuple[str, str, Optional[str], str, str]) -> Dict[str, Optional[str]]:
    return {
        "ts": row[0],
        "event_type": row[1],
        "badge_id": row[2],
        "status": row[3],
        "raw_message": row[4],
    }


def query_events_range(
    start_ts: str,
    end_ts: str,
    event_types: Optional[Sequence[str]] = None,
) -> List[Dict[str, Optional[str]]]:
    """Query normalized events across monthly databases in timestamp range."""
    start_date = datetime.strptime(start_ts, "%Y-%m-%d %H:%M:%S").date()
    end_date = datetime.strptime(end_ts, "%Y-%m-%d %H:%M:%S").date()
    db_paths = db_paths_in_range(start_date, end_date)
    if not db_paths:
        return []

    conn = sqlite3.connect(":memory:")
    try:
        aliases = attach_databases(conn, db_paths)
        where = "WHERE ts >= ? AND ts <= ?"
        params: List[str] = []
        if event_types:
            placeholders = ",".join(["?"] * len(event_types))
            where += " AND event_type IN ({0})".format(placeholders)

        union_sql = build_union_all_query(aliases, where_clause=where)
        sql = "SELECT ts, event_type, badge_id, status, raw_message FROM ({0}) ORDER BY ts ASC".format(
            union_sql
        )
        for _alias in aliases:
            params.extend([start_ts, end_ts])
            if event_types:
                params.extend(event_types)

        rows = conn.execute(sql, tuple(params)).fetchall()
        return [_event_row(row) for row in rows]
    finally:
        conn.close()


def query_month_events(month_key: str) -> List[Dict[str, Optional[str]]]:
    """Return all events from a specific month db."""
    db_path = get_month_db_path(month_key)
    if not os.path.exists(db_path):
        return []
    conn = sqlite3.connect(db_path)
    try:
        rows = conn.execute(
            "SELECT ts, event_type, badge_id, status, raw_message FROM events ORDER BY ts ASC"
        ).fetchall()
        return [_event_row(row) for row in rows]
    finally:
        conn.close()


def month_events_to_csv(events: Sequence[Dict[str, Optional[str]]]) -> str:
    """Serialize event records to CSV."""
    output = StringIO()
    writer = csv.writer(output)
    writer.writerow(["ts", "event_type", "badge_id", "status", "raw_message"])
    for item in events:
        writer.writerow(
            [
                item.get("ts"),
                item.get("event_type"),
                item.get("badge_id"),
                item.get("status"),
                item.get("raw_message"),
            ]
        )
    return output.getvalue()
