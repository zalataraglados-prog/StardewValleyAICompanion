# StardewAI Full-Chain Task Planning Roadmap

## Target Chain

The implementation chain is:

1. TransparentBridge reads verified game state.
2. Backend projects snapshot into canonical/world-model facts.
3. Small model emits `small_model_plan.v1` with structured task steps.
4. ActionQueueCompiler validates and compiles plan steps into executor queue items.
5. Runtime executor performs player-like behavior through safe, auditable primitives.
6. TransparentBridge reads after-state.
7. LiveTrainingLoop writes `plan_execution_episode.v1` and feature rows.
8. Training separates planner/model feedback from executor calibration.

## Current Proven Slice

- **Grandpa 21-point objective and farmhouse axis** - The strategic completion target is all 21 decompiled rule points; 12 points/four candles are a milestone only. `world_progress.marriage_house` keeps partnership, current farmhouse level, construction state, Carpenter availability, exact native cost tuple, direct score delta, and verified upgrade capabilities together. Levels 1-2 can satisfy the direct partnership/house score factor. Level 3 has zero direct Grandpa points, but the decompile verifies a new `Cellar`, cellar warps, Cask recipe, and an additional indoor object/machine location. The bridge projects Cellar map dimensions, static placeable unoccupied tiles, existing objects, and machine counts by qualified ID. The general machine chain now enumerates Farm, assigned FarmHouse, and assigned Cellar without row truncation, binds every row/candidate/queue/runtime request to `location_id`, uses same-map collision state only, and rolls remote work one transparent connector at a time before fresh replanning. Demand/throughput and route-cost utility remain before numeric level-3 ranking. Runtime purchase still uses only native Carpenter dialogue and performs no direct progress writes.
- **Joja development route** - `world_progress.joja_development` now projects the live `JoinJoja` endpoint, host route commitment, actor membership/greeting/event state, current money, pending order state, and all five native development rows with exact button, paired mail IDs, prices, and both actor/any-farmer completion evidence. The small model chooses only `purchase_joja_membership` or one `purchase_joja_project`; the compiler owns stand selection, button, price, expected balance, and native callback contract. The action queue rebinds every irreversible field against the latest snapshot. Runtime walks to the counter and uses only `JojaMart.checkAction`, native dialogue callbacks, `JojaMart.answerDialogue`, and `JojaCDMenu.receiveLeftClick`; it never writes money, mail, event, quest, or world progress directly. Decompiled `Utility.getGrandpaScore` does not award a separate Joja point, so this remains a full-game progression option and is deliberately absent from Grandpa score directions. Full solution regression passes, while isolated in-game membership and five-project smokes remain pending.
- `move_to_tile` plan step compiles to `executor.move_to_tile`.
- Runtime movement is collision-safe and returns `applied` or `blocked`.
- Applied and blocked movement produce `plan_execution_episode.v1` artifacts.
- Executor calibration rows are excluded from policy/strategy training.
- **Persistent material commitments** - The save/player strategy ledger stores exact actor-authorized node/slot/item reservations with goal ownership, optimistic revisions, cancellation, and immutable history. Machine-crafting candidates exclude any exact personal or Workbench consumption plan that would spend reserved stock; the daily plan and action compiler bind and recompute ledger ID, revision, guard status, and relevant reservation IDs. Immediately before runtime input, the training controller queries backend dispatch readiness against the latest ledger; stale queues send no game input and are recompiled with bounded retries.
- **Generic machine placement** - Inventory machines expose lossless native legal ranges for every loaded persistent location. The loaded-map head chooses an exact reachable tile and adjacent stand; compiler and dispatch rebind inventory identity, legal range, projection fingerprint, operational context, and material-ledger freshness. The runtime uses only `Utility.playerCanPlaceItemHere` and `Utility.tryToPlaceItem`. Hidden isolated smokes verify current-map placement and the full `Farm -> FarmHouse` remote chain: exact candidate selection, native building-door traversal, fresh snapshot, exact target-map tile, matching machine object, and one-item consumption. EVD-150 composes this placement head into ordinary strategic relocation across one resolved connector, EVD-151 supplies remote static BFS evidence for cluster-internal targets, and EVD-152 closes arbitrary resolved connector chains with typed per-segment evidence and fresh-snapshot suffix validation.
- **Empty-fleet machine lifecycle and strategic relocation** - Repeatable hidden isolated Furnace runs now prove both personal and Workbench construction. The Workbench path binds the exact access point, ordered adjacent chest nodes and per-source ingredient plan, opens the native menu, acquires/releases native mutexes through the menu exit callback, then reuses movement, placement, loading, natural processing and collection. The passing run consumed Workbench-chest Copper Ore/Stone, placed the Furnace, consumed player Iron Ore/Coal, naturally produced and collected one Iron Bar, and returned the same machine to idle. The transparent placement projection probes every distinct recoverable placed-machine type against native-legal current and relevant target-map ranges without scanning unrelated empty maps. A root-level row-compressed static walkability projection covers the same relocation scope without duplicating route data for each machine type. Core retains one positive-benefit relocation candidate per target location over the explicit eight-cycle policy horizon, reconstructs deterministic BFS for every resolved route segment and the final target stand, and prices the complete route. Backend persists and independently validates source/target/stand/fingerprint, every typed connector segment, exact target-route distance, relocation cost and policy strings as one save/player-scoped intent before dispatch. After removal and recovery, generic source-map placement is suppressed; each fresh snapshot must match the exact committed route suffix before the next connector may execute, and the loaded target snapshot must rebind legality, stand, inventory identity and native placement. Hidden isolated proofs close same-map Keg relocation, one-connector relocation into FarmHouse, and the two-connector `Farm -> FarmHouse -> Cellar` chain ending at `Cellar 6,7`. Special/conditional/random or multiplayer matrices remain pending.
- **Vanilla Incubator lifecycle** - The exact `Object.OutputIncubator` model resolves animal type, native duration and unscaled native purchase value, while reserving one future AnimalHouse slot for each active incubator. Core admits loading only when the current snapshot has an unreserved slot and carries the exact prediction through daily planning and queue validation. At maturity, the bridge exposes the native first-ready incubator order and `NamingMenu`; runtime clicks the native done button and verifies exact animal creation, occupancy growth, egg removal and menu closure. EVD-156 closes direct native naming; EVD-157 enumerates all nine live native egg IDs and proves copied-save natural multi-day ordinary and Ostrich incubator lifecycles. Modded callbacks remain fail closed.
- **Deterministic and distribution-complete special-machine callbacks** - Cask, Deconstructor, Incubator and Seed Maker have narrow callback-specific models rather than a broad custom-machine bypass. Seed Maker is exact for the current snapshot because the decompiled callback creates a fresh day/save/tile/time RNG instead of consuming `Game1.random`; hidden runtime proves predicted output identity equals the item held after native loading. Solar Panel exposes exact current/next-day weather by initialized location context, live charge state, native `Inside`/`Rain` clock blockers, and the `DayUpdate` battery/initial-duration rule without guessing a multi-day completion date. Statue of Endless Fortune exposes its exact birthday NPC/first-loved-item branch and the complete ordinary four-way distribution; ordinary-day actual identity remains blocked because it consumes shared `Game1.random`, and uncollected contents are marked as cleared and regenerated overnight. Mushroom Log exposes its complete current-environment item, stack and quality marginals from the exact 7x7 nearby-tree snapshot, including immature-tree count effects, mature moss, tree-type branches, three-day timer and rain acceleration; actual identity and the joint tuple remain blocked on unread shared RNG, and an existing held output is never attributed to the current tree layout. Machine execution semantics inventory custom callbacks across every trigger family, not only item placement, and fail closed on unvetted `DayUpdate`, `OutputCollected`, or `MachinePutDown` methods. Anvil and Geode Crusher remain blocked on shared RNG.
- **Generic storage acquisition and placement** - Learned native storage recipes now have a separate `storage_crafting.v1` projection rather than masquerading as machine recipes. Core requires ordinary storage acquisition only when no ordinary storage access exists or every ordinary stack slot is occupied, suppresses duplicate crafting when an inventory storage item is awaiting placement, and rebinds recipe/material/capacity demand before dispatch. Personal and Workbench paths share the native CraftingPage executor; an isolated ordinary Chest smoke verified exact ingredient/output deltas and recipe-count callbacks. Inventory player-storage objects continue through `storage_placement.v1`, whose current-map head preserves the walkable component, map endpoints, and every existing storage access stand. Remote locations execute one connector and defer exact tile selection until a fresh target-map snapshot. Forecast low-capacity expansion, special-branch smokes, full cross-location runtime, explicit sharing, and relocation remain pending.
- `interact_endpoint -> dialogue shop response -> buy_shop_item` is now proven for one safe Blacksmith purchase in the isolated runtime.
- `LiveTrainingLoop` can compile a daily plan from ranked candidates and execute all queue items sequentially through the real runtime executor.
- **`social.talk_npc`/`social.gift_npc` -> rolling route or `executor.social_interact`** — same-map social candidates compile to `move_to_social_stand` + `social_interact`. For a legal NPC whose current loaded instance is on another map, availability selects only the first exact transparent connector on a resolved route, preserves the NPC and exact gift continuation, and requires a fresh snapshot after the transition. Future schedule projection is not used. The native `executor.social_interact` validates all final same-map preconditions and calls `Game1.currentLocation.checkAction` as the only state-changing social call; blocked runtime results use `executor_calibration` scope.
- `recovery.stabilize_day` candidate-to-daily-plan chain is complete for all currently emitted candidates. When outside home, the candidate carries the exact current transparent connector into a single `executor.traverse_connector` plan step; the compiler revalidates connector identity, gate state, and current-map reachability, then requires a fresh snapshot after the transition. The time budget uses the selected connector or terminal sleep macro estimate instead of a fixed recovery duration.
- **Mining rolling floor-step execution** - `mining.reach_depth` remains the model-facing objective and compiles exactly one current-floor primitive before the next transparent snapshot. The compiler can select native stone mining, native melee combat, native full-charge slingshot combat, dense-cluster bomb placement with verified fuse escape, natural debris pickup, native food recovery, native ladder/shaft descent, or mandatory native mine exit with deterministic BFS and dynamic threat interruption. Time, energy, and unrecoverable-health boundaries become `executor.exit_mine` upstream instead of stranding the actor. Loaded vanilla MineShaft stones expose guaranteed, guaranteed-one-of, conditional, and union possible item identities; a high-level resource target selects its source without model-supplied stone IDs and prefers guaranteed nodes. Monster projections distinguish raw spawn-selected drops from the effective death branch, cover all eleven vanilla `getExtraDropItems` overrides from live fields, and preserve whole-branch replacement semantics. Large shared identity sets for random cosmetics, naturally dropping trinkets, and hard-mine treasure are serialized once as complete catalogs and referenced by monster rows; all three catalogs carry exact conditional per-identity probabilities, and hard-mine treasure also carries exact conditional expected stack quantities across its sequential mastery/trinket gates, player-mail gates, and all 26 decompiled branches. All vanilla runtime-type and common `GameLocation.monsterDrop` rules expose exact current-snapshot event, at-least-one, call-count, and expected-quantity semantics without consuming RNG; this includes Burglar's Ring double calls, early-return/else-if branches, geometric stacks, and Book of the Void duplication ordering. Targeted monster planning ranks guaranteed sources first and stable exact reward per projected millisecond second when complete combat/movement evidence exists, otherwise retaining the fail-closed distance fallback. Vanilla melee receiver semantics distinguish base-equivalent damage, temporary immunity, permanent immunity, and required Bug Killer/Crusader gates. Complete direct-melee and direct-slingshot cases expose discrete damage distributions and expected attacks; ranged selection additionally requires sufficient loaded ammo, a clear current projectile line, and at least four tiles of separation. Bomb selection reproduces the native object-destruction mask, rejects protected objects, and requires escape outside the full player-damage square before the 2.4-second fuse. Independent attack trinkets, temporary immunity wait cost, custom runtime types, projectile interception/travel calibration, and explosive-ammo area utility remain explicitly incomplete. Position/death-tile-seeded previews and malformed/incomplete probability catalogs remain excluded from stable ranking. Full-objective time calibration and isolated multi-floor runtime validation remain pending.
- **Mining transparent input foundation** — a loaded MineShaft now exports compact collision rows, exact breakable-stone/container classification, live durability and best-pickaxe remaining hits, current monster facts, exact per-stone ladder previews, and floor gates without invoking mutating mine methods. Isolated E: runtime validation passed on a non-empty floor; the perfect dynamic mining executor remains pending.
- **`strategy.grandpa_progress` direction output** — the snapshot-aware policy selects from the current `GrandpaTrainingSampleAdapter` candidates without a guessed fallback; the action compiler independently rebuilds the current candidate set, rejects stale or mismatched metadata, emits no strategy step on block, and budgets only the validated direction. Static implementation is merged; focused and full tests remain intentionally pending while the user is playing.
- **`raise_skill_levels` transparent input** — `player.skills_detail` exposes all six vanilla skill rows, including unmodified/effective levels, temporary buff deltas, exact accumulated experience, next-level thresholds, remaining experience, the level cap, and the Grandpa scoring formula. `player.luck_context` keeps daily luck, Special Charm, permanent Luck skill, and active Luck buffs separate. Luck has native XP sources and is not excluded from planning. Exact candidate XP slices now cover current-floor native monster defeat, bounded successful rod-fishing outcomes, every currently loaded vanilla MineShaft stone, player crop harvests, planted forage harvests, complete giant-crop harvests, complete vanilla wild-tree clearing, farm stumps/hollow logs, stationary spawned-object pickup, vanilla twig clearing, raccoon seed-spot digging, and locally deterministic ordinary artifact spots. Ordinary artifact outputs now pass through strict queue-to-runtime serialized-item multiset verification; eligible unseen-secret-note outcomes remain blocked on unexposed global RNG state. Mining stone plans preserve separate Mining/Luck bounds and source conditions; crop candidates distinguish Farming, Foraging, and Luck results; fishing, farm-harvest, pickup, and obstacle-clear executors record observed skill-XP deltas. The Grandpa direction now directly binds only current candidates with a permitted option/kind, a complete `exact*` projection status, valid nonnegative bounds, and positive possible XP. Missing native families are enumerated in `docs/skill-experience-source-audit.md` and remain a coverage backlog, not guessed candidates and not a reason to suppress already exact current actions.

