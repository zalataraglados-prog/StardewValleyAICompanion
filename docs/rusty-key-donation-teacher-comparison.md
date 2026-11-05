# Rusty Key Donation Teacher Comparison

Current criterion-local acceptance is tracked in
[local acceptance](rusty-key-local-acceptance.md). This document retains the
comparison producer's original scope; it does not own whole-criterion readiness.

## Scope

`RustyKeyDonationTeacherComparisonBuilder` independently rebuilds one conditional
threshold decision from its original before snapshot and commitment ledger.
It never reads a control selection, learner ranking, execution result or key
receipt. This is not a global best-day schedule or a new executor.

The declared alternatives are every original immediate museum donation and a
compiler-checked, bounded 600-update no-donation counterfactual. The latter is
comparison-only: it is not inserted into the automatic candidate pool, emitted
to the model's output layer, or dispatched. It is not a claim that waiting for
one horizon permanently prevents obtaining the key.

This local policy prefers projected threshold progress within the declared
planning horizon. It does not rank donation above unmodeled farming, mining or
other goals, nor claim that estimated duration is a guaranteed native duration.
The subsequent native join must independently verify the action and outcome.

## Preconditions And Reuse

The snapshot must have a valid state hash, explicit observed tick, readable
save/player identity, game 1.6.15, bridge version and Stage-1 decision day below
224. The museum must be current, count 59 and threshold 60; the native reward,
prerequisite event, key event and key must not already have been granted. No
native event or menu may be active. Both alternatives must fit the current
clock budget before the normal 19:00 recovery boundary.

The existing ledger reader verifies actor identity and reservation structure.
Original museum availability, the empty-baseline timeline ranker, daily-plan
compiler, action-queue compiler and stable queue identity helper are reused.
The request's goal is one of the existing Rusty Key/Grandpa IDs. Ownership is
not silently transferred between a parent goal and its child: an old retained
parent-goal custody reservation is compiled under that same parent goal.

All original donation rows remain in the denominator, including rows excluded
by availability or the timeline. Rejected, unavailable, over-horizon or failed
compilations generate no negative label. Equivalent legal donations are not
ordered by candidate ID: each can have a strict pair against the admitted
no-donation alternative, but no pair against its equivalent peer. If either
side is unadmitted, no pair is emitted. Missing clock provenance is still
rejected by the original wait compiler; the consumer does not weaken it.

Only the original native projection and compiler establish a donation's
conditional threshold credit. Native success does not create a Teacher label.
The always-false native/full-criterion/formal-training claims keep this source
separate from an outcome, coverage promotion or trainer admission.

## Commands And Replay

```powershell
dotnet <bootstrap.dll> build-rusty-key-donation-teacher-comparison --request <request.json> --output <comparison.json>
dotnet <bootstrap.dll> verify-rusty-key-donation-teacher-comparison --comparison <comparison.json> --output <replay.json>
```

Request schema is `rusty_key_donation_teacher_comparison_request.v1`, with
`snapshot_path`, `strategy_ledger_path`, and existing `goal_id` (default
`grandpa.maximum_21`). Outputs contain the exact three source hashes, decision
state/tick, ledger revision, both compiled alternatives and explicit strict
pairs. Replay rebuilds original sources and compares raw persisted JSON,
including get-only claims. No deserialization default can hide a forged claim.

Split identity is the original decision's `save_id:player_id:total_day`, through
the existing `PolicyTrajectoryDatasetBuilder.PartitionFor`. A later key event
on another day does not move this decision into a different partition.

## Retained Native Input And Continuation

The 30 new checks and original museum family pass 78 tests; complete game-free
Core passes 1307 with no skipped tests. Earlier local failures were incomplete
new fixture clock provenance/actor setup, not weakened production gates.

Both public commands were run on the already retained `rk-e8d7a6c3-1104`
donation before-state (`311bf34e...`, tick 1041), using `planning-0003`'s
pre-decision `source-ledger.json` revision 2, not its settled revision.
The original `(O)549` donation (estimated 480 updates) and bounded wait both
compile. Their independent pair is on day 6, in **train only**. Build/replay
SHA-256 is `b0b0de31cea03aea4c64666e27b87f0a93b38476a32a7ad2f7a96180ca8cc489`.
Artifacts stay outside Git at
`I:\StardewAITraining\rusty-key-terminal-source-20261105`.

The next step binds this independently chosen donation to the same run's
original native donation/reward, normal-night continuation and verified key
terminal. Preserve the real per-action boundaries; do not rewrite aggregate
queue-start semantics. Additional native partitions are not yet proven. This
is not three-partition coverage or full criterion completion, and does not
reopen accepted key/custody/visual gates or add a Day-0 reverse prerequisite.

The comparison implementation was merged by PR #434 at `49a168e7`, after
final-head `c7ac24fe` CI `41970315013` passed. A subsequent read-only source
index used the original C# request-journal reader and shared native sleep
boundary checker on 13 consecutive actions, ticks 1041 through 7053. Its SHA is
`1a4506e9a6f0bc09605fdcc68c78e830db9e93cea6d0500faee8c768fdbff4e4`.
It remains an input index, not the pending typed join or corpus certificate.
For priority interrupts, its actual queue is the recorded
`mechanical_compiled_queue_path`; the older effective-plan path keeps its
parent-plan meaning. Do not change the writer or old data to satisfy a new
extractor. A shared journal consumer must preserve the original reader rather
than copy it or introduce a Bootstrap-to-Live dependency cycle.

The typed causal producer is now implemented in the
[Teacher/native join contract](rusty-key-teacher-native-join.md); its final
verification and remaining corpus/coverage scope are tracked in current work.
