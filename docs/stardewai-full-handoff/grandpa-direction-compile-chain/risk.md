# Risk

## Risk Level

- Low for this static-analysis slice. All changes are bounded to direction validation, compiler hardening, and test improvement. No executor, runtime harness, or game-call changes were made.
- Medium until isolated runtime integration validates the complete strategy.grandpa_progress chain end-to-end.

## Changes from Previous Draft (Audit Remediation)

- **Removed**: Static direction whitelist in compiler. Direction identity is now validated via the live adapter candidate set.
- **Added**: Full chain rebuild (WorldModelProjector -> GrandpaEvaluationGoalEvaluator -> GrandpaTrainingSampleAdapter) inside compilation for transparent validation.
- **Added**: Exact equality validation of all metadata fields (domain, potential, priority, feedback, required minutes) against live candidate.
- **Added**: `strategic_goal` must be exactly `grandpa_max_score_year3`; missing or any other value is blocked.
- **Added**: `optional_minutes` must be present and exactly 0; missing or nonzero is blocked.
- **Added**: Rejection of nonempty model-supplied `hard_preconditions`, `resource_budget`, `executor_handoff` as unverified.
- **Changed**: `StrategyPlan` is now empty when any validation blocks the item; no partially invalid steps.
- **Changed**: `OptionalMinutes` always 0 in validated steps; `RequiredMinutes` always from `EstimateRequiredMinutes(candidate)`.
- **Changed**: Tests are all deterministic; no conditional branches that pass either outcome.

## Residual Risks

- Every strategy compilation now rebuilds the full projection-evaluation-adapter chain, which runs the same computation as the policy. This is by design for transparent validation but may have performance implications at scale.
- The domain-based minute estimates (economy=240, social=180, skills=360, world_progress=480, farm=120, exploration=360) are heuristics calibrated against known game timing but may not match actual runtime duration.
- Fail-closed behavior remains intentional. The compiler independently verifies candidate data and rejects anything that does not match the live snapshot state.
- The `WorldModelProjector` and `GrandpaEvaluationGoalEvaluator` are instantiated directly in the compiler. If their construction becomes expensive (dependency injection, I/O), it will need refactoring.

## Mitigations

- No new runtime, network, or game state mutation paths were added.
- Static tests assert 25 behavioral invariants including tamper-resistance, strategic_goal validation, optional_minutes validation, and empty-plan-on-block.
- The adapter's 11 native scoring directions are verified by coverage tests but are not used as an authority; the live candidate set is authoritative.
- All runtime validation commands are recorded as pending for the controller after user-play constraint is lifted.

## Executor Capability Reconciliation

- No executor capabilities were added, removed, or modified. This slice is purely strategy validation and compiler hardening.
- Existing transparent-state, parameter, timeline, menu, collision, budget, and post-state gates remain unchanged.
- `recovery.stabilize_day`, social, fishing, mining, machine, and crop executor paths are untouched.
