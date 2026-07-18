# Worker Notes

## 2026-07-14 Grandpa Direction Compile Chain (Controller Audit Remediation)

- Scope: bounded static-only StardewAI slice. Fixed controller-audit-identified defects in the compile chain; did not add new features.
- Active user-play constraint remained in force. I did not launch the game/SMAPI, deploy, build, test, run smoke scripts, touch `E:`, access credentials, or edit the real repository.
- All changes are within the sandbox project snapshot only.

## Completed (audit remediation)

1. Controller audit REJECTED first draft. Rewrote `ValidateStrategyPlan` to rebuild the CURRENT candidate set from the supplied `SnapshotEnvelope` using the full `WorldModelProjector -> GrandpaEvaluationGoalEvaluator -> GrandpaTrainingSampleAdapter` chain. The compiler no longer trusts model-supplied `direction_known`, `direction_blocked`, `potential_points`, `domain`, or `feedback_key` values. It finds the exact current `CandidateDirection` by `direction_id` within the live adapter candidate set.

2. Strict validation: rejects if the current candidate is absent, unknown, blocked, or non-positive. Validates `direction_domain`, `potential_points`, `priority_score`, `feedback_key`, and `required_minutes` for exact equality with the live candidate and `GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(candidate)`. Requires `strategic_goal` to be exactly `grandpa_max_score_year3` (missing or any other value blocked). Requires `optional_minutes` present and exactly 0 (missing or nonzero blocked). Model-supplied booleans (`direction_known`, `direction_blocked`) are redundant audit hints only and cannot make a candidate valid.

3. `CandidateDirection` has no typed `hard_preconditions`, `resource_budget`, or `executor_handoff` source. The compiler rejects nonempty model-supplied values for these fields. The verified `StrategyPlanStep` always emits empty arrays/string for these fields.

4. Strategy validation is computed once in `CompileAction` via a tuple return from `ValidateStrategyPlan`. If strategy validation or any global stale-state/compiler validation blocks the item, `NormalizedCommand.StrategyPlan` is empty. No partially invalid step with -1/default values is ever emitted.

5. Removed the static direction whitelist (`IsKnownGrandpaDirectionId`) as an authority. The adapter's live candidate set is the sole authoritative source. The current adapter defines 11 native Grandpa scoring directions; Joja development is intentionally not one of them.

6. Preserved the useful classifier placeholder removal and `MockSmallModelPolicy` no-fallback behavior. Made no-eligible tests deterministic: `PolicyFailsClosedWhenTargetAlreadyComplete` uses a target-complete snapshot where the adapter returns zero directions.

7. Replaced weak tests with tamper tests that modify domain, potential, priority, feedback, required minutes, optional minutes, hard_preconditions, resource_budget, and executor_handoff one at a time, asserting a precise block reason plus empty `StrategyPlan`. Added `CompilerRejectsDirectionAbsentFromSnapshotCandidateSet` (target-complete snapshot). Added `ValidPolicyOutputCompilesToStepMatchingRecomputedCandidate` which asserts that a valid policy output compiles to one step whose metadata equals the candidate recomputed from the same snapshot.

8. The compiler never emits a negative-minute strategy step. `CompileStrategyPlan` uses `EstimateRequiredMinutes(candidate)` which always returns a positive value. `OptionalMinutes` is always 0. When blocked, `StrategyPlan` is empty (no step with negative minutes).

9. Updated `WORKER_NOTES.md`, `evidence.md`, `transparency-coverage.md`, `risk.md`, and `test-results.txt` to remove false claims about current-snapshot validation and static whitelist authority. Tests are NOT RUN.

## Changed Files

- `src/StardewAI.Core/Execution/ActionQueueCompiler.cs` - Rebuilt `ValidateStrategyPlan` to use snapshot-aware chain; removed `IsKnownGrandpaDirectionId`; rewrote `CompileStrategyPlan` to accept validated `CandidateDirection`; modified `CompileAction` to compute validation once and emit empty `StrategyPlan` on block
- `tests/StardewAI.Core.Tests/GrandpaDirectionCompileChainTests.cs` - Rewrote all tests: deterministic no-eligible, tamper tests, absent/satisfied rejection, valid compilation equality, adapter coverage

## Tests (25 tests, NOT RUN)

- ClassifierDoesNotOutputAutoSelectBestDirection
- PolicySelectsKnownUnblockedPositivePotentialDirection
- PolicyFailsClosedWhenTargetAlreadyComplete (deterministic)
- CompilerRejectsAutoSelectBestDirection
- CompilerRejectsEmptyDirectionId
- CompilerRejectsStaleDirectionFromOldSnapshot
- CompilerRejectsDirectionAbsentFromSnapshotCandidateSet (new)
- ValidPolicyOutputCompilesToStepMatchingRecomputedCandidate (new)
- CompilerRejectsDomainMismatch (tamper)
- CompilerRejectsPotentialPointsMismatch (tamper)
- CompilerRejectsPriorityScoreMismatch (tamper)
- CompilerRejectsFeedbackKeyMismatch (tamper)
- CompilerRejectsRequiredMinutesMismatch (tamper)
- CompilerRejectsOptionalMinutesNonzero (tamper)
- CompilerRejectsMissingStrategicGoal (new)
- CompilerRejectsWrongStrategicGoal (new)
- CompilerRejectsMissingOptionalMinutes (new)
- CompilerRejectsHardPreconditionsValue (tamper)
- CompilerRejectsResourceBudgetValue (tamper)
- CompilerRejectsExecutorHandoffValue (tamper)
- Adapter direction coverage (11 native scoring directions)
- DirectionWithUnknownFactorIsNotSelected
- BlockedItemHasEmptyStrategyPlan
- AllDirectionsPresentInValidSnapshotAreCoveredByAdapter
- NonStrategyOptionDoesNotRebuildCandidateSet

## Recommended Test Commands

```powershell
dotnet test tests\StardewAI.Core.Tests\StardewAI.Core.Tests.csproj --no-restore --filter "GrandpaDirectionCompileChainTests"
dotnet test tests\StardewAI.Core.Tests\StardewAI.Core.Tests.csproj --no-restore --filter "MockSmallModelPolicyTests"
dotnet test StardewValleyAICompanion.sln --no-restore
```

## Risks

- See `risk.md` for updated risk assessment.
- Primary risk: every strategy compilation rebuilds the full projection-evaluation-adapter chain, which may have performance implications. The policy also runs this chain during generation. This is by design for transparent validation.
- The domain-based minute estimates in `GrandpaStrategyFeatureRowBuilder` are heuristics and may need tuning from runtime data.

## Remaining Work

- Runtime validation once user-play constraint is lifted
- Tuning of domain-based minute estimates from actual runtime data
- Full integration test with actual game session
