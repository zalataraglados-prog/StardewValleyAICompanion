# WORKER_NOTES.md - Grandpa Direction Daily Candidate Binding (Final)

## Summary

Revised the grandpa direction daily candidate binding system per controller audit. This is a typed direction-to-daily-candidate binding system that decomposes validated `strategy.grandpa_progress` directions into concrete candidates passable to `DailyPlanCompiler`.

## 2026-07-17 Controller Superseding Update

`complete_full_shipment` is now the fourth direct direction. It binds only `economy.ship_items` / `ship_inventory_item_to_bin` candidates carrying exact typed full-shipment contribution evidence. The native compiler, executor, immediate receipt, and delayed `basicShipped` settlement recorder are implemented. The remaining blocked count is eight. Focused Core 103/103, full Core 946/946, Backend 49/49, and E-drive isolated native shipping immediate smoke all passed.

## Controller Audit Corrections Applied

### 1. Contracts Contain DTOs Only
- **Before**: Catalog types `GrandpaDirectionCatalogEntry` and `GrandpaDirectionCatalog` lived in the Contracts project.
- **After**: Removed from Contracts. Added as `src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs`. Contracts retain only request/result/audit DTOs.

### 2. Candidate Identity Preserved
- **Before**: Bound candidates received rewritten `CandidateId` ("grandpa-bound.{direction}.{source}") and adjusted `Score` (base + priority * 0.01).
- **After**: Bound candidates keep original `CandidateId`, `Score`, `Rank`, and all action fields exactly as supplied. No rewriting or score adjustment.
- Candidates are rejected when `BlockReasons` is non-empty, even if `TimelineStatus` is not `blocked`.

### 3. Planned Gaps Honest and Exposed
- **Historical baseline**: Three direct rows listed speculative `required_transparent_fields`; nine blocked rows had no output when blocked.
- **After**: Direct rows have empty `RequiredTransparentFields` and `RequiredCapabilities`; full shipment additionally reports its covered transparent fields. Blocked results populate `MissingTransparentFields` and `MissingCapabilities` for all eight unsupported rows.

### 4. Nullable Intent Accurate
- **Before**: `Bind(GrandpaDirectionBindingRequest request, SnapshotEnvelope snapshot)` -- non-nullable parameter.
- **After**: `Bind(GrandpaDirectionBindingRequest request, SnapshotEnvelope? snapshot)` -- nullable parameter supports fail-closed null snapshot test. Backend resolves exact non-null snapshot before calling.

### 5. Scoring Authority Removed from Catalog
- Catalog entries contain only planning/binding policy: `direction_id`, `binding_rule_id`, `direct_binding_enabled`, `permitted_option_ids`, `permitted_candidate_kinds`, `required_transparent_fields` (planned gaps), `required_capabilities` (planned gaps), `block_reason_template`, `cc_joja_sensitive`.
- Domain, label, feedback key, related factor IDs, points, priority score, known/blocked state are all sourced from the live `CandidateDirection` rebuilt by `GrandpaTrainingSampleAdapter` for the requested snapshot.

### 6. State-Hash Binding Fail-Closed
- **Before**: Request embedded an optional `SnapshotEnvelope`; backend used latest-snapshot fallback.
- **After**: Request requires `state_hash` (no embedded snapshot). Backend resolves exact `SnapshotEnvelope` from `StateStore.Snapshots[state_hash]` -- no latest-snapshot fallback. Core binder verifies non-empty exact equality between request `StateHash` and snapshot `StateHash`; rejects on empty, unknown, or mismatch with precise audit wording.
- Ranked candidates are submitted under a request-level exact state hash and are not independently/per-candidate hash verified because `PolicyEventCandidatePrediction` has no state hash.

