# Money Threshold Production Evidence

## Purpose

The Money/Revenue coverage unit has six authoritative Grandpa thresholds and
three deterministic dataset partitions. Its exit denominator is therefore 18
criterion-partition cells. Every cell needs two independent channels:

1. a verified native terminal outcome;
2. an exact-denominator Teacher comparison joined to that outcome.

Hermetic fixtures prove the contracts, but never fill a production cell.

## Native outcome collection

`build-money-threshold-native-outcome-selection` consumes a persisted
`money_revenue_candidate_denominator_catalog.v2`. It rebuilds that catalog from
its transparent snapshot and dependency expansion before using it. Selection
is admitted only when:

- the current snapshot denominator is authoritative and comparison-ready;
- the requested criterion is one of the six evaluator-owned Money thresholds;
- `save_id:player_id:total_day` hashes into the requested partition;
- cumulative earnings are still below the requested threshold;
- an available, unprotected, exact shop-sale continuation can cross it;
- the selected continuation contains no debug option or parameter.

The smallest sufficient sale is selected, followed by estimated runtime cost
and stable identity as tie-breakers. This is an evidence-sampling policy, not a
Teacher label. The resulting episode independently derives every crossed
criterion from `GrandpaEvaluationGoalEvaluator`.

`scripts/Invoke-RuntimeMoneyThresholdNativeOutcome.ps1` is the production
collector for one cell. It requires an existing source save slot and never
loads that directory directly. The runner:

1. hashes the source save directory;
2. copies it under the run artifact directory without renaming the slot;
3. verifies that the copy initially has the same content hash;
4. starts the hidden, muted runtime against only that isolated save root;
5. captures a full transparent snapshot and builds the complete Revenue
   candidate denominator;
6. when the authoritative sale node says a matching service opens later that
   same day, waits for normal game time to reach that opening and rebuilds the
   snapshot denominator once; it never changes the clock directly;
7. builds and executes one native sale selection through the existing daily
   plan compiler and executor;
8. verifies the emitted `executor.sell_shop_item` episode with the shared
   native sale verifier and builds a native-only Money corpus row;
9. requires the actual partition, before/after earnings and complete crossed
   threshold set to equal the selection;
10. re-hashes the source save and fails if it changed.

The runner does not mutate time, inject inventory, edit cumulative earnings,
or invoke fixture setup. Missing inventory, closed services, incomplete
transparent data, a non-matching partition, or an unusable denominator all fail
closed. Failed isolated saves and logs are retained for diagnosis; the source
save remains the immutable seed.

The Revenue catalog v2 preserves the source candidate's service window,
allowed-now/allowed-today state, wait cost, gate reasons, stock/currency data,
shipping eligibility and collection metadata. This lets the runner schedule a
normal same-day wait without reimplementing shop calendars or silently dropping
fields needed by later counterfactual comparison.

## Comparison channel

A native outcome closes only half of a cell. Its source denominator remains the
input to the existing Revenue proof chain:

```text
candidate denominator
-> alternative skeleton denominator
-> terminal candidate rebind where required
-> concrete alternative denominator
-> counterfactual proof requests
-> isolated recurrence/execution proofs
-> strategy-ledger boundary capture
-> exact comparison admission
-> criterion-local Money corpus join
```

Selection artifacts and runtime summaries cannot bypass that chain or authorize
formal training. A native outcome may cross several thresholds; each criterion
view uses the same independently verified episode, while its comparison
admission remains criterion-local.

## Batch policy

Run one criterion-partition cell at a time. Reuse already built binaries and
deployed mods after the first run with `-SkipBuild` and `-SkipDeploy`, but never
reuse a live snapshot, candidate denominator, or isolated save between cells.
`Update-MoneyThresholdNativeOutcomeBatch.ps1` discovers passed run summaries,
replays every referenced catalog/selection/corpus, rejects duplicate split keys,
emits one combined native corpus, and rebuilds the 18-cell work plan. Run it
after each accepted collection and schedule only cells still reported missing.

High-cost full reconciliation is reserved for the Money Exit Gate. During
development, validate the new or invalidated cell and its exact proof branch.
This follows issues #195, #223, #224, #267, and #274 without weakening the
force-full promotion oracle.

## Current implementation status

The selection contract, CLI command, hermetic source cases, and isolated native
runner are implemented on the Money production branch. They have not been
built or executed in the quiet development window. Therefore production Money
coverage remains `0/18` until real runs are accepted and the independent
comparison channel is joined.