## Implementation Stages

### Stage 1: Plan Contract Foundation

Goal: every model plan step carries enough planning metadata for validation, execution, and training.

Required plan step fields:

- `kind`
- `target_location`
- `target_tile_x`, `target_tile_y`
- `estimated_minutes`
- `preconditions`
- `expected_effects`
- `safety_constraints`
- `failure_policy`

Acceptance:

- Compiler preserves metadata in normalized command parameters.
- Episode artifacts preserve model plan, compiled queue, execution result, and labels.

### Stage 2: Planner Output Surface

Goal: replace temporary local plan smoke source with a real small-model plan provider boundary.

Acceptance:

- Backend can accept or produce `small_model_plan.v1` without natural-language dependency.
- Plan source is swappable: local smoke, trained model, or future LLM-derived high-level objective adapter.

Current implementation:

- `DailyPlanCompiler` converts ranked timeline candidates into `small_model_plan.v1`.
- `/api/v1/planner/daily-plan/compile` accepts `ranked_event_candidates[]` and can optionally compile the generated plan into an action queue when a matching snapshot is available.
- Supported first slices: deferred waits split into compiler-valid `wait_ticks` chunks, shop/interact endpoints become `move_to_tile` plus `interact`, dialogue-shop branches add a whitelisted `choose_dialogue_response`, and buy candidates become `buy_shop_item`. Route connector candidates now carry exact connector kind, source tile, target location, optional arrival tile, bounded movement/time estimates, and compile into one `executor.traverse_connector` step before mandatory fresh-snapshot replanning. All currently emitted `recovery.stabilize_day` candidates compile to bounded close-menu, refresh-plan/wait, one exact rolling-horizon connector toward home, or verified at-home sleep operations. Connector plans fail closed if the latest transparent snapshot no longer confirms the connector, gate, or reachable start segment. Same-map social talk/gift candidates compile to `move_to_social_stand` + `social_interact`; remote current-loaded NPC candidates compile exactly one current transparent connector with social/gift continuation metadata, then require a fresh snapshot before any next route or interaction step.
- Fishing candidates compile to collision-safe stand movement plus `executor.catch_fish`. Mining reach-depth uses rolling-horizon compilation: the model emits only the objective, the compiler chooses one internal floor primitive from the latest snapshot, and the training loop requests another snapshot after that primitive. Internal mining primitives are not small-model actions. Unknown full-objective duration remains an upstream time-budget block until multi-floor calibration exists.

