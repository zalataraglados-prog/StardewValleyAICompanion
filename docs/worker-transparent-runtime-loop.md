# Transparent Runtime Read Worker Loop

This task loop is for completing the StardewAI transparent runtime read system without relying on examples, guesses, or mock data as proof of runtime correctness.

Codex remains controller. Worker AI may investigate, propose patches, and implement bounded slices in its own workspace. Worker AI must not print secrets, push, deploy, reset git, delete unrelated files, or claim live SMAPI validation without actual game runtime evidence.

## Target

Build toward a fully transparent, read-only Stardew Valley runtime bridge:

Natural game runtime state -> TransparentBridge -> shared Contracts -> Backend ingest -> auditable snapshots/events/capabilities.

The final system must expose every supported field with:

- `value`
- `status`
- `source`
- `adapter`
- `read_at_tick`
- `confidence`
- `reason` or `derivation` when required

No default value may represent a failed read.

## Non-Negotiable Rules

1. Every Stardew or SMAPI member claim must be verified against local decompiled assemblies before use.
2. Do not use documentation examples as proof.
3. Do not expand a slice unless the previous slice exits cleanly.
4. Do not call mock/unit tests runtime validation.
5. Do not write game state.
6. Do not add LLM, OCR, keyboard/mouse automation, executor, pathing, buying, selling, or save editing.
7. If live SMAPI was not run, state: `真实游戏运行时验收尚未执行`.

## Loop Inputs

Each loop starts from:

- Current branch and HEAD.
- Clean/dirty working tree report.
- Decompiled source under `I:\StardewValleyAICompanion-decompile`.
- Current shared contracts under `src/StardewAI.Contracts`.
- Current Bridge under `src/StardewAI.TransparentBridge`.
- Current Backend under `src/StardewAI.Backend`.
- Current tests under `tests`.

## Loop Body

Each worker cycle must run these stages in order.

### 1. Scope Selection

Pick exactly one bounded transparent-read slice from the backlog.

Allowed next slices:

- Phase 1A-3: complete runtime validation harness and manual SMAPI checklist capture.
- Phase 1B: farm static read slice, excluding mutation and automation.
- Phase 1C: current location read slice, excluding pathing and map graph execution.
- Phase 1D: NPC visible/current-location read slice, excluding schedules until separately verified.
- Phase 1E: quest/mail/progress read slice.
- Phase 1F: modded-state adapter registry and per-mod capability declaration.

Do not implement multiple slices in one cycle unless the controller explicitly approves.

### 2. Live Decompiled Evidence

For every proposed field:

- Locate the concrete decompiled file and line/pattern.
- Record the member path used by code.
- Record null/world-ready conditions.
- Record whether the field is public API, public game object, event argument, or derived.

Exit this stage only when every field has evidence.

The worker's final output must include an evidence table with one row per field or event:

- field/event name
- decompiled path
- line or search pattern
- member path
- source kind
- runtime null/readiness condition

No field or event may be reported implemented without a row in this table.

### 3. Contract Update

If the slice needs new payload shapes:

- Update shared contracts first.
- Update JSON schema.
- Do not duplicate DTOs in Bridge, Backend, or Core.
- Preserve `FieldEnvelope<T>` semantics.

Exit this stage only when contracts and schemas describe the exact slice.

### 4. Bridge Implementation

Implement read-only collection using verified members.

Required properties:

- No write calls to game objects.
- No save writes.
- No input simulation.
- No default values for failed reads.
- Stable `state_hash`.
- Event changes include `changed_fields`.

Exit this stage only when `dotnet build` succeeds.

### 5. Backend Ingest Hardening

Backend must:

- Validate `schema_version`.
- Recompute and verify `state_hash`.
- Preserve raw payload.
- Preserve `unavailable`.
- Reject invalid field envelopes.
- Link events to snapshots by hash when applicable.

Exit this stage only when negative tests cover malformed inputs.

### 6. Tests

Add or update tests for:

- Stable canonical hash.
- Field envelope legality.
- Unavailable fields not becoming defaults.
- Schema/version rejection.
- Event hash association.
- Capability read-only declarations.
- Slice-specific diff or event behavior.

