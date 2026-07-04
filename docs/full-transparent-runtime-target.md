# Full Transparent Runtime Target

This document defines the target before any full-runtime test may be claimed.

The project must not run or report full runtime acceptance until every accepted domain below is either implemented with field envelopes and local decompile evidence, or explicitly represented as `unavailable`, `stale`, or `error` with no default-value substitution.

## Full Target Domains

### Required Implemented Domains

These domains must be implemented before full-runtime acceptance starts:

- `environment`: game version, SMAPI version, bridge version, installed mods.
- `identity`: save identity and local player identity.
- `time`: season, day, time, current weather.
- `player`: location, tile, facing, money, health, max health, energy, max energy, current tool, active menu.
- `inventory`: every inventory slot, including empty slots and item identity.
- `farm`: farm identity, visible farm objects, terrain features, crops, buildings, animals, machines, chests, resource clumps, debris counts or item summaries where safe.
- `current_location`: current location identity, display name, outdoors/farm flags, objects, terrain features, characters, warps, map size/layers as read-only metadata.
- `npcs`: current visible/location NPC identity, tile, facing, friendship summary only when verified read-only; schedules are separate and must remain unavailable until verified.
- `quests_progress`: active quests, completed quests, mail flags, special orders, community center/Joja/museum/collections/progress facts when verified read-only.
- `menus`: active menu type and safe menu context only; no UI automation.
- `mods`: installed mods and adapter capabilities.
- `modded_state`: per-mod adapter registry and capability declarations; mod-specific data remains unavailable unless a verified adapter exists.

### Required Event Domains

- `SaveLoaded`
- `DayStarted`
- `TimeChanged`
- `LocationChanged`
- `InventoryChanged`
- `MenuChanged`
- `SnapshotPublished`
- `StateDesyncDetected`
- Slice-specific events for farm/location/NPC/progress only after field reads are implemented.

## Test Gate

Do not run or report full-runtime validation until all required implemented domains have:

- local decompiled evidence table rows,
- shared contract/schema support,
- Bridge implementation,
- Backend ingest validation,
- tests for valid and invalid payloads,
- read-only audit,
- capability manifest entries,
- explicit `unavailable` for unsupported fields.

Before that point, only code-preparation checks are allowed. A worker may run build/unit tests for its slice, but it must not report full-runtime validation.

## Worker Slices

The next worker slices are:

- `Phase 1B`: farm read slice.
- `Phase 1C`: current location and map metadata read slice.
- `Phase 1D`: visible/current-location NPC read slice.
- `Phase 1E`: quest, mail, and progress read slice.
- `Phase 1F`: modded-state adapter registry.
- `Phase 1G`: StateDesyncDetected and duplicate-event suppression hardening.

Workers must produce a patch or implementation plan for one slice only.

## Full Runtime Exit

The global target is complete only when:

- every required domain is implemented or explicitly unavailable,
- every supported field is enveloped,
- no supported field uses a default value for failed reads,
- no forbidden write/input/save operation is present,
- Backend rejects forged snapshots/events/capabilities,
- live Windows SMAPI validation status is `completed`,
- artifacts exist under `artifacts/smapi-runtime-acceptance/`,
- final report distinguishes code tests from real runtime validation.
