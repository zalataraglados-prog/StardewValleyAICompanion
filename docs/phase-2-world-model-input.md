# Phase 2 World Model Input

Phase 2 turns the transparent runtime snapshot into a typed planner input. It is not model training, policy optimization, or action execution.

## Contract

- Input: `snapshot.v1` from the transparent bridge/backend state store.
- Output: `world_model.v1` from `/api/v1/stardew/input/latest`.
- Authority: `state_hash` from the source snapshot remains the identity of the game state.
- Projection rule: only readable field envelopes are copied into planner facts. `unavailable`, `stale`, and `error` envelopes are never guessed or backfilled.

## Required planner facts

The first planner input baseline requires these readable paths:

- `identity.save_id`
- `identity.player_id`
- `time.season`
- `time.day`
- `time.time`
- `time.weather`
- `player.location_id`
- `player.money`
- `player.energy`
- `player.inventory`
- `farm.crops`
- `menus.active_menu`
- `transport.event_stream_websocket`

If any required path is missing or unreadable, `planner_inputs.blocked` is `true` and `block_reasons` names the missing facts.

## Fact layout

`facts` is grouped by planner domain:

- `game`: identity and time aliases.
- `player`: location, position, resources, tool, and inventory aliases.
- `farm`: readable farm facts from the snapshot.
- `current_location`: readable current location facts.
- `npcs`: readable NPC facts.
- `quests`: readable quest facts.
- `world_progress`: readable progress facts.
- `menus`: readable menu/UI facts.
- `mods`: readable mod metadata.
- `modded_state`: readable mod-provided state.

The output intentionally keeps `JsonElement` values so the bridge can add richer factual payloads without forcing the planner contract to guess their shape.

## Exit condition

This phase is complete when:

- `dotnet build` passes.
- `dotnet test --no-build --no-restore` passes.
- `/api/v1/stardew/input/latest` returns `schema_version = "world_model.v1"`.
- The world model reports `all_required_facts_readable = true` for the latest accepted fully transparent runtime snapshot.

After this, the next phase can build a planner/training dataset adapter on top of `world_model.v1` instead of reading raw game snapshots directly.
