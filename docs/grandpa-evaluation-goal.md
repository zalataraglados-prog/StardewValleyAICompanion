# Grandpa Evaluation Goal

Strategic target: earn all 21 Grandpa rule points by the year-3 evaluation. Four candles at 12 points are a milestone, not the optimization target and not a planner stop condition.

## Verified rule source

Rules were checked against the local decompile:

- `StardewValley.Utility.getGrandpaScore()`
- `StardewValley.Utility.getGrandpaCandlesFromScore(int)`

The scoring function can reach 21 points. Candle thresholds remain useful outcome milestones:

- `score >= 12`: 4 candles
- `score >= 8`: 3 candles
- `score >= 4`: 2 candles
- otherwise: 1 candle

## Score factors

The local evaluator mirrors `getGrandpaScore()`:

- Total money earned: `>= 50,000`, `>= 100,000`, `>= 200,000`, `>= 300,000`, `>= 500,000` each add 1 point.
- Total money earned: `>= 1,000,000` adds 2 points.
- Achievement `5` adds 1 point.
- Skull Key adds 1 point.
- Community Center accessible or completed adds 1 point.
- Community Center accessible adds 2 more points.
- Married or roommate and farmhouse upgrade level `>= 2` adds 1 point.
- Rusty Key adds 1 point.
- Achievement `26` adds 1 point.
- Achievement `34` adds 1 point.
- At least 5 friends with friendship points `>= 1975` adds 1 point.
- At least 10 friends with friendship points `>= 1975` adds 1 point.
- Player level `>= 15` adds 1 point.
- Player level `>= 25` adds 1 point.
- Mail flag `petLoveMessage` adds 1 point.

## Transparent inputs

The evaluator consumes `world_model.v1`, not raw snapshots. Required facts:

- `player.total_money_earned`
- `player.has_skull_key`
- `player.has_rusty_key`
- `player.married_or_roommate`
- `player.farmhouse_upgrade_level`
- `player.level`
- `world_progress.achievements`
- `world_progress.community_center`
- `npcs.friendships`
- `quests.mail_received`
- `game.year`
- `farm.grandpa_score`
- `player.active_object_qualified_id`

Missing facts are reported in `missing_fact_paths`; they are not guessed.

The actionable farmhouse side is projected separately as `world_progress.marriage_house`. It contains the exact next upgrade price/material tuple, three-day construction countdown, Carpenter endpoint, Robin presence, competing construction state, current partnership state, direct Grandpa score delta, and verified upgrade capabilities. The Grandpa score factor still requires partnership plus house level 2.

Level 3 is deliberately separate from that direct score factor. The decompile shows that it keeps the `FarmHouse2` main map and adds a linked `Cellar`, cellar warps, and the Cask recipe. The candidate therefore records an indirect production-infrastructure benefit: a new indoor object/machine placement location and cellar processing. `cellar_infrastructure` reads the assigned live Cellar map, counts statically placeable unoccupied tiles while excluding transient farmer/character occupancy, and records existing objects plus machine counts by qualified ID. The purchase candidate transports and rebinds those values. Level 3 can remain a long-horizon infrastructure candidate, but must not claim a direct Grandpa point.

`game.year`, `farm.grandpa_score`, and `player.active_object_qualified_id` are planning context. They do not add points, but they determine whether the initial year-3 evaluation is available and whether post-year-3 diamond re-evaluation can be planned.

## API

`GET /api/v1/goals/grandpa-evaluation/latest` returns `grandpa_evaluation_goal.v2` for the latest accepted snapshot. `target_score` is 21, while `four_candle_score_threshold` is 12 and `four_candle_milestone_met` reports the milestone separately.

## Exit condition

This slice is complete when:

- `dotnet build` passes.
- `dotnet test --no-restore` passes.
- The transparent bridge exposes every required input above as readable envelopes in a runtime snapshot.
- The API returns `four_candle_milestone_met = true` and `current_candles = 4` at 12 verified points without ending planning.
- The API returns `target_met = true` only at 21 verified points.
