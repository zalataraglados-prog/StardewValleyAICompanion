# Goal-Conditioned No-Human Teacher Plan

Status: approved architecture correction, implementation started in an isolated detached
worktree. This document is the continuity source for the correction. It does not promote
the local experiment into the product repository or authorize formal training.

The normative Teacher/Student supervision and convergence rules are in
[`TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md`](TEACHER_STUDENT_CONVERGENCE_CONTRACT_CN.md).
In particular, `selected=true` is an observed Student action, not an admissible positive
label. Historical behavior-cloning runs remain execution/control evidence only until the
typed supervision-source contract and independent Teacher relabel path are implemented.

## Why this correction exists

The existing structured policy training path can treat the option selected by the current
policy as the positive example. Long-horizon outcome weights change example strength, but a
zero-return rollout is not automatically converted into the correct alternative action.
This creates a self-reinforcing omission loop: if the current policy repeatedly selects pet
care while omitting harvests, its own trajectories mostly teach that same selection.

Human demonstrations would provide an independent bootstrap signal, but they are not
available in the required quantity and must not become a prerequisite. The corrected design
therefore uses an authoritative, deterministic teacher to generate and relabel strategy
examples. Human recordings remain optional post-baseline calibration evidence only.

## Fixed product target

- Stage 1 starts from a new save and earns all 21 native Grandpa rule points by the initial Year 3,
  Spring 1 evaluation.
- Four candles at 12 points are an intermediate milestone, never a planner stop condition.
- Stage 2 warm-starts from the admitted Stage 1 checkpoint and reaches 100% on the native
  Perfection tracker. Stage 2 is a separate goal condition and acceptance manifest; it must not
  silently change the Stage 1 denominator or deadline.
- Use the Community Center route. A Joja conversion cannot satisfy the full native
  Community Center score path.
- Do not use glitches, item spawning, direct save edits, direct state mutation, coordinate
  teleportation, or an LLM controlling primitive input.
- Reuse the single existing candidate, daily-plan, ActionQueue, Product Executor, and fresh
  post-state verification paths. The teacher must not create a second executor.
- Keep player-command-only and cosmetic actions outside autonomous candidates and policy
  training.

The intended Stage 2 expansion is controlled rather than unsupervised. The Stage 1 model may
propose routes for newly opened Perfection subgoals, and teacher relabeling may turn those visited
states into new training data. New Perfection dependencies, fields, candidates, compiler bindings
and native receipts must still be admitted before they can receive a positive label. This preserves
the transferable farming, economy, routing, timing and resource-allocation policy learned for 21
points without treating model confidence as evidence of a previously unknown rule.

## Evidence authority and conflict policy

Truth order:

1. Runtime-loaded content for the exact game version and active mod set.
2. Decompiled methods and IL for executable semantics, formulas, conditions, and effects.
3. Stardew Valley Wiki revisions for independent omission detection and corroboration.
4. Versioned strategy, min-max, and speedrun guides for strategy hypotheses only.
5. Isolated real-game rollouts for end-to-end proof.

Wiki or guide text cannot create or override a runtime field or native rule. Every imported
claim receives a stable claim ID, source URL/revision, native evidence reference, verdict,
and executable implication. A disagreement is blocking until runtime/decompile evidence
resolves it. For example, the Wiki describes total skill levels 30/50 while native Grandpa
code checks `Farmer.Level >= 15/25`; the property conversion must be proven rather than
assuming either wording is directly interchangeable.

The locked `game-1.6.15-20260723T093543Z-linux-v24` profile is the current source. Its goal
index contains 19 criteria totaling 21 points, 31 bundle records, 231 recipe outputs, and no
goal-index blockers. Its authoritative dependency graph contains 35,335 nodes and 41,262
edges. The historical v19 and current v24 goal-index payloads differ only in generation time,
but all new outputs must still pin v24 explicitly.

The immutable v24 source validation contains the historical warning
`content_root_not_supplied`. Slice 1 closes that warning with a supplemental audit which
rehashes all 3,550 XNB files against the isolated 1.6.15 Content directory and binds the
result to the locked raw manifest. The immutable source artifact is not rewritten.

Reviewed secondary sources:

- https://stardewvalleywiki.com/Grandpa
- https://stardewvalleywiki.com/Bundles
- https://stardewvalleywiki.com/Fish
- https://stardewvalleywiki.com/Crops
- https://github.com/Zamiell/stardew-valley/blob/main/Min-Max_Guide.md

The min-max guide is useful for opportunity-cost and route hypotheses, but it explicitly
optimizes early money and omits friendship, museum, and most Community Center work. It is
therefore not a complete teacher for the 21-point target.

## Target hypergraph

Required node classes:

- score criterion and milestone;
- inventory, quality, quantity, currency, and skill requirement;
- capability, location, event, recipe, shop, building, and tool unlock;
- crop, fish, forage, monster, artifact, machine, animal, social, and quest acquisition;
- calendar, weather, clock, NPC schedule, construction, and processing window;
- resource reservation, production capacity, route budget, and stochastic outcome;
- model-level option, candidate kind, compiler binding, and runtime primitive.

Required edge classes:

- AND prerequisite;
- OR acquisition alternative;
- consumes, produces, preserves, and unlocks;
- must-start-before, available-during, repeat-after, and finishes-after;
- deterministic effect and stochastic distribution;
- supports-score-criterion and compiles-to-option.

Every edge carries exact provenance and a confidence/admission state. Missing edges remain
typed blockers; they are never inferred from an example or silently skipped.

`src/StardewAI.Core/Training/GrandpaDirectionCatalog.cs` is the sole source for direction,
criterion, option, effective-goal, demand-family, label, and feedback mappings. The sample
adapter, daily subgoal resolver, and isolated frontier generator all consume that same
catalog. The experiment's `goal-method-expansion-overlay.v1.json` contains only unresolved
dependency descriptions and authority claim IDs. It must never repeat production mappings;
otherwise a later edit could silently create two incompatible goal-to-method systems.

## Execution slices and exit conditions

### Slice 1: Evidence freeze

Generate and verify a machine-readable v24 evidence lock for the goal index, dependency
graph, progression graph, option governance, runtime assembly identity, source validation,
and Wiki registry. Rehash the isolated Content root.

Exit: every locked hash matches, assembly identity is exact, source validation has zero
blocking issues, the direct Content rehash closes `content_root_not_supplied`, and source
drift fails closed.

### Slice 2: Claim and conflict ledger

Create a typed ledger for all 19 Grandpa criteria and every rule used by their acquisition
paths. Record native evidence, runtime data, Wiki corroboration, guide hypotheses, verdict,
and affected graph edges.

Exit: every criterion has native plus runtime provenance and independent review; every
disagreement is resolved or blocks downstream generation. Acquisition-rule claims are
appended transactionally during Slice 3 and cannot enter the graph before passing this gate.

### Slice 3: Complete reverse hypergraph

Expand each criterion backward through all valid methods until each branch reaches an
existing training-eligible option or an explicit implementation blocker. Initial domains
are earnings, aggregate skills, museum completion, Skull Key, Community Center and ceremony,
marriage plus house level 2, Rusty Key, Master Angler, Full Shipment, friendships, and pet
love.

Exit: 19/19 criteria have at least one complete executable route, all alternatives and
calendar gates are represented, and no route terminates at an untyped prose instruction.

### Slice 4: Feasible frontier and deadline propagation

From a fresh state, propagate latest-start dates, seasonal windows, construction/processing
lead time, reserve quantities, money, energy, route time, and opportunity cost. Generate
mandatory-today, prepare-ahead, maintenance, strategic, opportunistic, and deferred sets.

Exit: every unmet criterion produces an admitted method frontier or a machine-readable
unsatisfiable proof. Upstream facts eliminate impossible candidates before compilation.

### Slice 5: Hierarchical deterministic teacher

Plan in four layers: 21-point target, season commitments, weekly resource commitments, and
daily location bundles. Optimize lexicographically for target feasibility, irreversible
deadlines, completion slack, resource/time efficiency, and only then style.

Exit: the teacher never uses the existing policy score as a label, never bypasses hard
constraints, and every selected model-level option compiles through the existing chain.

### Slice 6: Autonomous teacher dataset

Fork isolated save checkpoints, execute teacher plans in the real game, capture fresh
before/after state, and retain verified outcomes. Generate counterfactual branches for close
alternatives. Learner-visited states are relabeled by the teacher; learner selections are
never accepted as positives merely because they were selected.

Exit: every admitted row has source hashes, goal/method labels, complete candidate context,
real execution receipts, a completed day boundary, and long-horizon return binding.
Teacher preference, native outcome and Student behavior must be stored as three explicit
provenance classes. The dataset builder must reject rows which infer preference from the
Student's selected flag.

### Slice 7: Curriculum training

Train goal-to-method first, then method bundles, day plans, multi-day values, and recovery.
Mechanical movement, combat, tool use, harvesting, menus, and interaction sequences remain
compiler/executor responsibilities.

Exit: held-out teacher states improve over the deterministic V0 reference without option
vocabulary leakage or executor-calibration contamination.

This is operational convergence, not a global-optimum proof. Freeze thresholds for held-out
Teacher disagreement, learner-state recovery, native 21/21 success, hard-rule violations and
runtime performance before evaluating a candidate checkpoint; do not tune the thresholds on
the same evaluation run.

### Slice 8: Robustness and Year-3 evaluation

Run fixed-seed regression, then unseen seeds across all supported farm maps and standard or
remixed Community Center states. Track frame time and snapshot volume as acceptance metrics.

Exit: unattended isolated runs reach exactly 21 verified points by the initial evaluation,
with no missing fields, direct state writes, stuck action loops, duplicate execution paths,
or snapshot-induced performance regression.

### Slice 9: Promotion and later human adaptation

Review the isolated implementation. Promote only reusable contracts and source after all
gates pass. Keep datasets, checkpoints, recordings, binaries, and experiment reports in
hash-locked local storage. Human-like pacing and preferences remain a reversible layer over
the frozen strongest policy.

Exit: the perfect baseline is independently reproducible and human adaptation can be turned
off without changing its data or checkpoint.

## Information leakage policy

A privileged teacher may use simulator branches and future outcomes to estimate value, but
must not teach an action that depends on information absent from the student feature set.
For normal play, unknown RNG is marginalized across scenarios. If a highest-intelligence
profile exposes a 100-day deterministic forecast, that forecast must be an explicit,
versioned transparent input available at inference time.

## Hardware boundary

Slices 1 through 6 are data integrity, graph search, game execution, and dataset generation;
they do not require the RTX 5070 laptop. The 5070 8 GB node becomes useful in Slice 7 for
sequence models, mixed precision, larger batches, and checkpoint comparisons. GPU compute is
not a substitute for the missing goal-method graph or teacher correctness.

The current `119` host may carry the remaining Slice 3 through Slice 6 game-runtime work only as
single-concurrency, bounded jobs. Source edits, builds, regression tests, release assembly and
evidence review remain local; the server receives only a manifest-locked release and a cloned save.
The server is not a replacement for the Slice 7 GPU node. Multi-day server rollout must wait for
content-addressed or delta snapshot storage because the current full-snapshot loop grows too quickly
for the host's remaining disk.

## Execution status

### 2026-09-05: Slice 1 passed

- Nine locked knowledge/raw artifacts match exact size and SHA-256.
- The runtime assembly is Stardew Valley 1.6.15.24356 with MVID
  `46c95350-5805-4442-8e93-61092d55e101`.
- The isolated Content root matches 3,550/3,550 manifest entries, with zero missing,
  unexpected, size-mismatched, or hash-mismatched XNB files.
- The direct Content aggregate SHA-256 is
  `8dac17912064d68256fa2299fc998de8622a8298417ac9de287920d7eb4c2b91`.
- Machine report: `experiments/local-data/output/evidence-audit-v24.json`.

### 2026-09-05: Slice 2 criterion contract passed

- All 19/19 Grandpa criteria are covered by verified claims.
- Seven decompile/runtime source files are hash-locked; unresolved claims and missing
  criteria are both zero.
- Native `Farmer.Level` is the sum of six base skill fields divided by two. Native
  `gainExperience` rejects Luck skill index 5, so the five trainable vanilla skills at
  totals 30/50 correspond to the score thresholds 15/25. Temporary buffs are excluded.
- Runtime `Data/Bundles` has 31 records: 30 standard Community Center bundles plus
  `Abandoned Joja Mart/36` (The Missing), which is a post-completion Movie Theater path.
- Native `isLocationAccessible("CommunityCenter")` requires event `191393`; this is the
  completed Community Center ceremony, not ordinary physical access to the building.
- One early-money guide claim remains deliberately classified as a strategy hypothesis,
  not a fact or teacher label.
- Machine report: `experiments/local-data/output/claim-conflict-audit-v1.json`.

Slice 3 is next: expand all 19 criteria backward to training-eligible options while adding
and auditing each acquisition rule before it is admitted to the hypergraph.

### 2026-09-05: Slice 3 root frontier established

- The current source option matrix is rebuilt reproducibly in the isolated lab instead of
  using the July v24 governance snapshot. Current counts are 228 registered options, 151
  runtime-verified options, and 62 training-eligible options.
- All 19 criteria bind to 11 explicit root methods and existing option IDs from the sole
  production `GrandpaDirectionCatalog`. Unknown claims, option IDs, criteria, duplicate
  criteria, or a missing/extra dependency overlay direction fail generation.
- The sample adapter, daily subgoal resolver, and isolated V0 teacher no longer maintain
  independent Grandpa mapping switches. Ambiguous unbound candidates receive no guessed
  Grandpa direction; an explicit valid binding or a unique catalog match is required.
- Two criteria currently have fully expanded, training-eligible frontiers:
  `skull_key` through the existing `mining.obtain_skull_key` chain, and `pet_love` through
  exact initial adoption, native event acceptance/naming, daily care and native day settlement.
- Twelve criteria are connected but still have typed dependency-expansion work. This is
  knowledge translation work, not evidence that their underlying actions are absent.
- Five criteria are blocked by current product governance: museum donation affects two
  criteria, Community Center donation affects two, and partnership plus farmhouse upgrade
  affects one. Their native executors are runtime-verified but the high-level options remain
  `EvaluationOnly` due explicit-confirmation policy.
- `2/19` is therefore the count of complete goal routes, not an action implementation count.
  It must never be reported as "only two actions exist."
- The pet route locks the actual 1.6.15 cat/dog event keys. Their `d` precondition excludes
  Monday, Tuesday, Thursday, Saturday and Sunday, leaving Wednesday/Friday from 06:00 through
  09:30 after 1,000 cumulative earnings in sunny weather. Wiki Spring-only/Spring-20 fallback
  prose is not admitted because the locked event keys and handlers do not contain it.
- Daily petting grants capped +12. A watered assigned bowl grants capped +6 at the following
  `Pet.dayUpdate`; current rain fills an outdoor bowl only after that morning's location and
  character day updates, so it cannot be credited to the same settlement.
- Hidden E-drive `EVD-322` now passes 3/3, including accepted pet adoption and native default
  naming. The Chinese runtime script and English base script are tracked separately so
  localization does not masquerade as source drift.
- Machine report: `experiments/local-data/output/goal-method-frontier-v3.json`.

### 2026-09-05: Slice 3 earnings route fact expansion passed

- `earn_money` now has a typed 13-node reverse dependency graph over already admitted
  acquisition, machine, sale, shipping, and native day-settlement options. All eight policy
  dependency option IDs are currently training-eligible and the deterministic sleep owner is
  runtime-verified.
- Native `Farmer.Money`, `ShopMenu`, `Game1` new-day shipping, and `ShippingMenu` sources are
  hash-locked. Current cash, settled `totalMoneyEarned`, and uncredited shipping-bin value are
  separate state variables. Shop buyback is explicitly modeled as a cumulative-earnings
  reversal, so sale/buyback cycling cannot become a teacher exploit.
- Shipping-bin contents no longer collapse economically different stacks. The bridge groups
  pending rows by qualified item, quality, and native unit price and exposes an exact pending
  settlement total plus a completeness flag and next-day timing.
- The source-backed mechanics and transparent inputs pass, but the route is not training-ready:
  a seed-robust fresh-save Year-3 lower-bound proof, counterfactual reserve/opportunity-cost
  labels, and a training-admitted crop-production branch remain explicit blockers.
- The 1.6 min-max guide and Wiki fishing strategy can seed alternatives. They cannot become
  deterministic route rules without isolated rollout evidence visible to the student.
- Regression PASS: 19/19 criteria facts, 15/15 source locks, 228 current options, 151
  runtime-verified, 62 training-eligible, and zero blocking knowledge-export issues.

The next implementation slice expands the twelve pending routes from exact runtime
assets, then introduces an isolated-training authorization contract for the five governance
blockers without weakening the product's normal confirmation policy.

### 2026-09-06: Slice 3 friendship route fact expansion passed

- `raise_friendships` now has a typed nine-node dependency graph over the native Grandpa
  population, ten-villager portfolio deficits, current access, exact talk/gift transitions,
  ordered day settlement, deadline budget, and recurrence.
- `npcs.grandpa_friendship_progress` is the only admitted Grandpa friendship score input. It
  calls the same native `Utility.ForEachVillager` and
  `getNumberOfFriendsWithinThisRange(player, 1975, 999999, false)` path; arbitrary persisted
  `friendshipData` rows can no longer inflate the evaluator.
- Current talk candidates carry the exact projected points before/delta/after and are excluded
  after the daily talk or at zero gain. Gift candidates with zero/negative target gain or an
  unresolved stochastic spouse-jealousy side effect are excluded upstream. The native social
  executor rejects missing or mismatched deltas.
- The Grandpa direction binder independently joins each talk/gift candidate back to exactly
  one live `Utility.ForEachVillager` row. Nonmembers, event actors, already qualifying villagers,
  missing rows, stale point values, nonpositive deltas, inconsistent after-values, and injected
  `grandpa_friendship_*` evidence all fail closed. Accepted candidates carry compiler-owned
  before/after deficits and remaining ten-villager portfolio slots without creating a second
  social executor.
- The locked day recurrence preserves source order: spouse/dating penalties happen first; an
  additional ordinary `-2` may then apply using the post-penalty points; weekly reset/bonus is
  later and positive modifiers still apply through `Farmer.changeFriendship`.
- The bridge now publishes the exact relationship, gift-date, maximum-heart, friendship-book,
  language and spouse inputs for that recurrence. The sole `FriendshipDayTransitionSimulator`
  distinguishes requested penalties from applied deltas: native spouse multiplication truncates
  `-20` to `-13` and a later `-2` to `-1` in the ordinary unclamped case. Eight consecutive native
  cloned-save transitions now validate the projection; the simulator still neither executes sleep
  nor authorizes a whole-route positive label by itself.
- This route remains `in_progress`. Current loaded path timing and the pure future presence/
  route/contact consumer contracts are complete, and `FutureSocialItineraryVerifier` validates a
  supplied visit order. A future-date player-route/gate/traversability/final-approach/interaction-
  eligibility evidence producer, ordered-proposal training binding, and a fresh-save ten-villager
  deadline proof are still blockers. The arithmetic observation that 99 successful ordinary `+20` talk days reaches
  1,980 is not an access or whole-goal feasibility proof.
