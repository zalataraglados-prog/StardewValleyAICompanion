# Grandpa Training Sample Adapter

This slice converts `world_model.v1` plus `grandpa_evaluation_goal.v2` into `training_sample.v1`.

It does not require an executor. Without an executor, feedback comes from observed transparent state deltas after human play or any external action source:

- before snapshot: `source_state_hash`
- after snapshot: future transparent snapshot
- feedback: score/factor changes computed from the two snapshots

The adapter never invents feedback. `feedback.available_now` is `false` until an after-state is observed.

## Output

`GET /api/v1/training/grandpa-evaluation/latest` returns:

- target score state: current score, 21-point target, points needed, complete flag
- planner state: missing transparent facts and blocking reasons
- candidate directions: deterministic ways to gain remaining Grandpa points
- feedback placeholder: `observed_state_delta`

## Candidate Directions

Directions are grouped by Grandpa scoring factors:

- earn money
- complete museum collection
- obtain Skull Key
- complete or unlock Community Center
- marriage or roommate plus farmhouse upgrade
- obtain Rusty Key
- complete Master Angler
- complete Full Shipment
- raise friendships
- raise skill levels
- earn pet love

Each direction exposes potential points, related factor IDs, blocked status, and a deterministic priority score. This is planner/training input, not execution authority.

## Exit Condition

- `dotnet build` passes.
- `dotnet test --no-restore` passes.
- Only 21-point complete goal states produce no candidate directions; reaching four candles at 12 points does not stop direction generation.
- Incomplete goal states produce candidate directions.
- Missing transparent facts block the sample instead of producing guessed feedback.
