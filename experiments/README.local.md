# Goal-conditioned bootstrap lab

This directory is an isolated, local-only experiment based on source commit
`f0902d586647ac1f11fa34bf7c177f153a2a7043`. It must not be committed or pushed
until its contracts and results have been reviewed for integration.

## Problem being corrected

The existing structured policy can clone whichever candidate the current policy
selected. That cannot reliably learn the reverse mapping from a long-term goal to
the methods and day plans which achieve it. Failed or incomplete AI rollouts can
therefore reinforce their own omissions.

The bootstrap path is now:

1. authoritative goal/dependency knowledge;
2. authoritative deterministic teacher demonstrations, with human demonstrations optional;
3. nearest successful demonstration retrieval for the current state;
4. deterministic reconciliation against current candidates and hard constraints;
5. the existing daily-plan compiler, action-queue compiler, and executor;
6. fresh-state effect and completed-day verification before admission.

The experiment does not replace the production compiler or executor. It only
supplies a better goal-to-method/day-plan teacher upstream.

The current convergence and supervision contract is
[`../docs/TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`](../docs/TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md).
The deterministic Teacher is bounded and reproducible, but the complete system is not yet
converged: only 2/19 criterion frontiers are executable and the production trainer still
derives pair direction from `candidate.Selected`. No formal dataset or checkpoint may be
promoted until explicit `teacher_preference`, `native_outcome` and `student_observation`
provenance replaces that behavior.

## Isolation and admission rules

- `StardewAI.DemonstrationRecorder` observes input and semantic boundaries. It
  never suppresses or injects player input.
- Snapshot calls are asynchronous, coalesced, profile-limited, and rate-limited.
- A raw recording is never expert data by itself.
- Semantic admission requires a sealed manifest, verified SHA-256 sources, fresh
  start/end state hashes, explicit intent marks, verified semantic segments, and
  a completed day boundary.
- Legacy AI rollouts remain evaluation/executor evidence and cannot teach the
  goal-to-method policy.
- Existing model scores are not used as teacher labels.
- Student selections are observations only. They cannot become positives without an
  independent Teacher preference or separately attributed native outcome.
- Demonstrations guide ordering only inside the same hard priority class. Runtime
  availability, timeline, safety, and deterministic completion stay authoritative.

## Recorder commands

The recorder is built but not installed by this experiment.

```text
stardewai_demo_start [goal_id] [label]
stardewai_demo_mark <method_id> [option_id]
stardewai_demo_stop
```

After recording, review an annotations file based on
`recording-admission-annotations.example.json`, then semanticize it:

```powershell
dotnet run --project .\experiments\StardewAI.GoalConditionedBootstrap -- `
  semanticize-recording `
  --knowledge <goal-dependency-index.json> `
  --recording <sealed-recording-directory> `
  --annotations <reviewed-annotations.json> `
  --output <expert-demonstrations.jsonl>