- The bridge now emits the exact mod-aware raw master-schedule catalog for the native Grandpa
  villager population on explicit `social`/`full` snapshot profiles. Each entry and the complete
  population catalog are SHA-256 bound, while ordinary high-frequency profiles remain unchanged.
  `NpcFutureScheduleResolver` now closes native key precedence, `GOTO`, `NOT friendship`, `MAIL`,
  rain alternatives, static accessibility replacement and static endpoint parsing as a pure Core
  function. Hidden isolated current-state audits matched all 29 loaded native schedules with zero
  mismatch on both ordinary winter 28 and Desert Festival day 2. The latter also matched both
  loaded `aHHMM` entries from native adjacent-route pixels and the exact native time formula.
  Follow-up `runtime-npc-arrival-timing-smoke-20260906-024215` verified all 137 loaded movement
  rows. The pure presence resolver now excludes NPC transit intervals, and the route/contact
  resolvers demand exact same-date player segment, gate, traversability and final-approach timing.
  No producer supplies that complete future evidence yet, so seed-robust ten-villager routing
  remains blocked and cannot emit positive labels.
- The recurrence now has an eight-transition native cloned-save proof. The bridge exposes the
  new-day date used by `updateFriendshipGifts`, and the pure simulator matched 39/39 unique NPCs and
  all 31 existing friendship rows on every transition with zero mismatch. The controlled Linus row
  covered weekly bonus, daily talk/gift reset, and ordinary not-talked decay. This closes recurrence
  formula/runtime conformance, but not future contact feasibility or the ten-villager deadline proof.
- Claim audit represents source-verified but not runtime-proven mechanics explicitly as
  `native_verified_runtime_pending`. Such claims may constrain fail-closed implementation and
  dependency expansion, but cannot cover a goal criterion or emit a positive training label.

### 2026-09-06: Slice 3 current-date social frontier admitted

- The current-date route evidence producer now consumes the complete live route graph, native
  action-gate evidence and the locked movement calibration. On the isolated day-start snapshot it
  resolved all 39 unique social NPC identities, emitted 92 exact talk contact windows, and left no
  NPC or ranking-admission blocker.
- Gift windows no longer represent an unbound promise. `SocialGiftInventoryBindingResolver` is the
  sole inventory/taste binding path shared by the existing current social candidate builder and the
  future contact frontier. A future gift opportunity carries an exact slot, item identity, quality,
  stack, native taste and positive expected delta. A conclusively non-giftable inventory is an
  upstream exclusion; missing evidence remains a hard block. The current fixture contains tools and
  weapons only, so it correctly emits zero gift opportunities instead of 91 placeholders.
- An unscheduled player spouse remains a live identity-tracking directive, not a fabricated all-day
  fixed tile. The directive compiles through the existing social candidate, one-connector daily-plan
  continuation and native executor chain. The isolated snapshot produced one Abigail talk intent
  from `Farm` toward the currently observed `FarmHouse` target and requires fresh identity rebinding
  after every connector and before interaction.
- Route duration is explicitly conditional. The calibrated bound covers movement and connector
  traversal only; unchanged collision/topology facts, native connector control, no unmodeled dialogue
  delay, and continued NPC presence/eligibility are recorded as timing preconditions.
- `current_social_contact_frontier.v2` is ready to supply current-date ranking inputs. It does not by
  itself prove multi-day visit-order selection, gift acquisition, seed robustness, or the complete
  ten-villager Year-3 deadline. Those remain the next friendship-route admission work.
- Regression PASS: full Core `2378/2378`; focused social `90/90`; isolated bootstrap regression
  `585/585` exports with zero blocking knowledge factors. Real snapshot output:
  `experiments/local-data/output/current-social-contact-frontier.json`.

### 2026-09-06: Slice 3 current-date ordered teacher action admitted

- `CurrentSocialDayItineraryPlanner` now selects the fixed nine-NPC friendship-deficit
  portfolio nearest the native 1,975-point threshold, recomputes exact route evidence after each
  chosen stand tile/time, and emits only the prefix accepted by `FutureSocialItineraryVerifier`.
  It makes no global-optimality claim.
- The real isolated day-start snapshot produced seven verified visits ending at 21:46. Marnie,
  Clint, Lewis, Emily, Jodi, Pam and Linus are admitted in order; Robin and Demetrius are recorded
  as omitted rather than silently treated as feasible.
- `CurrentSocialDayTeacherLabelBuilder` labels only the first action executable from the current
  state. A cross-map connector is eligible only when its social option, continuation option, NPC
  and target location agree and its exact friendship transition still joins one live native
  Grandpa-population row.
- The selected Marnie candidate binds to `raise_friendships`, compiles to one accepted daily-plan
  step and then one pending `executor.traverse_connector` queue item through the existing
  `DailyPlanCompiler` and sole `ActionQueueCompiler`. The other six visits are deferred itinerary
  context and require fresh-snapshot replanning after every connector.
- Regression PASS: full Core `2381/2381`; isolated bootstrap regression `585/585` exports,
  228 current options, 151 runtime-verified options, 62 training-eligible options, and zero
  blocking knowledge factors. Machine-readable outputs are
  `experiments/local-data/output/current-social-day-itinerary.json` and
  `experiments/local-data/output/current-social-day-teacher-label.json`.
- The friendship route remains `in_progress`. Next admission work is a multi-day, cloned-save
  teacher rollout that advances only after native receipts, then a fresh-save ten-villager
  deadline proof. Gift acquisition and global route optimization remain separate improvements.

### 2026-09-06: Slice 3 rolling native teacher transition admitted

- The day-start projection and rolling projection now have separate owners. At 06:00 the existing
  audited schedule selection/replay remains authoritative. After 06:00,
  `NpcCurrentLoadedSchedulePresenceResolver` consumes the schedule already selected by the game and
  derives remaining arrival windows with the decompiled adjacent-pixel timing formula. It does not
  rerun morning schedule selection against a later snapshot.
- Native facing is intentionally an integer, not a fabricated `0..3` enum. Decompiled
  `NPC.parseMasterScheduleImpl` accepts the parsed integer directly; the live Demetrius sleep row
  carried `11`. `_sleep` still makes the terminal window ineligible because exact social contact at
  that endpoint is not established.
- Dynamic spouse directives now accept legal current-day ten-minute timestamps, but compilation
  requires `ObservedAtTime` to equal the fresh snapshot time. A stale live-position directive fails
  closed instead of generating a route to an old tile.
- In `runtime-native-social-smoke-20260906-130437`, the Marnie teacher action completed through 22
  applied/verified primitive receipts over 17 loop iterations. The native talk changed friendship
  `1416 -> 1436` and `TalkedToToday false -> true`; exactly one policy trajectory was emitted. The
  resulting `DialogueBox` was closed by the ordinary
  `recovery.stabilize_day -> executor.close_menu` planning/execution chain.
- The fresh post-recovery snapshot was at 09:10 in `AnimalShop`. Rolling frontier v3 and teacher v2
  excluded the completed Marnie talk and admitted Clint as the next candidate with one pending queue
  item. The original smoke process had already completed all runtime actions but ended on an old
  06:00-only validation contract; `summary.json` records this distinction and the offline
  finalization against the preserved snapshot.
- Regression PASS: full Core `2388/2388`; isolated bootstrap `585/585`, 228 current options, 151
  runtime options, 62 training-eligible options and zero blocking knowledge factors. Formal
  training remains paused.
- Next admission work remains a cloned-save multi-day rollout controller: build one fresh teacher
  label, execute one selected objective to a native receipt, mechanically recover menus, replan on
  the same day, sleep only when no admitted action remains, and audit the native friendship day
  transition. Only after that controller proves repeated clean transitions should a fresh-save
  ten-villager deadline run begin.

### 2026-09-06: Slice 4 multi-day controller policy admitted

- `FriendshipTeacherRolloutPolicy` is now the sole phase/exit decision owner for the upcoming
  runtime coordinator. The coordinator may call existing tools, but it must not duplicate candidate,
  route, action expansion, menu handling, sleep or friendship-settlement logic.
- The ordered phases are: build a label from the fresh snapshot; execute only its selected candidate;
  require the native receipt; recover a blocking menu; rebuild on the same day; request a native save
  boundary only after evidence-complete day exhaustion or the explicit per-day safety cap; audit the
  exact friendship transition; then begin the next day from a fresh label.
- Completion and failure are explicit. Ten qualifying villagers completes the loop. Deadline,
  malformed state, failed native transition, unknown label blocker, or three no-progress days block
  it. A verified post-sleep snapshot is audited before deadline failure is evaluated, so the final
  native transition cannot be skipped merely because it crossed the configured boundary.
- The day-exhaustion allowlist contains exactly
  `current_social_itinerary_no_exact_nonqualifying_target` and
  `current_social_itinerary_no_verified_first_visit`. Coverage gaps and compiler errors cannot be
  converted into an early sleep. Focused policy tests pass `10/10`; full Core passes `2398/2398`.
- Next implementation slice is the thin runtime coordinator and cloned-save launcher that consume
  this policy and the existing LiveTrainingLoop/teacher-label/day-transition components. Its first
  admission target is two teacher objectives plus one audited native day boundary, with formal
  training still disabled.

### 2026-09-06: thin coordinator reached the first native day boundary

- The isolated coordinator reuses `CurrentSocialDayTeacherLabelBuilder`, `LiveTrainingLoop`, the
  existing action compiler/runtime path, native save-boundary handling, and
  `FriendshipDayTransitionSnapshotAuditor`. It does not post directly to the executor and always
  passes `--skip-training` during this admission stage.
- The preserved run `runtime-friendship-teacher-rollout-20260906-143527` completed two exact
  teacher-selected objectives. Marnie changed from 1,416 to 1,436 friendship points and Clint from
  354 to 374; both changed `talked_to_today` from false to true and each emitted exactly one
  successful policy trajectory.
- The same clone then crossed the native save boundary from total day 223 to 224. All 39 native NPC
  rows and all 31 friendship rows matched the decompiled transition model with zero mismatch.
- Population-wide friendship changed from the day-start sum 8,389 to 8,377 despite the two verified
  target gains. Untalked-villager decay can therefore hide useful goal-directed work. The no-progress
  guard must accept either positive net portfolio movement or at least one exact, positive,
  teacher-selected objective receipt; it still records the net delta separately.
- Year 3 Spring 1 opened native event `558291` from `Data/Events/FarmHouse`. The snapshot exposed
  command 8, speaker Grandpa, `event_up=true`, and `boundary_kind=automatic_progress`. Treating this
  as an ordinary closeable dialogue was wrong. The coordinator now delegates it to the sole existing
  `story.advance_event -> executor.advance_story_event` chain and does not weaken ordinary menu-close
  safety. Dialogue decisions, story minigames, and player-control boundaries remain fail-closed until
  explicitly bound.
- Focused coordinator/policy/report tests pass 30/30 and the social snapshot backend contract passes
  5/5. A later local run, `runtime-friendship-teacher-rollout-20260906-150903`, was manually aborted
  before objective completion because snapshot generation affected the interactive workstation. It
  is interruption evidence only, not an admitted rollout.
- The persistent source-save tree retained SHA-256
  `39525AB21372EAA38465FDFD3B7C6CF085FE280A7AEA0C04A46AEE67F5EEAEB8` across both runs. Formal
  training remains disabled.

### 2026-09-06: server bounded admission passed two objectives and one native day boundary

- Releases `r41` through `r43` were assembled locally with complete file manifests and deployed as
  immutable directories. Each run used writable run-local mod copies, a run-local cloned save,
  hidden rendering, dummy audio, concurrency 1, finite child timeouts and `--skip-training`.
- `r41` produced the first real server connector receipt and exposed a route-validation defect: the
  compiler revalidated the first parallel graph edge instead of the explicit connector coordinates
  selected by the planner. `ActionQueueCompiler` now validates the exact source, destination and
  connector tile carried by the command.
- `r42` passed that edge and then failed closed on Backwoods `TouchAction=asdlfkjg`. Locked 1.6.15
  decompilation shows that tiles `(13,29)` through `(15,29)` remove those three map properties when
  touched. Only a dry, single-player, after-day-3 visit from 19:20 through 20:19 has a 2.5% chance to
  add mail `asdlkjfg1` and cosmetic sound/sprites. It is a pass-through incidental branch, not a
  warp, door or movement blocker, so route branch coverage now classifies it as `covered_for_read`
  while retaining the exact conditional side effect in the audit note.
- `r43` (`friendship-teacher-admission-r43-20260906-171904`) passed the bounded scope. Robin changed
  `362 -> 382` through 23 verified native primitives and Abigail changed `154 -> 174` through 17.
  Both set `talked_to_today=false -> true`, produced one policy trajectory, and used the existing
  dialogue recovery chain. The native boundary advanced Spring 18 to Spring 19 and audited 38 NPCs
  plus 29 friendship rows with zero mismatch. Population friendship still changed by `-14`, while
  two exact positive teacher receipts correctly counted as goal-directed progress.
- The run used a persistent source save with identical before/after tree hash
  `ad9a78a1b652d1557041d9d7f15b8f711ce082ecbe1c1894953cd2bbfad3dede`. The complete 519-file,
  372,400,193-byte evidence tree was copied to local archival storage; server and local aggregate
  SHA-256 both equal `28cb2d1fef494c17bbb5eb91b26570a0c5a11450ec11280331076b595cdac46d`.
- The release archive SHA-256 is
  `8e306afd325e60069dfe8ba2fa490f80bd7807527d5eac21ced1ca0217694b17`. Regression after the two
  route corrections passed Core `2423/2423`, Backend `177/177`, KnowledgeCompiler `585/585` with
  zero blocking factors, and the `228/151/62` option/runtime/training matrix.
- Scope remains strict: `formal_training_started=false`. This clone did not present a Grandpa event,
  so `storyEventAdvanceCount=0`; the server result does not close the Grandpa-event continuation
  target. It proves two goal-conditioned social objectives and one audited native day transition.

### 2026-09-06: content-addressed snapshot storage admitted on the server

- Release `r44` (`friendship-teacher-storage-r44-20260906-203436`) repeated the same bounded teacher
  scope through the existing candidate, DailyPlan, compiler and native executor chain. It did not
  introduce a second planner or executor. Robin again changed `362 -> 382` through 23 verified
  primitives and Abigail `154 -> 174` through 17; both emitted one policy trajectory. The native
  boundary advanced total days `17 -> 18`, audited 38 NPCs and 29 friendship rows with zero mismatch,
  and the persistent source-save hash remained
  `ad9a78a1b652d1557041d9d7f15b8f711ce082ecbe1c1894953cd2bbfad3dede`.
- Every bulk before/after snapshot is now a small manifest whose immutable gzip blobs are addressed
  by SHA-256. Chunking is by root property and by child property under `state`; each read verifies
  compressed size, uncompressed size, chunk hash, reconstructed logical size and logical hash.
  Ranking, queue and execution receipts remain ordinary auditable JSON.
- Independent local verification covered 96 manifests, 2,400 chunk references and 1,013 unique
  blobs with zero errors. The snapshots represented 315,482,766 logical bytes and occupied
  10,181,914 bytes as manifests plus blobs, a 96.77% reduction. The complete 1,532-file evidence
  tree was copied back and matched the server checksum list file-for-file.
- Regression for this release passed Core `2430/2430`, Backend `177/177`, KnowledgeCompiler
  `585/585` with zero blockers, the `228/151/62` option/runtime/training matrix, and the isolated
  experiment regression. `formal_training_started=false` and `storyEventAdvanceCount=0` remain
  explicit; storage admission is not model-training admission or Grandpa-event evidence.

### Deterministic teacher versus the student model

A complete authoritative teacher can produce a 100% save without a learned model once the full
goal-method dependency graph, feasibility rules, native action chain and receipt verification are
complete. The model is still valuable for amortizing long-horizon search, choosing human-like
alternatives, and adapting to a player's preferences. It is not allowed to repair unknown teacher
rules by guessing, and the current teacher is not yet complete enough to claim a 100% save.

### 2026-09-06: Grandpa automatic event admission passed

- The archived isolated source was independently parsed as Year 3, Spring 1 with event `558291`
  absent from `eventsSeen`. Release `r45` correctly failed closed when the automatic event moved
  from command 6 to command 7 between snapshot compilation and dispatch.
- Release `r46` retained exact event ID, command index, raw command and projection-fingerprint
  checks, but allowed a bounded fresh-snapshot recompile. The second dispatch completed the native
  event through 19 dialogue clicks and produced exactly one verified
  `executor.advance_story_event` receipt. The post-state had no active event and did not advance the
  game day.
- The server admission exited `bounded_evidence_complete` specifically because the configured
  `558291` evidence count reached one. The source-save tree remained
  `221d9c7d5f0cd3aa4406b6b81950b4e576b8048070249f1900c51f0a2e974ac5`; the complete 161-file
  evidence tree was transferred locally and verified file-for-file. Formal training remained off.
- The coordinator now stops its story child loop after the first verified action, avoiding unused
  post-completion iterations. This optimization passed focused tests and does not weaken the native
  executor's stale-command rejection.

### 2026-09-06: Stage 1 to Stage 2 checkpoint continuation implemented

- The structured policy trainer now accepts an optional initialization checkpoint. It preserves the
  Stage 1 feature vocabulary, adds newly observed goal and candidate features, and converts inherited
  weights to the new normalization scale while preserving candidate ordering before optimization.
- The child checkpoint records the parent checkpoint ID and SHA-256 plus inherited and new feature
  counts. The parent hash is part of the child checkpoint ID, so continuation cannot be confused with
  a from-scratch run over the same dataset. Backend, model CLI, LiveTrainingLoop, and formal artifact
  promotion all carry this provenance.
- Stage 2 remains a controlled curriculum expansion. The learner may reuse Stage 1 farming, economy,
  routing, timing and allocation weights, but it cannot positively label a new Perfection route until
  its rule, dependency, transparent input, option, compiler binding and native receipt have passed the
  existing gates.
- This is architecture readiness, not a trained model. The current Stage 1 frontier remains 2/19
  executable criteria and 17/19 pending dependency closure, with zero missing dependency graphs and
  zero option-governance-blocked criteria. Full 21-point teacher coverage and a new-save 21/21
  rollout still precede formal Stage 1 training.
- The skill-level direction now has typed routes for verified farming, fishing, foraging, mining and
  exact skill-book XP plus deterministic crop maintenance. It remains pending because ordinary mine
  transit only fights incidental threats; a generic combat-XP goal-level binding with a fresh native
  XP receipt and the full fresh-save deadline proof are still required. Incidental combat is not
  accepted as proof that all five unmodified skills can reach level 10.

## Server continuation policy

The remaining bounded admission should run headlessly on the existing `119` test server so the
interactive workstation is not affected. This changes the execution location, not the evidence or
training rules.

### Observed server envelope

