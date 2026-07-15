# Risk

## Risk Level

- Medium until build/unit validation can run.
- Runtime risk remains high by design because the perfect mining/combat executor is explicitly not implemented.

## Residual Risks

- `MiningReadAdapter` is statically reviewed only in this pass; compile/API mismatches remain possible until validation runs.
- Required mining object, collision/passability, and special floor classifications are intentionally unavailable, so live mining candidates will fail closed until a fuller decompile-backed adapter exists.
- Monster ranged/special behavior is explicitly unavailable without a complete decompile-backed behavior table.
- Route/collision details are exposed as context for planning, but this slice does not implement a native mine navigation/combat executor.
- Future ladder probability is intentionally unavailable; planning must not treat it as an observed tile.

## Mitigations

- Candidate generation fails closed when any required mining group or nested required fact is missing, stale, errored, or unavailable.
- Known impossible target depths and family mismatches are rejected before runtime.
- Compiler always returns `mining_cost_estimate_unavailable` and `mining_perfect_executor_not_implemented`, keeps mining timing/energy unknown, and emits no fake low-level actions.
- Tests were added for the core contract and can be run after the active-play constraint is lifted.
