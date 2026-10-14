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

### Collection waves

The canonical 18-cell denominator is not a requirement to launch the game 18
times. The evidence work plan also projects missing cells into conditional
collection waves:

- native waves group missing thresholds by dataset partition;
- comparison waves group missing criterion-local admissions by an existing,
  replay-verified native row.

One native trajectory may close several threshold cells in its partition only
when its source cumulative earnings are below the lowest missing threshold and
its verified result reaches the highest missing threshold. The native selection
builder still derives and verifies the complete crossed-criterion set. If no
single authoritative candidate satisfies both bounds, the wave must be split
and the plan rebuilt; the scheduler cannot synthesize or merge evidence.

One comparison wave initializes the Revenue denominator once for its native
row, but still requires a separate exact comparison admission for every listed
criterion. All admissions attached to one seed must select the same native
outcome. Criteria with different selected outcomes require independent seed
runs. These waves reduce orchestration work only; they do not change cell
counts, admit fixtures, create Teacher labels, or authorize formal training.

### Native Revenue trajectory normalization

`build-money-threshold-native-trajectory` closes the representation gap
between a single sale episode and a complete Revenue alternative. Its v1
manifest selects one request ID from an already verified terminal proof
denominator. The builder rebuilds the authoritative request catalog and the
entire terminal proof, then reuses the existing per-alternative verifier to
join:

```text
exact source snapshot
-> optional acquisition/processing rollout
-> resource-neutral terminal route
-> native shop sale or shipping deposit
-> verified Revenue recurrence / shipping settlement
-> final money checkpoint
```

The receipt has four explicit shapes: direct sale, direct shipping,
acquisition then sale, and acquisition then shipping. It preserves the exact
request/concrete-alternative identity, source/final checkpoints, Revenue
prefix, terminal evidence SHA, and all transitive artifact paths/hashes. It is
native evidence only: `teacher_preference_label_verified=false` and
`formal_product_training_authorized=false` are fixed. No Teacher comparison is
consulted while building it.

Money corpus v4 consumes either a legacy isolated sale episode or this complete
trajectory through one discriminated source contract. A trajectory row binds
its concrete alternative, source/final checkpoints, complete Revenue prefix,
acquisition requirement and transitive artifacts. The criterion-local join
requires the comparison-selected concrete alternative, terminal evidence,
prefix and terminal artifacts to match; it cannot truncate a shipping or
rebound result to the last sale/settlement transition.

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

`Initialize-MoneyRevenueComparisonWork.ps1` starts this chain for one accepted
native run without launching the game. It replays the run through the shared
run verifier, rebuilds the authoritative alternative skeleton denominator, and
emits `money_revenue_comparison_collection_plan.v1`. Every alternative is
listed exactly once. The plan distinguishes:

- acquisition/processing alternatives that first require a verified rollout
  and fresh terminal-candidate rebind;
- direct shop-sale or shipping alternatives that can proceed to terminal
  recurrence, ledger-boundary, opportunity-cost, and reservation evidence;
- the exact direct sale alternative whose already verified native episode can
  be reused as its terminal episode input.

Episode reuse is deliberately narrow. It does not satisfy route cost, ledger
boundaries, reservation safety, recurrence, comparison admission, or the
criterion-local corpus join. The plan is a deterministic work queue, not a
Teacher label or evidence receipt.

When acquisition work is required, the initializer can consume an optional
`-AcquisitionRolloutIndex` with schema
`money_revenue_acquisition_rollout_index.v1`. Its `proofs` array maps each
required `alternative_id` to an existing rollout proof manifest and receipt.
The assembly command requires the index to equal the plan's full fresh-rebind
denominator: missing, duplicate, or extra alternatives fail closed. It then
hands the generated manifest to the existing rollout/rebind verifier and, only
after every proof replays, builds the existing direct-plus-rebound concrete
alternative catalog. If no alternative needs rebind, the same path advances
with an empty proof index automatically. No rollout receipt is trusted merely
because it appears in the index.

After the concrete denominator is authoritative, the initializer accepts an
optional `-SourceStrategyLedgerPath`. The existing counterfactual-request
builder verifies that the ledger binds the source snapshot and derives every
terminal ledger anchor before emitting the exact request denominator. Without
that path, initialization stops explicitly at
`awaiting_source_strategy_ledger`; it does not infer or manufacture a ledger.