Exit this stage only when all applicable tests pass.

### 7. Read-Only Audit

Search at minimum:

`Game1.player.*=`, `.Money =`, `.Stamina =`, `.health =`, `.maxHealth =`, `Items.Add`, `Items.Remove`, `Items.Clear`, `Items.OverwriteWith`, `Items.ReduceId`, `Game1.timeOfDay =`, `Game1.currentSeason =`, `Game1.dayOfMonth =`, `Game1.warpFarmer`, `InputSimulator`, `simulate`, `performClick`, `DoFunction(`, `WriteJsonFile`, `WriteConfig`, `SaveGame`, `currentLocation =`.

The audit must also include a slice allowlist:

- allowed source files
- allowed Stardew/SMAPI member paths
- allowed event subscriptions
- explicitly forbidden domains for this slice

Exit this stage only when findings are listed with file paths or the search has no matches, and the allowlist proves the slice did not expand beyond scope.

### 8. Verification Commands

Run:

```powershell
dotnet build
dotnet test
node -e "const fs=require('fs'); for (const p of ['schemas/json/snapshot.schema.json','schemas/json/event.schema.json','schemas/json/capability.schema.json']) JSON.parse(fs.readFileSync(p,'utf8')); console.log('schema json ok')"
git diff --check
git status --short
git log -1 --oneline
```

Exit this stage only when command outputs and exit codes are recorded.

### 9. Live SMAPI Validation

If the worker can run the game:

1. Start Backend.
2. Start SMAPI.
3. Load a test save.
4. Capture snapshot.
5. Manually compare visible game state to snapshot.
6. Move within same location.
7. Warp to another location.
8. Change inventory.
9. Open and close menus.
10. Wait for time change.
11. Verify events.
12. Stop Backend.
13. Confirm game continues running.
14. Restart Backend.
15. Check reconnect behavior.

If not run, report exactly:

`真实游戏运行时验收尚未执行`

Live validation status must be one of:

- `completed`: every manual validation step was executed and evidence is attached.
- `not_executed`: SMAPI/game runtime was not run.
- `failed`: SMAPI/game runtime was run, but one or more checks failed.

Only `completed` can close a runtime-validation cycle. `not_executed` can close a code-only preparation cycle, but must not be described as runtime completion.

## Cycle Exit Conditions

A code-preparation cycle may be marked complete only when all are true:

- Scope stayed within one approved slice.
- Every used game/SMAPI member has local decompile evidence.
- The final report contains the required evidence table.
- Shared contracts are the only DTO source.
- Build passes with 0 errors.
- Tests pass.
- JSON schema sanity check passes.
- `git diff --check` passes.
- Read-only audit is reported.
- Live SMAPI status is explicitly reported.
- Remaining gaps are listed.

A runtime-validation cycle may be marked complete only when all code-preparation exit conditions are true and live SMAPI validation status is `completed`.

If live SMAPI status is `not_executed` or `failed`, the worker must report one of:

- `code_preparation_complete_runtime_pending`
- `runtime_validation_failed`

The worker must not report `runtime_complete`.

## Global Exit Conditions

The full transparent-runtime objective is not complete until:

- All accepted runtime domains are covered by explicit capabilities.
- Each supported field has envelope provenance and confidence.
- All unsupported fields are unavailable/error/stale, never defaulted.
- Backend rejects forged or malformed state.
- Event stream covers state changes without duplicate unchanged events.
- Live SMAPI validation has been completed on Windows.
- The final report distinguishes code validation, unit tests, integration tests, and real game runtime validation.

The global objective must not be marked complete while any accepted domain has status `unverified`, `not_executed`, or `runtime_validation_failed`.

## Worker Output Format

Worker must return:

- Slice name.
- Files changed.
- Decompiled evidence list.
- Implemented fields/events/capabilities.
- Tests added or changed.
- Commands run with exit codes.
- Read-only audit result.
- Live SMAPI validation status.
- Remaining backlog.
- Patch or branch/commit reference.