The first 2026-09-06 read-only probe reported 2 CPU cores, 3.6 GiB RAM, and 7.6 GiB free on a 59 GiB
root filesystem. After retaining the immutable releases and `r43` evidence, about 5.7 GB remained;
the online-judge containers stayed healthy and available memory returned to about 2.6 GiB after the
run. Existing Stardew data occupies about 12 GiB under `/root/stardew-junimo`. These values make the
server suitable for concurrency-1, no-render bounded rollouts, but not for unbounded artifact
retention or GPU model training.

### Transfer and execution gates

1. Build and test an immutable source bundle locally, record its Git revision plus a complete file
   SHA-256 manifest, and transfer that exact bundle. Never run from an unrecorded dirty server tree.
2. Verify the server game/runtime identity against the locked Stardew Valley 1.6.15 profile before
   accepting any result. A different executable, content root, mod allowlist, or bridge schema blocks
   the run.
3. Copy the test save to a run-specific directory. Record the persistent source-save hash before and
   after; only the clone may change.
4. Keep concurrency at 1, rendering and audio disabled, HTTP services private, and child-process
   timeouts finite. Give the rollout an explicit CPU/memory envelope so the online judge remains
   healthy.
5. Require at least 2 GiB free plus the measured artifact budget before launch. `r44` reduced its
   315,482,766 logical snapshot bytes to 10,181,914 physical manifest/blob bytes, but non-snapshot
   logs, cloned saves and releases still count against the disk gate. Stop before the reserve is
   crossed; never delete prior server data implicitly.
6. After every native day boundary, close the transaction, hash the evidence, copy it back to local
   archival storage, verify the copy, and only then permit the next plan. Server-local artifacts are
   not the only copy.
7. Stop on goal completion, deadline, source drift, nonzero transition mismatch, an unbound story
   decision, repeated no-progress, storage pressure, child timeout, or online-judge health loss.

### Immediate server admission target

The admission is now split so a partial result cannot be mislabeled complete:

1. `r43` passed Stage A: two fresh teacher labels and exact native social receipts, one native save
   boundary with zero friendship-transition mismatch, a clean bounded exit, an unchanged persistent
   source save and a complete hash-matched local evidence copy.
2. `r46` passed Stage B on a Year 3 Spring 1 isolated clone: event `558291` produced one verified
   existing-story-executor receipt, a fresh inactive post-state, a clean bounded exit, an unchanged
   source save and a complete hash-matched local evidence copy.
3. `r44` passed the storage prerequisite with content-addressed immutable gzip blobs, per-step
   manifests and full read-time integrity checks. Repeated-day and fresh-save deadline runs may use
   this mode, but must retain the same disk reserve, source-save isolation and evidence-copy gates.

Optimized multi-day game rollouts should run on the server under these gates so the interactive
workstation stays usable. The next gate is full Stage 1 goal-method teacher coverage and a new-save
21/21 curriculum rollout. This is still dataset/teacher admission, not formal model training;
Slice 7 remains assigned to the RTX 5070 node.

### 2026-09-07: Exact collection denominators and missing-outcome binding admitted

- Stage 1 remains a fresh-save native Grandpa `21/21` run by the initial Year 3 Spring 1
  evaluation. Stage 2 warm-starts from the admitted Stage 1 checkpoint and targets the native
  Perfection tracker at `100%`; Stage 2 complexity cannot weaken the Stage 1 admission gate.
- `AuthoritativeRequirementInventoryBuilder` now derives the four Stage 1 collection sets from
  runtime exports and exact decompiled predicates. Vanilla 1.6.15 contains 154 Full Shipment
  objects, 72 Master Angler fish, 95 museum-donatable items, and 30 standard Community Center
  bundles. The post-Community-Center Missing Bundle is supplemental.
- The generated inventory is source-path and SHA-256 bound. It contains 351 requirement groups,
  and all 351 now have at least one identity-safe native acquisition source. Structured runtime
  sources cover crops, fish, shops, machines, animals, fruit/wild trees, forage, artifact spots,
  geodes, fish ponds and monster drops; exact decompiled guards cover the remaining native special
  branches. Status is `complete` with zero unresolved first-source groups.
- Source completeness is not executable-route completeness. Every selected source still requires
  its calendar, unlock, location, facility, resource, stochastic fallback, candidate, compiler and
  fresh native receipt dependencies before it may supervise the model.
- Identity-unsafe graph edges are rejected. In particular, a numeric object ID cannot be joined
  to a big-craftable recipe output with the same numeric ID. This reduces apparent route coverage
  instead of silently training against the wrong item.
- The transparent bridge publishes exact live fish and museum denominators, item rows, completion
  counts, and missing IDs. The static `Data/Objects` classification is cached by the live content
  dictionary instance, while save-specific completion is recomputed on every snapshot to avoid
  both stale state and repeated full-catalog scanning.
- The Master Angler binder now requires a complete candidate outcome distribution and admits
  `catch_fish` only when at least one possible qualified item intersects the current native missing
  set. A candidate containing only already-caught fish is excluded upstream and cannot reinforce a
  useless fishing loop.
- Existing runtime admission also includes exact combat XP coverage for all six native skill
  indices, a bounded friendship teacher rollout, and the native Grandpa event `558291`. Four
  confirmation-gated product options have isolated teacher-only authorization; governance blockers
  are zero without changing normal player confirmation policy.
- Current frontier accounting remains 2 executable criteria, 17 pending dependency expansions, and
  0 governance-blocked criteria. Formal training remains disabled. The next Slice 3 work expands the
  now-complete first-source inventory through executable dependencies, beginning with future fish
  season/weather/time/location/access scheduling and shared acquisition-source-to-option bindings,
  then admits fresh native receipts.

### 2026-09-07: Master Angler source partition and dual acquisition binding admitted

- `Data/Fish` requirement rows are no longer accepted as acquisition actions by themselves. The
  exact 72-species denominator now remains fully source-covered through 60 `Data/Locations` rod
  species, two guarded `MineShaft.getFish` overrides (`(O)158` and `(O)161`), and ten guarded
  `CrabPot.DayUpdate` trap species. All source files are path- and SHA-256-bound.
- `master-angler-opportunity-catalog-v1.json` records every species' raw time, season, weather and
  fishing-level constraint, every matching location rule, both mine areas, and both trap water
  classes. Its hard partition is `60 + 2 + 10 = 72`, with zero unresolved species.
- The catalog also normalizes all 180 matching `Data/Locations` rules into native minimum/maximum
  year, season, clock and weather constraints. `GameStateQuery.cs` is a separately hashed source
  and regression guard: native `YEAR 2` means year 2 or later, not only year 2. There are zero
  unparsed rules and zero impossible static intersections. Fifteen rules retain festival, special
  order or random predicates as explicit dynamic gates; the catalog never assumes those gates pass.
- A decompile-wide override audit now requires exactly five native `getFish` overrides. Farm only
  redirects to another location table, IslandLocation and IslandSouthEast inject walnuts, Railroad
  injects Caroline's Necklace, and MineShaft directly injects collection fish. The MineShaft set is
  four area routes: Stonefish at areas 0/10, Ice Pip at 40, and Lava Eel at 80. Lava Eel keeps both
  its Caldera location rules and this mine alternative; source alternatives do not duplicate the
  72-species denominator.
- `master-angler-stage-one-window-index-v1.json` expands the catalog through the initial Year 3
  evaluation boundary (`total_day < 224`). All 72 species have at least one static window; the
  index contains 1031 year-season/source spans and exposes each species' earliest and latest static
  day for deadline-first teacher ranking. It deliberately remains ineligible as a training label
  until target-date dynamic conditions, unlock/access, route/tile, equipment, live candidate and
  fresh native receipt evidence resolve.
- `complete_master_angler` now reuses both existing policy options: `fishing.catch_fish` for a
  complete legal cast distribution and `fishing.collect_crab_pots` for one exact ready output. Both
  must intersect the fresh native missing-species set. Crab-pot garbage, non-collection outputs and
  already-caught species remain upstream exclusions.
- Trap placement, bait loading and day settlement are represented only as dependencies on the
  existing `executor.place_crab_pot`, `executor.load_crab_pot_bait`, and
  `recovery.stabilize_day` paths. They are compiler-owned transitions, not policy labels and not a
  second executor.
- The source catalog does not assert future accessibility. Remaining blockers are a date/weather/
  clock/location/access/route/tile/equipment condition producer, one high-level trap-infrastructure
  intent which expands the existing mechanical primitives, and a fresh-save stochastic deadline
  proof. Frontier accounting therefore correctly remains 2 executable, 17 pending, 0 governance
  blocked; formal training remains disabled.
- Regression PASS: Core `2446/2446`; isolated bootstrap full regression; 585/585 exports; 351/351
  first acquisition sources; 72/72 Master Angler opportunity sources; 180/180 normalized location
  calendars; 72/72 Stage 1 deadline windows; 0 unresolved source or calendar rows.

### 2026-09-07: Master Angler crab-pot lifecycle, capacity and rolling clock gate

- The Master Angler trap branch now reuses one mechanical lifecycle: route to a persistent pot,
  collect a ready output, load bait when required, route to a legal water tile, place an exact
  inventory pot, and wait for native day settlement. These are dependency actions beneath the
  high-level goal; they are not extra policy labels or a second executor.
- `player.crab_pot_network` publishes the loaded global network rather than only current-location
  objects. Planning can therefore service a remote pot or suppress a redundant placement without
  pretending the pot is local. Exact placement is rebound from a fresh snapshot after routing.
- The locked vanilla `CrabPot.DayUpdate` implementation is SHA-256 bound as
  `A7ABEA39D49F8E3631843BF5047C3273E56CD74265AD8D310BE13A1D45AFA363`.
  Mariner outcomes use the native uniform eligible-row branch. The ordinary branch preserves
  native row order, junk gate, first-success semantics and fallback trash. The bridge exposes a
  conservative per-serviced-cycle distribution; special bait improvements are not overstated.
- Nested trap-row DTO fields now carry explicit snake-case JSON names. This closes a real bridge/
  Core contract bug that fixture-only tests had hidden by already supplying snake-case JSON.
- Placement capacity is stochastic rather than binary. For this Stage 1 direction, the planner
  combines all exact existing-pot miss probabilities over the remaining serviceable cycles before
  the exclusive `total_day = 224` deadline and suppresses extra capacity only at a `0.95` success
  threshold. Missing probability evidence, zero remaining cycles, and incomplete identities fail
  closed. Stage 2 must inject its own target horizon instead of inheriting this Stage 1 constant.
- At this checkpoint, a rolling current-step clock gate rejected a catch, ready-pot collection or
  route whose current action plus the terminal fishing reserve could not fit before the
  authoritative opportunity closed. The complete multi-edge proof was added in the 2026-09-08
  follow-up below.
- Crab-pot candidate ownership is split by concern into service, placement, routing and capacity
  partials. Shared lifecycle helpers have one implementation; no duplicate executor path was added.
- Regression PASS at this checkpoint: full Core `2461/2461`; TransparentBridge and isolated bootstrap builds have
  `0` warnings and `0` errors; isolated knowledge regression exports `585/585` with `0` blockers.
  The regenerated option matrix is reproducibly locked at 228 options, 62 training-eligible and
  151 runtime-verified entries.
- Runtime game evidence has not been collected for this slice. Formal training remains disabled.

### 2026-09-08: Master Angler current-date full-route Teacher proof and review

- The deterministic Teacher now starts from the exact native 72-species missing set, filters the
  authoritative current-date source windows, and only then batches route searches for the remaining
  relevant target locations. It no longer searches every catalog location on every call.
- Every remote intent consumes the full-profile route graph, date-bound static walkability and gate
  evidence, current player tile/time, game version, and a SHA-256-locked conservative movement
  calibration. The proof accumulates every connector approach and transition through the target map.
  The terminal fishing candidate is still rebound from a fresh snapshot after the final transition.
- Window admission now reserves terminal catch time from the later of source-window opening and
  conservative route arrival. A route that is topologically valid but arrives too late produces no
  Teacher candidate. The candidate gate independently recomputes the full remaining route after each
  fresh snapshot and uses `max(current_action, remaining_route) + terminal_reserve`.
- The older topological-only Master Angler precheck was removed. Route feasibility and timing have one
  Core authority; this added no action compiler or runtime executor implementation.
- Review split route-location contracts, search results, calibration artifact loading, window-index
  parsing, runtime source matching, and experiment snapshot parsing into focused partials. No duplicate
  Master Angler lifecycle or second execution path remains in the reviewed slice.
- Regression PASS: Core `2467/2467`; isolated knowledge exports `585/585` with zero blockers; current
  option matrix remains locked at 228 registered, 62 training-eligible and 151 runtime-verified; the
  full isolated regression and Master Angler late-arrival self-test pass.
- This closes only the current-date Master Angler route/time leaf inside Slice 3/4. The 19-criterion
  reverse hypergraph is not complete. Formal training remains disabled pending future-date scheduling,
  a fresh full-profile global crab-pot network/probability snapshot, proactive trap-capacity lifecycle
  proof, and fresh native terminal/overnight receipts.

### 2026-09-09: Per-requirement acquisition bindings entered the Teacher graph

- The acquisition report now retains all 450 exact alternatives and all 1,599 authoritative route
  occurrences beneath the four Stage 1 collection denominators. Each occurrence preserves item and
  source identity, supervision and uncertainty mode, endpoint high-level options, supporting options,
  and independent runtime/Teacher admission state.
- The same report is consumed by the sole Goal-Method frontier builder. Existing high-level options
  now connect through typed acquisition-route and alternative nodes to each exact requirement and its
  owning root method. This is the missing static bridge required for a future Teacher to choose a route
  for a specific unmet item; no second planner or executor was introduced.
- All four collection directions expose per-set readiness. Full Shipment is 154/154, Master Angler is
  72/72, Museum Collection is 95/95, and the standard Community Center inventory is 30/30 for terminal
  acquisition lowering. This means every authoritative acquisition route has an admitted implementation;
  it does not prove that every route is currently feasible or deadline-safe.
- The stale Full Shipment blocker claiming that tree moss was missing has been removed. Remaining work
  is dynamic: live calendar/unlock/resource/route-time/probability/retry and cross-goal reservation
  binding, followed by native day-settlement and fresh-save completion proof.
- Frontier accounting intentionally remains 2/19 executable and 17/19 dependency-pending. Promoting a
  criterion now requires its long-horizon proof; static acquisition completeness cannot silently change
  the criterion status.
- Repository input locks now hash explicitly normalized UTF-8/LF text for tracked source files. This
  removes false drift caused by Git checkout line-ending conversion without weakening raw-byte locks for
  external knowledge and decompile artifacts.

### 2026-09-09: Live Full Shipment requirement frontier admitted

- `CurrentFullShipmentTeacherFrontierBuilder` now intersects one fresh transparent snapshot, its exact
  same-state ranked candidates, the 154-item authoritative denominator, and the per-item acquisition
  lowering. All four source files are SHA-256 recorded; state-hash or denominator drift fails closed.
- The live Full Shipment aggregate is not trusted in isolation. Item identities, shipped flags and counts,
  missing IDs, aggregate counts, completion state, and ratio must agree with one another and with the
  authoritative inventory before any label can be emitted.
- A positive label requires either complete native Full Shipment contribution flags for the exact missing
  item, or an exact candidate output identity on an admitted endpoint of that item's authoritative route.
  Completed items, same-option/different-output candidates, blocked candidates, and unbound supporting
  options are excluded.
- A missing requirement with no current exact candidate is deferred and emits no negative label. The real
  r36 fixture contains 154 requirements, 6 completed and 148 missing, but no exact current candidate; the
  report therefore remains label-ineligible instead of manufacturing 148 negative examples.
- This closes the first dynamic static-graph-to-live-candidate join. It does not raise the overall 2/19
  frontier or authorize formal training. The museum and standard Community Center slice described below
  now applies the same contract; the next slice joins the existing Master Angler date-window intent.

### 2026-09-09: Live museum and standard Community Center frontiers admitted

- `CurrentCollectionTeacherFrontierBuilder` consumes the same four hash-bound inputs as the Full Shipment
  frontier. Museum completion is trusted only when all 95 `donatable_items` rows, missing IDs and aggregate
  counts agree with the authoritative denominator. Aggregate-only historical snapshots block the museum
  set without blocking independently valid Community Center evidence.
- The 30 standard Community Center requirement groups are joined by exact `bundle_data_key`. Every runtime
  ingredient must match authoritative identity, quantity, minimum quality and completion position. OR
  bundles retain their required-slot count and remaining-slot count, so alternatives are candidate choices
  rather than instructions to acquire every listed item.
- Direct donation labels require exact native inventory and completion projections. Acquisition labels need
  an exact qualified output, positive quantity and admitted authoritative endpoint; requirements above
  quality zero additionally require an exact, consistent output-quality parameter. Unknown or insufficient
  quality is deferred. Item reservations remain explicit until native donation, while money bundles expose
  a distinct reserve-until-native-payment contract.
- The typed fixture admits museum donation/acquisition and Bundle donation/high-quality acquisition while
  rejecting unknown quality, low quality and an unproven Vault payment. The real r36 snapshot correctly
  blocks only its legacy museum aggregate, observes all 30 standard bundles and emits no labels because its
  current candidate pool has no exact match. No unavailable route becomes a negative training example.
- This is a dynamic evidence adapter, not a long-horizon completion proof, so the 2/19 criterion frontier and
  formal-training gate do not change. Next, Master Angler's existing date-window intent is joined into the
  same current-requirement frontier and the four collection sets receive one selection contract.

### 2026-09-09: Four-set current collection candidate contract admitted

- `CurrentMasterAnglerTeacherFrontierBuilder` now joins the exact native 72-fish progress denominator,
  the authoritative requirement inventory and acquisition lowering, the same-state current candidate
  ranking, and the existing target-date intents. The snapshot, inventory, lowering, ranking, intent,
  window-index, opportunity-catalog and route-timing-calibration hashes are all retained and checked.
  Stale state or copied intent artifacts fail closed.
- Core owns one `MasterAnglerCurrentCandidateMatcher`. Route connectors, terminal catches and ready
  crab-pot collections must carry the exact target intent, conservative remaining-route plus terminal
  reserve marker, and exact possible-result identity. A crab-pot candidate for a different fish cannot
  acquire a label merely by attaching valid intent metadata.
- `CurrentStageOneCollectionTeacherFrontierBuilder` unifies Full Shipment, Master Angler, Museum
  Collection and the standard Community Center into one current candidate-membership contract. One
  physical candidate appears once and receives every exact requirement credit it can advance; Community
  Center OR bundles retain their remaining-slot limit. Unavailable requirements remain deferred and do
  not become negative examples.
- This contract deliberately does not select a preferred candidate. It exposes no learner rank or score,
  emits no negative labels, and records `teacher_preference_label_eligible=false`. Candidate membership
  is necessary input to the next Teacher step, not evidence that formal training is ready.
- The typed four-set fixture, focused current-collection self-test, full isolated bootstrap regression,
  Core game-free tests and Backend tests pass. The historical r36 snapshot is correctly rejected for
  Master Angler because it predates the required per-fish transparent denominator; no current label is
  fabricated from that aggregate-only evidence.
