# Small Model Action Queue And Executor

The small model must not call an executor directly.

The execution path is:

`small_model_action.v1`
-> `ActionQueueCompiler`
-> `action_queue.v1`
-> `IExecutorPort`
-> `execution_batch_result.v1`
-> future transparent snapshot feedback.

Execution is mode-scoped and actor-scoped.

- `training_singleplayer`: AI controls the training save's single farmer through a training sandbox. This is the current training path.
- `coop_companion`: AI controls a separate companion/farmhand actor in a future co-op companion mode.
- `human_player`: forbidden for model-directed execution.

## Small Model Output

`small_model_action.v1` is the only accepted small-model action output shape. It may reference registered `OptionSpec.option_id` values, plus plain string parameters.

It must include:

For training:

- `execution_mode = "training_singleplayer"`
- `actor.actor_type = "training_farmer"`
- `actor.control_surface = "training_sandbox"`
- a non-empty actor id such as `training_farmer.main`

For future co-op companion play:

- `execution_mode = "coop_companion"`
- `actor.actor_type = "ai_companion"`
- `actor.control_surface = "companion_actor"`
- a non-empty actor id such as `ai_companion.main`

It may not emit:

- keyboard or mouse commands
- raw coordinates
- arbitrary method names
- direct SMAPI/game calls
- save edits
- `human_player` as the target actor
- `keyboard_mouse` as the control surface

## Compiler

`ActionQueueCompiler` owns the boundary between model text and executable intent.

It checks:

- schema version
- source `state_hash`
- execution mode and actor isolation
- registered `OptionSpec`
- transparent required-state factors through the verifier

Unknown options, state hash mismatch, missing transparent facts, unsupported mode, or forbidden actor/control surface produce a blocked queue/item.

## Executor

The current executor is `DryRunExecutorPort`.

It returns `execution_batch_result.v1` and never mutates game state. This gives the system a stable execution result contract while keeping real execution behind `IExecutorPort`.

`TrainingSandboxExecutorPort` is the training path. It only accepts `training_singleplayer` queues targeting `training_farmer/training_sandbox`, returns `executor_mode = "training_sandbox"`, and emits feedback-ready before/after state hashes. It represents isolated training execution, not the player's live input surface.

Future executors must consume `action_queue.v1`, not small-model output.

Training executors may control the single training farmer in an isolated training save. Co-op executors must prove that their control target is the companion actor/farmhand. Neither path may steal keyboard or mouse focus from the human player.

## Feedback

Training feedback should come from:

- queue item selected
- executor result
- before state hash
- after transparent snapshot
- goal/factor delta

This makes execution feedback dense enough for training without letting the model bypass compiler and verifier checks.
