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

- **Joja development route** - `world_progress.joja_development` now projects the live `JoinJoja` endpoint, host route commitment, actor membership/greeting/event state, current money, pending order state, and all five native development rows with exact button, paired mail IDs, prices, and both actor/any-farmer completion evidence. The small model chooses only `purchase_joja_membership` or one `purchase_joja_project`; the compiler owns stand selection, button, price, expected balance, and native callback contract. The action queue rebinds every irreversible field against the latest snapshot. Runtime walks to the counter and uses only `JojaMart.checkAction`, native dialogue callbacks, `JojaMart.answerDialogue`, and `JojaCDMenu.receiveLeftClick`; it never writes money, mail, event, quest, or world progress directly. Decompiled `Utility.getGrandpaScore` does not award a separate Joja point, so this remains a full-game progression option and is deliberately absent from Grandpa score directions. Full solution regression passes, while isolated in-game membership and five-project smokes remain pending.
- `move_to_tile` plan step compiles to `executor.move_to_tile`.
- Runtime movement is collision-safe and returns `applied` or `blocked`.
- Applied and blocked movement produce `plan_execution_episode.v1` artifacts.
- Executor calibration rows are excluded from policy/strategy training.
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
