# Candidate Availability Gating Policy

This document is the handoff rule for future agents working on option generation, route planning, shop interaction, and training data collection.

## Core Rule

If a candidate can be ruled out from transparent state or decompile-confirmed hard rules, rule it out upstream in candidate availability. Do not wait for the compiler, executor, or runtime smoke to discover the same failure.

The executor is the last safety net for stale state, unexpected runtime drift, or still-unsupported side effects. It is not the normal filter for known closed shops, absent shop owners, blocked route gates, unsupported action branches, active menus, missing fields, or impossible time windows.

## Availability Classes

| class | meaning | candidate behavior | executor behavior |
|---|---|---|---|
| `always_available` | Pure mechanical action once the local context is already legal, such as walking to a reachable tile or watering a known tile when tool/state gates pass. | Emit as available when required transparent fields are present and local path/tool checks pass. | Verify effect and block only on stale/drifted runtime state. |
| `windowed_available` | Action has explicit time/date/weekday/season/festival windows, such as shop doors and shop services. | Emit only when `allowed_now=true`, or emit a separate wait/positioning candidate when `next_open_time` is near and wait cost is acceptable. | Never be the first place that discovers the shop is closed. |
| `state_gated` | Action depends on story state, mail, keys, friendship, weather, NPC schedule, owner presence, building state, menu state, inventory, or side-effect whitelist. | Emit only when all required gate fields are known and pass; otherwise mark unavailable with concrete `block_reasons`. | Block only if the live game diverges from the snapshot or if side-effect verification is incomplete. |

These classes are labels for implementation discipline. They may be encoded as fields later, but the current required output is still `available`, `block_reasons`, and candidate-specific details.

## Required Candidate Fields

Candidates that depend on time or state gates should expose enough information for the planner and later agents to see why they were accepted or excluded:

| field | required meaning |
|---|---|
| `allowed_now` | The candidate may be selected immediately from the current snapshot. |
| `allowed_today` | The candidate can still become legal today without sleeping, if known. |
| `next_open_time` / `effective_open_time` | Earliest same-day time the candidate can become legal. |
| `closes_at` / `close_time` | Latest legal same-day service time. |
| `wait_cost` | Estimated waiting cost if the planner wants to wait nearby instead of dropping the candidate. |
| `gate_reasons` / `block_reasons` | Concrete hard-rule reasons; never replace these with model judgement. |

Current shop endpoint implementation uses `current_location.shop_action_tiles[].service_time_status` and `owner_service_status` for these gates. If a later agent adds fields, keep the older reason names stable unless there is a migration.

## Shop And Route Rule

For shop-like candidates, upstream candidate filtering must consider:

- Door/entrance gate: `LockedDoorWarp`, `ConditionalDoor`, festival closure, Town Key, green rain, friendship, mail, and route action branch coverage.
- Service window: current time/day/season/weather and the entrance-derived open/close window.
- Owner/service gate: required owner NPC exists, is loaded in the service location, and is inside the service counter area when required.
- Interior endpoint: the player still needs to route adjacent to the actual counter/action tile and execute the whitelisted interaction, not merely enter the building.
- Purchase side effects: after shop menu opens, item purchase remains a separate side-effect-whitelisted action.

If the store is closed, the owner is away, the route gate is illegal, or the endpoint cannot be reached, the candidate is unavailable. The planner may generate a different candidate such as `wait_near_shop_until_open` only when the window is known, the wait fits the daily budget, and the route to the waiting position is legal.

## Direct Runtime Traversal Caveat

The route-to-shop smoke harness may use direct runtime traversal for training speed. That shortcut must still honor transparent route gates before crossing a map edge or door. It must not use direct location changes to bypass a closed `LockedDoorWarp`, friendship gate, festival closure, or unsupported branch.

Normal player-input execution should be stricter: walk to the connector, trigger the actual game route/door interaction, then verify observed location/tile/menu state.

## Training Consequence

Training rows should teach the small model to choose among legal, well-described candidates. They should not teach the model that illegal candidates are acceptable because the executor later blocks them. A blocked executor row is calibration and safety evidence, not a valid policy target for a known hard-rule failure.