- The 2/19 executable criterion frontier and formal-training gate remain unchanged. The next slice is a
  deterministic, learner-independent preference query over this unified current candidate set. Native
  receipts, future-date scheduling and the remaining long-horizon criterion proofs still follow before
  formal full training.

### 2026-09-10: Independent current collection Teacher preference admitted

- `CurrentStageOneCollectionTeacherPreferenceBuilder` rebuilds the four-set current frontier from the
  hash-bound requirement inventory, acquisition lowering, same-state candidate ranking, transparent
  snapshot and Master Angler target-date intents. It rejects state, goal, denominator, candidate-credit,
  selection-group or source-hash drift before evaluating a preference.
- The fixed lexicographic Teacher policy uses only authoritative current-day deadline evidence, deadline
  slack and cutoff time, terminal requirement transitions, exact cross-set and cross-requirement credits,
  candidate scarcity, deterministic execution time and deterministic energy. Learner rank, policy score,
  model score and expected reward are never read as preference criteria and are cleared before compilation.
- Candidate IDs provide stable report ordering only. If the top candidates have the same authoritative
  vector, the query fails closed with `blocked_authoritatively_tied_top_candidates`; it does not convert an
  arbitrary identifier order into supervision. Requirements absent from the current candidate set remain
  deferred, and non-selected eligible candidates are counterfactual alternatives rather than binary negatives.
- The selected choice must pass the existing `DailyPlanCompiler -> ActionQueueCompiler` chain against the
  same transparent snapshot. A blocked plan or queue prevents label eligibility. The typed regression proves
  selection is invariant under reversed learner scores, proves exact ties are refused, and proves the selected
  action reaches one pending queue item through the existing compiler path.
- This is a current-state preference label only. It keeps `formal_training_authorized=false` and does not
  change the 2/19 executable criterion frontier. The next slice binds a selected preference to a fresh native
  before/after receipt and emits a training row only after the exact credited requirement transition is
  verified. Future-date scheduling and the remaining long-horizon criterion proofs still follow.

### 2026-09-10: Fresh current collection receipt admission

- `CurrentStageOneCollectionTeacherReceiptBuilder` consumes the exact persisted Teacher preference
  artifact that supplied the executed queue. It recomputes the preference from the five hash-bound
  authority inputs and compares the selected candidate, complete current membership, Teacher order and
  compiled command semantics while preserving the original random queue identity for receipt binding.
- Admission currently requires exactly one pending queue item containing exactly one mechanical step. The
  native episode must bind the run, queue, primitive option and kind, full effective queue item,
  before/after state hashes and ticks, and must be applied, successful, verified, fresh, free of block or
  failure reasons, supported by positive primitive-verification reasons, and supported by non-empty changed
  facts. A receipt from another queue or primitive fails closed.
- Every requirement credit on the selected candidate must independently change in the fresh after state.
  The verifier covers exact inventory acquisition, pending or settled Full Shipment, museum donation,
  exact Community Center ingredient completion, native fish collection and an exact Master Angler route
  endpoint. A generic successful executor status cannot substitute for requirement progress.
- The admitted row reuses `policy_decision_trajectory.v2`. It contains only the complete Teacher-current
  candidate set, clears all learner ranking signals and carries structured Teacher provenance plus all
  source/receipt hashes and verified transitions in `audit.teacher_supervision`. The canonical dataset
  validator checks this optional evidence whenever present.
- This closes the first single-state, single-primitive preference-to-outcome admission path. It does not
  authorize formal training. Multi-primitive candidate completion, future-date scheduling and the remaining
  17 Stage 1 criteria remain fail-closed work.

### 2026-09-11: First isolated current collection Product row admitted

- `Invoke-CurrentStageOneCollectionTeacherProductRollout.ps1` now performs the bounded real rollout in an
  isolated save copy with hidden, silent game processes. It creates a fresh transparent snapshot, ranks the
  complete current collection frontier, persists the deterministic Teacher preference and executes the
  exact compiled one-item queue through `LiveTrainingLoop` in Product mode.
- The first admitted run used a collision-checked 41-tile Product preposition, regenerated all authority
  artifacts at the resulting state, and then natively executed the selected `farm.collect_machine_outputs`
  candidate as `executor.collect_machine_output`. The verified transition was Full Shipment Raisins with
  exact inventory quantity `0 -> 1`.
- The emitted `policy_decision_trajectory.v2` row passed the canonical dataset validator as `1` input,
  `1` accepted, `0` rejected, `0` duplicate and `0` conflict. The script now performs that validation itself
  and fails closed unless exactly one row is accepted.
- This proves the bounded current-state Teacher-to-compiler-to-Product-to-receipt-to-dataset path; it does
  not start formal training. The next slice repeats whole-candidate rollouts and extends receipt admission
  to candidates whose existing compiler lowers them into multiple ordered primitives.

### 2026-09-11: Bounded multi-primitive Teacher rollout admitted

- Teacher queues are now bounded to `1..8` ordered items, with one mechanical primitive per item. The
  persisted plan-to-item mapping, selected-candidate precondition, actor, mode, item identity and original
  command state hash are checked before dispatch. Duplicate identities and semantic substitutions fail
  closed.
- The existing sequential executor is the sole execution path. After each primitive it obtains the fresh
  after snapshot and rebinds only the next normalized command's `state_hash`; it does not rerank, call the
  learner or mutate the remaining command semantics. `queue_execution_receipt.v1` preserves the original
  compile hash and the effective per-step hash/tick chain, native verification, changed facts and exact
  final completion marker.
- Receipt admission requires every planned item to execute in order and only the final item to complete
  the fixed Teacher candidate. A successful mechanical step without an authoritative requirement-credit
  transition is retained as execution evidence but emits no policy row. Any receipt identity, freshness,
  ordering, command, native verification or hash-chain error stops the rollout.
- The hidden isolated run `bounded-stage-one-collection-teacher-20260911-211944` executed three successive
  state-dependent episodes. It admitted the two-primitive Raisins machine collection, correctly withheld
  a label for the shipping-bin approach, then admitted the native shipping deposit. The canonical dataset
  accepted both emitted rows with zero rejection, duplicate or conflicting duplicate.
- Formal training remains disabled. The next implementation slice collapses collection continuations that
  are still exposed as separate approach/terminal candidates into one bounded, fresh-rebound candidate
  queue, beginning with shipping, donation and route-then-interact paths. Future-date scheduling and the
  remaining 17 Stage 1 criteria remain subsequent fail-closed work.

### 2026-09-11: Bounded local shipping candidate admitted

- A same-map `economy.ship_items` candidate away from the shipping-bin stand now compiles once into
  `executor.move_to_tile -> executor.ship_inventory_item_to_bin`. Both persisted items retain the exact
  shipping continuation identity, and the existing Teacher queue runner only rebinds the second command's
  state hash to the first command's fresh verified after-state.
- Cross-map travel remains deliberately rolling: only the first connector that is valid in the current
  snapshot is compiled. On arrival at the Farm, a fresh snapshot produces the bounded local move-plus-deposit
  queue. No target-map collision coordinate is precompiled against a source-map snapshot.
- Hidden, silent, isolated run `bounded-shipping-candidate-queue-20260911-221453` admitted two consecutive
  two-item queues: machine-output collection, then local shipping approach plus native deposit. Both had one
  verified requirement transition; the canonical dataset accepted `2` rows and rejected `0`. Formal training
  remains disabled.
- The next slice applies this boundary to Museum and Community Center donation: rolling cross-map connectors,
  followed by one freshly compiled existing native donation primitive and an exact collection receipt. The
  donation primitives already own target-map BFS, endpoint interaction and the native menu lifecycle, so a
  separate target-map stand-move would duplicate execution logic. Future-date scheduling and the remaining
  17 Stage 1 criteria follow that proof.

### 2026-09-13: Typed rolling donation routes admitted before runtime proof

- Outside the target building, `museum.donate_items` and
  `community_center.donate_bundle_items` expose only the first connector verified by the current transparent
  route graph. The continuation locks the exact inventory item and slot; Community Center also locks the
  bundle key, ingredient index, required stack and item quality.
- A normal daily-plan run may keep that mechanical continuation leased across a fresh snapshot. The Teacher
  product path instead records the connector as one independently verified supervision episode; its outer
  controller rebuilds the complete ranking and Teacher preference from the fresh state for the following
  episode, where the native terminal donation carries terminal-transition priority.
- The current collection Teacher frontier admits those candidates as
  `authoritative_collection_rolling_route_step`. This is explicitly an intermediate route transition, not a
  collection completion. A receipt must prove the declared connector endpoint was reached; final Museum or
  Community Center credit still requires the exact native collection state to change from incomplete to
  complete.
- Negative inventory slots, insufficient quality and mismatched typed continuation data fail closed before
  selection. The existing donation primitives remain the sole owners of target-map pathing and native menu
  execution; no second local movement/donation implementation was introduced.
- Focused candidate and continuation tests, the current-collection bootstrap self-test, all 32 game-free Core
  tests and all 188 Backend tests pass. Formal training remains disabled. The next acceptance gate is a hidden,
  silent, isolated real-game run proving connector execution, fresh continuation recompilation, native
  donation and the exact terminal state transition for both donation surfaces.

### 2026-09-13: Rolling donation route runtime gate closed

- Hidden, silent, isolated run `runtime-collection-donation-routes-20260913-011522` passed both Museum and
  Community Center cases. Each normal daily-plan episode selected the exact Town route candidate, executed
  one verified connector, freshly continued into the existing native donation primitive, and verified the
  exact terminal collection-state transition. Both continuations completed; formal training remained off.
- The run exposed a real transparency omission instead of bypassing it: Town uses the hardcoded native
  `WarpCommunityCenter` action rather than an argument-bearing ordinary `Warp`. Locked 1.6.15 code fixes its
  destination at `CommunityCenter (32,23)` and gates native entry on `Game1.MasterPlayer` receiving either
  `ccDoorUnlock` or `JojaMember`. Route graph, current connector and action-gate projections now share that
  exact contract.
- Connector execution still performs collision-checked movement followed by native
  `GameLocation.checkAction`; no direct coordinate warp or duplicate donation state machine was added. The
  existing `EnterSewer` hardcoded action is also accepted by the same declared `action_warp` runtime family,
  aligning the executor with the transparent route scope it already advertised.
- This closes the real-game gate for current-date rolling Museum and Community Center donations. It does not
  prove future availability or authorize formal training. The next slice is cross-date scheduling: join
  calendar, unlock, inventory/resource reservation and opportunity windows into the authoritative Teacher
  dependency chain before a future-day action can be selected.

### 2026-09-13: Cross-date dependency-axis inventory gate

- Every authoritative acquisition route kind and every requirement-route occurrence now carries the same
  exact 12-axis downstream inventory: calendar window, unlock state, location route, facility capacity,
  resource inputs, currency budget, inventory reservation, processing lead time, stochastic retry budget,
  daily time/energy budget, opportunity cost and fresh terminal receipt. Missing, extra or duplicate members
  fail closed at lowering, frontier loading and current Teacher authority admission.
- Each `authoritative_acquisition_route` frontier node exposes the inventory and remains explicitly
  `pending_per_route_axis_evidence`. Inventory completeness is not dynamic feasibility: no route is admitted
  for a future date merely because all dependency names are present.
- The regression gate checks the top-level contract, every route-kind row and every route occurrence. Existing
  v1 lowering artifacts without the required inventory are intentionally stale and must be regenerated from
  the locked exact-version inputs. A negative self-test removes one axis from one route and verifies rejection.
- Release build, the aggregate current-collection self-test, all 33 game-free Core tests and all 188 Backend
  tests pass. No game or formal training run was started.
- The next fixed slice resolves `calendar_window` per route from authoritative evidence, emitting an explicit
  satisfied, evidence-backed not-applicable or blocked result. The remaining axes are then resolved in the
  declared order before any future-day Teacher candidate can become eligible.

### 2026-09-13: First acquisition-route calendar source resolution

- `acquisition_route_calendar_resolution.v1` preserves every exact requirement-route occurrence identity and
  hash-binds the requirement inventory, acquisition lowering, Master Angler window index and its opportunity
  catalog. It deterministically recompiles the window index from the verified catalog and compares the typed
  result, so stale lowering, modified window content or a tampered catalog chain fails before emission.
- The first resolver reuses the existing decompile-backed Master Angler calendar pipeline for location fishing,
  MineShaft fishing overrides and crab pots. It does not introduce a parallel fish calendar implementation.
  Each resolved occurrence carries the exact source windows including date range, season, clock, weather,
  dynamic conditions, location or mine area, equipment restrictions and stochastic markers.
- The exact 1.6.15 run retained all `1,599` occurrences: `264` have authoritative static source windows and
  `1,335` remain explicit blocks. The resolved set is `20` crab-pot, `241` location-fishing and `3` mine-override
  occurrences, all with at least one window.
- Eight additional `native_location_fish_spawn` occurrences produce non-Master-Angler items from fishing rows.
  They intentionally remain `blocked_authoritative_fish_window_not_found`; the regression locks their exact
  occurrence identities instead of borrowing an unrelated species window.
- This is a partial static-source report, not target-date eligibility. Resolved rows remain
  `target_date_pending`, all other route kinds remain parser-blocked, and training eligibility is false. The
  next slice generalizes one native calendar-condition parser for Data/Locations forage, artifact and non-fish
  fishing rows, then adds crop and shop conditions without duplicating per-route parsing logic.

### 2026-09-13: Data/Locations calendar source resolution

- The former Master Angler calendar normalizer is now the single shared
  `NativeCalendarConstraintNormalizer`. It statically intersects native `LOCATION_SEASON`, `SEASON`, `YEAR`,
  `TIME` and `WEATHER` clauses. Predicates that the exact 1.6.15 Data/Locations payload requires target-date
  state to answer remain verbatim dynamic conditions; an unknown predicate is preserved as unparsed and blocks
  the route instead of being accepted or discarded.
- Rebuilding the 72-species Master Angler opportunity catalog through the shared normalizer retained its exact
  SHA-256, so this refactor did not create or alter a second fish-calendar interpretation. Master Angler fish
  rows still resolve through the existing catalog/window index. Only the eight non-fish fishing-row outputs
  fall through to their exact Data/Locations row.
- Calendar resolution now hash-binds exactly one `runtime_data_locations` evidence file. Artifact spots,
  forage spawns and non-fish fishing outputs bind by exact location, source-row index and route kind. Missing
  evidence, stale hashes, source-identity drift, unsupported seasons, unparsed conditions, impossible static
  domains and windows after the Stage 1 deadline all fail closed with explicit blockers.
- The locked exact-version run preserves all `1,599` route occurrences and resolves `505`; `1,094` remain
  explicit parser blocks across 28 route kinds. Resolved evidence comprises 20 crab-pot, 241 Master Angler
  location, 3 mine override, 68 artifact-spot, 165 forage and 8 non-fish fishing occurrences. Every resolved
  route has at least one source window, and no current exact Data/Locations clause is unknown.
- Release build, aggregate current-collection self-test, all 33 game-free Core tests and all 188 Backend tests
  pass. No game or formal-training process was started. These rows remain
  `resolved_static_source_window_target_date_pending`: dynamic target-date checks and the other 11 dependency
  axes are not satisfied by this report.
- The next fixed slice resolves crop source seasons and growth timing, then shop stock conditions. Growth and
  processing lead time must remain an independently evidenced `processing_lead_time` result; a valid source
  season alone must never authorize a Teacher candidate.

### 2026-09-13: Data/Crops calendar source and growth constraints

- The authoritative requirement inventory now hash-binds exactly one runtime `Data/Crops` export plus the
  locked `Crop` growth/season source and `HoeDirt` planting/speed source. Source guards bind
  `IsInSeason`, `SeedsIgnoreSeasonsHere`, `DaysInPhase`, `RegrowDays`, wild-seed output selection, planting
  season checks and speed-increase distribution. Missing evidence, stale hashes, malformed crop rows or
  source-identity drift fail closed.
- Every `harvests_as` occurrence must bind exact `crop:<seed id>`, `Data/Crops` and
  `payload.<seed id>.HarvestItemId` identities. Its typed source record preserves native seasons, every phase
  duration, base first-harvest duration, regrow duration, watering and paddy requirements, native planting
  location rules, texture/sprite identity and the complete known output domain. Native-season windows and
  out-of-season `seeds_ignore_seasons` location-capability windows are distinct, so a greenhouse or Island
  override cannot be silently generalized to an ordinary outdoor tile.
- Spring, summer, fall and winter wild-seed crops `495..498` are cross-checked through their runtime
  `TileSheets\\crops`/sprite-23 identity and the decompiled season-specific random-output branches. Their full
  output domains remain stochastic in every window. The route-kind lowering now uses
  `source_resolved_downstream` instead of claiming that every crop harvest is deterministic.
- This slice does not turn a legal planting season into a harvest promise. Fertilizer, Agriculturist, paddy
  acceleration, actual watering history, seed and tile resources, location access, planting rules and the
  requested first-harvest or regrow date remain independently evaluated dependency axes. The static report
  continues to emit `target_date_pending` and cannot produce a formal Teacher label.
- The locked 1.6.15 rebuild preserves all `1,599` route occurrences and resolves `580`; `1,019` remain explicit
  parser blocks. All `75` crop occurrences resolve, including `8` stochastic wild-seed occurrences. Focused
  four-set tests, stale-crop-evidence rejection, exact artifact validation, Release build, all 33 game-free Core
  tests and all 188 Backend tests pass. No game or training process was started. The full regression script's
  assertions and syntax are updated, but its execution remains independently blocked by the pre-existing
  `OptionCapabilityRegistrySource.cs` input-lock drift, which this slice does not overwrite.
- `acquisition_routes_complete` continues to mean every required item has at least one authoritative route; it
  is not a claim that every optional alternative route has already been enumerated. The wild-seed output domain
  is losslessly preserved here, while expanding all reverse graph alternatives remains a separate dictionary
  completeness task and must not be inferred from the current occurrence count.
- The next fixed slice resolves shop stock/opening/native condition sources. After that, target-date resolution
  composes calendar sources with the remaining dependency axes before any future-day candidate is admitted.

### 2026-09-13: Data/Shops stock and access source resolution

- The authoritative requirement inventory now hash-binds the exact runtime `Data/Shops` export, the compiled
  `access-constraint-index.json`, and locked decompiled `ShopBuilder`, `Utility.TryOpenShopMenu`, `ShopMenu`, and
  `GameStateQuery` sources. Guards cover stock construction, item queries, row conditions, Pierre's stock-list
  exception, price and quantity modifiers, finite/synchronized stock, owner selection, closed messages, currency
  charging, barter consumption, recipe learning and purchase actions. Missing or stale evidence fails closed.
- Every `sells` occurrence must match its exact `shop:<id>`, `Data/Shops`, and stock-row path, then match that same
  row in the access index with a native handler for every condition clause. The typed result preserves source-row
  identity/hash, shop currency, price, barter item/count, finite-stock mode, recipe flag, relevant modifiers,
  purchase actions, owners, static interaction endpoints, and base-map door windows associated with those endpoint
  maps. Runtime stock and menu receipts are still mandatory.