`Complete-MoneyRevenueComparisonWork.ps1` consumes a
`money_revenue_terminal_proof_index.v1` only after that exact request catalog
exists. The terminal-manifest assembler requires one proof entry per request
ID, normalizes every referenced artifact path, and then delegates route,
before/after ledger capture, reservation, native sale/shipping, recurrence, and
opportunity-cost validation to the existing terminal proof builder.

The completion script builds one comparison admission per criterion actually
crossed by the seed run. When a unique authoritative preference exists, it
builds a native trajectory receipt for that exact selected request and replays
the corpus v4 criterion-local join before adding the receipt to attachment
index v3. Direct sale, shipping and acquisition/processing outcomes therefore
follow the same native trajectory path; none is truncated to a terminal sale.
A non-unique Pareto frontier is reported separately as
`comparison_admissions_blocked_by_incomparable_frontier`, because recollecting
the same action would not resolve that ambiguity. If different crossed criteria
select different alternatives, they require distinct seed runs because one
corpus row cannot own multiple native trajectories.

## Batch policy

Run one criterion-partition cell at a time. Reuse already built binaries and
deployed mods after the first run with `-SkipBuild` and `-SkipDeploy`, but never
reuse a live snapshot, candidate denominator, or isolated save between cells.
`Update-MoneyThresholdNativeOutcomeBatch.ps1` discovers passed run summaries,
replays every referenced catalog/selection/corpus, rejects duplicate split keys,
emits one combined corpus, and rebuilds the 18-cell work plan. Its optional
`-ComparisonAdmissionIndex` input has schema
`money_threshold_comparison_admission_index.v3` and contains an `admissions`
array of `run_id`, `criterion_id`, `admission_manifest_path`,
`admission_receipt_path`, and optional `native_trajectory_receipt_path`. Each pair must
reference an accepted seed run and a criterion actually crossed by that seed.
Batch request v4 permits one common comparison-selected trajectory override per
seed; the criterion-local corpus join rebuilds the trajectory and admission and
checks the selected alternative, prefix and terminal artifacts. Multiple
outcomes for one seed fail closed. Run the batch updater after each accepted
native or comparison artifact and schedule only cells still reported missing.

The updater now invokes `build-money-threshold-production-package` once for the
batch, canonical corpus, work plan, and optional coverage attachment. Native
runs and comparison sources are deeply replayed once while the batch corpus is
built. The work-plan and attachment stages consume that same validated in-memory
corpus, require their declared corpus path to equal the file just written, and
recheck its in-memory row invariants plus file hash without replaying every
transitive native artifact again. Standalone commands retain full independent
replay, and downstream coverage/reconciliation still replays the persisted
corpus at its own trust boundary. This is single-invocation proof reuse, not a
cross-run cache or a relaxation of evidence validation.

When an existing production Goal Method coverage request should consume the
latest corpus, pass `-CoverageRequestTemplate` to the batch updater. It writes a
`money_threshold_coverage_request_manifest.v1`, replays the corpus, preserves
all non-Money sources, and replaces every older Money source with exactly one
`money_threshold_progression_corpus` source. `-CoverageRequestOutput` may select
the destination; without a template no coverage request is written. A source-ID
collision with a non-Money source fails closed.

Batch request v4 carries complete-trajectory comparison attachments. Request
v3 retains direct-sale selected-episode compatibility, v2 remains replayable
with comparison attachments but no outcome override, and legacy v1 remains
native-only. The v3 output separately reports native-complete,
Teacher-comparison-complete, and fully complete cells. Batch output and the
canonical work plan share one cell projector, so criterion and partition
completeness cannot drift between two implementations.
Legacy comparison index v1 remains readable only without an outcome override;
v2 is direct-sale episode-only, and complete trajectory authority is exclusive
to v3.

High-cost full reconciliation is reserved for the Money Exit Gate. During
development, validate the new or invalidated cell and its exact proof branch.
This follows issues #195, #223, #224, #267, and #274 without weakening the
force-full promotion oracle.

## Current implementation status

The selection contract, CLI command, isolated native runner, replayed batch,
comparison collection planner, criterion-local comparison attachment path,
native Revenue trajectory receipt, and unified corpus v4 adapter are
implemented. GitHub game-free quality is
the validation authority during quiet development; no local game or high-load
test is run. Production Money coverage remains `0/18` until real independent
runs and comparison admissions are accepted.
