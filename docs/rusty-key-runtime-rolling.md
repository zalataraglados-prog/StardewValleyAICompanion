# Rusty Key Controlled Runtime

## Implemented Entry

`scripts/Invoke-RuntimeRustyKeyRollingSupply.ps1` connects the existing public
builders and runtime services into a bounded calibration/control run. It attaches
only to an explicitly supplied isolated runtime; it does not launch/kill a game,
alter input focus, create a fixture, train a model or choose a player's answer.
The actual game mechanics, source intents, compiler and native verifiers remain
in the existing C# implementations. PowerShell only orchestrates their artifacts,
HTTP calls and the original precompiled/daily-plan live-loop adapters.

Required inputs are project root (optional default), output directory, unique run
ID, Backend/snapshot/executor URLs, isolated saves path, authoritative requirement
inventory, acquisition lowering, Master Angler static window index, route timing
calibration and fishing forecast manifest. The runtime must already be running
with the same isolated run identity. The wrapper refuses an existing run root,
pins source/binary hashes and never silently substitutes historical inputs.

## Stage Flow

1. Capture/ingest one initial full snapshot and retain the actor/save identity.
2. Rebuild the rolling producer. Held donation needs no acquisition axes. Only
   when acquisition sources are needed, invoke the original 14-axis batch.
3. Generate current ranking through `build-rusty-key-current-ranking`, using the
   original source-bound request factory and availability/ranking classes, scoped
   to the selected route's endpoint/support options. No script-generated shop,
   geode, mining or recipe parameters, learner preferences or all-option scans.
4. Prepare the current stage before a real commit. Support uses the original
   support request, atomic commit, commit-gated compiler, live executor, native
   transition receipt and canonical settlement. Preserve its exact settled-ledger
   file and proof into the next fresh-axis rebuild/replan.
5. Terminal acquisition commits the admitted request, refreshes ranking against
   the committed ledger, and uses the original dispatch/binding/native receipt
   and canonical route settlement. Museum output custody stays in that ledger.
6. Held donation executes the original compiled queue. The existing live custody
   hook verifies/settles the native consumption; retain the post-donation ledger,
   not merely the old lease. Native threshold/day/story handoff reuses the existing
   daily-plan path and must have one safe current candidate. No event skip,
   fabricated key, direct inventory mutation or bound player-answer substitution.

The ordinary commit-receipt CLI now accepts the same control execution arguments
as dispatch/binding and independently rebuilds retained support lineage. Its old
Teacher call shape is unchanged. Current ranking generation is not execution
authorization; commit, binding and dispatch gates remain mandatory.

## Authority And Failure Boundaries

Rusty Key does not require a completed current Community Center denominator. The
shared pipeline uses explicit `CalendarMode=current_sources` for this museum-only
controller. It reuses the original calendar compiler with the current snapshot,
preserving live-only sources such as exact geode drops. This supersedes the
initial static-mode choice in EVD-353: static mode loses current-only sources
and a successful synthetic shop example did not establish geode support.

The calendar and target-date calendar bind the full snapshot SHA-256, state hash,
save, actor and game version, and downstream builders independently rebuild that
mode. Removing or forging the binding cannot fall back to static authority.
The full route and museum denominators remain present; current Community Center
routes are explicitly blocked with no admitted windows because their denominator
was not requested. This is not a CC completion or availability claim. Other callers
retain the original strict `current` default. The new source/target CLI handlers
are batch-safe and the original 14-axis pipeline remains shared.

Each stage writes a dispatch intent and immutable proof inputs/outputs. The loop
has a bounded step count and disk floor; an existing run cannot be replayed even
through the library entry. A rejection or ambiguous network/native result stops
with diagnostics, never blind native retry. Reaching a step budget is incomplete,
not success. Final actor/save identity is rechecked; stale/untyped key projections
do not establish a key observation. Wrapper input hashes must also remain stable.

The shared daily-plan adapter now explicitly distinguishes product/harness
execution (legacy default unchanged) and passes a supplied snapshot manifest at
its original path. Copying relative content-addressed references to another
directory would break blob resolution and duplicate storage.

## Verification And Remaining Work

Product client identity/final-response recovery now reuses the existing journal
through the original live dispatch call; see [Product Receipt Recovery](product-receipt-recovery.md).
It does not replace the isolated native trace below or recover an ambiguous
Backend mutation. Harness-only boundary runs retain requests but cannot use a
Product receipt lookup as native proof.