- `SEASON`, native range-style `YEAR` including negation, `DAY_OF_WEEK`, `DAY_OF_MONTH`, and `TIME` are projected
  over the pre-Grandpa deadline. Other natively recognized clauses remain exact dynamic predicates rather than
  being guessed. In the current 109 target rows those include player book state, synchronized choice, and
  synchronized random availability. Festival/event entry, owner presence and schedules, dynamic maps, live stock,
  affordability and barter resources remain target-date dependency work even when a stock row has an all-day
  static source window.
- The locked 1.6.15 rebuild preserves all `1,599` route occurrences and resolves `689`; `910` remain explicit
  parser blocks. All `109` shop occurrences resolve across 32 shops: 68 barter rows, 69 finite-stock rows, one
  recipe row, and two stochastic-availability rows. Native-handler gaps and empty windows are zero. Twenty-seven
  occurrences have exact counter endpoints and 22 additionally join to base-map door windows; absent dynamic or
  festival endpoints stay explicit downstream access obligations.
- Shop code is split into route/source binding, calendar projection, and access-evidence modules. Focused four-set
  tests cover weekday and negated-year semantics, dynamic predicate retention, barter/stock/action facts,
  endpoint/door joins, and stale shop/access evidence rejection. Release build and exact artifact validation pass;
  no game or formal training was started. The next fixed slice consumes an explicit target date and resolves only
  the calendar axis, leaving the other 11 axes independent and fail closed.

### 2026-09-13: explicit target-date calendar axis

- `acquisition_route_target_date_calendar.v1` consumes the authoritative inventory, route lowering, Master Angler
  window index, static source report, and one explicit `target_total_day`. It recompiles the entire static source
  report from the locked evidence and requires equivalent complete typed JSON before evaluating the date, so a modified
  intermediate report cannot silently change eligibility.
- Each route occurrence is classified as a static-window match, a static-window miss, or an unresolved upstream
  source. The boolean is deliberately named `static_window_matches_target_date`; it is not a general availability
  flag. Matched native time windows, weather modes, dynamic predicates, stochastic markers and location obligations
  remain attached. No unlock, access, capacity, resource, currency, reservation, lead-time, retry, daily-budget,
  opportunity-cost or terminal-receipt fact is inferred.
- For locked 1.6.15 Spring 1 (`target_total_day=0`), all 1,599 occurrences remain present. The calendar axis resolves
  for the 689 supported sources: 451 match that date and 238 do not; the other 910 remain explicit source-parser
  blocks. Fifteen matches still carry dynamic predicates. Both the aggregate and every downstream admission path
  remain `training_label_eligible=false`.
- The focused fixture proves first-year inclusion and second-year exclusion for `!YEAR 2`, native inclusive `TIME`
  maximum conversion into the internal right-open interval, dynamic-condition retention, tamper rejection and
  deadline rejection. The downstream `unlock_state` stage described below consumes this result; a resolved static
  window is still not a claim that festival, unlock, route or resource conditions are satisfied.

### 2026-09-13: explicit target-date unlock-state axis

- `acquisition_route_target_date_unlock_state.v1` recompiles and compares the complete target-date calendar artifact,
  then requires an exact-version transparent snapshot from that same `target_total_day`. The bridge adds one compact
  `world_progress.game_state_query_unlock_state` field containing every farmer's stable ID, Current/Host flags, three
  native mail collections, all `Stats.Values`, active special-order IDs/rules, and `IslandNorth.bridgeFixed`.
- The evaluator implements only decompile-verified unlock predicates currently present in authoritative acquisition
  sources: `PLAYER_HAS_MAIL`, `PLAYER_SPECIAL_ORDER_ACTIVE`, `PLAYER_SPECIAL_ORDER_RULE_ACTIVE`, `PLAYER_STAT`, and
  `IS_ISLAND_NORTH_BRIDGE_FIXED`, including normal GSQ negation. Native Current, Host, Any, All and existing numeric-ID
  selection is preserved. `Target` remains blocked until the source-call context is carried explicitly; it is never
  silently treated as Current. Missing stat keys resolve to zero and pending-mail `%&NL&%` handling matches native code.
- Predicate ownership is explicit. Festival/day predicates remain calendar work, `RANDOM` and `SYNCED_*` remain
  stochastic work, `PLAYER_HAS_ITEM` remains resource work, and player-location checks remain route work. Unknown
  predicates block the route. This prevents the unlock stage from consuming conditions owned by another dependency
  axis or turning a partial result into a training label. Every result also carries the upstream
  `source_resolution_status` and complete matching source windows losslessly, so later axes never reconstruct or
  silently discard time, weather, source and pending-condition evidence.
- The locked day-37 archived snapshot produces 1,599 rows: 910 upstream source blocks, 191 static misses, 489 routes
  whose unlock state matches, and 9 routes blocked because that older snapshot lacks the new bridge field. It also
  preserves six dynamic calendar conditions, one stochastic condition and one resource condition, with zero unknown
  conditions. Focused tests cover distinct Current/Host players, Any/All/numeric selection, mail modes, negation,
  inclusive stat ranges, bridge state, missing evidence, cross-day rejection and artifact tampering.
- This stage remains `training_label_eligible=false`. The next fixed slice resolves residual target-date festival
  conditions from exact runtime/decompile evidence; that slice is now implemented below.

### 2026-09-13: explicit target-date festival-state axis

- `acquisition_route_target_date_festival_state.v1` deterministically rebuilds and compares the preceding unlock
  artifact from the same authoritative inputs and same snapshot. Route rows contain the complete typed
  `upstream_route`, so source, window and unlock evidence are preserved by composition rather than copied again.
- The compact `world_progress.game_state_query_calendar_state` field projects exact current total day, time and
  `Game1.stats.DaysPlayed`, all loaded `Data/Festivals/FestivalDates` keys, active passive-festival IDs, and the loaded
  passive-festival season/day/start-time/condition catalog. It scans no maps and invokes no mutating or random code.
- The evaluator follows the locked 1.6.15 handlers for `DAYS_PLAYED`, location-independent `IS_FESTIVAL_DAY` including
  offsets, and `IS_PASSIVE_FESTIVAL_OPEN` including the inclusive start-time edge. Normal GSQ negation is supported.
  Location-scoped ordinary-festival forms remain explicitly blocked until the festival location catalog and any
  Here/Target call context are projected; none occur in the authoritative 1,599-route inventory.
- The archived day-37 snapshot retains all 1,599 occurrences: 919 inherit upstream blocks, 191 are static-window
  misses, 483 continue through this axis, and the exact six Trout Derby, SquidFest or ordinary-festival rows block
  because the archive predates the new bridge field. Unknown conditions remain zero. The fixture resolves all three
  predicate families at the passive-festival opening boundary and proves offset, negation, missing-field, unsupported
  context and upstream-tamper behavior.
- This remains `training_label_eligible=false`. The next fixed slice is `location_route`; stochastic and resource
  predicates continue to their independently owned axes.

### 2026-09-13: explicit target-date location-route axis

- `acquisition_route_target_date_location_route.v1` deterministically rebuilds and compares the preceding festival
  artifact from the same authority inputs and same snapshot. Every row embeds its complete typed upstream route.
- Target binding preserves native source identity. Native-season crops bind to `Farm`; season-independent crops only
  bind to runtime maps whose exact `SeedsIgnoreSeasonsHere()` result is true; shops use matching live `shop_endpoint`
  rows; crab pots use complete live exact placed locations; `Default`, `Farm_<type>`, exact Data/Locations keys and
  mine fishing overrides follow the locked decompiled rules. The bridge adds per-map `location_context_id` and
  `seeds_ignore_seasons_here`, plus the exact active `farm_type_key`, instead of inferring these facts from examples.
- The stage batch-reuses the sole Core `FutureRouteDateEvidenceProducer.ProduceLocationArrivals`; it does not add a
  second BFS or route system. Same-day all-map walkability, connector gates and versioned conservative movement
  timing must be complete. Guaranteed arrival must precede a retained source-window end, while restricted weather is
  evaluated in the target map's location context.
- The result proves source-map arrival only. Random/live source appearance, fishable or terminal tile reachability,
  shop stock, resources, final interaction and fresh native receipt remain downstream. The fixture preserves 76/76
  occurrences and matches 4/4 active sources, including waiting until a locked shop opens at 9:00 and arriving at
  9:02. Removing full-map route evidence blocks exactly the four active sources while leaving 72 static misses
  resolved. A real day-223 archive preserves all 1,599 occurrences: 197 static misses, 927 inherited upstream blocks
  and 475 activity rows blocked on missing route-date and movement-context evidence; no row is guessed available.
- This remains `training_label_eligible=false`. The next fixed dependency axis is `facility_capacity`; stochastic,
  resource, budget, reservation, lead-time, retry, daily-budget, opportunity-cost and fresh-receipt axes retain
  independent ownership.

### 2026-09-13: explicit target-date facility-capacity axis

- `acquisition_route_target_date_facility_capacity.v1` deterministically rebuilds and compares the complete
  location-route artifact from the same authority inputs, snapshot and movement calibration. All 33 authoritative
  route kinds belong to one exhaustive capacity classification; an unknown kind cannot default to no capacity.
- The low-frequency all-location route projection now carries compact `prepared_cultivation_capacity.v1` rows. They
  enumerate existing HoeDirt and empty-bush garden pots only, preserving open, occupied, pot and per-harvest-item
  counts. Occupied slots whose live crop output identity is unavailable are counted separately and fail closed when
  they could change a negative result. Potential untilled land is not scanned or inferred.
- Among route kinds currently able to pass the preceding axes, ordinary fishing/location/shop sources explicitly do
  not consume facility capacity, placed crab pots reuse the exact live network already proven by `location_route`,
  and crop routes require enough existing target-output crops plus open prepared-soil slots across matched source
  locations to cover the full amount under the authoritative minimum harvest yield. An open slot satisfies only
  capacity: seed/input, planting, watering, growth, target-date harvest and final receipt remain independent axes.
- Machine, pond, animal, fruit-tree, tapper and cooking-facility route kinds are classified as capacity-bearing but
  remain blocked by earlier source/location stages today. If one reaches this stage before its locked facility binding
  is implemented, it fails closed per route. This preserves the denominator without pretending those families are
  facility-free.
- The fixture retains 76/76 occurrences: 72 upstream static misses and four active capacity matches, including two
  crop occurrences backed by two open Farm soil slots. Removing that field blocks only the two crop occurrences;
  complete zero capacity yields two resolved misses. The archived day-223 snapshot retains all 1,599 rows as 197
  upstream static misses and 1,402 inherited upstream blocks, with no capacity guessed from the older schema.
- Training authorization remains false. The next fixed dependency axis is `resource_inputs`; capacity-bearing source
  families must still receive their exact earlier source/location bindings when those upstream parsers are opened.

### 2026-09-13: explicit target-date resource-input axis

- `acquisition_route_target_date_resource_inputs.v1` deterministically rebuilds and compares the complete facility
  artifact, then joins every route occurrence to its exact static source row. All 33 route kinds have one explicit
  resource class; deferred reward, machine, animal, pond, geode and recipe inputs cannot fall through to no-input.
- Available quantities come from canonical `farm.material_inventory_graph.v1` and the existing
  `MaterialSupplyProjection`. This includes actor-authorized immediately available player/chest nodes without
  duplicating global inventories; inaccessible/shared quantities remain excluded. Material, rod and crab-pot reads
  are independently lazy and cached, so only an active route of that family touches its field. Reservation
  competition is still owned by the later `inventory_reservation` axis.
- Existing target crops offset demand only by their authoritative minimum harvest yield. Remaining output demand is
  divided upward by that same minimum yield, and each required open prepared-soil slot requires one exact `Data/Crops`
  seed.
  Shop barter items are checked here, but money and other native currencies remain under `currency_budget`. Ordinary
  target-date fishing requires no consumable; if every retained window requires Magic Bait, the snapshot must prove a
  bait-capable rod plus attached or loose `(O)908`. A placed crab pot with target output, loaded bait or owner
  Luremaster needs no new input; an unserviced pot fails closed until the complete native bait candidate domain is
  bound instead of guessing one bait ID.
- Reusable tools are not counted as consumable resource quantities. Their ownership and exact live usability remain
  fresh candidate/compiler/runtime preconditions in the already implemented action stack. Facility establishment,
  lead time, retries, reservations, daily time/energy and terminal receipts remain separate axes.
- The focused fixture retains 76/76 occurrences and resolves all four active routes: one one-seed route, one two-seed
  route, one ten-Wood barter route and one no-input fishing route. Removing the canonical graph blocks only the three
  actual input routes; removing the seed produces two resolved misses. The archived day-223 snapshot remains 197 upstream
  non-applicable and 1,402 inherited blocks, with zero guessed resource matches.
- Shop barter requirements use the exact current native `ShopBuilder` quote shared with the following currency stage,
  including item-query overrides. Static `Data/Shops` trade terms remain provenance and drift evidence, not a second
  executable purchase model.
- Training authorization remains false. The next fixed dependency axis is `currency_budget`.

### 2026-09-13: explicit target-date currency-budget axis

- `acquisition_route_target_date_currency_budget.v1` deterministically rebuilds and object-compares the resource-input
  artifact, joins exact alternative amounts from acquisition lowering, and preserves every route occurrence. All 33
  route kinds have one explicit currency class; unknown kinds cannot fall through to a free route.
- The bridge exposes `shop_currency_balances.v1` from one shared native reader used by player state, unloaded-shop
  previews and the live shop menu. The locked 1.6.15 domain is exactly `0 money`, `1 star tokens`, `2 club coins` and
  `4 Qi gems`; unsupported IDs fail closed. Shop purchase identity is `(shop_id, synced_key, qualified_item_id)` and
  its current `ShopBuilder` quote owns price, currency, stock, buyability and effective barter terms.
- A missing or malformed complete quote/currency projection is missing evidence. A complete quote that is sold out,
  not buyable, below minimum output quality or unaffordable is a resolved miss. Purchases use
  `ceil(required amount / native output stack)` operations and scale price, finite stock and barter count together.
  `native_money_payment` reads its exact positive amount from lowering and requires the money currency. No balance is
  inferred from future sales or unrelated candidate utility.
- The focused fixture preserves 76/76 occurrences with four active matches, three currency-free routes and 72 upstream
  non-applicable routes. Removing the shop quote or currency field blocks only the affected purchase; 50 available
  money against the scaled 200 total produces one known miss. Training authorization remains false.
- This axis proves one route occurrence only. `inventory_reservation` is the next fixed axis and must prevent the same
  balance or material from satisfying multiple selected routes, while keeping future income distinct from current
  spendable state. Lead time, retries, daily budgets, opportunity cost and fresh terminal receipts remain downstream.

### 2026-09-13: explicit target-date inventory-reservation axis

- `acquisition_route_target_date_inventory_reservation.v1` deterministically rebuilds and object-compares the
  currency-budget artifact, then reads one explicit controller-owned `strategy_commitment_ledger.v1`. Missing files,
  save/player mismatch, malformed reservation contracts, duplicate IDs and active reservations that exceed current
  transparent supply fail closed before route evaluation.
- Each upstream-matched route is evaluated independently after subtracting every other active reservation. Material
  inputs reuse `MaterialSupplyProjection` and are allocated deterministically to exact actor-authorized node/slot/item
  rows; currency inputs reuse `NativeCurrencySupplyProjection` over the locked four-member native domain. Cancelled
  and completed rows remain auditable but do not consume supply.
- Output is one atomic claim set per feasible route, with deterministic reservation IDs, current state hash and
  expected ledger revision. Existing active claims for the same source decision are excluded from the supply
  calculation and then compared exactly, distinguishing a new proposal, an idempotently committed set and a required
  replacement. The report never selects all alternatives or mutates the ledger.
- The focused fixture preserves 76/76 occurrences: 72 upstream non-applicable rows, four matches, one no-claim route,
  three proposed claim sets, three material claims and one currency claim. Independent material and currency
  reservations each turn only the affected shop route into a resolved conflict; cancelled rows release supply,
  exact existing claims are recognized, and globally overbooked or wrong-player ledgers are rejected.
- Attached Magic Bait cannot yet be addressed by the slot-based material ledger and therefore blocks instead of being
  treated as loose inventory. Atomic controller commit/route portfolio selection remains downstream. Training
  authorization stays false; the next fixed dependency axis is `processing_lead_time`.

### 2026-09-13: explicit target-date processing-lead-time axis

- `acquisition_route_target_date_processing_lead_time.v1` deterministically rebuilds and object-compares the complete
  inventory-reservation artifact, joins every occurrence back to its authoritative static source row, and validates
  same-day snapshot identity. All 33 route kinds have an explicit processing class; no unknown production route may
  inherit a zero-duration default.
- This axis owns deterministic production waiting only. Immediate shop, fishing, geode, forage and other direct
  interactions require no processing delay, but their action duration remains downstream in `daily_time_energy_budget`
  and their random attempts remain downstream in `stochastic_retry_budget`.
- Crop routes combine locked native `DaysInPhase`, minimum yield/quality bounds with exact per-tile `farm.crops` or loaded
  `current_location.crops`. A new planting retains its authoritative base duration but emits only the decompile-proven
  not-before-next-day lower bound because fertilizer, profession and paddy acceleration are not resolved at an open
  aggregate slot. This is sufficient to reject same-day output without inventing an exact maturity date. An existing
  crop matches only when its live row is harvest-ready and the summed proven output covers the complete amount and
  minimum quality; missing rows, aggregate/detail count drift, non-exact harvest identity or an inconsistent zero-day
  countdown fail closed. Dead or later-maturing crops are resolved misses.
- Crab-pot routes require the complete persistent pot network. A consistent ready target output is immediate; a
  serviced empty pot only proves a next-morning production attempt under locked native `CrabPot.DayUpdate`, leaving
  output probability to the retry axis. Ready crab-pot outputs are summed by exact stack and quality. Machine, animal,
  pond, fruit-tree, solar-panel and tapper lead-time evaluators
  remain explicit blockers until their upstream source/facility/input bindings are complete.
- The focused fixture remains 76/76: 72 upstream non-applicable occurrences, two immediate matches and two known crop
  misses. A ready-crop variant produces four matches; removing the required live crop detail blocks only the two crop
  routes, and a tampered reservation artifact is rejected. Training authorization remains false. The next fixed axis
  is `stochastic_retry_budget`, followed by daily time/energy, opportunity cost and fresh terminal receipt.

### 2026-09-13: authoritative route amount/quality retrofit

- Every route occurrence now carries `match_kind`, positive `required_amount` and non-negative `minimum_quality` from
  authoritative lowering through static calendar, target-date calendar, unlock and every nested downstream artifact.
  Each join compares that contract back to the static source row. The former late currency-only amount lookup was
  removed because it allowed facility, resource and processing stages to silently evaluate one ordinary item.
- Locked `Data/Crops` fields now include minimum/maximum harvest stack, extra-harvest chance and minimum/maximum quality.
  Facility capacity, seed demand and ready-crop output use only guaranteed minimum yield. Requirements above the static
  minimum quality block until exact live per-tile harvest-quality projection exists. Native shop output stack/quality
  and crab-pot output stack/quality are preserved and evaluated rather than inferred.
