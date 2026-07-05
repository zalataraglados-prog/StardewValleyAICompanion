# StardewAI Mathematical Model

This document materializes the local planning model for the AI-only Stardew Valley companion. It is a non-executable contract for future planner, verifier, and preference-ranker implementations.

## CF-SMDP Tuple

The planner uses a constrained factored semi-Markov decision process:

```text
M = (S, O, T, D, R, C, gamma, H, V, U)
```

- `S`: factored CanonicalState derived from transparent SMAPI snapshots.
- `O`: option set, where each option is a high-level temporally extended action.
- `T(s' | s, o)`: transition model over state factors after option completion, abort, or timeout.
- `D(tau | s, o)`: option duration model in game minutes, game ticks, and real-time seconds.
- `R(s, o, s', tau; u)`: weighted reward model conditioned on user state and mode.
- `C(s, o)`: hard safety and feasibility constraints.
- `gamma`: semi-Markov discount applied over elapsed in-game time.
- `H`: search horizon in options, game minutes, and real-time budget.
- `V`: verifier function that filters unsafe or unverifiable options before ranking.
- `U`: user-mode overrides and preference parameters.

The safe candidate set is:

```text
Safe(s) = { o in O | PreconditionsSatisfied(s, o)
                    and InvariantsSatisfied(s, o)
                    and StateVerified(s)
                    and Recoverable(s, o)
                    and C(s, o) = true }
```

The planner must never treat LLM text as direct execution authority. LLM output can propose goals or explain choices, but options must pass deterministic validation and verifier checks.

## State Factors

State factors map onto `snapshot.v1` CanonicalState. Every game fact must be read through a transparent field envelope with provenance, adapter, tick, confidence, and explicit `unavailable` semantics.

| Factor | Source domains | Required planner meaning |
| --- | --- | --- |
| `game` | date, time, season, weather, events | Calendar constraints, time windows, festival/shop risk. |
| `player` | position, stamina, health, money, inventory, tools, buffs | Feasibility, costs, available actions, exhaustion risk. |
| `farm` | crops, soil, terrain, machines, animals, buildings, chests | Farm work queue, recurring obligations, resource availability. |
| `locations` | maps, objects, terrain, interactables, passability | Navigation feasibility and path confidence. |
| `npcs` | locations, schedules, gifts, friendship, availability | Social options, gift timing, route opportunities. |
| `quests` | active/completed quests, deadlines, requirements | Goal progress and deadline pressure. |
| `world_progress` | bundles, collections, skills, unlocks, mail flags | Long-term progression value and unlock dependencies. |
| `menus` | active UI/menu state | Execution gating and click safety. |
| `mods` | installed mods and versions | Compatibility, adapter selection, unknown-state risk. |
| `modded_state` | mod-provided facts | Extension point; unavailable or unknown facts must not be guessed. |

Unknown, stale, or partially read data lowers confidence and can block options when the missing factor is safety-critical.

## Option Specs

Each option must be represented as:

```text
OptionSpec = {
  id,
  name,
  domain,
  initiation_conditions,
  goal_conditions,
  estimated_effects,
  duration_model,
  policy,
  success_conditions,
  abort_conditions,
  recovery_policy,
  safety_constraints,
  required_state_factors,
  reversible,
  irreversible_risk_class
}
```

Initial option families:

- `farm.maintain_crops`: water, harvest, replant, clear dead crops.
- `farm.process_machines`: load/unload machines while preserving protected items.
- `economy.buy_supplies`: visit shop and buy seeds or materials subject to reserve rules.
- `economy.sell_items`: sell only explicitly unprotected items after inventory verification.
- `social.gift_npc`: locate NPC, verify gift preference, deliver gift.
- `quest.advance`: perform a quest step with deadline and item checks.
- `exploration.visit_location`: route to a location for discovery, forage, or mining.
- `recovery.stabilize_day`: reduce risk, finish only urgent tasks, preserve control.

Every option must support preview before execution. In assisted or executor modes, preview state must include expected cost, expected elapsed time, required items, failure cases, and abort policy.

## Reward Model

The scalar planning score is:

```text
R = w_goal_progress * goal_progress
  + w_interaction_density * interaction_density
  + w_novelty * novelty
  + w_shared_experience * shared_experience
  + w_user_preference_match * user_preference_match
  + w_resource_efficiency * resource_efficiency
  - w_fatigue_cost * fatigue_cost
  - w_irreversible_risk * irreversible_risk
  - w_time_pressure * time_pressure
  - w_repetitive_work * repetitive_work
  - w_plan_fragility * plan_fragility
  - w_state_uncertainty * state_uncertainty
```