### Stage 3: Executor Primitive Curriculum

Goal: expand player-like primitives in safe order.

Order:

1. `executor.move_to_tile`
2. `executor.face_direction`
3. `executor.interact`
4. `executor.wait_ticks`
5. menu-safe primitives: `executor.choose_dialogue_response`, `executor.close_menu`, `executor.buy_shop_item`
6. `executor.use_tool` / resource clearing and tool-target verification
7. **`executor.social_interact`** — native social interaction via `Game1.currentLocation.checkAction` only; all blocked results are `executor_calibration`.

Acceptance:

- Each primitive has applied and blocked `plan_execution_episode.v1` samples.
- Blocked executor failures are calibration-only.
- Menu-opening and purchase steps must remain bracketed by transparent menu state, not inferred from model memory.

### Stage 4: Task-Level Options

Goal: compile higher-level plan steps into primitive executor sequences.

Initial options:

- maintain crops
- exit farmhouse / enter farmhouse
- navigate within current location
- sleep to next day

Acceptance:

- A task plan compiles to multiple executor queue items.
- Each item has preconditions, expected effects, safety constraints, and failure policy.

### Stage 5: Daily Closed Loop

Goal: run a minimal autonomous day loop with execution feedback.

Initial loop:

- read state
- plan safe movement / simple farm maintenance
- execute white-listed primitives
- write episodes
- stop on blocked or unknown state