- The focused amount-two fixture proves two crop slots, two seeds, ten barter items, 200 money, two finite-stock purchase
  operations and two ready output units. This is an anti-hallucination contract repair, not new training evidence;
  `training_label_eligible` remains false.
- `stochastic_retry_budget` remains the next axis, but retry-expanded consumables cannot inherit a reservation made for
  one baseline attempt. Its output must feed a final resource/currency/atomic-reservation validation before execution.
  Any implementation that merely appends retry time after the present claim set is invalid.

### 2026-09-13: explicit target-date stochastic retry budget, first segment

- `acquisition_route_target_date_stochastic_retry_budget.v1` deterministically rebuilds and object-compares the full
  processing-lead-time artifact, preserves all route occurrences, and validates every carried uncertainty mode against
  the exhaustive 33-route-kind catalog. `uncertainty_mode` now travels from lowering through static calendar,
  target-date calendar and unlock instead of being reconstructed by a late lookup.
- Deterministic fresh receipts and source-resolved guaranteed outputs need zero random retries. A native stochastic
  route also needs zero retries when exact live processing evidence proves that the required amount and quality are
  already materialized. Every other native stochastic route fails closed until exact target-location and terminal
  action probability evidence is present; a current-location fishing projection is not remote-location evidence.
- The 0.95 success threshold now has one Core owner, `StochasticRetryPolicy`, shared with crab-pot capacity assessment.
  Retry-expanded consumables still require a resource/currency/atomic-reservation loopback before execution, and
  daily action time remains downstream.
- The focused fixture preserves 76/76 occurrences: 74 are upstream non-applicable, the exact shop receipt passes with
  zero retries, and the reachable Beach fishing route blocks on missing terminal-tile probability. Processing artifact
  tampering is rejected. Training authorization remains false. The next slice adds remote fishing rule/tile probability
  evidence, then exact multi-success retry math and reservation revalidation.
- Issue #128 closes a later-discovered ontology leak: `source_resolved_downstream` proves source identity, not outcome
  determinism. Zero retry now requires either a single deterministic source-specific output or a fresh exact output
  proof. Locked 1.6.15 `Crop` evidence shows seasonal wild seeds choose and persist
  `replaceWithObjectOnFullGrown` at planting, so the transparent bridge and cultivation-capacity projection share one
  resolver for that live selected output instead of treating `indexOfHarvest` as the final item. Missing or out-of-domain
  selected output evidence fails closed; any future retry expansion still requires resource, currency and atomic
  reservation revalidation before it can authorize execution.

### 2026-09-13: authoritative Master Angler spawn-chance input inventory

- The requirement inventory now hash-binds a distinct `native_fish_spawn_chance_rule` source to decompiled
  `SpawnFishData.GetChance`. Source guards lock the base chance, curiosity-lure, daily-luck, quantity-modifier,
  targeted-bait and luck-level formula branches. The existing `native_location_spawn_rules` evidence also guards
  the `GameLocation.getFish` precedence/random-order, two-pass, item-query and generic-fish acceptance call sites.
- All 180 matching native Master Angler location rules now retain direct or random item selection plus every static
  `GetChance` input. The locked 1.6.15 catalog contains 174 direct-selection rules, six random-selection rules and
  14 fish-caught-seeded rules, with zero unresolved static inputs. A synthetic regression covers typed chance
  modifiers because none of those 180 native matching rules currently contains one.
- This is not terminal catch-probability closure. The next evaluator must construct the complete Default-plus-target-
  location competing rule set for one target tile/context, apply precedence and randomized equal-precedence order,
  both targeted-bait passes, item-query selection and `CheckGenericFishRequirements`. Until that exact or explicitly
  conservative result exists, the stochastic retry axis remains blocked and formal training remains unauthorized.

### 2026-09-14: demand-only fishing terminal probability evidence

- `fishing_forecast` is a purpose-limited bridge profile keyed by the exact loaded location ID and fishing-rod slot.
  Its cache identity includes both values, and its collector admits only `world`, `fishing` and unavailable-field
  bookkeeping domains. It does not enumerate unrelated locations or run the heavy player, menu, farm or NPC readers.
- The bridge projects the complete Default-plus-requested-location first-pass spawn-rule inventory, every fishable
  tile/depth, exact rule eligibility, output selectors and generic `Data/Fish` acceptance. Decompiled
  fish-caught-seeded rolls use `Utility.CreateRandom(uniqueGameId, PreciseFishCaught * 859)` and are represented as a
  deterministic pass/fail for the captured state, never as an independent retry probability. Unseeded `RANDOM`,
  custom query resolvers, chance modifiers and unresolved item queries fail closed.
- `acquisition_route_target_date_fishing_probability.v1` hash-binds a manifest of same-save, same-player, same-day,
  same-time forecast snapshots within a 30-tick capture window. It preserves all 76 fixture route occurrences and
  evaluates only active `native_location_fish_spawn` routes. Every fishable bobber tile is paired with mechanically
  legal cardinal stand positions; rule precedence and equal-precedence competitors produce a conservative first-pass
  lower bound. The focused Beach fixture resolves `(O)145` to a positive 0.2 lower bound and remains deterministic.
- This artifact cannot authorize training. Collision-aware stand reachability, the second targeted-bait pass,
  retry-context stability, exact multi-success retry math, retry-expanded resource/currency/reservation validation,
  daily time/energy and a fresh terminal receipt remain downstream. The next slice feeds this probability evidence
  into stochastic retry budgeting only after an explicit repeatability proof; fixed-seed state must never be promoted
  to an IID retry model.

### 2026-09-14: no-bait fishing retry budget closure

- A first-pass probability now carries a separate repeatability proof. The proof requires an explicitly empty bait
  slot, no Magic Bait or Curiosity Lure probability effect, no temporary Fishing-level buff, and an unchanged selected
  rule prefix: every target or possible preceding competitor must have resolved eligibility, no mutable
  condition/per-item condition, no catch limit or catch flag, no future minimum-level activation, and an independent
  native RNG roll. A valid one-cast probability remains usable as evidence even when this stricter retry proof fails,
  but it cannot be multiplied into repeated attempts.
- `StochasticRetryPolicy.RequiredIndependentAttemptCount` owns the exact binomial lower-tail calculation for one or
  more required successes at the shared 0.95 threshold. It uses log-space summation and a bounded binary search, so
  large attempt limits do not allocate or iterate an attempts-by-output matrix. Zero probability, mathematical
  certainty requests for a non-certain trial, and budgets above 100,000 attempts fail closed.
- The stochastic artifact now deterministically rebuilds and hash-checks the fishing-probability artifact and its
  forecast manifest before joining all route occurrences. The focused `(O)145` fixture requires 14 total casts for
  one success at a conservative 0.2 single-cast lower bound; 13 are additional attempts. Because repeatability is
  currently proven only for an empty bait slot, this route does not expand material reservations. For required output
  quantities above one, additional retries are correctly `total attempts - required output quantity`, not
  `total attempts - 1`.
- The 76-route fixture now completes the stochastic axis with two matching routes, zero probability blockers and
  training authorization still false. Stand reachability and preserving the same rule context across the resulting
  action-time window remain owned by `daily_time_energy_budget`; opportunity cost and fresh terminal receipts remain
  later axes. Baited retries stay blocked until per-cast consumption and expanded atomic reservation ownership are
  implemented.

### 2026-09-20: non-scalar target-date opportunity-cost axis

- `acquisition_route_target_date_daily_time_energy_budget.v2` first closes the conservative same-snapshot route,
  terminal-duration and native-energy budget for each active occurrence. The opportunity-cost builder then
  deterministically rebuilds and object-compares that complete upstream artifact; a copied or edited report cannot
  become Teacher evidence.
- Cost remains an auditable vector instead of one guessed utility scalar: guaranteed elapsed game minutes, required
  native energy, exact material quantities keyed by qualified item/quality/live unit sale price, and each native
  currency ID. Material sale value is retained only as a readable audit summary. It cannot erase item identity or
  convert money, star tokens, club coins and Qi gems into a common unit.
- Pareto comparison is restricted to routes with the same requirement set, requirement and alternative index. A
  route is dominated only when another route is no worse in every exact dimension and strictly better in at least
  one. Equal vectors and time/energy/material/currency trade-offs remain on the frontier for later portfolio policy.
  Learner scores, future earnings and speculative downstream value are prohibited.
- Material dimensions are recovered through the atomic reservation claim's exact node/slot/item identity and the
  same raw transparent snapshot. Missing quality or sale price, stale state hashes, unauthorized or changed slots,
  invalid quantities, unknown currency domains and arithmetic overflow fail closed. This axis neither selects a
  route portfolio nor commits a claim.
- The focused regression passes for all 76 occurrences: two current matches remain separate Pareto-front routes and
  74 remain upstream non-applicable. Dedicated pure checks cover strict dominance, incomparable trade-offs and equal
  vectors. This report still cannot predict which route will execute or fabricate its terminal receipt.

### 2026-09-21: exact route execution binding and fresh terminal receipt

- `acquisition_route_execution_binding.v1` is a pre-dispatch artifact created only after route selection. It
  deterministically rebuilds the complete opportunity-cost chain, requires the selected occurrence to belong to the
  complete Pareto frontier, deterministically rebuilds the verified portfolio commit receipt, and hash-binds the
  lowering report, source snapshot, committed strategy ledger, portfolio receipt and immutable pending action queue.
  The accepted candidate ID is derived from the occurrence rather than supplied as an alias, and every queue item
  repeats the exact occurrence, requirement, route, source, quantity and quality identity plus the reservation
  portfolio ID and committed ledger revision. Every option must belong to the selected route's authoritative
  endpoint/support set, at least one endpoint must be present, and actor, mode and state hash must remain exact.
  Qualified item identity, an uncommitted preflight or a stale queue is never sufficient to infer a route.
- `acquisition_route_fresh_terminal_receipt.v1` is post-execution evidence, not another predictive target-date axis.
  It recomputes and compares the binding, then validates the canonical ordered queue receipt against same-save,
  same-player, same-target-day snapshots. Every queue item must preserve order and identity, rebind the correct state,
  carry native primitive verification and changed facts, advance to a fresh state/tick, and close the final boundary.
- Item routes independently recount exact player inventory rows at or above minimum quality and require a net gain of
  the complete `required_amount`. The focused regression explicitly rejects a one-unit gain for a two-unit route.
  Native community-center money payments instead require both the exact money decrease and the corresponding bundle
  ingredient `false -> true` transition. The shared exact inventory verifier also fixes the older Stage 1 Teacher
  receipt path, which previously accepted any positive increase for a multi-quantity requirement.
- Release build and the complete focused 76-route Bootstrap chain pass with zero warnings/errors. All twelve fixed
  dependency-axis contracts now have an explicit boundary, but a verified individual route is only route-level
  training evidence. Portfolio composition across requirements, atomic reservation commit and formal rollout-
  controller admission remain mandatory; `formal_training_authorized` stays false.

### 2026-09-21: scoped route portfolio admission and atomic reservation commit

- `acquisition_route_portfolio_proposal.v1` names an explicit requirement-group scope, exact selected route
  occurrences and any exact route occurrences being replaced. The corresponding admission builder rebuilds and
  object-compares the full target-date opportunity-cost chain instead of trusting a copied report.
- Every scoped group is checked against its authoritative `all_required` or
  `choose_at_least_required_slots` rule. Exactly one route may serve each selected alternative, selected routes may
  not escape the declared scope, and every selected occurrence must remain on the complete Pareto frontier. The
  builder validates a caller proposal but deliberately does not scalarize incomparable cost vectors or invent a
  Teacher preference.
- The selected cost vectors are summed without erasing dimensions: elapsed minutes, energy, exact material
  identity/quality/live price and each native currency remain separate. Claim sets are combined and preflighted
  against the same snapshot and ledger so cross-route slot or balance overbooking fails before dispatch.
- `POST /api/v1/strategy/commitments/reservation-portfolios/commit` now provides the missing storage transaction.
  Releases and all material/currency upserts run against an in-memory staging ledger under one repository lock;
  any failure discards the staging result. Success is saved once, advances the ledger once, and records all component
  history plus a portfolio commit marker at that same revision.
- `acquisition_route_portfolio_commit_receipt.v1` recomputes the pre-commit admission and verifies the exact committed
  active claim set. Every admitted selection advances exactly one ledger revision, cancels every explicit release,
  retains every exact material/currency claim and records each component plus one portfolio marker at that same
  revision. A claimless selection still performs a marker-only atomic commit so later settlement has real ownership.
- Each route execution binding now rebuilds this receipt, requires its occurrence to appear exactly once in the
  selected portfolio and requires all normalized commands to repeat the exact portfolio ID and committed revision.
  This prevents preflight-only, stale or unrelated route queues from borrowing reservation ownership.
- Backend regression is 199/199, the experiment Release build is warning-free, and the focused 76-route Bootstrap
  chain passes real shop-route material + money commit/receipt, tampered-ledger rejection, claimless marker commit,
  missing queue ownership rejection and fresh terminal evidence. Formal training remains false until an independent
  Teacher selects among incomparable portfolios and the rollout controller closes ordered multi-route execution,
  fresh replanning and portfolio-level completion evidence.

### 2026-09-21: complete bounded portfolio Teacher preference

- `acquisition_route_portfolio_teacher_preference_request.v1` supplies only goal/state/ledger identity and an exact
  requirement-group scope. The Teacher derives candidates from authoritative selection rules and every target-date
  Pareto occurrence; no caller candidate list or learner rank enters the denominator.
- `all_required` uses every alternative. `choose_at_least_required_slots` enumerates every feasible subset from the
  required count through all alternatives, then takes the Cartesian product of every Pareto route for each selected
  alternative and across scoped groups. More than 4,096 portfolios blocks the whole request without truncation.
- Every generated proposal reuses `acquisition_route_portfolio_admission.v1` and atomic preflight. Unavailable
  portfolios are deferred without negative labels. A Teacher label exists only for one unique aggregate vector that
  strictly Pareto-dominates all other admitted candidates; equal or time/energy/material/currency trade-offs remain
  explicitly unresolved instead of being ordered by IDs, learner score, sale-value totals or currency conversion.
- The selected proposal/admission serialize into artifacts that the original admission builder can reproduce exactly.
  Per-route execution binding rebuilds the preference and rejects an otherwise legal caller-selected proposal that is
  not its unique selection. The focused 76-route chain and strict-dominance/equality/trade-off pure checks pass with a
  warning-free Release build. Formal authorization remains false pending evidence-backed incomparable-portfolio policy,
  ordered route execution, fresh replanning, reservation lifecycle and portfolio-level completion evidence.

### 2026-09-21: exact completed-route reservation settlement

- `reservation-portfolios/settle-completed-route` is the post-execution counterpart to atomic portfolio commit. It
  consumes a fresh state hash, optimistic ledger revision, exact portfolio/goal/route source identity, the lowercase
  SHA-256 of a fresh terminal receipt, and the route's complete active reservation ID set. Missing or extra IDs reject
  the mutation before any row changes. Success marks every route-owned material/currency row completed and records
  all components plus one portfolio route-completion marker at one new revision. Claimless routes still receive the
  marker, so lack of reserved inputs cannot erase the execution boundary.
- `acquisition_route_portfolio_settlement_receipt.v1` does not trust the API request. It deterministically rebuilds
  the execution binding and fresh terminal receipt, derives the exact active set from the committed ledger, compares
  the canonical request, verifies the returned result, and replays the Core transaction using the marker timestamp.
  Result/ledger drift, extra history, partial completion or a leaked active claim fails closed.
- Backend regression is 202/202 and the focused 76-route chain proves the shop route's two-claim settlement plus
  tamper rejection with a warning-free Release build. The verified receipt sets `fresh_replan_required=true` and
  keeps formal authorization false. Settlement alone cannot dispatch a stale route ID; the checkpoint and verified
  continuation sections below own completed-alternative carry-forward and current-state reselection.

### 2026-09-22: initial portfolio rollout checkpoint

- `acquisition_route_portfolio_rollout_checkpoint.v1` deterministically rebuilds the selected Teacher proposal and
  exact completed-route settlement, then binds the completed occurrence to its authoritative requirement alternative.
  Progress is evaluated per scoped `all_required` or `choose_at_least_required_slots` rule rather than by route count.
- Whole-portfolio completion is admitted only when every scope has no remaining required slot, every route selected by
  the current proposal is completed, and the settled ledger contains no active reservation for any selected decision.
  The focused shop route proves this exact single-transition path; a two-alternative `all_required` check proves that
  one completed alternative remains incomplete.
- Every incomplete checkpoint requires a fresh replan and cannot authorize a pending ID from the old proposal. The
  checkpoint itself does not select the next route; the verified continuation Teacher boundary below owns removal of
  completed alternatives and current-state reselection. Until the selected continuation is committed, executed,
  settled and folded into a second checkpoint, `formal_training_authorized` remains false.

### 2026-09-22: verified continuation Teacher denominator

- `acquisition_route_portfolio_continuation_teacher_request.v1` is not caller-authored planning state. Its builder
  exactly rebuilds the initial checkpoint, rejects a completed portfolio, then binds the checkpoint hash, terminal
  state hash, settled ledger hash/revision, cumulative completed alternatives and next transition number.
- The continuation Teacher removes completed alternatives inside the same enumeration implementation and reduces each
  rule's remaining slots. Cumulative completed plus newly selected alternatives must still satisfy the authoritative
  rule in the same admission/preflight implementation. A normal proposal that supplies completion evidence without the
  rebuilt checkpoint is blocked, so caller choice cannot bypass the denominator.
- `all_required` and `choose_at_least_required_slots` continuation combinatorics have focused positive checks; completed
  portfolios have a negative request-generation check and the full 76-route regression remains green.

### 2026-09-22: continuation commit and second-route dispatch

- The selected continuation proposal/admission now enters the existing exact commit-receipt implementation only after
  its request and Teacher preference are rebuilt from the verified checkpoint. Proposal, admission, commit receipt and
  execution binding preserve one prior-checkpoint hash and one completed-alternative set; drift fails closed.
- The existing execution-binding, fresh terminal and settlement implementations are reused with a verified continuation
  proof. A two-route fixture commits shop plus fish, executes and settles the shop route, rebuilds only the remaining
  fish scope, performs a marker-only continuation commit, executes/settles fish, and emits a cumulative checkpoint that
  exactly replays both transitions with `transition_count=2` and all scopes complete.
- `acquisition_route_portfolio_rollout_proof_manifest.v1` replaces the first-continuation-specific proof surface with
  one ordered chain. Its verifier rebuilds the initial checkpoint and every continuation transition, requires each
  stored checkpoint to equal the rebuilt artifact, binds the immediately prior hash, and advances transition count
  exactly once. Existing first-continuation calls are compatibility wrappers over the same verified-checkpoint core.
- The two-transition positive fixture, tampered-checkpoint rejection and standalone proof-receipt CLI passed at this
  milestone. The code had no fixed continuation limit; the later three-transition fixture below supplies the repeated-
  path regression that was still missing here.

### 2026-09-22: terminal portfolio rollout controller admission

