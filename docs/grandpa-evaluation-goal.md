# Grandpa Evaluation Goal

Strategic target: reach the highest Grandpa evaluation result at the year-3 evaluation. In game terms this means at least 12 Grandpa score points, which maps to 4 candles.

## Verified rule source

Rules were checked against the local decompile:

- `StardewValley.Utility.getGrandpaScore()`
- `StardewValley.Utility.getGrandpaCandlesFromScore(int)`

The scoring function can reach 21 points. Candle thresholds are:

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

The actionable farmhouse side is projected separately as `world_progress.marriage_house`. It contains the exact next upgrade price/material tuple, the three-day construction countdown, Carpenter endpoint, Robin presence, competing construction state, and current partnership state. The Grandpa direction may use this house-axis candidate before level 2, but partnership completion remains fail-closed until bouquet/proposal/roommate acquisition and waiting stages have their own exact native chain.

`game.year`, `farm.grandpa_score`, and `player.active_object_qualified_id` are planning context. They do not add points, but they determine whether the initial year-3 evaluation is available and whether post-year-3 diamond re-evaluation can be planned.

## API

`GET /api/v1/goals/grandpa-evaluation/latest` returns `grandpa_evaluation_goal.v1` for the latest accepted snapshot.

## Exit condition

This slice is complete when:

- `dotnet build` passes.
- `dotnet test --no-restore` passes.
- The transparent bridge exposes every required input above as readable envelopes in a runtime snapshot.
- The API returns `target_met = true` and `current_candles = 4` for a state that has at least 12 verified points.