### 7. CC/Joja Route Commitment Treated as Unresolved
- **Before**: Speculative `EvalCcJojaExclusivity()` checked `community_center.completed` and `joja_membership` bools, interpreted simultaneous true as inconsistency.
- **After**: Both CC and Joja rows are unconditionally blocked. Both report `cc_joja_route_commitment_unavailable`. Audit records `CcJojaRouteCommitmentResolved = false` for both. No speculative bool traversal check remains. Route commitment is explicitly recorded as unresolved, not as exclusivity-resolved.

### 8. Speculative Field/Capability Checks Removed
- **Before**: `FieldReadableInSnapshot()` and `CapabilityAvailable()` inspected snapshot paths for non-direct-binding directions.
- **After**: These methods are removed entirely. The eight non-direct rows are unconditionally blocked as planned contract gaps. Their `required_transparent_fields` and `required_capabilities` in the catalog represent planned gaps, not runtime checks.
- Four direct rows bind only already-current, available, timeline-legal candidates using exact permitted option/kind checks. Full shipment also requires exact contribution evidence. Missing permitted candidate produces `no_current_permitted_candidate` with precise rejection detail.

### 9. Readiness Semantics and Provenance Corrected
- **Before**: `BindingCoverageStatus` used `full` (2+ candidates) / `partial` (1) / `none` semantics.
- **After**: `BindingCoverageStatus` is `ready` (1+ valid permitted candidates) or `blocked`. No arbitrary two-candidate threshold.
- Rejects candidates when: `TimelineStatus == "blocked"`, `BlockReasons` non-empty, `Available == false`, `AllowedNow != true`, `AllowedToday != true`.
- Clone arrays (`Parameters`, `GateReasons`, `BlockReasons`, `TimelineReasons`) so output mutation cannot alias input arrays.
- Provenance parameters added once; existing provenance names on source candidates are preserved and not duplicated. Duplicate provenance names (second occurrence of the same name, even with matching values) reject with `candidate_provenance_duplicate`.
- Does not convert long-horizon required minutes into daily `EstimatedTicks`. Does not claim factor completion or predict deltas.

### 10. Tests Updated And Run
- Focused binding/contribution suite passed 103/103; full Core passed 946/946; Backend passed 49/49. Coverage includes:
  - Catalog: 12 entries, non-overlapping, policy-only (no score metadata), 4 direct-binding
  - State hash: empty reject, null snapshot reject, mismatch reject, exact match
  - Rejection: empty direction_id, unknown direction_id, target-complete, direction-absent
  - Blocking: all 8 non-direct directions unconditionally blocked as planned contract gaps
  - CC/Joja: both rows unconditionally blocked with `cc_joja_route_commitment_unavailable`
  - Direct binding: earn_money, raise_friendships, complete_master_angler with provenance
  - Availability gates: `AllowedNow == false`, `AllowedToday != true`, unavailable, blocked timeline, block_reasons non-empty rejected
  - Candidate preservation: CandidateId, Score, Rank, ExpectedReward, all action fields unchanged
  - Readiness: single candidate = `ready` coverage status (not `full`)
  - Safety: no fabricated values, no duplicate provenance, no score/metadata claims
  - Arrays cloned: `Parameters`, `GateReasons`, `BlockReasons`, `TimelineReasons` independently allocated
  - Provenance: existing names preserved; new ones added exactly once; duplicate provenance names rejected
  - Metadata sourced from adapter output, not catalog
  - `MissingTransparentFields` and `MissingCapabilities` non-empty for all 8 blocked rows

### 11. Backend Endpoint Cleaned
- `POST /api/v1/planner/grandpa-direction-binding/bind`
- Requires `state_hash` from request body (returns 422 if empty)
- Resolves snapshot from `StateStore.Snapshots[state_hash]` -- returns 422 if unknown
- No latest-snapshot fallback
- Passes `GrandpaDirectionBindingRequest` + resolved `SnapshotEnvelope` to binder

## Architecture