- `acquisition_route_portfolio_rollout_admission_receipt.v1` is a separate controller artifact. It rebuilds the ordered
  proof chain from the manifest and requires the supplied proof receipt to be exactly equal to that recomputation;
  trusting `proof_chain_verified` or another caller-provided boolean is insufficient.
- A verified but incomplete chain returns typed blockers and no admission. A terminal chain receives
  `controller_admission_granted=true` and `teacher_training_evidence_eligible=true`; a modified receipt is rejected.
- The authorization scope is only `verified_acquisition_route_portfolio_teacher_evidence`.
  `formal_product_training_authorized` remains false, so this receipt cannot bypass option admission, dataset/checkpoint
  validation, Product Executor/version locks, or the native-save transaction.

### 2026-09-22: three-transition repeated rollout proof

- The positive fixture now selects three independent scopes and routes: Pantry shop purchase, one transparent ready
  Parsnip for Full Shipment, and one Master Angler catch. The same initial/continuation builders execute and settle all
  three routes; there is no transition-specific planner or synthetic terminal checkpoint.
- Each continuation rebuilds the complete target-date chain from the latest snapshot and settled ledger. Fishing
  forecast snapshots are refreshed to the current save/player/time/tick identity, so the third transition passes the
  existing stale-forecast guard rather than bypassing it.
- Ready-crop daily budgeting uses the same `CropHarvestBudgetPolicy.HarvestTicksPerCrop` constant as the native action
  compiler. It binds the exact number of transparent ready-crop tiles implied by authoritative minimum stack, then
  re-proves every local route segment and retains the ordered stand, arrival, action and completion schedule. Single-
  and multi-crop harvests use this same typed route-step path; an unreachable tile or source-window overrun fails closed.
- The terminal proof reports `transition_count=3`, complete scoped progress and scoped Teacher-evidence admission.
  Incomplete chains, modified checkpoints and forged proof receipts remain rejected.
- `acquisition_route_portfolio_supervision_dataset.v1` now rebuilds the supplied proof and admission before emitting
  three hash-linked transition rows. Teacher preference, native outcome and Student observation are separate typed
  channels; the Teacher-driven fixture records zero Student observations and never infers one from execution.
  Unavailable portfolios are deferred without negative labels.
- `acquisition_route_portfolio_supervision_corpus_manifest.v1` re-verifies every source and deterministically splits
  rows by the verified snapshot save-day key. Exact duplicates are removed, conflicting identities fail closed, and
  comparison-pair coverage is an independent trainer gate. The continuation fixture now exposes three complete
  portfolio alternatives: two quality Parsnips, one Green Bean, or both. The deterministic Teacher strictly selects
  the Green Bean portfolio and emits two real pairwise preferences; no learner score or invented negative participates.
  The blocked regression still feeds the same validation source twice and proves exact deduplication plus missing-
  partition rejection. The positive regression independently rebuilds the full three-transition proof for three save
  identities whose verified save-day keys land in train, validation and test. It yields nine accepted rows and six real
  pairwise preferences, with three rows and one split key in each partition, so the corpus now reaches
  `ready_goal_method_trainer_input`. Forecast refresh also inherits the complete save/player/date identity from its base
  snapshot before rehashing; cross-save forecast evidence therefore cannot leak into a rollout. Formal product training
  remains false until the dedicated trainer/scorer path is integrated under the separate 19/19 Teacher-coverage gate.
  The existing `StructuredPolicyTrainer.BuildPairs` must not consume it because that trainer derives
  labels from `candidate.Selected` rather than these explicit Teacher pairwise preferences.

### 2026-09-22: dedicated explicit-pair goal-to-method trainer

- `train-acquisition-route-goal-method` consumes only a ready
  `acquisition_route_portfolio_supervision_corpus_manifest.v1`. It replays every source proof/admission/dataset chain,
  recomputes cleaned and partition digests, and validates each explicit pair against the candidate denominator and
  strict non-scalar cost dominance before optimization. Manifest readiness is necessary but is not trusted as proof.
- `explicit_teacher_pairwise_portfolio_ranker.v1` builds pair differences only from
  `teacher_preference.pairwise_preferences`. `candidate.Selected`, learner scores, `dominated_by`, save IDs and proposal
  identities are excluded from the feature/label path. The old `StructuredPolicyTrainer.BuildPairs` is not reused.
- The checkpoint binds corpus and partition hashes, version pins, hyperparameters and an optional initialization
  checkpoint. Its ID is recomputed from those inputs on every save/load. Blocked corpora, forged source digests,
  candidate-selected label claims and forged checkpoint IDs fail closed.
- The bounded independent-save fixture produces 27 features and reaches 1.0 pair accuracy on each 3-row/2-pair
  train, validation and test partition. This proves plumbing and boundary integrity only; it is not a generalization
  claim. The checkpoint retains `formal_product_training_authorized=false`.
- The next fixed slice is a read-only checkpoint scorer and controlled goal-to-method selection integration. It must
  rank only candidates already admitted by the deterministic authoritative denominator and cannot bypass reservation,
  execution binding, fresh native outcome or the independent 19/19 Teacher-coverage gate.

### 2026-09-22: read-only checkpoint scoring boundary

- `score-acquisition-route-goal-method-row` reloads a checkpoint, re-verifies its bound corpus and every source proof,
  resolves exactly one verified row, and scores only `admission_ready` candidates with complete cost vectors. It does
  not accept a caller-authored candidate list.
- Training and inference now share one typed feature context and encoder. The scoring artifact records the full ranked
  denominator, deterministic Pareto-frontier membership, model top proposal and Teacher-selected proposal. On the
  three-candidate fixture the Teacher proposal ranks first.
- This boundary is intentionally observational: `selection_mode=read_only_teacher_comparison`,
  `portfolio_commit_authorized=false`, and `formal_product_training_authorized=false`. It cannot emit a proposal or
  enter the existing commit/dispatch chain.
- The next fixed slice rebuilds the same complete denominator from a fresh live snapshot. A unique strict-Pareto
  Teacher remains authoritative; an incomparable frontier may receive only a gated shadow model choice until the
  independent 19/19 coverage gate authorizes a later product-selection transition.

### 2026-09-22: fresh-state shadow selection and independent coverage gate

- `score-live-acquisition-route-goal-method-shadow` rebuilds the complete target-date portfolio denominator from a
  fresh initial or continuation state. When the deterministic Teacher finds a unique strict-Pareto choice, that choice
  remains authoritative and the checkpoint is observational. When the frontier is genuinely incomparable, the
  checkpoint may rank only members of that verified Pareto frontier and emits a shadow choice with no portfolio commit
  or formal-training authority. The dedicated incomparable fixture constructs a real elapsed-time versus material-cost
  trade-off and verifies this fail-closed path.
- `goal_method_teacher_coverage_gate.v1` is a separate promotion gate. It rebuilds the authoritative goal-method graph
  from raw inputs instead of reading a previously generated graph report, then re-verifies every declared corpus,
  rollout and opportunity denominator. Corpus declarations cannot name criteria; requirement-set bindings in the
  rebuilt frontier are the only route from evidence to a method and its criteria.
- A criterion is ready only when its sole catalog-backed method is `executable_frontier`, an explicit Teacher pair
  varies that method in train, validation and test, and a verified native outcome for that method exists in all three
  partitions. The current bounded corpus reports 19 catalog-mapped criteria, 2 comparison-covered, 4 native-outcome-
  covered, 2 split-complete across both channels, and 0/19 ready because the collection methods remain
  `pending_dependency_expansion`.
- The gate rejects caller-supplied formal authorization and unknown source adapters. Its output always keeps
  `formal_product_training_authorized=false`; a later controller promotion must separately require a complete 19/19
  report. During implementation the old generated lowering was correctly rejected after its catalog hash drifted;
  regression must rebuild lowering before this gate rather than reuse stale output.
- The next fixed slice is therefore not broader model training. It is dependency expansion for the evidenced
  collection methods, followed by additional typed Teacher-source adapters and split-complete evidence for the other
  Grandpa methods until the independent count reaches 19/19.

### 2026-09-23: live standard/remixed Community Center denominator binding

- The earlier collection frontier assumed the fixed 30-bundle standard layout. That assumption was insufficient for
  formal training because the active save persists its generated `NetWorldState.BundleData`; remixed saves can keep the
  same 30-key topology while changing bundle identity, required slots and ingredients. The post-Community-Center
  `Abandoned Joja Mart/36` Missing Bundle is a separate supplemental row and must never enlarge the Community Center
  completion denominator.
- The authoritative requirement inventory now compiles `Data/Bundles` and `Data/RandomBundles` together with guarded
  decompiled `BundleGenerator.Generate`, `ParseRandomTags`, `ParseItemString`, `Game1.GenerateBundles`,
  `SaveGame.LoadDataToLocations` and `Utility.fuzzyItemSearch` behavior. The locked catalog contains all 30 standard
  active templates, the one supplemental template, five randomized areas, 26 replaced keys, 43 selectable remixed
  templates and the four retained Vault keys. Item-name resolution preserves the native first-match rule and explicit
  `(O)390` Stone override instead of guessing through duplicate `Data/Objects` names.
- `build-current-community-center-denominator` validates every exact live row, aggregate count, key/area/ID identity,
  ingredient order, quantity, quality and required-slot completion. A standard save must equal all locked standard
  templates. A remixed save must be reproducible as one whole-area native bundle-set/pool assignment, honor fixed-index
  preference, avoid pool-template reuse and preserve random ingredient removal as an ordered subsequence. Unknown,
  custom, missing and tampered rows fail closed. Output separates 30 active rows from supplemental rows and hashes only
  the normalized active denominator identity.
- Focused regression admits the real standard snapshot after refreshing its stale pre-fix aggregate, synthesizes and
  admits a valid native remixed realization, and rejects a one-stack ingredient tamper. `Run-Regression.ps1` now locks
  catalog counts and runs this matrix. The checked-in bridge source already computes `complete_bundle_count` from exact
  projected row completion; the archived snapshot's 21-versus-25 mismatch predates that fix and is not accepted as live
  evidence without fixture normalization.
- This closes active standard/remixed denominator identification, not the whole `complete_community_center` method.
  Formal product training remains false. Remaining blockers are the Junimo-text/unlock event and receipt chain,
  remixed dynamic requirement-route lowering through every target-date axis, and room reward/mail/final ceremony
  settlement. The next slice must consume this bound denominator when constructing the dynamic collection frontier;
  it must not retain the old standard-only alternatives for a remixed save.

### 2026-09-23: Community Center ingredient acquisition catalog admitted

- The bound denominator could not safely replace the old standard-only collection frontier until every ingredient
  identity selectable by `Data/Bundles` or `Data/RandomBundles` had an authoritative acquisition interpretation.
  Enumerating only the four historical static requirement sets would omit valid remixed ingredients, including the
  live synthesized realization's `(O)223` and `(O)233` rows.
- The authoritative requirement inventory now emits 175 unique bundle-ingredient identities and 185 concrete
  acquisition targets across standard templates, the supplemental Missing Bundle, remixed set templates and remixed
  pool templates. Exact items retain their authoritative acquisition routes. Native category ingredients are expanded
  without guessing: egg category `-5` binds eight accepted `Data/Objects` targets and milk category `-6` binds four.
  Money payments retain a typed native-money target. An identity with no accepted native target or no route-covered
  target fails the inventory build closed.
- The current standard/remixed denominator normalizes every live ingredient against that catalog and carries
  `qualified_item_id`, `match_kind`, every concrete acquisition target and its route evidence. Those fields now
  participate in the denominator hash, so route-catalog drift cannot silently reuse an older active denominator.
  Focused tests lock the 175/185 counts, category expansion, formerly omitted remixed identities, complete route
  evidence for every active standard/remixed row, and tamper rejection. The complete regression suite passes.
- This closes the authoritative acquisition-source prerequisite. At this point the next fixed slice was to derive a
  current dynamic Community Center requirement/lowering set from the active denominator and feed it into the
  collection frontier; the following section records that implementation.

### 2026-09-23: current Community Center collection frontier binding

- Every public current collection frontier, Stage 1 collection frontier, preference and receipt build now rebuilds the
  strict current Community Center denominator from the same authoritative inventory and transparent snapshot. The
  stable set ID remains `community_center_standard` for downstream compatibility, but its groups, required slots and
  alternatives come only from the active save's standard or remixed denominator; the historical static alternatives
  are no longer consumed for current Community Center membership.
- The current denominator is lowered through the one existing acquisition route-kind authority rather than through a
  second executor or route catalog. Exact items and money remain typed alternatives. A category ingredient remains one
  native bundle slot while exposing every accepted concrete item target; direct donation and acquisition-endpoint
  candidates bind the concrete qualified item ID while retaining the original slot and alternative identity.
- The frontier reports the active bundle mode and denominator SHA-256. Preference, receipt and trajectory Teacher
  supervision propagate that SHA-256, and dataset validation rejects a row without it. The builder also recomputes the
  denominator hash and rejects inventory/snapshot drift or a tampered denominator instead of falling back to the
  standard catalog.
- The focused regression deliberately supplies a static two-group fixture and a different three-group remixed current
  denominator containing category `-5`. It proves the current three-group denominator wins, the category remains one
  slot with concrete egg target `(O)176`, a non-member target is excluded, and a forged denominator hash fails closed.
- This completes current collection-frontier membership, not the whole `complete_community_center` method. Formal
  product training remains false. The next fixed slice is to carry the same dynamic denominator identity and
  requirements through every target-date axis and its receipts. Junimo-text/unlock events, room rewards/mail and final
  ceremony settlement remain later explicit blockers.

### 2026-09-23: current Community Center acquisition-route calendar root

- `build-current-acquisition-route-calendar-resolution` now rebuilds the strict save-bound Community Center
  denominator and replaces the historical static `community_center_standard` route occurrences at the acquisition
  calendar root. Every non-Community-Center route remains unchanged. The artifact hash-binds the active bundle mode,
  denominator, source state and transparent snapshot instead of allowing a standard-layout route set to survive on a
  remixed save.
- Collection membership and calendar lowering share one `CurrentCommunityCenterRequirementAuthorityBuilder`; this is
  not a second route catalog or executor. Exact-item alternatives retain their native identities. A category ingredient
  remains one native selectable slot, while each accepted concrete item contributes its own stable acquisition-route
  occurrence under that slot. Duplicate concrete target/route identities are deterministically collapsed before
  occurrence numbering.
- The focused fixture removes a static Parsnip shop route, keeps two current exact-item harvest routes and adds one
  remixed egg-category shop route. The rebuilt report contains only those three Community Center occurrences while
  preserving the complete 77-route fixture denominator. Provenance and replacement assertions fail closed.
- This closes only the save-bound root calendar input. Existing target-date calendar and the remaining dependency axes
  still rebuild the static root, so formal product training remains false. The next fixed slice must make the explicit
  target-date calendar consume and validate this current root, then propagate the same denominator identity through
  each downstream axis and receipt before the Junimo/unlock, room reward/mail and final ceremony chain is admitted.

### 2026-09-23: current Community Center explicit target-date calendar

- `build-current-acquisition-route-target-date-calendar` consumes the persisted current acquisition-route calendar,
  rebuilds that root from the same authoritative inventory, lowering, window index and transparent snapshot, and
  object-compares the complete report before evaluating an explicit target day. Passing a static root to the current
  command, a current root to the static command, or changing denominator provenance fails closed.
- The target-date report carries the active bundle mode, denominator hash, source-state hash and snapshot hash in
  addition to the route-root file hash. Calendar evaluation remains the existing shared implementation; no second
  condition parser, route catalog or executor was introduced. Non-Community-Center route rows are byte-equivalent to
  the static target-date result for the same inputs.
- The remixed fixture preserves the two active exact-item harvest routes, carries the concrete egg target for one
  category slot, excludes the removed static shop route and rejects a forged denominator hash. One deliberately
  unmatched fixture shop row remains an explicit source block rather than being promoted to a target-date match.
- Production regression now executes both current CLI stages against the standard fixture derived from the locked
  transparent snapshot, with its redundant completion aggregate recomputed from the exact bundle rows, and verifies
  the complete provenance chain. The archived raw snapshot reports 21 complete bundles while its exact rows prove 25;
  strict production code rejects that stale aggregate, and the current bridge source is separately locked to derive
  the aggregate from those rows. Formal product training remains false.

### 2026-09-23: current Community Center target-date dependency chain

- `build-acquisition-route-target-date-unlock-state` now determines its deterministic rebuild mode from the verified
  target-date artifact. A current artifact must rebuild its Community Center denominator and calendar root from the
  same transparent snapshot; a static artifact still follows the static path. An artifact/snapshot mode mismatch,
  denominator change or route-root drift fails the existing full-object comparison.
- This is one dispatch boundary, not a parallel current implementation of every dependency axis. Festival, location,
  facility capacity, resource inputs, currency, reservation, processing lead time, fishing probability, stochastic
  retry, daily time/energy and opportunity cost already rebuild their immediate predecessor. Their exact upstream
  SHA-256 fields therefore bind the current target-date identity transitively, while stages that join the route root
  continue to require the same complete occurrence set and requirement identity.
- Production regression now constructs the complete 1,599-occurrence current root from the real day-37 snapshot and
  runs the unchanged unlock and festival commands through recursive rebuild. The focused suite continues to cover the
  complete dependency chain through opportunity cost and separately proves that remixed category targets are not
  replaced by static alternatives. Formal training remains false.
- The next fixed boundary was current-denominator propagation through portfolio selection, commit/execution binding,
  fresh terminal receipts, settlement, continuation and supervision artifacts. The following section records that
  implementation. Junimo/unlock events, room reward/mail and final-ceremony settlement remain separate Community
  Center completion blockers.

### 2026-09-24: current Community Center portfolio and receipt provenance

- `AcquisitionRoutePortfolioBuilder.Prepare` now derives one typed `community_center_provenance` value from the
  deterministically verified target-date calendar and exact portfolio decision snapshot. A current provenance requires
  standard/remixed mode, valid denominator/snapshot hashes and an exact source-state/snapshot match; a static path must
  carry no current-save fields.
- Portfolio admission, independent Teacher preference, atomic commit receipt, execution binding, fresh terminal
  receipt, settlement receipt, initial/continuation checkpoint, continuation request, rollout proof/admission and each
  supervision row now expose that value. Builders copy it through one shared support type and reject disagreement where
  independently rebuilt artifacts meet, instead of relying only on an opaque predecessor file hash.
- A continuation must retain the same static/current identity, bundle mode and active denominator SHA-256 as its prior
  checkpoint. Its source-state and snapshot hashes are intentionally regenerated from the latest terminal state before
  the next decision. This preserves fresh-state replanning without permitting a standard/remixed denominator switch
  inside one proof chain.
- Focused regression covers current remixed provenance construction and stale-state rejection. The complete three-step
  portfolio rollout verifies that every exported supervision row retains the expected static provenance identity. This
  closes current-denominator propagation through the training-evidence boundary; it does not authorize formal product
  training.
- The next fixed boundary is the native Community Center completion lifecycle: Junimo text/unlock events, room reward
  and mail effects, and final ceremony settlement must receive fresh native before/after proof and terminal admission.

