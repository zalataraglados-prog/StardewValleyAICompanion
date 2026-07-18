# Evidence

## Code Change Evidence (Audit Remediation)

- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:1-15` - Added using directives for `StardewAI.Contracts.Training`, `StardewAI.Core.Goals`, `StardewAI.Core.Training`, `StardewAI.Core.WorldModel`.
- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:3052-3150` - `ValidateStrategyPlan` now rebuilds the CURRENT candidate set from the supplied `SnapshotEnvelope` using `WorldModelProjector.Project -> GrandpaEvaluationGoalEvaluator.Evaluate -> GrandpaTrainingSampleAdapter.Build`. Returns `(string[] BlockingReasons, CandidateDirection? ValidatedDirection)`. Validates exact equality of all metadata fields against the live candidate. Rejects absent/unknown/blocked/non-positive candidates. Rejects nonempty `hard_preconditions`, `resource_budget`, `executor_handoff`. Requires `strategic_goal` exactly `grandpa_max_score_year3` (missing or any other value blocked). Requires `optional_minutes` present and exactly 0 (missing or nonzero blocked). Removed `IsKnownGrandpaDirectionId` static whitelist.
- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:3301-3328` - `CompileStrategyPlan` now accepts a validated `CandidateDirection` and emits a step with metadata from the live candidate. `RequiredMinutes` from `EstimateRequiredMinutes(candidate)`, `OptionalMinutes = 0`, `HardPreconditions/ResourceBudget/ExecutorHandoff` as empty arrays/string.
- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs:616-722` - `CompileAction` computes strategy validation once via tuple deconstruction. Only emits `StrategyPlan` when status is "pending" and `validatedDirection` is not null. Blocked items always get empty `StrategyPlan`.
- `tests/StardewAI.Core.Tests/GrandpaDirectionCompileChainTests.cs` - Complete rewrite: 25 deterministic tests including tamper tests (one field at a time with precise block + empty `StrategyPlan`), strategic_goal missing/wrong rejection, optional_minutes missing rejection, absent-from-candidate-set rejection, valid compilation equality against recomputed candidate, and adapter coverage proof.

## Direction Contract Evidence

All 11 native Grandpa direction IDs from the adapter remain authoritative. The compiler validates direction identity and metadata exclusively against the live candidate set produced by `GrandpaTrainingSampleAdapter.BuildDirections()`. No static whitelist is duplicated in the compiler.

Direction IDs: `complete_community_center`, `raise_friendships`, `complete_full_shipment`, `raise_skill_levels`, `marriage_and_house_upgrade`, `complete_master_angler`, `complete_museum_collection`, `obtain_rusty_key`, `obtain_skull_key`, `earn_money`, `earn_pet_love`.

Reference: `GrandpaTrainingSampleAdapter.BuildDirections()` defines all 11 via `DirectionSpec`. The current coverage test verifies the adapter continues to cover all 11 IDs. Joja development remains a full-game route, but local `Utility.getGrandpaScore()` gives it no score factor.

## Test Coverage Evidence

- The strategy tests cover classifier deferral, eligible selection, deterministic fail-closed completion, stale/tampered metadata rejection, exact live-candidate recompilation, adapter coverage for 11 native directions, and non-strategy safety.

## Validation Evidence

- Tests were NOT RUN this session (task constraint #9).
- Static review confirms all behavioral invariants are implemented.
- Build and test commands are recorded in `test-results.txt` and `WORKER_NOTES.md`.
