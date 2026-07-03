from __future__ import annotations

import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "backend"))

import main  # noqa: E402


@pytest.fixture(autouse=True)
def clear_state() -> None:
    main.snapshots.clear()
    main.events.clear()
    main.capabilities.clear()
    main.audit_records.clear()


@pytest.fixture()
def client() -> TestClient:
    return TestClient(main.app)


def transparent_field(value: object, tick: int = 100) -> dict[str, object]:
    return {
        "value": value,
        "status": "available",
        "source": {"kind": "game_object", "path": "Game1.player"},
        "adapter": "vanilla",
        "read_at_tick": tick,
        "confidence": 1.0,
    }


def sample_snapshot() -> dict[str, object]:
    return {
        "schema_version": "snapshot.v1",
        "bridge_version": "0.1.0",
        "game_tick": 100,
        "real_timestamp": "2026-07-03T14:00:00Z",
        "state_hash": "hash-100",
        "completeness": "partial",
        "unavailable_fields": ["world.npcs"],
        "state": {
            "game": {
                "current_location": transparent_field("Farm"),
            },
            "player": {
                "money": transparent_field(500),
                "stamina": transparent_field(270),
            },
            "farm": {},
            "locations": {},
            "npcs": {},
            "quests": {},
            "world_progress": {},
            "menus": {},
            "mods": {},
            "modded_state": {},
        },
    }


def test_ingests_snapshot_event_capability_and_syncs(client: TestClient) -> None:
    snapshot_response = client.post("/api/v1/snapshots", json=sample_snapshot())
    assert snapshot_response.status_code == 200
    assert snapshot_response.json() == {"accepted": True, "state_hash": "hash-100"}

    event = {
        "schema_version": "event.v1",
        "event_id": "evt-1",
        "event_type": "LocationChanged",
        "game_tick": 101,
        "in_game_time": {"day": 1, "time": 610},
        "real_timestamp": "2026-07-03T14:00:05Z",
        "source": "SMAPI.GameLoop",
        "state_hash_before": "hash-100",
        "state_hash_after": "hash-101",
    }
    assert client.post("/api/v1/events", json=event).status_code == 200

    capability = {
        "capability_id": "read.player.basic",
        "access_mode": "read",
        "status": "available",
        "source": {"adapter": "vanilla"},
        "limitations": [],
        "required_permission": "observer",
        "supported_game_versions": ["1.6"],
        "supported_mods": [],
        "known_conflicts": [],
    }
    assert client.post("/api/v1/capabilities", json=capability).status_code == 200

    sync_response = client.get("/api/v1/sync?after_tick=100")
    assert sync_response.status_code == 200
    sync_payload = sync_response.json()
    assert sync_payload["latest_snapshot"]["state_hash"] == "hash-100"
    assert sync_payload["snapshot_count"] == 1
    assert sync_payload["event_count"] == 1
    assert sync_payload["capability_count"] == 1
    assert sync_payload["events"][0]["event_id"] == "evt-1"
    assert sync_payload["capabilities"][0]["capability_id"] == "read.player.basic"
    assert [record["event_type"] for record in sync_payload["audit_head"]] == [
        "SnapshotIngested",
        "EventIngested",
        "CapabilityIngested",
    ]


def test_snapshot_rejects_state_without_transparent_metadata(client: TestClient) -> None:
    snapshot = sample_snapshot()
    snapshot["state"]["player"]["money"] = 500

    response = client.post("/api/v1/snapshots", json=snapshot)

    assert response.status_code == 422
    detail = str(response.json()["detail"])
    assert "snapshot" in detail
    assert "state" in detail


def test_event_schema_validation_reports_missing_required_field(client: TestClient) -> None:
    response = client.post(
        "/api/v1/events",
        json={
            "schema_version": "event.v1",
            "event_id": "evt-1",
            "event_type": "LocationChanged",
            "game_tick": 101,
            "real_timestamp": "2026-07-03T14:00:05Z",
            "source": "SMAPI.GameLoop",
        },
    )

    assert response.status_code == 422
    assert "event schema validation failed" in response.json()["detail"]
    assert "in_game_time" in response.json()["detail"]


def test_stardew_input_endpoint_normalizes_latest_snapshot(client: TestClient) -> None:
    assert client.post("/api/v1/snapshots", json=sample_snapshot()).status_code == 200

    response = client.get("/api/v1/stardew/input/latest?goal=water%20crops&mode=efficiency")

    assert response.status_code == 200
    payload = response.json()
    assert payload["state_hash"] == "hash-100"
    assert payload["mode"] == "efficiency"
    assert payload["facts"]["game.current_location"] == "Farm"
    assert "farm.crops" in payload["unavailable_required_fields"]


def test_action_compiler_outputs_non_executable_command_preview(client: TestClient) -> None:
    assert client.post("/api/v1/snapshots", json=sample_snapshot()).status_code == 200

    response = client.post(
        "/api/v1/action-compiler/compile",
        json={"goal": "water crops today", "mode": "efficiency"},
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["schema_version"] == "action_compiler.v1"
    assert payload["intent"] == "farm.maintain_crops"
    assert payload["compiler_status"] == "blocked"
    assert payload["options"][0]["id"] == "option.farm.maintain_crops.preview"
    assert payload["command_previews"][0]["permission_required"] == "planner"
    assert payload["command_previews"][0]["executable"] is False
    assert payload["diagnostics"]["command_schema_errors"] == []


def test_action_compiler_check_reports_loaded_compiler(client: TestClient) -> None:
    assert client.post("/api/v1/snapshots", json=sample_snapshot()).status_code == 200

    response = client.get("/api/v1/action-compiler/check")

    assert response.status_code == 200
    payload = response.json()
    assert payload["status"] == "ok"
    assert payload["compiler_loaded"] is True
    assert payload["command_preview_count"] == 1
