# Small Model Action Queue And Executor

The small model must not call an executor directly.

The execution path is:

`small_model_action.v1`
-> `ActionQueueCompiler`
-> `action_queue.v1`
-> `IExecutorPort`
-> `execution_batch_result.v1`
-> future transparent snapshot feedback.

## Small Model Output

`small_model_action.v1` is the only accepted small-model action output shape. It may reference registered `OptionSpec.option_id` values, plus plain string parameters.

It may not emit:

- keyboard or mouse commands
- raw coordinates
- arbitrary method names
- direct SMAPI/game calls
- save edits

## Compiler

`ActionQueueCompiler` owns the boundary between model text and executable intent.

It checks:

- schema version
- source `state_hash`
- registered `OptionSpec`
- transparent required-state factors through the verifier

Unknown options, state hash mismatch, or missing transparent facts produce a blocked queue/item.

## Executor

The current executor is `DryRunExecutorPort`.

It returns `execution_batch_result.v1` and never mutates game state. This gives the system a stable execution result contract while keeping real execution behind `IExecutorPort`.

Future executors must consume `action_queue.v1`, not small-model output.

## Feedback

Training feedback should come from:

- queue item selected
- executor result
- before state hash
- after transparent snapshot
- goal/factor delta

This makes execution feedback dense enough for training without letting the model bypass compiler and verifier checks.
