# Small Model Action Queue And Executor

The small model must not call an executor directly.

The execution path is:

`small_model_action.v1`
-> `ActionQueueCompiler`
-> `action_queue.v1`
-> `IExecutorPort`
-> `execution_batch_result.v1`
-> future transparent snapshot feedback.

Execution is actor-scoped. The executor is for an AI companion actor, not the human player's local input surface.

## Small Model Output

`small_model_action.v1` is the only accepted small-model action output shape. It may reference registered `OptionSpec.option_id` values, plus plain string parameters.

It must include:

- `actor.actor_type = "ai_companion"`
- `actor.control_surface = "companion_actor"`
- a non-empty AI actor id such as `ai_companion.main`

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
- actor isolation
- registered `OptionSpec`
- transparent required-state factors through the verifier

Unknown options, state hash mismatch, missing transparent facts, or a non-companion actor produce a blocked queue/item.

## Executor

The current executor is `DryRunExecutorPort`.

It returns `execution_batch_result.v1` and never mutates game state. This gives the system a stable execution result contract while keeping real execution behind `IExecutorPort`.

Future executors must consume `action_queue.v1`, not small-model output.

Future real executors must also prove that their control target is the companion actor/farmhand. They must not steal keyboard or mouse focus from the human player.

## Feedback

Training feedback should come from:

- queue item selected
- executor result
- before state hash
- after transparent snapshot
- goal/factor delta

This makes execution feedback dense enough for training without letting the model bypass compiler and verifier checks.
