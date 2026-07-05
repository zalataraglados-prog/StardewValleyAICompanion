# Mock Model Output Loop

This loop tests the outer engineering path before any local model training.

It is not a trained model and must not be used to claim policy quality. It is a deterministic substitute for the future small model output port.

## Purpose

Use `mock-small-model.rule.v1` to exercise:

- `small_model_action.v1` serialization.
- `ActionQueueCompiler` acceptance and blocking behavior.
- executor/simulator contracts.
- episode capture and future dataset shape.

The mock producer is replaceable. A trained model must emit the same `small_model_action.v1` contract before it can enter the compiler.

## Task Classes

The task classes are mutually exclusive at the output boundary:

| Category | Model output | Compiler responsibility |
| --- | --- | --- |
| `mechanical` | option id only | expand complete deterministic action sequence |
| `parameterized_mechanical` | option id plus scalar target parameters | expand deterministic action sequence after parameter validation |
| `spatial_planning` | option id plus position-plan requirement | validate/generate tile or route plan before expansion |
| `economic_strategic` | option id plus detailed plan requirement | validate budget, target, shop/menu/NPC/quest constraints |
| `recovery` | fallback option plus reason | stabilize or stop safely |

The third class is `parameterized_mechanical`. It covers goals like "mine to level 40": the model must provide the target, but the executor should still derive the atomic route/actions mechanically.

## Endpoint

`POST /api/v1/mock-model/small-model-action`

Input:

```json
{
  "goal": "water crops",
  "state_hash": "snapshot hash",
  "execution_mode": "training_singleplayer"
}
```

Output:

```json
{
  "schema_version": "small_model_action.v1",
  "source_model": "mock-small-model.rule.v1",
  "actions": [
    {
      "option_id": "farm.maintain_crops",
      "parameters": [
        { "name": "intent_category", "value": "mechanical" }
      ]
    }
  ]
}
```

Then send that output to:

1. `POST /api/v1/small-model/action-queue/compile`
2. `GET /api/v1/action-queues/{queueId}/time-budget`
3. `POST /api/v1/action-queues/{queueId}/simulate-training-transition`

This gives a no-training smoke test for the future model path.

## Time And Mining Assumption

The time budget validator uses Stardew's decompiled time shape:

- `Game1.timeOfDay` is the current clock.
- the clock advances in 10-minute steps and caps at `2600`.
- `TimeChangedEventArgs` exposes old/new clock values.
- player energy is read from `Game1.player.Stamina`.

Mining uses the `perfect_human_player` execution profile. Random mine layout facts such as `MineShaft.mineRandom`, `mineLevel`, ladder fields, monster areas, and ladder discovery affect calibration and elapsed-time estimates, but they must not be converted into low-level operation-failure fear. The model should not learn that mining is undesirable because an executor moves poorly. Poor execution belongs to executor quality, not strategic preference.
