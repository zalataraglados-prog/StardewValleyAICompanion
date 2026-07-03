# Transparent Read Target And Worker Slices

## Target

Full transparent reading is a primary project goal. The engine must be able to read every game fact required by the AI companion through `StardewAI.TransparentBridge`, with explicit provenance and completeness metadata. Chat, planning, memory, and future execution are consumers of this transparent fact layer, not substitutes for it.

Current status is skeleton only. Any unimplemented game fact must be returned as `unavailable`; no layer may infer, guess, OCR, screenshot, inspect process memory, or use manual input as the normal source of game truth.

## Field Contract

Every state field must carry:

- `value`
- `status`: `available`, `derived`, or `unavailable`
- `source`
- `adapter`
- `read_at_tick`
- `confidence`

Unavailable fields must also carry a reason. Derived fields must carry a derivation source.

## Phase 0.5 Completion Criteria

- Versioned JSON snapshot can be returned.
- Event stream can emit traceable game changes.
- Capability manifest lists readable, partial, unavailable, and disabled capabilities.
- Audit log records snapshot, event, verifier, and command-preview decisions.
- Observer mode performs no writes to game state or save data.
- Unknown or unsupported mod state is visible in snapshot and capabilities.
- Backend can ingest and validate snapshot/event/capability records.
- All tests and builds are runnable from a clean checkout.

## Worker Protocol

Codex is controller. Workers are lower-privilege and work through isolated Git worktrees. They must not push, deploy, reset, delete project files, or edit the main worktree directly.

Controller repository:

```text
I:\StardewValleyAICompanion
```

Worker worktrees:

```text
I:\StardewAI-workers\schema-contract  -> worker/schema-contract
I:\StardewAI-workers\bridge-readers    -> worker/bridge-readers
I:\StardewAI-workers\backend-sync      -> worker/backend-sync
```

## Slice A: Schema Contract

Owner: `worker/schema-contract`

Allowed paths:

- `schemas/json/`
- `docs/`

Deliverables:

- Expand snapshot schema toward full `CanonicalState`.
- Define common field envelope and unavailable/derived semantics.
- Define or refine schemas for capabilities, events, commands, audits, action specs, option specs, and executor port.
- Add schema validation tests only if they do not require backend changes.

Do not edit:

- `src/`
- `backend/`

## Slice B: Bridge Readers

Owner: `worker/bridge-readers`

Allowed paths:

- `src/StardewAI.TransparentBridge/`

Deliverables:

- Refactor `ModEntry` toward collector/adapters without changing API routes.
- Add read-only collectors for foundational domains: player, world, inventory, farm/map objects, NPCs, shops, mods, and UI/menu state.
- For unimplemented fields, return explicit `unavailable`.
- Keep command execution disabled.

Do not edit:

- `backend/`
- `schemas/`

## Slice C: Backend Sync And Verification

Owner: `worker/backend-sync`

Allowed paths:

- `backend/`
- `tests/`

Deliverables:

- Ingest snapshots, events, capabilities, and audit records.
- Validate that state fields use transparent metadata envelopes.
- Provide latest snapshot, event tail, capability state, and audit endpoints.
- Add focused tests for ingest and validation.

Do not edit:

- `src/`
- `schemas/`

## Integration Rules

- Controller reviews each worker diff before applying to `main`.
- Merge order: schema, backend, bridge.
- If slices conflict, schema wins over implementation names; implementation adapts.
- Verification after every integration:

```powershell
dotnet build
.\.venv\Scripts\python.exe -m pytest
```