### Contracts (`GrandpaDirectionDailyCandidateBindingContracts.cs`)
- **`GrandpaDirectionBindingRequest`**: `state_hash`, `direction_id`, `ranked_candidates` (no embedded snapshot)
- **`GrandpaDirectionBindingResult`**: bound/blocked candidates, metadata from adapter, audit
- **`GrandpaDirectionBindingAudit`**: `state_hash_verified`, `state_hash_empty_or_unknown`, `cc_joja_route_commitment_resolved`
- No catalog types (moved to Core)

### Core Catalog (`GrandpaDirectionCatalog.cs`)
- **`GrandpaDirectionCatalogEntry`**: policy-only binding metadata
- **`GrandpaDirectionCatalog`**: 12-entry static catalog with 4 direct and 8 blocked planned gaps

### Core Binder (`GrandpaDirectionDailyCandidateBinding.cs`)
- Accepts `(request, snapshot?)` -- nullable for fail-closed test
- Verifies state hash equality first on non-null snapshot
- Rebuilds direction set from snapshot via adapter -> evaluator -> adapter pipeline
- Sources all direction metadata from adapter output (not catalog)
- CC/Joja: unconditional block with unresolved route commitment
- Non-direct: unconditional block as planned contract gap
- Direct: filters by permitted option/kind + availability gates + block-reasons gate
- Adds provenance parameters without overwriting existing names
- Clones all candidate arrays
- Populates `MissingTransparentFields`/`MissingCapabilities` on blocked results from catalog

## Changed Files

| File | Change |
|------|--------|
| `src/StardewAI.Contracts/Training/GrandpaDirectionDailyCandidateBindingContracts.cs` | MODIFIED: Catalog types removed |
| `src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs` | NEW: Catalog types from Contracts |
| `src/StardewAI.Core/Training/GrandpaDirectionDailyCandidateBinding.cs` | MODIFIED: Nullable snapshot, no CandidateId/Score rewrite, BlockReasons gate, Missing fields |
| `tests/StardewAI.Core.Tests/GrandpaDirectionDailyCandidateBindingTests.cs` | MODIFIED: Corrected assertions, added 2 tests |
| Dedicated docs (`test-results.txt`, `WORKER_NOTES.md`, `evidence.md`, `risk.md`) | MODIFIED: Truthfulness corrections |

## Binding Rules

| Direction | Direct Binding | Permitted Kinds | Status |
|-----------|---------------|-----------------|--------|
| earn_money | Yes | sell_or_ship_inventory_item | Active |
| raise_friendships | Yes | social_talk_current, social_gift_current | Active |
| complete_master_angler | Yes | catch_fish | Active |
| complete_full_shipment | Yes | ship_inventory_item_to_bin | Active with exact contribution evidence |
| raise_skill_levels | No | - | Blocked (planned contract gap) |
| obtain_skull_key | No | - | Blocked (planned contract gap) |
| complete_museum_collection | No | - | Blocked (planned contract gap) |
| obtain_rusty_key | No | - | Blocked (planned contract gap) |
| complete_community_center | No | - | Blocked (planned contract gap + CC/Joja route unresolved) |
| complete_joja_development | No | - | Blocked (planned contract gap + CC/Joja route unresolved) |
| marriage_and_house_upgrade | No | - | Blocked (planned contract gap) |
| earn_pet_love | No | - | Blocked (planned contract gap) |

## Risks

1. **State hash binding**: Backend must have the snapshot pre-ingested for the exact state_hash. Late ingest ordering is a possible production concern.
2. **CC/Joja route commitment**: Remains unresolved because transparent state does not prove which route the player committed to. Both rows stay blocked until new transparent evidence is exported.
3. **Planned contract gaps**: 8 of 12 directions remain blocked until their transparent candidate/compiler/executor chains are complete.
4. **Runtime boundary**: Full shipment has native immediate runtime proof and prior delayed settlement proof; the other direct directions retain their own dedicated runtime evidence.
