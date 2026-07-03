from __future__ import annotations

from dataclasses import dataclass
from typing import Any
from uuid import uuid4


FieldPath = tuple[str, ...]


@dataclass(frozen=True)
class StardewCompilerInput:
    state_hash: str
    game_tick: int
    user_goal: str
    mode: str
    facts: dict[str, Any]
    unavailable_required_fields: list[str]


def build_stardew_input(
    snapshot: Any,
    user_goal: str = "",
    mode: str = "relaxed",
) -> dict[str, Any]:
    state = snapshot.state
    facts = {
        "game.current_location": _field_value(state, ("game", "current_location")),
        "game.time_of_day": _field_value(state, ("game", "time_of_day")),
        "player.money": _field_value(state, ("player", "money")),
        "player.stamina": _field_value(state, ("player", "stamina")),
        "player.inventory": _field_value(state, ("player", "inventory")),
        "menus.active_menu": _field_value(state, ("menus", "active_menu")),
    }
    required = _required_fields_for_goal(user_goal)
    unavailable = [
        ".".join(path)
        for path in required
        if not _field_available(state, path)
    ]

    return {
        "state_hash": snapshot.state_hash,
        "game_tick": snapshot.game_tick,
        "user_goal": user_goal,
        "mode": mode,
        "facts": facts,
        "unavailable_required_fields": unavailable,
    }


def compile_actions(
    snapshot: Any,
    payload: dict[str, Any],
    command_validator: Any,
) -> dict[str, Any]:
    user_goal = str(payload.get("goal") or payload.get("user_goal") or "").strip()
    mode = str(payload.get("mode") or "relaxed")
    compiler_input = build_stardew_input(snapshot, user_goal, mode)
    intent = _classify_goal(user_goal)

    options = _candidate_options(intent, compiler_input)
    command_previews = [
        _command_preview(option, compiler_input)
        for option in options
    ]

    validation_errors: list[str] = []
    for preview in command_previews:
        errors = sorted(command_validator.iter_errors(preview), key=lambda item: item.path)
        validation_errors.extend(error.message for error in errors)

    blocked = any(not preview["executable"] for preview in command_previews)
    status = "invalid" if validation_errors else "blocked" if blocked else "ok"

    return {
        "schema_version": "action_compiler.v1",
        "compiler_status": status,
        "input": compiler_input,
        "intent": intent,
        "options": options,
        "command_previews": command_previews,
        "diagnostics": {
            "command_schema_errors": validation_errors,
            "unavailable_required_fields": compiler_input["unavailable_required_fields"],
            "execution_policy": "preview_only",
        },
    }


def _candidate_options(intent: str, compiler_input: dict[str, Any]) -> list[dict[str, Any]]:
    base = {
        "domain": intent.split(".", 1)[0],
        "duration_model": {
            "unit": "in_game_minutes",
            "estimate": 30,
            "confidence": 0.4,
        },
        "policy": "human_executor_preview_only",
        "abort_conditions": ["state_hash_mismatch", "required_field_unavailable"],
        "recovery_policy": "stop_and_request_replan",
        "required_state_factors": _required_field_names_for_intent(intent),
        "reversible": True,
        "irreversible_risk_class": "low",
    }

    templates = {
        "farm.maintain_crops": {
            "id": "option.farm.maintain_crops.preview",
            "name": "Maintain farm crops",
            "initiation_conditions": ["farm state available", "player stamina known"],
            "goal_conditions": ["crop obligations inspected"],
            "estimated_effects": ["may consume stamina", "may advance crop readiness"],
            "success_conditions": ["player confirms crop work completed"],
            "safety_constraints": ["block_unavailable_required_state", "block_unverified_movement"],
        },
        "economy.buy_supplies": {
            "id": "option.economy.buy_supplies.preview",
            "name": "Preview supply purchase",
            "initiation_conditions": ["money known", "shop availability known"],
            "goal_conditions": ["purchase list verified"],
            "estimated_effects": ["may spend money after confirmation"],
            "success_conditions": ["player confirms purchase completed"],
            "safety_constraints": ["never_spend_below_emergency_reserve", "block_unknown_ui_clicks"],
        },
        "social.gift_npc": {
            "id": "option.social.gift_npc.preview",
            "name": "Preview NPC gift plan",
            "initiation_conditions": ["NPC state available", "inventory known"],
            "goal_conditions": ["gift target verified"],
            "estimated_effects": ["may improve friendship after manual action"],
            "success_conditions": ["player confirms gift delivered"],
            "safety_constraints": ["never_sell_protected_items", "block_unavailable_required_state"],
        },
        "recovery.stabilize_day": {
            "id": "option.recovery.stabilize_day.preview",
            "name": "Stabilize current day",
            "initiation_conditions": ["basic player and time state available"],
            "goal_conditions": ["urgent risks identified"],
            "estimated_effects": ["reduces plan fragility"],
            "success_conditions": ["player confirms safe stopping point"],
            "safety_constraints": ["block_state_hash_mismatch", "block_mutation_in_observer_or_planner_mode"],
        },
    }

    selected = templates.get(intent, templates["recovery.stabilize_day"])
    return [{**base, **selected}]