### 2026-09-24: native Community Center completion lifecycle admission

- The transparent bridge now emits one typed `community_center_lifecycle.v1` projection instead of treating
  `ccIsComplete`, six room-mail flags and `isLocationAccessible("CommunityCenter")` as interchangeable completion
  signals. It separately exposes the Town `611439` door-unlock event, first-note and Wizard-letter state, WizardHouse
  `112` Junimo-text event, all-area state, unclaimed/missed bundle rewards, room-mail settlement, the final Town
  `191393` ceremony and the final admitted state. All three event rows are loaded from the current runtime assets and
  their base-English scripts are SHA-256 locked to the independently decompiled 1.6.15 rows; a missing or modified row
  blocks the lifecycle projection rather than falling back to documentation examples.
- The existing native donation executor remains the sole donation implementation. It already waits for bundle bits,
  inventory consumption, bundle reward, complete-bundle count, restored room, pending room/Bulletin mail and newly
  visible notes. For the last room it now also waits for `Junimo.returnToJunimoHutToFetchStar` to place
  `ccIsComplete`; it never writes that flag itself. This closes the timing gap where an all-areas snapshot could have
  been accepted before the native final-star sequence settled.
- The prerequisite first Crafts Room note is now an explicit lifecycle candidate rather than an assumed manual step.
  The save-bound candidate uses the live area-1 note/interaction endpoint, shared collision routing and the existing
  `executor.interact`; the runtime follows the original `CommunityCenter.checkAction -> checkBundle ->
  JunimoNoteMenu.setUpMenu` path and verifies the `seenJunimoNote` false-to-true transition plus the newly scheduled
  `wizardJunimoNote` letter. It does not write mail, quests, bundle state or events directly. A remote candidate emits
  one existing route connector and requires a fresh Community Center snapshot before the interaction is compiled.
- Stage 1 donation receipts no longer accept only one bundle ingredient changing from false to true. The verifier binds
  the exact compiled queue item and requires its qualified inventory delta plus every projected bundle, reward, room,
  mail, all-area and new-note postcondition. Vault money payments retain their separate exact-money verifier. Any one
  omitted side effect blocks the training row.
- `build-community-center-lifecycle-receipt` admits five explicit fresh transitions against a verified sequential queue
  receipt: initial door unlock (`611439`), first-note interaction, Junimo text unlock (`112`), room-mail day settlement
  through the existing recovery/sleep chain, and final ceremony (`191393`). Final completion is admitted only when native room-mail
  completion was already true and the ceremony changes both event-seen and location-accessible state. Cross-day mail
  is therefore attributed to `recovery.stabilize_day`, while final accessibility is attributed to
  `story.advance_event`; neither is incorrectly credited to the last donation.
- Static focused tests lock the event identities and hashes, reject direct progress mutation, verify the last-star wait,
  accept a complete donation projection, and reject receipts with a missing room-mail or Wizard-letter side effect.
- The isolated runtime gate passed in
  `artifacts/runtime-community-center-lifecycle/runtime-community-center-lifecycle-20260924-174253/summary.json`.
  It copied but did not modify the source save, executed the native `611439` unlock, first Junimo note, `112` Wizard
  event, final bundle donation, native sleep/day settlement and `191393` ceremony, and admitted all five lifecycle
  transitions as fresh training labels. The final projection was `completion_admitted`.
- Runtime evidence exposed and closed three cross-boundary gaps. Ordinary story-event requests now carry a bounded
  `story_event_max_runtime_ticks=14400`, which covers the exact current `112` script rather than timing out during its
  long native pauses. Sleep observes SMAPI's native `GameLoop.Saved` receipt and can finish at a new-day story-event
  handoff without consuming that event's dialogue; the naturally triggered `558291` Grandpa evaluation was then
  handled by the existing story executor as a non-lifecycle interstitial. Sequential receipt admission remains strict,
  but now recognizes the exact three-step `executor.sleep -> sleep` macro instead of requiring every queue item to
  contain exactly one compiled step.
- The final ceremony is not modeled as one decision-free action. One automatic story action advances to the native
  command-99 question boundary; a fresh `advance_story_event_choice` candidate binds a typed response and completes
  the remaining event. Neither phase calls `skipEvent` or writes event/mail/completion flags. This closes the runtime
  lifecycle gate; formal product training still waits for the later single strategic-policy facade and its remaining
  admission gates.

### 2026-09-23: issue #129 StrategicPolicy convergence disposition

- Verdict: accept the single strategic entry, shared artifacts, deterministic hard authority, learned soft-preference
  path and event-driven strategic replanning. The existing `RankLiveShadow` implementation already proves the first
  bounded form: a sole Pareto-frontier member remains authoritative, while a checkpoint can rank only the verified
  multi-member frontier and cannot commit it. This is useful evidence, not yet a product interface or runtime
  admission.
- Terminology is tightened before implementation. Runtime does not host an online Teacher beside a Student.
  `TeacherOracle` remains offline supervision, relabeling, counterfactual, coverage and benchmark machinery. The
  runtime unique-frontier branch is `DeterministicSelection`. A "unique strict-Pareto" decision means exactly one
  admitted frontier member that passes the existing proof that it dominates every other admitted candidate under
  `OpportunityCostDominates`; neither weighted scalarization nor a model score can manufacture that status.
- `StrategicPolicy.SelectMethod` must be a facade over the existing authoritative denominator, feasibility,
  reservation/ledger and Pareto artifacts. It must not own another candidate generator, route legality graph,
  verifier, compiler or executor. Both branches return one versioned `StrategicDecision` with state hash, ledger
  revision, denominator/frontier identities, selected method, `selection_authority`, model/checkpoint identity when
  applicable, blocker/fallback reason and separate formal-training/runtime-authority flags.
- The learned branch may rank only admitted, non-dominated members of a genuinely incomparable/equal frontier.
  Missing, corrupt, stale, version-mismatched or non-admitted checkpoints cannot change a deterministic decision. If
  an incomparable frontier has no runtime-authorized model, selection fails closed. A rare online Teacher fallback is
  explicitly deferred and cannot enter through this refactor. Unlike the current `RankLiveShadow`, which loads and
  verifies the checkpoint before inspecting the rebuilt frontier, the product policy must resolve the deterministic
  branch first. It records `model_invoked=false`; an optional shadow-audit failure is non-authoritative and cannot
  block that deterministic result.
- Strategic replans are triggered by day start, goal/profile/preference change, material or currency availability
  drift, reservation/ledger revision drift, selected-method completion, execution failure and player interruption.
  They are not per-frame Teacher runs. Existing fresh-snapshot checks after connector traversal, action effects and
  queue continuation remain mandatory mechanical validity checks; they do not by themselves create a new strategic
  model call or training row.
- Implementation order is fixed. First finish the immediately preceding dynamic standard/remixed Community Center
  frontier and its unlock/reward/ceremony receipt chain. Then extract the current live-shadow branching into the
  single policy contract, keep existing scorer/Teacher commands as thin compatibility adapters, add unique-frontier,
  incomparable-frontier, dominated/blocked, missing/corrupt-model-before-deterministic-selection and trigger-dedup
  regressions, and record candidate count, frontier count, model invocation, decision latency and fallback/blocker
  reason. Only after the independent 19/19 coverage gate and a separate runtime promotion gate may the learned branch
  authorize portfolio commit. Formal product training and runtime model authority remain false now.

### 2026-09-25: single StrategicPolicy facade implemented

- The first bounded product-shaped facade now exists as `StrategicPolicy.SelectMethod`. It consumes the existing
  authoritative portfolio scoring set and returns one `strategic_decision.v1`; it does not own candidate generation,
  route legality, admission, reservation, compilation or execution. The decision records the state and ledger
  identity, denominator and frontier hashes/counts, selected method, authority, model identity, replan fingerprint,
  latency, blockers/fallbacks and separate runtime/model/training authority flags.
- The deterministic branch is resolved before any checkpoint access. A unique strict-Pareto member remains usable
  when the optional checkpoint or corpus is absent/corrupt; `model_invoked=false`. Compatibility shadow auditing may
  run only after that result and its failure is recorded as a non-authoritative fallback. An incomparable/equal
  frontier requires a verified checkpoint and scores only admitted, non-dominated frontier members. Missing or
  invalid model evidence fails closed, and the learned result remains read-only with portfolio commit, runtime model
  authority and formal product training all false.
- Event-driven replan metadata recognizes day start, goal/profile/preference changes, material/currency drift,
  reservation-ledger drift, selected-method completion, execution failure and player interruption. Identical event
  fingerprints deduplicate instead of creating another strategic decision. Fresh mechanical validation still does
  not imply a strategic model call.
- `score-live-acquisition-route-goal-method-shadow` is now a compatibility adapter over the facade instead of a
  second branching implementation. The new `select-strategic-method` command exposes the unified decision directly.
  Full Stage-1 self-test and a separate post-refactor incomparable-frontier adapter test pass. This closes the
  interface-convergence slice only; runtime learned authority still waits for independent 19/19 coverage and the
  separate promotion gate.

### 2026-09-25: Community Center enters the independent coverage gate

- The `complete_community_center` dependency graph is now `complete`. Its three former blockers were closed by the
  save-bound standard/remixed denominator, the shared target-date acquisition chain through opportunity cost and
  rollout receipts, and the isolated native unlock/note/reward/mail/final-ceremony lifecycle proof. The dependency
  graph continues to reuse the existing event, donation and sleep options; no second executor was introduced.
- Rebuilding the authoritative frontier yields 4/19 executable criteria and 15/19 dependency-pending criteria. The
  existing three-partition corpus comparison-covers 2 and native-outcome-covers 4. The exact two Community Center
  criteria now pass both channels in train, validation and test, so independent coverage advances from 0/19 to 2/19.
- The coverage self-test locks both the aggregate count and the exact admitted criterion IDs. Forged formal-training
  authorization and unknown source kinds remain rejected. The coverage report is still incomplete and retains
  `formal_product_training_authorized=false`; neither learned runtime authority nor portfolio commit is promoted.
- Coverage no longer treats `requirement_set_readiness` as an implicit source-adapter flag. Each verified adapter now
  records its explicit `teacher_source_kinds` on the methods it can derive. The acquisition portfolio adapter still
  derives ownership only from authoritative requirement sets, while executable non-collection methods such as Skull
  Key and pet love remain blocked by `goal_method_teacher_source_adapter_missing` until their own typed evidence
  adapters exist.
- The next dependency work remains source expansion and independent split-complete Teacher/native evidence for the
  other 17 criteria. A runtime promotion gate may consume only a future independently rebuilt 19/19 report, never the
  current partial count or a caller-authored authorization flag.

### 2026-09-25: typed pet-love Teacher source adapter

- `pet_love_terminal_interaction_corpus.v1` is the first non-collection source adapter. The source request contains no
  criterion IDs and cannot select a method. The adapter is hard-bound to `earn_pet_love` and re-verifies fresh
  before/after snapshots plus one `training_execution_result.v1` from the existing pet executor.
- Admission is intentionally narrow: the native pet must be ready and unpetted for that player/day, friendship must
  move from 988-999 to exactly 1000, `timesPet`, `lastPetDay` and the per-player grant flag must settle exactly, and
  `petLoveMessage` must appear only after the action. The Teacher comparison is execute-now versus same-state defer;
  it proves immediate terminal progress without importing guide assumptions, water-bowl forecasts or future RNG.
- Split identity is derived from save ID, player ID and total day. Duplicate save-days, caller partition labels,
  modified corpus rows, mismatched state hashes, non-native pet types, stale projections and non-terminal interactions
  fail closed. The CLI and direct builder produce byte-identical corpus artifacts.
- A three-partition deterministic fixture proves the adapter and moves only its explicit fixture report from 2/19 to
  3/19. It is not counted as production native evidence. The authoritative report remains 2/19 until three independent
  isolated native save-day receipts are captured. Formal product training and runtime model authority remain false.

### 2026-09-25: criterion, method and action-capability reconciliation

- `build-goal-method-coverage-reconciliation` rebuilds both the authoritative frontier and the independent Teacher
  coverage report, then joins them to the current option-governance matrix. It does not accept a generated frontier,
  caller-authored criterion ownership or a claimed action count.
- The three denominators are now explicit and non-interchangeable: 19 Grandpa score criteria, 11 strategic root
  methods, and 36 distinct existing high-level options referenced by those methods. The current `2/19` value is only
  the split-complete Teacher/native criterion coverage count. It is not an action-registry, compiler or executor count.
- Current primary criterion dispositions are exact: 2 coverage-ready Community Center criteria, 15 criteria whose
  root methods still have typed dependency-graph blockers, 1 pet-love criterion whose adapter exists but production
  three-split native evidence is not connected, and 1 Skull Key criterion that still needs its typed Teacher source
  adapter. This prevents dependency proof work from being misreported as missing mechanical actions.
- Referenced-option status is an inventory-only diagnostic. Optional alternatives, compiler-owned deterministic
  dependencies and the later Product Executor promotion gate do not become current method blockers merely because
  every referenced option is not individually promoted. Current `open_work_kinds` and `next_actions` are derived only
  from the rebuilt method frontier and verified Teacher/native coverage. This prevents option-matrix breadth from
  manufacturing duplicate action work or moving the separate runtime-promotion gate ahead of 19/19 coverage.
- The reconciliation artifact remains read-only and records `formal_product_training_authorized=false`. Coverage-ready
  does not imply product integration, and product integration does not supply Teacher comparisons or native split
  evidence.
- The immediate follow-up is a single batched evidence slice for Full Shipment and Master Angler. Their existing
  acquisition corpus already supplies native outcomes in train, validation and test, but its portfolio-composition
  pairs do not vary either method. Adding routes to one `all_required` requirement is not a valid repair: same-
  requirement dominated routes are removed before portfolio enumeration, while surviving equal or incomparable routes
  cannot produce the unique strict-Pareto Teacher label. The verified adapter must instead rebuild the selected
  portfolio's next-route denominator at every transition and admit an execute-now versus defer comparison only when one
  route strictly dominates every other pending selected route and is exactly the route bound to the native receipt.
  This is evidence wiring through the existing compiler/executor and rollout hash chain, not new shipping or fishing
  action development; long-horizon terminal proofs remain a separate frontier requirement.

### 2026-09-25: Full Shipment terminal settlement verifier

- `build-full-shipment-terminal-settlement-receipt` now provides the missing fail-closed admission boundary for the
  final native shipping day. It verifies the mutually hash-bound authoritative requirement inventory and acquisition
  lowering, then hash-binds a single compiler-owned native sleep queue, its execution receipt and fresh before/after
  snapshots. The selected candidate ID is supplied
  independently by the caller and must match the runtime receipt; the receipt cannot authenticate its own identity.
- The transition is admitted only when the exact 154-item denominator starts with one missing item and ends at
  154/154, the terminal item's native `basicShipped` count moves from zero to exactly one, every shipping-bin view
  settles from one pending unit to zero, `total_days` advances exactly once, and achievement 34 appears without any
  existing shipment or achievement regression. Missing achievement, an uncleared bin, a same-day transition and a
  drifted denominator are explicit regression failures.
- `Invoke-RuntimeFullShipmentTerminalSmoke.ps1` now prepares only an isolated save copy at exact 153/154 state by
  replaying the same native eligibility predicate and requiring the locked denominator to remain 154. The fixture
  clears achievement 34 and the shared shipping inventory, installs one `(O)24` copy, and positions the actor at an
  existing shipping endpoint. It is a debug-only state fixture and never substitutes for a Product action or label.
- Hidden, silent runtime `runtime-full-shipment-terminal-20260925-171152` selected the exact generated
  `ship:Farm:71,14:9:24:deposit` candidate, compiled and executed the existing `economy.ship_items` path, then used a
  fixture-only relocation to the native home sleep path and compiled the existing `recovery.stabilize_day` path. The
  admitted receipt proves 153/154 -> 154/154, terminal `basicShipped` 0 -> 1, shared bin 1 -> 0, total day 223 -> 224,
  and achievement 34 false -> true with no blocking reason. The source save remained unchanged.
- This closes the isolated native terminal transition, not the Full Shipment method. The dependency graph remains
  `in_progress`: the admitted achievement-34 terminal receipt must be bound to the complete 154-requirement
  acquisition/reservation recurrence, and that recurrence still needs a fresh-save Year 3 deadline proof.
  Coverage-ready therefore remains 2/19 and formal Product training remains unauthorized.
- `build-full-shipment-settlement-receipt` now closes the missing ordinary native-day boundary used by recurrence
  iterations 1 through 153. Its shared verifier requires exactly one authoritative missing item to become shipped,
  native `basicShipped` 0 -> 1, the shared bin view 1 -> 0, one day advance, no regression in any other item or
  achievement, and achievement 34 to remain absent. A complete transition is rejected here and must use the dedicated
  terminal receipt instead. Focused ordinary/two-item/premature-achievement regressions and all terminal regressions
  pass; no game was started for this verifier slice.
- `build-full-shipment-recurrence-proof-receipt` now implements that ordered proof boundary. It requires exactly one
  unique iteration for every authoritative Full Shipment requirement, rebuilds each stored acquisition rollout and
  Stage-1 shipping Teacher receipt, rebuilds ordinary settlements for rows 1 through 153, and reserves the dedicated
  achievement-34 terminal receipt for row 154. Snapshot identity and exact prior-settlement roots prevent cross-save
  splicing; acquisition-to-deposit and deposit-to-sleep phase boundaries must preserve the exact `state_hash`, so no
  unreceipted movement or fixture mutation can hide between queues. Shipped counts must advance contiguously,
  settlement days may never move backward, and the terminal sleep must end no later than the exclusive total-day-224
  boundary.
- This is a verifier contract, not the missing runtime evidence. No complete 154-iteration fresh-save manifest has
  been generated or admitted yet, so `complete_full_shipment` remains `in_progress`, coverage-ready remains 2/19,
  and formal Product training remains unauthorized. The next bounded slice is to drive the existing runtime compiler
  and executor from a fresh save, persist all 154 acquisition/deposit/settlement artifacts into this manifest, and
  admit the resulting receipt without fixture mutation. Calendar, resource, route-time, probability, retry and
  reservation gates remain mandatory upstream inputs.

## Review questions

Public review should focus on the following points before Slice 5/6 promotion:

- Does the deterministic teacher remain independent of learner scores at every label boundary?
- Is every goal-to-method edge source-backed, and are unresolved alternatives represented as typed
  blockers rather than omissions?
- Is resetting the no-progress counter on an exact positive teacher receipt, while retaining a
  negative population net delta, the correct failure semantics?
- Are story decisions sufficiently isolated from automatic event progress?
- Are the server resource and evidence-transfer gates strict enough for a co-located workload?
- Is classifying Backwoods `asdlfkjg` as pass-through `covered_for_read`, while exposing its exact
  time/weather/player-count/random side effect, sufficiently conservative for route training?
- Should content-addressed full-state blobs be mandatory before the first repeated-day server run,
  or is an independently hash-audited delta representation preferable?
- Does every formal row carry an independent Teacher preference or separately attributable native
  outcome instead of deriving the positive label from the Student's selected flag?
