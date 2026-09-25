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

`build-goal-method-teacher-coverage` is the independent formal-training coverage gate.
It rebuilds this frontier from the raw inputs instead of trusting the generated report,
re-verifies each declared supervision corpus, and derives criterion ownership only through
the production direction catalog and a verified typed source adapter. The acquisition adapter
derives method ownership from frontier requirement-set bindings; non-collection methods remain
source-less until a separate adapter verifies them. A source cannot
name or self-assert covered criteria. Coverage requires an executable method plus explicit
Teacher comparison and verified native-outcome evidence in train, validation, and test.
The current bounded corpus maps all 19 criteria, comparison-covers 4, outcome-covers 4,
and has split-complete comparison/outcome evidence for Community Center, Full Shipment, and
Master Angler. Its verified next-route adapter admits execute-now versus defer pairs only when
the receipt-bound route strictly dominates every other pending route in the selected portfolio.
It still admits exactly the two Community Center criteria after its dynamic denominator,
target-date acquisition chain, and native lifecycle proof closed the shared method. The other
17 criteria remain blocked, so the gate reports `formal_product_training_authorized=false`. Pass a verified
corpus through `Run-Regression.ps1 -GoalMethodCorpusManifest <path>` to re-run this exact
gate against the freshly rebuilt lowering rather than a stale generated artifact.

`build-full-shipment-settlement-receipt` is the fail-closed ordinary native-day adapter for the
Full Shipment recurrence. It accepts exactly one newly settled authoritative item, requires its
native shipped count to move 0 -> 1, the shared bin view to move 1 -> 0, the missing denominator
to decrease by exactly one, the day to advance exactly once, and all other progress to remain
monotonic. It rejects a terminal transition so achievement 34 cannot bypass the stricter terminal
contract.

`build-full-shipment-terminal-settlement-receipt` is the fail-closed terminal
adapter for the same recurrence. It takes the mutually hash-bound authoritative
requirement inventory and acquisition lowering, one independently identified native sleep candidate,
its compiled queue and execution receipt, plus fresh before/after snapshots. Admission requires the exact 154-item
denominator to move from one missing item to complete, the final item to move from zero to one
native shipment, the shared shipping-bin view to settle from one to zero, `total_days` to advance
exactly once, and achievement 34 to appear without regressing existing progress. Hidden isolated
runtime `runtime-full-shipment-terminal-20260925-171152` admitted this final transition through the
existing shipping and sleep chains. The Full Shipment dependency graph remains `in_progress`
because the ordered 154-item fresh-save recurrence and Year 3 deadline proof are still absent; the
coverage gate therefore remains 2/19.

`build-full-shipment-recurrence-proof-receipt` is the fail-closed whole-recurrence adapter. Its
manifest must contain exactly 154 unique authoritative requirements. For each row it rebuilds the
stored acquisition rollout proof, exact Stage-1 `economy.ship_items` Teacher receipt, and either an
ordinary or terminal native-day settlement receipt. The prior settlement artifact is the exact root
of the next acquisition proof; the acquisition/deposit/sleep phase boundaries must have identical
`state_hash` values; save/player/game identity cannot drift; shipment counts must advance 0 -> 154
without gaps; settlement time cannot reverse; and only row 154 may end with achievement 34 at or
before total day 224. This command verifies supplied artifacts but does not generate the
154-step fresh-save runtime chain and does not authorize formal training.