def _command_preview(option: dict[str, Any], compiler_input: dict[str, Any]) -> dict[str, Any]:
    unavailable = compiler_input["unavailable_required_fields"]
    preconditions = [
        {"id": field, "required": True}
        for field in option["required_state_factors"]
    ]
    precondition_results = [
        {
            "id": field,
            "passed": field not in unavailable,
            "reason": "available" if field not in unavailable else "field_unavailable",
        }
        for field in option["required_state_factors"]
    ]

    return {
        "command_id": f"cmd.preview.{uuid4().hex}",
        "normalized_command": {
            "type": "option_preview",
            "option_id": option["id"],
            "state_hash": compiler_input["state_hash"],
            "mode": compiler_input["mode"],
        },
        "required_preconditions": preconditions,
        "current_precondition_result": precondition_results,
        "expected_effects": option["estimated_effects"],
        "irreversible_effects": [],
        "estimated_duration": option["duration_model"],
        "risk_level": "blocked" if unavailable else "low",
        "recoverability": "unknown" if unavailable else "recoverable",
        "permission_required": "planner",
        "executable": False,
    }


def _classify_goal(goal: str) -> str:
    lowered = goal.lower()
    if any(token in lowered for token in ("crop", "water", "harvest", "farm", "作物", "浇水", "收菜", "农场")):
        return "farm.maintain_crops"
    if any(token in lowered for token in ("buy", "seed", "shop", "pierre", "购买", "买", "种子", "商店")):
        return "economy.buy_supplies"
    if any(token in lowered for token in ("gift", "npc", "social", "friend", "送礼", "社交", "好感")):
        return "social.gift_npc"
    return "recovery.stabilize_day"


def _required_fields_for_goal(goal: str) -> list[FieldPath]:
    return [
        tuple(field.split("."))
        for field in _required_field_names_for_intent(_classify_goal(goal))
    ]


def _required_field_names_for_intent(intent: str) -> list[str]:
    if intent == "farm.maintain_crops":
        return ["game.current_location", "player.stamina", "farm.crops"]
    if intent == "economy.buy_supplies":
        return ["game.time_of_day", "player.money", "locations.shops", "menus.active_menu"]
    if intent == "social.gift_npc":
        return ["player.inventory", "npcs.schedules", "npcs.friendships"]
    return ["game.time_of_day", "player.stamina", "menus.active_menu"]


def _field_available(state: dict[str, Any], path: FieldPath) -> bool:
    field = _nested_get(state, path)
    return isinstance(field, dict) and field.get("status") == "available"


def _field_value(state: dict[str, Any], path: FieldPath) -> Any:
    field = _nested_get(state, path)
    if isinstance(field, dict) and "value" in field:
        return field["value"]
    return None


def _nested_get(value: dict[str, Any], path: FieldPath) -> Any:
    current: Any = value
    for part in path:
        if not isinstance(current, dict) or part not in current:
            return None
        current = current[part]
    return current