All reward terms are normalized to `[0, 1]` before weighting. Hard safety constraints are not penalties; they filter options before reward scoring.

Low-level executor mistakes are not reward penalties. For any domain covered by a `perfect_human_player` executor assumption, failures like missed clicks, missed swings, bad dodging, poor bobber control, slow menu navigation, or walking into walls belong to executor calibration or executor bugs. They must not reduce the strategy value of mining, fishing, combat, shopping, farming, or social play.

## Safety Constraints

Hard constraints:

- Never execute when required transparent state fields are `unavailable` or stale.
- Never sell protected, quest-critical, unique, equipped, or user-pinned items.
- Never spend below `emergency_reserve_money`.
- Never continue after snapshot hash mismatch or unexpected menu transition.
- Never perform irreversible operations without a passing verifier decision.
- Never automate movement when map passability or destination is unverifiable.
- Never click UI controls when active menu identity is unknown.
- Never bypass the planner/verifier path from dialogue or LLM output.
- Never mutate real game state in observer or planner-only modes.
- Never convert low-level perfect-executor failures into strategic dislike for a gameplay domain.

Soft constraints:

- Prefer lower fatigue during `relaxed`, `recovery`, and high-fatigue states.
- Prefer novelty during `exploration` if safety and deadlines are acceptable.
- Prefer social interaction during `social` and when NPC opportunities are time-limited.

## Verifier Thresholds

Verifier decisions are tri-state: `allow`, `block`, or `needs_user_confirmation`.

Default thresholds:

- Minimum snapshot completeness: `partial`; `unavailable` blocks all state-dependent options.
- Minimum required field confidence: `0.92`.
- Maximum field age for execution preview: `2` game ticks.
- Maximum plan fragility for unattended execution: `0.35`.
- Maximum irreversible risk without confirmation: `0.0`.
- Minimum route confidence for movement: `0.95`.
- Minimum menu confidence for UI actions: `0.98`.
- Minimum item identity confidence for sell/drop/gift: `0.99`.
- Minimum recoverability score for assisted execution: `0.85`.

Verifier must include explicit reasons, checked fields, observed tick, state hash, and required user confirmation if not allowed.

## User-Mode Overrides

Supported modes:

- `efficiency`: maximize long-term progress and resource efficiency.
- `relaxed`: maintain progress with lower fatigue and more interaction.
- `exploration`: favor new locations, events, mechanics, and discoveries.
- `recovery`: protect the save, resolve urgent obligations, avoid pressure.
- `social`: prioritize NPC interactions, gifts, festivals, and shared events.

User state parameters:

- `interaction_need`: raises value of interactive and explainable options.
- `efficiency_preference`: raises goal progress and resource efficiency.
- `fatigue_estimate`: raises penalties for repetitive and time-pressured plans.
- `risk_tolerance`: only relaxes soft penalties; hard constraints remain hard.
- `novelty_preference`: raises exploration and discovery value.

Mode overrides adjust reward weights, planner horizons, and confirmation requirements. They must not disable hard safety constraints.

## Planner Search Params

Initial planner configuration:

- Search algorithm: beam search over verified options.
- Beam width: `8`.
- Max option depth: `6`.
- In-game horizon: `780` minutes.
- Real-time planning budget: `1200` milliseconds.
- Candidate option cap per expansion: `32`.
- Replan triggers: state hash mismatch, event interrupt, option abort, time-window change, required field confidence drop.
- Discount: `gamma = 0.985` per 10 in-game minutes.

The planner should rank plans after verifier filtering and before natural-language explanation.

## Preference Ranker Params

The first trainable or tunable component is the preference ranker, not the planner core.

Ranker features:

- Reward terms and weighted total.
- User mode and user state parameters.
- Plan duration, depth, and interruption risk.
- State uncertainty and unavailable-field count.
- Expected interaction points.
- Novelty source count.
- Deadline slack.
- Historical acceptance/rejection labels.

Default ranker:

- Deterministic weighted linear model.
- Pairwise comparison target for future training.
- Calibration target: ranker confidence must track user acceptance.
- Explanation output must name the top positive and top negative factors.

## Open Uncertainties

- Exact crop, machine, animal, shop, NPC schedule, and modded-state field coverage depends on TransparentBridge adapters.
- Duration distributions need empirical calibration from observed movement and UI timings.
- Reward weights are initial priors and should be tuned from user feedback.
- State uncertainty aggregation needs real-world failure examples.
- Mod compatibility may require per-mod adapters and blocklists.
- Full executor mode should remain disabled until route, menu, item identity, and recovery verification are proven.
- Preference ranker training data format is not finalized.
- Multiplayer behavior is out of scope until single-player safety contracts are stable.
