from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


app = FastAPI(title="StardewAI Backend State Store", version="0.1.0")


class StoredSnapshot(BaseModel):
    schema_version: str
    bridge_version: str
    game_tick: int
    real_timestamp: datetime
    state_hash: str
    state: dict[str, Any]
    raw: dict[str, Any] = Field(default_factory=dict)


snapshots: dict[str, StoredSnapshot] = {}
events: list[dict[str, Any]] = []
audit_records: list[dict[str, Any]] = []


@app.get("/health")
def health() -> dict[str, Any]:
    return {
        "status": "ok",
        "service": "stardewai-backend",
        "timestamp": datetime.now(timezone.utc).isoformat(),
    }


@app.post("/api/v1/snapshots")
def ingest_snapshot(snapshot: dict[str, Any]) -> dict[str, Any]:
    state_hash = snapshot.get("state_hash")
    if not state_hash:
        raise HTTPException(status_code=400, detail="snapshot.state_hash is required")

    stored = StoredSnapshot(
        schema_version=snapshot.get("schema_version", "unknown"),
        bridge_version=snapshot.get("bridge_version", "unknown"),
        game_tick=int(snapshot.get("game_tick", 0)),
        real_timestamp=_parse_timestamp(snapshot.get("real_timestamp")),
        state_hash=state_hash,
        state=snapshot.get("state", {}),
        raw=snapshot,
    )
    snapshots[state_hash] = stored
    audit_records.append(
        {
            "event_type": "SnapshotIngested",
            "state_hash": state_hash,
            "real_timestamp": datetime.now(timezone.utc).isoformat(),
        }
    )
    return {"accepted": True, "state_hash": state_hash}


@app.get("/api/v1/snapshots/latest")
def latest_snapshot() -> StoredSnapshot:
    if not snapshots:
        raise HTTPException(status_code=404, detail="no snapshots ingested")
    return max(snapshots.values(), key=lambda item: item.real_timestamp)


@app.post("/api/v1/events")
def ingest_event(event: dict[str, Any]) -> dict[str, Any]:
    if "event_type" not in event:
        raise HTTPException(status_code=400, detail="event.event_type is required")
    events.append(event)
    return {"accepted": True, "count": len(events)}


@app.get("/api/v1/audit")
def audit(limit: int = 100) -> list[dict[str, Any]]:
    return audit_records[-limit:]


def _parse_timestamp(value: Any) -> datetime:
    if isinstance(value, str):
        normalized = value.replace("Z", "+00:00")
        return datetime.fromisoformat(normalized)
    return datetime.now(timezone.utc)