### Native Acquisition Boundary Preparation

`Invoke-RuntimeRustyKeyThresholdSmoke.ps1 -SupplyMode current_geode` reuses the
existing isolated-save copy, exact mod allowlist, hidden/muted startup and owned
process cleanup, then invokes `Invoke-RuntimeRustyKeyRollingSupply.ps1`. Default
`held_item` mode preserves the old fixed-item threshold smoke. Current-geode mode
requires explicit requirement inventory, acquisition lowering, Master Angler
window index, movement calibration and fishing forecast manifest paths before
any startup. Builds bind the selected E runtime, not an implicit F installation.

The setup helper first asks the existing geode fixture for a current native
prediction. It chooses no hardcoded output: the output must be exact, persistent,
present in the live museum denominator and bound to a native acquisition source.
The existing museum fixture now accepts `qualified_item_id` (legacy default
`(O)96`), validates native registry/eligibility before mutation, and excludes that
exact item while setting 59 donations. Native donation-tile eligibility excludes
occupied pieces, so the fixture still enumerates tiles after its explicit clear.

The original geode fixture is then called again with the same input/counters,
clearing the temporary donated-item inventory. Before measurement, the helper
requires an unchanged save/actor/version, the same predicted output, 59 donations,
an undonated target, and zero target quantity in inventory. Raw requests, replies
and snapshots are retained; the starting snapshot is hash-bound. Unsupported or
drifted predictions stop without rerolling a measured action. Geode input and
counter parameters configure this explicit test boundary, not a learned policy.

All fixture mutation ends before the original rolling controller starts. No
script-generated crack-geode request or second acquisition executor is added.
Even if acquisition, donation and key observation complete, the outer result is
only `runtime_control_observation_pending_independent_native_trace_review` until
the retained canonical custody/consumption and native receipts are reviewed.
It does not prove acquisition of 60 items, pre-Year-3 completion or criterion
partitions. Boundary setup itself explicitly proves no native acquisition.

The preparation transport/guard tests cover 24 positive and rejection cases,
including two different predicted targets, unremoved inventory, already-donated
or mistyped state, missing source, prediction/counter/save/actor/version drift,
setup rejection, replay, missing isolation and all five startup input omissions.
Harness construction and the targeted source-order guard are separately checked;
none of these substitute for a real native run after quiet restrictions lift.

The real public CLI regression now includes 22 fresh-control and 20 support-rebind
checks, covering current intent/ranking and ordinary control commit-receipt CLIs.
The 18 pre-commit step checks remain. `Test-RuntimeRustyKeyRolling.ps1` reuses those
fixtures to run the actual 14-axis batch, source ranking and preparation for
support, shop terminal and current-geode terminal stages without HTTP/game access.
Its separate test doubles
exercise controller stage order, exact retained proof/ledger, failed/foreign/stale
states, budgets, existing-run rejection and both executor adapters. These are
code/synthetic tests, not native game receipts.

The runtime test has 13 named checks. A further 14 calendar checks reproduce the
static-mode geode loss and verify current-source preservation, full denominators,
explicit CC exclusion, actual CLI output, downstream independent reconstruction,
binding tampering, fresh/foreign/version drift and the unchanged strict CC gate.
The synthetic geode fixture includes consistent currency and date-map evidence;
missing evidence was rejected rather than weakening production validation.
Existing rolling 34, threshold 30,
dispatch, supervision and command-batch regressions pass, as do 55 focused
shared runtime source guards. No game was started for these checks.

This controlled calibration entry is implemented, but no native run of this new
entry has been certified. It is not yet the MOD's default production root selector
or a complete criterion/Teacher acceptance. Continue with a same-version isolated
native acquisition -> custody -> donation -> consumption trace, then ambiguous
receipt recovery, custody relocation, joint opening/time budgets, criterion
Teacher/native partitions and the pre-Year-3 deadline. The run summary is only
`runtime_control_observation_only_not_criterion_evidence`; formal training,
preference eligibility and coverage promotion remain false.

The updated shipped-Companion requirement for on-demand critical-decision Teacher
verification remains binding; see `TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`.
This calibration controller neither implements that future product gate nor
waives its fail-closed policy. It does not promote an elapsed-time control choice
to strict-Pareto, a Teacher comparison or ordinary Student runtime authority.