Acceptance:

- No manual feedback is required for training samples.
- Every iteration writes before snapshot, plan, queue, execution, after snapshot, episode, and row.

### Stage 6: Perfect-Policy Training And Freeze

Goal: train and benchmark the strongest policy against transparent state and a mechanically perfect executor, without teaching the strategy layer to avoid goals because of low-level executor failures.

Acceptance:

- The best policy checkpoint, feature schema, option vocabulary, compiler version, executor version, and evaluation corpus are frozen as a reproducible baseline.
- Executor calibration failures remain separated from policy reward.
- The frozen baseline remains selectable as the non-adapted reference policy.

### Stage 7: Post-Training Human Adaptation

Goal: after the perfect-policy baseline is frozen, add a separate, reversible companion adaptation layer for human play preferences. This is a required product stage; its detailed behavior is intentionally still to be designed with the user.

Design backlog (TBD, not implementation commitments yet):

- cooperation, role division, initiative, pacing, and interruption preferences;
- player-specific resource ownership, goal reservation, and non-interference rules;
- human-like timing or bounded intentional suboptimality without corrupting the perfect baseline;
- communication/personality/voice behavior and consent controls;
- privacy-bounded learning of player habits;
- separate evaluation of optimality, human-likeness, usefulness, and disruption.

Hard boundaries:

- Human adaptation must be a configurable policy/output wrapper, profile, or separately trained checkpoint; it must not destructively relabel or overwrite perfect-policy data.
- Transparency, legality, safety checks, output recording, and executor correctness cannot be weakened for human-likeness.
- The perfect baseline and every adaptation profile must be independently selectable and disableable.
- Multiplayer companion execution must not steal keyboard focus, physical input, resources, or player-reserved goals.

Current base invariant: the material graph exposes shared and other-player resources for planning awareness but marks them non-spendable by default. Native transfer and Workbench execution reject those nodes again at runtime. Stage 7 may add explicit, versioned sharing grants, but it cannot weaken this default when a grant is absent or stale.

Exit conditions:

- The frozen perfect baseline is reproducible and still passes its benchmark.
- Adaptation profiles are versioned, reversible, and covered by separate evaluation data.
- Human play tests show that the selected profile is useful and non-disruptive while executor and transparency invariants remain intact.
- All still-TBD adaptation decisions are resolved and recorded before this stage can be declared complete.

## Non-Goals For This Chain

- No direct LLM control of executor.
- No guessed map coordinates.
- No direct coordinate teleport.
- No irreversible purchases/sales without later safety policy.
- No visual/keyboard end-to-end model as a first implementation path.