```

The `teacher-plan` command accepts optional `--demonstrations` and `--query`
arguments. Without an admitted match it falls back to the deterministic teacher;
it never falls back to a legacy AI rollout.

## Current status

- Both projects build with zero warnings and zero errors.
- The v24 evidence gate passes: nine locked artifacts and 3,550/3,550 runtime Content
  files match, with no missing, unexpected, size-mismatched, or hash-mismatched XNB files.
- The criterion claim gate passes: 19/19 Grandpa criteria are covered, seven native/runtime
  source locks match, and there are no missing criteria or unresolved conflicts.
- The native 15/25 skill thresholds are proven equivalent to vanilla total trainable-skill
  levels 30/50. The 31st runtime bundle is the post-Community-Center Missing Bundle, not a
  standard Community Center obligation.
- The real r36 regression plans one mailbox action and all six same-location tree
  harvests while deferring 19 optional remote social route prefixes.
- All 226 sampled legacy AI trajectories are rejected as expert data.
- A sealed human fixture is admitted, retrieved first for a nearby state, reaches
  teacher-plan ordering, and is rejected after source tampering.
- No real human gameplay recording is required for bootstrap training.
- No model training has resumed yet. Stage 1 is the fresh-save native Grandpa
  `21/21` target; Stage 2 warm-starts from that admitted checkpoint and targets the
  native Perfection tracker at `100%`.
- A generated, source-hashed requirement inventory now fixes all four collection
  denominators used by Stage 1: 154 Full Shipment objects, 72 Master Angler fish,
  95 museum donations, and 30 standard Community Center bundles. The Missing Bundle
  remains supplemental and is not counted as a Community Center prerequisite.
- The inventory contains 351 requirement groups and now has at least one
  identity-safe, source-hashed native acquisition source for all 351. This closes
  denominator and first-source omission detection; it does not yet prove the
  calendar, unlock, resource, candidate, compiler, or native-receipt chain for each
  source and therefore does not authorize training by itself.
- `acquisition-route-option-lowering-v1.json` now classifies all 33 route kinds and
  all 1,599 route occurrences across those four sets against the current option
  governance matrix. Unknown route kinds and stale catalog rows fail closed. The
  report distinguishes normally trainable options, isolated-clone teacher options,
  runtime-only deterministic dependencies, high-level options pending runtime
  admission, primitive-only gaps, and genuinely missing options instead of treating
  every source as executable.
- The current exact result is 33/33 admitted route kinds. Wild-tree chop drops lower through
  `foraging.chop_wild_tree -> clear_obstacle_tile -> executor.clear_obstacle`; the Fall
  hazelnut row in `Data/WildTrees.SeedDropItems` lowers through the existing native tree-shake
  option rather than chopping. Both artifact-spot routes lower through the single
  `foraging.excavate_artifact_spots -> clear_obstacle_tile -> executor.clear_obstacle`
  chain, with `(O)SeedSpot` and other clearables excluded upstream. EVD-334 verifies that
  exact high-level chain through hidden native Hoe execution, projected outputs, skill
  experience, durable counters and a fresh post-action snapshot. The existing primitive
  evidence was not used as a substitute for this high-level receipt.
- EVD-335 lowers `native_spring_onion_harvest` through the single
  `foraging.harvest_spring_onions -> harvest_crop_tile -> executor.harvest_crop` chain.
  The bridge derives `(O)399` only for exact base forage crop ID `1`, because the native
  crop keeps `indexOfHarvest` empty and creates the item inside `Crop.harvest`. Hidden
  runtime evidence verifies inventory, exact `+3` Foraging XP, crop removal and a fresh
  state hash; ordinary crops, ginger and custom crops remain outside this evidence scope.
- This lowering is deliberately a terminal-transition join, not a fresh-save route
  proof. Calendar, unlock, facility, input-resource, calibrated travel-time, native
  outcome/retry, reservation, and fresh-receipt dependencies still have to be closed
  before a route can supervise formal training.
- `build-current-full-shipment-teacher-frontier` performs the first live requirement join. It
  requires a fresh snapshot and same-state ranking, validates the complete 154-item transparent
  denominator, and emits positive bindings only for exact missing-item completion or admitted
  acquisition endpoint candidates. Requirements absent from the current candidate pool are
  deferred and never emitted as negative labels. The real r36 fixture has 6 completed and 148
  missing requirements but no current exact candidate, so it correctly remains label-ineligible.
- The transparent bridge publishes the exact live fish and museum collection rows,
  including missing IDs. Static catalogs are cached by the live `Data/Objects`
  instance while per-save completion remains fresh on every snapshot.
- `complete_master_angler` can label `catch_fish` only when the candidate's complete
  outcome set intersects the current native missing-fish denominator. Already-caught
  outcomes are excluded upstream instead of becoming repeated teacher positives.
- `Data/Fish` rows are treated as constraints, not acquisition actions. The generated
  `master-angler-opportunity-catalog-v1.json` proves the exact source partition:
  60 location-rule rod species, two guarded MineShaft override species, and ten guarded
  Crab Pot species. The direction binder reuses `fishing.catch_fish` and
  `fishing.collect_crab_pots`; both require a fresh exact missing-species intersection.
- All 180 matching location rules have normalized static year, season, clock and weather
  constraints. The normalizer is guarded by a hashed decompile of `GameStateQuery.cs`, including
  its minimum-year semantics for `YEAR 2`. Festival, special-order and random predicates remain
  unresolved dynamic gates at opportunity-selection time rather than being treated as true.
- The decompile-wide `getFish` override inventory is closed at five files. MineShaft contributes
  four collection-fish area routes: Stonefish 0/10, Ice Pip 40, and Lava Eel 80; the other native
  overrides are guarded non-collection injections or a location-table redirect.
- `master-angler-stage-one-window-index-v1.json` expands every source through the exclusive Year 3
  Spring 1 deadline (`total_day=224`) and gives each species an earliest/latest static opportunity
  day. It is an urgency index, not a teacher label: dynamic conditions, unlocks, routes, fishable
  tiles, equipment, the existing live candidate and fresh native receipts still fail closed.
- Source enumeration and the current-date route/time leaf are closed. A current intent now requires
  complete date-bound static walkability/gates, a SHA-256-locked conservative movement calibration,
  every remaining connector, and enough post-arrival time for the terminal catch. Future-date
  scheduling, proactive trap infrastructure, stochastic retry budgeting, and fresh native deadline
  receipts remain required before Master Angler is a complete fresh-save Teacher route.
- Stage 2 checkpoint continuation is implemented but remains unexercised by formal training.
  `StructuredPolicyTrainer` can initialize from the admitted Stage 1 checkpoint, preserve its
  feature vocabulary, rebase inherited weights onto the new normalization scale without changing
  pre-optimization candidate order, and add goal-specific Perfection features. The CLI uses
  `--initialize-from-checkpoint`; LiveTrainingLoop uses
  `--policy-initialization-checkpoint-path`. The child checkpoint records the parent ID, SHA-256,
  inherited feature count, and new feature count.
- This mechanism does not make Perfection self-authorizing. New goal rules, dependency routes,
  candidates, compiler bindings, transparent fields, and native receipts still require separate
  admission before teacher labels may be generated.
- The current Stage 1 frontier is still incomplete: 2/19 criteria are executable and
  17/19 have typed dependency graphs still pending closure; no criterion lacks a graph.
  Four normally confirmation-gated
  options have isolated teacher authorization, so governance blockers are now zero;
  this does not bypass runtime receipts or make an incompletely expanded route valid.
- Native combat XP evidence covers all six skill indices, including the rejected Luck
  path. The bounded friendship rollout and native Grandpa event `558291` are admitted
  runtime evidence, but neither is a complete fresh-save 21-point proof.

## Authority gates

`Run-Regression.ps1` first verifies the evidence lock and claim ledger, rebuilds the
current option matrix, then generates the authoritative requirement inventory, its
exact route-to-option lowering, the same-state live Full Shipment Teacher frontier, the
goal-method frontier, and the bootstrap regression.
Any source drift,
denominator mismatch, unknown option binding, or unresolved factual claim fails before
teacher output is generated. Every requirement-inventory source is rehashed when the
frontier is built.

The current source rebuild is deliberately separate from immutable v24 game truth. It pins
the detached base commit, option/governance source hashes, current snapshot hash, native
action denominator, fixed generation timestamp, and output hash. Its current result is 228
registered options, 151 runtime-verified options, and 62 training-eligible options.

The goal-method frontier does not maintain a second direction catalog. Direction metadata,
criterion ownership, permitted option IDs, effective goals, and demand families come only
from `src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs`, which is also consumed by the
production sample adapter and daily subgoal resolver. The local
`goal-method-expansion-overlay.v1.json` stores verified claim IDs and only pre-graph blocker
descriptions for a direction that does not yet have a dependency graph. Once a graph exists,
its blockers come solely from `goal-method-dependency-expansions.v1.json`; they are not copied
back into the overlay. Regression requires exact one-to-one direction coverage and hashes both
sources, so either production drift or experimental drift fails closed. The dependency file
also stores typed reverse-route nodes and edges. Policy-option nodes must reference
training-eligible options, while deterministic
compiler-owned transitions need runtime verification and never become model labels. The first
complete multi-step route is `earn_pet_love`: exact initial-adoption event gate, native event
acceptance and naming, daily pet/bowl care, and native day settlement.
The current graph report is `local-data/output/goal-method-frontier-v3.json`.
Its `breadth_coverage` section classifies every criterion exactly once, gives every
non-executable criterion a resolvable typed blocker ID, and clusters policy or
deterministic option dependencies reused by multiple directions. Regression rejects
missing classifications, dangling blocker references, and false shared clusters. The
shared leaves include `recovery.stabilize_day`, `fishing.catch_fish`,
`farm.collect_animal_products`, and the other reused option IDs in the generated report.
Museum completion and Rusty Key additionally share the typed
`museum_item_acquisition_and_reservation` family. Shared dependencies must be improved
once and reused rather than reimplemented per Grandpa direction.
The current requirement report is
`local-data/output/authoritative-requirement-inventory-v1.json`. Current candidate membership
for Full Shipment, Master Angler, Museum Collection, and the standard Community Center can be
built with `build-current-stage-one-collection-teacher-frontier`. The command hash-binds the
authoritative inventory and acquisition lowering to one transparent snapshot, its same-state
candidate ranking, and Master Angler's date-window intents. A physical candidate is emitted once
with every exact requirement credit it can advance; unavailable requirements are deferred rather
than converted to negatives. `build-current-stage-one-collection-teacher-preference` rebuilds that
membership and applies a fixed learner-independent lexicographic policy over authoritative deadline,
terminal-transition, exact shared-credit, scarcity, deterministic-time and energy evidence. Learner
rank, score, model score and expected reward cannot decide the label and are cleared before the
selected candidate enters the existing daily-plan and action-queue compilers. An exact top tie, an
incomplete transparent denominator or a blocked compiled queue fails closed. Non-selected current
candidates remain counterfactual alternatives rather than negative examples. This report still records
`formal_training_authorized=false`: the next admission slice requires a fresh native before/after receipt
for the selected action. Future scheduling and the remaining long-horizon 19-criterion proofs also still
block formal training.

`build-current-stage-one-collection-teacher-receipt` now admits either the legacy exact single-primitive
receipt or `queue_execution_receipt.v1` for `1..8` ordered queue items. It requires the persisted
`--preference` artifact that was actually executed and all five source artifacts, recomputes the Teacher
semantics, preserves the original queue identity, and verifies every item, primitive, effective command,
fresh state/tick boundary and hash-chain transition. Only the last item may complete the fixed candidate,
and every credited requirement must then be proven from the fresh after snapshot before an existing
`policy_decision_trajectory.v2` row is emitted.

`Invoke-CurrentStageOneCollectionTeacherProductRollout.ps1` runs up to 16 state-dependent episodes in one
hidden, silent, isolated game process. Each episode regenerates the complete current candidate set and an
independent Teacher preference. Applied continuation steps without requirement progress remain unlabelled;
contract failures still stop immediately. The preserved run
`bounded-stage-one-collection-teacher-20260911-211944` executed machine collection as
`move_to_tile -> collect_machine_output`, withheld a row for shipping-bin approach, and admitted the
subsequent native deposit. Its canonical dataset has `2` accepted, `0` rejected, `0` duplicate and `0`
conflicting rows. `formal_training_authorized=false` remains explicit; the next step is to collapse the
remaining collection approach/terminal continuations into bounded whole-candidate queues before expanding
future-date coverage.

## Hardware

The recorder, semanticizer, retriever, deterministic teacher, regression suite,
and an initial small CPU behavior-cloning baseline do not need the RTX 5070 8 GB
laptop. The 5070 becomes useful after the expert dataset exists, for sequence
models, larger batches, mixed-precision experiments, and checkpoint sweeps. GPU
compute is not the current blocker.
