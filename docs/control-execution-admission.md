# Control Execution And Teacher Supervision

## Contract

A deterministic control selection may execute an already admitted route without
claiming an independent Teacher preference. The two selection sources are mutually
exclusive. Native execution evidence and preference evidence remain separate.

The first supported control source is the retained
`rusty_key_rolling_supply_proposal.v1` acquisition selection. It is independently
rebuilt from the complete museum inventory, fresh snapshot, original ledger and
retained acquisition axes. Raw JSON must equal that rebuilding, including get-only
false authorization fields. The selected route, ordinary proposal and ordinary
admission must match exactly. A caller boolean, ranking or one-item proposal alone
is not control authorization.

Execution still uses the existing atomic portfolio commit, independently rebuilt
commit receipt, source-bound current candidate, daily-plan/action compiler,
immutable queue binding, native outcome verifier and canonical settlement. Other
goals' resources, stale states, mismatched queues and unknown sources retain their
existing rejection paths. No new planner, executor, reservation store or game rule
is introduced.

## Entrypoints And Artifacts

For the ordinary acquisition commands, supply `--portfolio-control-selection`
instead of `--portfolio-preference-request` / `--portfolio-teacher-preference`:

- `compile-acquisition-route-dispatch`
- `build-acquisition-route-execution-binding`
- `build-acquisition-route-fresh-terminal-receipt`
- the existing route settlement request/receipt commands

All original source, committed-ledger, exact-route and queue arguments are still
required. Export the selected proposal/admission with
`build-rusty-key-rolling-supply`, commit through the existing portfolio service and
retain the resulting commit receipt. A held-donation output already contains the
existing donation queue; it is not an acquisition selection for this entrypoint.

The control binding adds optional `portfolio_control_selection` provenance and
source SHA-256. Its `portfolio_teacher_preference_verified` remains false and its
Teacher source hash is empty. The queue source is
`deterministic_control.acquisition_route_dispatch.v1`, not a Teacher namespace.
Persisted control bindings are rebuilt, not trusted because `verified=true`.
Absent optional control fields preserve legacy Teacher artifact serialization.

A valid control native receipt sets `fresh_terminal_receipt_verified=true` but
`route_training_evidence_eligible=false`. The canonical settlement accepts this
execution evidence and can retain the acquired museum item in the existing
custody rows. Changing the training flag invalidates source rebuilding. Existing
Teacher rollout checkpoints and supervision consumers remain Teacher-only; this
slice does not make control traces preference labels or authorize formal training.

## Verification And Remaining Work

`self-test-control-execution --output-root <isolated-path>` exercises the public
source-file builders, actual Teacher-off CLI, real compiler and ledger services,
native-receipt validation, custody settlement and rejection cases. Its shop,
museum identities and native receipts are explicitly synthetic; the shop is not
a claim about a vanilla museum-item source. No game is launched.

The 20 named checks passed locally, together with the complete legacy
`self-test-current-stage-one-collection`, hermetic bootstrap critical paths,
25-case rolling producer and 30-case threshold suites. Release builds have no
warnings/errors. CI runs the focused control entrypoint as a separate gate.

This slice admits a fresh rolling acquisition step. Autonomous live-root
orchestration, supporting-transition/continuation control-source rebuilding,
ambiguous-network recovery, custody relocation, joint opening/time budgets and a
single native acquisition-to-donation-to-consumption trace remain to be closed.
Unsupported control continuations fail closed rather than substituting a Teacher.
Criterion Teacher/native partitions and the pre-Year-3 deadline remain separate.
Frozen coverage counts and `formal_product_training=false` do not change.
