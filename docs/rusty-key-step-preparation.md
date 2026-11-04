# Rusty Key Step Preparation

## Purpose And Authority

The autonomous rolling coordinator must know whether the next stage is actually
executable before committing its resources. `RustyKeyRollingStepPreparationBuilder`
is the typed, read-only entrypoint for this decision. It does not create a second
planner, compiler, executor, allocator, reservation store or Teacher.

It independently rebuilds the rolling control report from retained sources and
compares its raw JSON, including get-only false authorization claims. A supplied
support proof uses the existing independent support-settlement/fresh-replan path.
Caller-written readiness flags or a learner score cannot authorize a stage.

## Stage Contract

- `held_donation`: return the producer's existing compiled queue, without requiring
  acquisition axes or an acquisition resource commit. Dispatch still uses the
  existing current-state/reservation gate and native receipt verification.
- `terminal_acquisition`: the original dispatch endpoint selection and compiler
  can compile a current terminal transition. Return the ordinary admitted commit
  request, not the speculative queue. After the real commit, refresh ranking
  against the committed ledger and use the original dispatch/binding builders.
- `supporting_transition`: an existing compiled support role or the exact shared
  no-terminal-endpoint result may invoke the original support-request builder.
  Return its exact current request/atomic commit and retained source inputs. The
  normal support commit, compilation, native receipt, settlement and fresh replan
  remain mandatory. Missing/tampered candidate evidence is not support fallback.
- `native_key_handoff`: the donation threshold is already satisfied. No extra
  supply queue/commit is emitted and `step_ready` remains false. This does not
  prove actual key possession or bypass the ordinary reward/day/story chain.
- `blocked`: no executable stage is ready, so no atomic commit request is emitted.

All results have `execution_authorized=false`,
`teacher_preference_label_eligible=false` and `formal_training_authorized=false`.
This is preparation, not a substitute for dispatch authorization.

## Pending Supply Safety

A reproduced regression selected an item for acquisition again when it was
already held but the museum mutex prevented immediate donation. Before new
supply, the producer now projects the existing material graph through the shared
reservation service. It counts distinct undonated identities, not stack size, and
subtracts all active claims. Only this exact goal's authenticated museum output
custody may be added back; a forged marker or a different goal cannot spend it.

Pending identities remain in the full 95-item fixture denominator and, when
acquisition is still needed, their route evaluations explicitly explain why they
were not selected. Other-goal fully protected inventory does not satisfy supply;
a spare unit does. Authorized storage avoids duplicate production but remains an
explicit transfer/donation dependency, not completed progress. If pending items
already cover the deficit, the producer reports
`rusty_key_owned_supply_pending_donation_or_transfer` without building unrelated
acquisition axes or claiming the threshold is satisfied.

The shared [output-custody transfer control](museum-output-custody.md#original-control-and-compiler-binding)
now provides the original candidate/commit/compiler/JIT/receipt/settlement path.
It is not yet selected by this rolling builder: stored pending supply still has
the explicit dependency above. The next integration must choose authenticated
retrieval as a mechanical preparation, then fresh-replan the original donation;
do not delete the pending guard, reacquire the item or call it a machine input.

Independent held-queue reconstruction also exposed random compiler-generated
plan/queue identities. The producer now binds its plan and shared queue identity
stabilizer to source hashes and the selected candidate. Native action semantics
and the original acquisition identity prefix remain unchanged.

## Entrypoint And Verification

`prepare-rusty-key-rolling-step` accepts the same source arguments as
`build-rusty-key-rolling-supply`, plus `--portfolio-control-selection <report>`
and `--output <preparation>`. Acquisition also requires `--ranking <current>`.
Optional `--control-supporting-transition-proof` retains the verified support
lineage. Blocked reports exit 2; native handoff is reported without a supply step.

`self-test-rusty-key-rolling-step` passes 18 named synthetic checks, including two
actual hidden CLI processes, real current candidates/compiler, missing/stale
ranking, missing candidate denominator, forged raw claims, held deterministic
rebuilding and threshold handoff. `self-test-rusty-key-rolling-supply` passes 34
checks including pending supply, protected/spare units, exact custody history,
authorized/unauthorized storage and unknown inventory. Existing 20 control,
18 support-rebind, full-threshold, dispatch and focused supervision regressions
also pass. No game was launched; raw diagnostics remain outside Git.

## Exact Next Step

The controlled runtime adapter is now implemented; its code/test boundaries and
entrypoint are described in [runtime rolling](rusty-key-runtime-rolling.md).
The following native trace and remaining acceptance work are still required.

The direct acquisition/custody/donation/consumption/night/key trace is already
[accepted](geode-dialogue-lifecycle.md#accepted-native-boundary); do not repeat
it as an unmet gate. Sparse ordinary native transfer and reply recovery are also
accepted prerequisites. Next connect the bound custody-transfer control above
to the existing rolling preparation and runtime HTTP/precompiled runner, retain
actual dispatched items and fresh receipts, independently verify original-service
settlement, and fresh-replan after each transfer. Obtain real acquired output ->
storage -> withdrawal -> original donation/consumption proof with other-goal
protection. Joint timing/opening windows, criterion partitions and the pre-Year-3
deadline remain later gaps. A standalone transfer CLI is not the autonomous
live-root loop, and frozen coverage counts remain unchanged.
