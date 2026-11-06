# Friendship Policy Rows And Shared Inputs

## Scope And Reuse

PR #461 was accepted by CI `42145083661` (38 steps) and merged as
`3ba9796f`. Its two strong native receipts remain the source of truth. This slice
adds original `policy_decision_trajectory.v2` rows and an explicit replay provider;
it does not implement another executor, rerun the game or authorize product training.
Totals remain 9/19 executable, 9/19 split-complete, 9/19 ready and 7/11 roots,
formal=false. These are criterion-local results, not a complete new-save Teacher.

`build-friendship-teacher-row --native-receipt ... --trajectory-id ... --output ...`
replays the typed native receipt once, including the original independent
comparison, complete immutable queue and every native request/frame/nonce/fact.
It then uses the original trajectory builder and aggregate receipt conversion.
`verify-friendship-teacher-row --row ... --output ...` rebuilds from those sources
and compares the full row. Both commands require a new JSON output path.

The row retains the complete neutralized decision candidate context, the original
five strict pairs, the full movement-plus-talk queue and exact threshold transition.
SelectedQueueItemId remains the queue head, as required by the existing shared
contract; the threshold is established by the complete native receipt, not credited
to the first movement item. Unknown long-horizon returns remain pending.

## Explicit Provider And Source Family

The production registry now explicitly registers
`deterministic_teacher.current_friendships` / `v1`, alongside the unchanged
Collection and Skills identities. No fallback provider or self-asserted teacher
label is accepted. The supervision subtype is
`policy_teacher_supervision.friendships.v1`, binding its typed native receipt,
preference, original before/after captures and aggregate execution hashes.
It does not fabricate Community Center inventory, acquisition or denominator
hashes. Cross-family native paths/hashes are rejected. New optional fields are
omitted when null, preserving old source serialization.

The existing pair admission and dataset builder still compare full source-bound
state features, candidates, labels and native outcome. Complete-order=false and
the 19/15 uncompared candidates remain retained context, not negative labels.
Each original save/date split is preserved. Two day223 sources cannot establish
train/validation/test coverage or promote the whole friendship root.

## One Input Representation

The snapshot-aware state projector adds four native numeric facts for
`goal.social.raise_friendships`: eligible villagers, qualifying count, and the
remaining counts for five and ten, plus two threshold flags. Unreadable or
cross-state data is rejected, never encoded as zero. Skills keeps its old exact
projection; unrelated goals keep their old feature bytes.

`FriendshipPolicyInputProjector` owns an immutable input copy and one calibrated
route context per decision, reusing the original contact frontier and complete
route budget producer/cache. Its candidate `policy_features` are model-only:
verified arrival clock, full arrival budget in game minutes, connector count,
verification flag, status and timing kind. Arrival budget is not interaction
duration. A missing calibration or unproved tail emits an explicit unknown status
and no invented numeric zero. An available first connector is not a full route.
Existing points, deltas and portfolio parameters are already encoded and are not
duplicated. None of these model features are added to action parameters.

The existing current-contact certification was moved without changing its gates
from the experiment consumer into a shared Core adapter. Teacher and structured
runtime callers reuse it; old candidate generation, baseline behavior, default
64-candidate runtime window, binding/compiler/executor and strict native reader
contracts remain unchanged. The model encoder reads the optional feature vector;
the trajectory validator rejects malformed/nonfinite or duplicate feature names.

Backend structured Friendship inference requires explicit compatible
`route_timing_calibration_json`, certifies candidates before binding (including a
root-goal rerank), and uses the shared projections. The live loop forwards its
existing `--route-timing-calibration` input. Feedback reads the ranking's effective
Friendship goal and checks the original compiled queue/item, rather than assuming
the outer Grandpa goal or requiring optional legacy NormalizedCommand.GoalId.
Other goal features are not rewritten. No implicit F: discovery or fresh
calibration is introduced.

## Verification And Next Slice

Native and derived files stay under
`I:\StardewAITraining\friendship-native-join-20261106`, outside Git. Original b
receipts must still replay to `ff353d1e...` and `77f215a8...`; no historical
native summary is rewritten. Public row/dataset results and final test/CI evidence
are recorded in the PR closing report.

Actual public Build/Verify results (each row has 29 decision candidates, five
strict pairs, two queue items and the original train partition):

| Criterion | Row and matching replay | SHA256 |
| --- | --- | --- |
| friendships_5 | five-teacher-row-a.json / five-teacher-row-verified-a.json | 0e805776a1cf28c7c54f856f3fe815b9b5103de0eb4ab8001b8a0abfee6f454a |
| friendships_10 | ten-teacher-row-a.json / ten-teacher-row-verified-a.json | c8ec56d0aa18ff7bd5c3ea9da9267f12ff165a08e53339f45338250d31d35136 |

`teacher-dataset-a` uses the original PolicyDataset CLI, explicit independent
decisions and original registry for both new rows, two accepted Skills b rows and
the accepted Collection row: five accepted, zero rejected, train=5, validation=0,
test=0, all long returns pending. Manifest SHA
`1e47826beb256aacb8455b25606c4852c88d3d096edc9e9b8651e21e51b76529`.

`backend-inputs-a/summary.json` records direct Friendship and Grandpa-to-Friendship
HTTP paths on the retained native snapshot, with all 29 common candidate input
vectors equal to the Teacher row in both cases. The synthetic unit checkpoint is
only an interface probe, not a trained product model; no action was executed.
The owned hidden Backend process exited. Feedback goal-reader tests additionally
check original queue goals/items with absent legacy command goals.

Eight public row mutations/overwrite probes in `row-rejections-b` reject without
output or source changes. The initial a probe incorrectly required exit code 2;
the original CLI correctly rejected with 1. Only that external assertion was
corrected; no production gate or CLI semantics was changed. Final local Core
1902 and Backend 607 pass with zero skips, including 36 new tests. Original
EVD-338 and whitespace checks pass; final CI belongs in the PR closing record.

Next: typed criterion corpus and the original coverage/reconciliation source,
then the conditional local method and only genuinely missing policy partitions.
Do not resample accepted native actions/calibration, borrow partitions, reopen
accepted roots or reduce the global 21-point Teacher responsibility. No workers,
server changes, product-model training or cleanup belong to this slice.