`compile-acquisition-route-dispatch` closes the production boundary between one independently
selected, atomically reserved acquisition route and the existing Product compilers. In addition to
the execution-binding inputs, it requires `--ranking`, `--queue-output`, and `--output`. It finds a
same-state live candidate only when item, endpoint option, and authoritative route source all match;
then it removes learner rank, score, model score, and reward before invoking `DailyPlanCompiler` and
`ActionQueueCompiler`. Before selection, the command independently rebuilds every endpoint candidate
from the same transparent snapshot and committed ledger. Candidate membership and every non-learning
field must equal the supplied ranking; only rank, score, model score, expected reward, and model-source
metadata are ignored. Expanded native primitives retain route, reservation, source-candidate, source-
ranking, and source-evidence lineage. Legacy high-level `option_request` queues and expanded
`compiled_action_steps` queues have distinct validation shapes and cannot masquerade as each other.
Exact source binding currently covers shops, crops, target-date location or mine fish, crab-pot
outputs, regular or deluxe animal products, fish ponds, fruit trees, location forage, solar panels, base
wild-tree seeds, wild-tree seed-drop rows, wild-tree chop rows, wild-tree tapper outputs, and fixed native bush, tea, spring-onion,
ginger, tree-moss harvests, location and object-data artifact spots, explicit and bounded-default geode drops, and
ordinary machine rules plus legacy unique flavored or literal machine output rows, exact selected-monster
`Data/Monsters` reroll rows, exact selected stone-95 radioactive ore nodes, and learned cooking recipes. Monster rows are exposed only from the native burglar-ring
probability projection and only for the runtime monster selected by the current rolling floor step; the mutable
`objectsToDrop` list is not treated as provenance. Radioactive ore requires the same selected tile, source object,
direct-node branch and guaranteed `(O)909` projection. Ordinary
ready machines bind through the persisted native `lastOutputRuleId`; the row fallback is used only when that field is
absent and does not choose among duplicate matching rows. Other route kinds fail
closed until their live candidates expose exact source identity. A selected item with more than one distinct
matching source row also fails closed rather than creating an ambiguous teacher label. Wild-tree chopping is the
bounded exception: its complete output domain remains visible, while the dispatch projection selects the lowest
currently applicable `ChopItems` row per target item because one native chop executes all applicable rows. Required supporting options
also remain upstream work. The report and queue paths must differ. A blocked build atomically
overwrites the queue path with a non-executable blocked envelope, so a stale successful queue cannot
survive a failed rebuild. This command does not run the queue or authorize formal training.

`build-goal-method-coverage-reconciliation` is the read-only denominator and gap audit for that
gate. It takes the same frontier inputs and `--request`, rebuilds both authorities, and joins the
result to the current option-governance matrix. Its report separates 19 score criteria, 11 root
methods, and the distinct existing options referenced by those methods; it must not describe a
criterion coverage count as an action implementation count. Each method records typed dependency
blockers, implemented versus active Teacher adapters, split evidence, and an inventory-only view of
referenced option evidence and Product Executor status. Option inventory diagnostics never become
current method blockers or next actions; method work is derived only from the rebuilt frontier and
verified Teacher/native coverage. The current report classifies 2 criteria as coverage-ready, 15 as dependency-graph
incomplete, pet love as production evidence not connected, and Skull Key as missing a typed
Teacher source adapter. It never authorizes formal product training.

`build-pet-love-teacher-corpus --request <path> --output <path>` builds the first
non-collection coverage source. Each request row names fresh before/after snapshots and one
`training_execution_result.v1`; the builder derives method ownership and dataset partition,
recomputes snapshot hashes, and accepts only an exact native pet interaction that crosses from
988-999 friendship to 1000 and produces `petLoveMessage`. Repeated evidence from the same
save/player/day is rejected. The focused three-partition fixture exercises a 3/19 coverage result
and tamper rejection, but the authoritative current report stays at 2/19 until equivalent native
receipts exist in train, validation, and test.
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

The requirement report also contains a version-locked Community Center denominator catalog built
from runtime `Data/Bundles`, `Data/RandomBundles`, and guarded decompiled generation/persistence
rules. `build-current-community-center-denominator --requirement-inventory <path> --snapshot
<path> --output <path>` binds an active save to either the exact 30-bundle standard layout or a
whole-area native remixed realization. It reports the Missing Bundle separately, rejects unknown or
tampered bundle rows, and never counts the supplemental row toward Community Center completion.
The inventory also binds all 175 selectable ingredient identities to 185 concrete acquisition
targets and authoritative routes. Category ingredients are expanded to the exact native object set
(eight egg targets for `-5`, four milk targets for `-6`), while item and money identities remain
typed. The current denominator carries and hashes those targets, so acquisition-route drift fails
closed instead of silently preserving a stale denominator.
`self-test-current-community-center-denominator` covers standard, remixed, and tamper-rejection
paths, locks the 175/185 catalog counts and verifies acquisition coverage for every active ingredient;
it is part of `Run-Regression.ps1`. Current collection-frontier, preference and receipt builds now
rebuild and consume that save-bound denominator instead of the static `community_center_standard`
alternatives. The stable set ID is retained for compatibility, category ingredients remain one native
slot with concrete accepted item targets, and the denominator hash is propagated into Teacher
supervision and required by dataset validation. The save-bound acquisition calendar root can be built with:

