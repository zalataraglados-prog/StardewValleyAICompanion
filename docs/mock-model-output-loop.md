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
4. `GET /api/v1/action-queues/{queueId}/training-episode`
5. `GET /api/v1/action-queues/{queueId}/training-feature-row`
6. `POST /api/v1/action-queues/{queueId}/training-feature-row/append`
7. `POST /api/v1/training/baseline/train`
8. `POST /api/v1/training/baseline/predict`

This gives a no-training smoke test for the future model path.

`training_episode.v1` is the dataset boundary for this loop. It deliberately splits feedback into three channels:

- `strategy_value`: goal progress and reward terms for the model.
- `hard_feasibility`: compiler, time, and simulator blockers.
- `executor_calibration`: perfect-executor duration, state deltas, resource costs, and decompile-backed calibration notes.

The initial `strategy_value` calculator only scores facts confirmed by `simulated_transition.v1`. For `farm.maintain_crops`, watered crops produce a positive `crop_watered` term and explicit energy use produces an `energy_spent` cost term. Blocked queues do not create negative strategic preference by themselves.

Low-level executor failures listed in `preference_penalty_exclusions` are excluded from strategic preference learning. They belong to executor quality or duration calibration, not to the model's desire to choose a task.

`training_feature_row.v1` is the first model-facing export. It keeps state features, action features, and labels separate so the same row can feed a C# HTN/CF-SMDP heuristic, ML.NET, or LightGBM-style learner later. This phase does not require Python, CUDA, TorchSharp, or a neural model runtime.

The dataset writer appends `training_feature_row.v1` records to JSONL. The baseline trainer currently aggregates rows by `option_id` and reports average reward/progress and hard-block rate. This is a smoke-test trainer for the data pipeline, not the final learned policy.

The baseline predictor consumes the aggregate training report or a dataset path and ranks candidate options. Unseen options remain in the output with neutral reward and high uncertainty penalty, which keeps exploration visible without pretending evidence exists.

## Time And Mining Assumption

The time budget validator uses Stardew's decompiled time shape:

- `Game1.timeOfDay` is the current clock.
- the clock advances in 10-minute steps and caps at `2600`.
- `TimeChangedEventArgs` exposes old/new clock values.
- player energy is read from `Game1.player.Stamina`.

Mining uses the `perfect_human_player` execution profile. Random mine layout facts such as `MineShaft.mineRandom`, `mineLevel`, ladder fields, monster areas, and ladder discovery affect calibration and elapsed-time estimates, but they must not be converted into low-level operation-failure fear. The model should not learn that mining is undesirable because an executor moves poorly. Poor execution belongs to executor quality, not strategic preference.
