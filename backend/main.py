from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from uuid import uuid4

from fastapi import FastAPI, HTTPException
from jsonschema import Draft202012Validator, FormatChecker
from jsonschema.exceptions import ValidationError
from pydantic import BaseModel, Field

from action_compiler import build_stardew_input, compile_actions


app = FastAPI(title="StardewAI Backend State Store", version="0.1.0")
SCHEMA_DIR = Path(__file__).resolve().parents[1] / "schemas" / "json"
TRANSPARENT_FIELD_KEYS = {
    "value",
    "status",
    "source",
    "adapter",
    "read_at_tick",
    "confidence",
}


def _load_schema(name: str) -> dict[str, Any]:
    import json

    with (SCHEMA_DIR / name).open("r", encoding="utf-8") as schema_file:
        return json.load(schema_file)


schema_validators = {
    "snapshot": Draft202012Validator(
        _load_schema("snapshot.schema.json"),
        format_checker=FormatChecker(),
    ),
    "event": Draft202012Validator(
        _load_schema("event.schema.json"),
        format_checker=FormatChecker(),
    ),
    "capability": Draft202012Validator(
        _load_schema("capability.schema.json"),
        format_checker=FormatChecker(),
    ),
    "command": Draft202012Validator(
        _load_schema("command.schema.json"),
        format_checker=FormatChecker(),
    ),
}


class StoredSnapshot(BaseModel):
    schema_version: str
    bridge_version: str
    game_tick: int
    real_timestamp: datetime
    state_hash: str
    state: dict[str, Any]
    raw: dict[str, Any] = Field(default_factory=dict)


class StoredEvent(BaseModel):
    event_id: str
    event_type: str
    schema_version: str
    game_tick: int
    real_timestamp: datetime
    source: str
    raw: dict[str, Any] = Field(default_factory=dict)


snapshots: dict[str, StoredSnapshot] = {}
events: list[StoredEvent] = []
capabilities: dict[str, dict[str, Any]] = {}
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
    _validate_payload("snapshot", snapshot)
    metadata_errors = _transparent_metadata_errors(snapshot.get("state", {}))
    if metadata_errors:
        raise HTTPException(
            status_code=422,
            detail={
                "message": "snapshot.state fields must include transparent metadata",
                "errors": metadata_errors,
            },
        )

    stored = StoredSnapshot(
        schema_version=snapshot["schema_version"],
        bridge_version=snapshot["bridge_version"],
        game_tick=int(snapshot["game_tick"]),
        real_timestamp=_parse_timestamp(snapshot.get("real_timestamp")),
        state_hash=snapshot["state_hash"],
        state=snapshot["state"],
        raw=snapshot,
    )
    snapshots[stored.state_hash] = stored
    _append_audit(
        "SnapshotIngested",
        stored.game_tick,
        stored.state_hash,
        {"schema_version": stored.schema_version},
    )
    return {"accepted": True, "state_hash": stored.state_hash}


@app.get("/api/v1/snapshots/latest")
def latest_snapshot() -> StoredSnapshot:
    if not snapshots:
        raise HTTPException(status_code=404, detail="no snapshots ingested")
    return max(snapshots.values(), key=lambda item: item.real_timestamp)


@app.post("/api/v1/events")
def ingest_event(event: dict[str, Any]) -> dict[str, Any]:
    _validate_payload("event", event)
    stored = StoredEvent(
        event_id=event["event_id"],
        event_type=event["event_type"],
        schema_version=event["schema_version"],
        game_tick=int(event["game_tick"]),
        real_timestamp=_parse_timestamp(event["real_timestamp"]),
        source=event["source"],
        raw=event,
    )
    events.append(stored)
    _append_audit(
        "EventIngested",
        stored.game_tick,
        event.get("state_hash_after") or event.get("state_hash_before") or "",
        {"event_id": stored.event_id, "event_type": stored.event_type},
    )
    return {"accepted": True, "count": len(events)}


@app.get("/api/v1/events")
def list_events(after_tick: int | None = None, limit: int = 100) -> list[StoredEvent]:
    selected = events
    if after_tick is not None:
        selected = [event for event in selected if event.game_tick > after_tick]
    return selected[-limit:]


@app.post("/api/v1/capabilities")
def ingest_capabilities(payload: dict[str, Any] | list[dict[str, Any]]) -> dict[str, Any]:
    items = payload if isinstance(payload, list) else [payload]
    accepted: list[str] = []
    for capability in items:
        _validate_payload("capability", capability)
        capabilities[capability["capability_id"]] = capability
        accepted.append(capability["capability_id"])
        _append_audit(
            "CapabilityIngested",
            _latest_game_tick(),
            _latest_state_hash(),
            {
                "capability_id": capability["capability_id"],
                "status": capability["status"],
                "access_mode": capability["access_mode"],
            },
        )
    return {"accepted": True, "count": len(accepted), "capability_ids": accepted}


@app.get("/api/v1/capabilities")
def list_capabilities() -> list[dict[str, Any]]:
    return list(capabilities.values())


@app.get("/api/v1/audit")
def audit(limit: int = 100) -> list[dict[str, Any]]:
    return audit_records[-limit:]


@app.get("/api/v1/sync")
def sync(after_tick: int | None = None) -> dict[str, Any]:
    latest = _latest_snapshot_or_none()
    selected_events = events
    if after_tick is not None:
        selected_events = [event for event in selected_events if event.game_tick > after_tick]

    return {
        "latest_snapshot": latest,
        "snapshot_count": len(snapshots),
        "event_count": len(events),
        "capability_count": len(capabilities),
        "events": selected_events,
        "capabilities": list(capabilities.values()),
        "audit_head": audit_records[-10:],
    }


