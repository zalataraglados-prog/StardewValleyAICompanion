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