```powershell
dotnet run --project StardewAI.GoalConditionedBootstrap -- `
  build-current-acquisition-route-calendar-resolution `
  --requirement-inventory <path> `
  --acquisition-lowering <path> `
  --master-angler-windows <path> `
  --snapshot <path> `
  --output <path>
```

This command strictly rebuilds the current denominator, replaces only the static Community Center route
occurrences, and records bundle-mode, denominator, source-state and snapshot hashes. It does not authorize
the existing static target-date chain. The first current target-date stage is:

```powershell
dotnet run --project StardewAI.GoalConditionedBootstrap -- `
  build-current-acquisition-route-target-date-calendar `
  --requirement-inventory <path> `
  --acquisition-lowering <path> `
  --master-angler-windows <path> `
  --calendar-resolution <current-root-path> `
  --snapshot <same-snapshot-path> `
  --target-total-day <day> `
  --output <path>
```

It deterministically rebuilds and compares the current root before evaluating the shared calendar axis,
then carries the current denominator provenance into its output. The existing downstream commands require no
current-specific variants: unlock chooses deterministic static/current recompilation from this artifact, and every
later dependency axis recursively rebuilds its predecessor and hash-binds that identity through opportunity cost.
Portfolio admission now derives one typed `community_center_provenance` value from that target-date artifact and the
decision snapshot. Teacher preference, atomic commit receipt, execution binding, fresh terminal receipt, settlement,
rollout checkpoint, continuation request, terminal proof/admission and every supervision row copy and cross-check the
same value. A continuation may advance its source state/snapshot hashes, but cannot switch static/current mode, bundle
mode or denominator hash inside one rollout. Static evidence must keep every current-save provenance field empty.
The Junimo unlock, room reward/mail and final-ceremony settlement chain is still incomplete, so formal product
training remains disabled.

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

`Invoke-RuntimeFullShipmentTerminalSmoke.ps1` proves the final Full Shipment day transition without
adding another shipping or sleep executor. It copies one runtime save, prepares the copy at exact
153/154 through a debug-only native-eligibility fixture, deposits one authoritative terminal item through
`economy.ship_items`, relocates only the isolated actor to the existing native sleep path, and executes
`recovery.stabilize_day`. The resulting queue, execution receipt, and fresh snapshots are admitted by
`build-full-shipment-terminal-settlement-receipt` only when the exact item settles 0 -> 1, all shared bin
views settle 1 -> 0, the day advances once, 154/154 is reached, and achievement 34 appears. This is a
runtime calibration receipt, not a fresh-save proof of the complete 154-item recurrence and not formal
training authorization.

The ordinary and terminal settlement builders now share one exact projection verifier. Ordinary
steps require exactly one missing item to settle while achievement 34 stays absent; the terminal
wrapper additionally requires 153/154 -> 154/154 and the native achievement transition. The ordered
recurrence manifest and verifier now bind those receipts to rebuilt acquisition rollouts and shipping
Teacher receipts. The missing artifact is a populated 154-row fresh-save runtime chain; it may not
replace Product queues with fixture mutations or infer continuity from item IDs alone. Until that
artifact passes, Full Shipment remains `in_progress`, coverage remains 2/19, and training remains off.

## Hardware

The recorder, semanticizer, retriever, deterministic teacher, regression suite,
and an initial small CPU behavior-cloning baseline do not need the RTX 5070 8 GB
laptop. The 5070 becomes useful after the expert dataset exists, for sequence
models, larger batches, mixed-precision experiments, and checkpoint sweeps. GPU
compute is not the current blocker.