@app.get("/api/v1/stardew/input/latest")
def latest_stardew_input(goal: str = "", mode: str = "relaxed") -> dict[str, Any]:
    latest = _latest_snapshot_or_none()
    if latest is None:
        raise HTTPException(status_code=404, detail="no snapshots ingested")
    return build_stardew_input(latest, goal, mode)


@app.post("/api/v1/action-compiler/compile")
def compile_action(payload: dict[str, Any]) -> dict[str, Any]:
    snapshot_hash = payload.get("state_hash")
    latest = snapshots.get(snapshot_hash) if isinstance(snapshot_hash, str) else _latest_snapshot_or_none()
    if latest is None:
        raise HTTPException(status_code=404, detail="no matching snapshot available")

    result = compile_actions(latest, payload, schema_validators["command"])
    _append_audit(
        "ActionCompilerPreviewed",
        latest.game_tick,
        latest.state_hash,
        {
            "compiler_status": result["compiler_status"],
            "intent": result["intent"],
            "option_count": len(result["options"]),
            "command_preview_count": len(result["command_previews"]),
        },
    )
    return result


@app.get("/api/v1/action-compiler/check")
def check_action_compiler() -> dict[str, Any]:
    latest = _latest_snapshot_or_none()
    if latest is None:
        return {
            "status": "blocked",
            "reason": "no_snapshots_ingested",
            "compiler_loaded": True,
        }

    result = compile_actions(
        latest,
        {"goal": "stabilize current day", "mode": "recovery"},
        schema_validators["command"],
    )
    return {
        "status": "ok" if not result["diagnostics"]["command_schema_errors"] else "invalid",
        "compiler_loaded": True,
        "compiler_status": result["compiler_status"],
        "state_hash": latest.state_hash,
        "command_preview_count": len(result["command_previews"]),
        "command_schema_errors": result["diagnostics"]["command_schema_errors"],
    }


def _parse_timestamp(value: Any) -> datetime:
    if isinstance(value, str):
        normalized = value.replace("Z", "+00:00")
        return datetime.fromisoformat(normalized)
    return datetime.now(timezone.utc)


def _validate_payload(kind: str, payload: dict[str, Any]) -> None:
    try:
        schema_validators[kind].validate(payload)
    except ValidationError as exc:
        path = ".".join(str(part) for part in exc.absolute_path)
        location = path or "<root>"
        raise HTTPException(
            status_code=422,
            detail=f"{kind} schema validation failed at {location}: {exc.message}",
        ) from exc


def _transparent_metadata_errors(value: Any, path: str = "state") -> list[str]:
    errors: list[str] = []
    if isinstance(value, dict):
        keys = set(value)
        if keys & TRANSPARENT_FIELD_KEYS:
            missing = sorted(TRANSPARENT_FIELD_KEYS - keys)
            if missing:
                errors.append(f"{path} missing metadata keys: {', '.join(missing)}")
            _validate_transparent_field_types(value, path, errors)
            return errors

        for key, child in value.items():
            child_path = f"{path}.{key}"
            if isinstance(child, (dict, list)):
                errors.extend(_transparent_metadata_errors(child, child_path))
            else:
                errors.append(f"{child_path} must be a transparent field object")
        return errors

    if isinstance(value, list):
        for index, child in enumerate(value):
            child_path = f"{path}[{index}]"
            if isinstance(child, (dict, list)):
                errors.extend(_transparent_metadata_errors(child, child_path))
            else:
                errors.append(f"{child_path} must be a transparent field object")
        return errors

    errors.append(f"{path} must be an object or array")
    return errors


def _validate_transparent_field_types(
    field: dict[str, Any],
    path: str,
    errors: list[str],
) -> None:
    if "status" in field and field["status"] not in {"available", "derived", "unavailable"}:
        errors.append(f"{path}.status must be available, derived, or unavailable")
    if "adapter" in field and not isinstance(field["adapter"], str):
        errors.append(f"{path}.adapter must be a string")
    if "read_at_tick" in field and not isinstance(field["read_at_tick"], int):
        errors.append(f"{path}.read_at_tick must be an integer")
    confidence = field.get("confidence")
    if "confidence" in field and (
        not isinstance(confidence, (int, float)) or confidence < 0 or confidence > 1
    ):
        errors.append(f"{path}.confidence must be a number between 0 and 1")


def _append_audit(
    event_type: str,
    game_tick: int,
    state_hash: str,
    details: dict[str, Any],
) -> None:
    audit_records.append(
        {
            "schema_version": "audit.v1",
            "event_id": str(uuid4()),
            "event_type": event_type,
            "real_timestamp": datetime.now(timezone.utc).isoformat(),
            "game_tick": game_tick,
            "state_hash": state_hash,
            "details": details,
        }
    )


def _latest_snapshot_or_none() -> StoredSnapshot | None:
    if not snapshots:
        return None
    return max(snapshots.values(), key=lambda item: item.real_timestamp)


def _latest_game_tick() -> int:
    latest = _latest_snapshot_or_none()
    return latest.game_tick if latest else 0


def _latest_state_hash() -> str:
    latest = _latest_snapshot_or_none()
    return latest.state_hash if latest else ""
