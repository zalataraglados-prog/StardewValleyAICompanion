# Owned Crop Seed Transfer Preparation

> Current successor: [native-format outcome and original settlement](money-crop-seed-native-settlement.md).
> That kernel is implemented; original runtime/journal wiring, owned planting,
> lifecycle release, managed activation and actual native sampling remain explicit
> next work. The preparation-only statements below describe this slice's scope.

## Scope And Accepted Parent

PR509 merged as `8e540c72c3521efa7b3b7c729c3a41f2f399667d` after all42
CI steps in42381801869 (Backend768/Core2442), same-head review without findings
and tested/main tree equality. Its purchase runtime transaction remains accepted.
L1 stays19/19 criteria and11/11 roots, formal=false. Do not repeat L1 or the
accepted13-day E production trajectory for this accounting work.

This slice implements preparation and dispatch binding, **not native transfer
completion**. It adds no executor, planner, automatic activation or training flag.
The automatic active-resource barrier remains in place.

## One Original Preparation

`CropTeacherSeedTransferSupport.Open` binds the original v2 run request, full
current snapshot, base ledger, crop commitment, new step marker and selected
owned chest reservation. It shares source hashing, full supply validation,
deadline and conservative idle-recapture rules with the purchase scope through
`CropTeacherSeedSupportSource`; purchase-only shop/funding restrictions stay in
the original purchase scope. Chest-only supply needs no fabricated shop intent.

The original `CropTeacherOwnedSeedSupply.TransferIntent` and
`MaterialTransferProjector` determine the actual transfer quantity and native
destination allocations. Native chest authorization, location, lock, capacity
and special-chest gates are not widened. Only the selected crop's reserved
quantity moves; the native source stack can include another owner's share.

When one owned claim must fill several player slots, the original claim is
partitioned conservatively in the **same original portfolio preparation commit**.
Its ID is retained for the first allocation; additional IDs extend that ID with
the step and destination slot. The original ID sorts first, so the old service
shrinks it before inserting the remaining partitions. Existing/historical IDs
are never reused. All partitions preserve the Teacher owner, goal, seed and
purpose, and sum to exactly the original quantity. No native movement occurs
while partitioning and no other owner's row is changed in preparation.

The same original preparation projects all affected claims through chest slot
compaction, including passive claims belonging to other roots. Out-of-scope
inventories remain untouched. Protected output custody is not reinterpreted as
crop seed supply.

The clone-only preflight is not authority. The real commit reply must independently
replay in full, at its real returned timestamp, before an immutable
`CropTeacherSeedTransferMarker` can produce an action. Original durable commit
reply lookup can recover that exact reply after repository restart; it performs
no second mutation and does not imply that native transfer happened.

## Original Compilation And Dispatch

The marker emits one `executor.transfer_material` action for the original
`ActionQueueCompiler`. Its typed native intent and execution payload still come
from the old binder/projector/compiler. The new `crop_seed_transfer_*` namespace
binds the crop, Teacher owner, step, ledger revision and exact moved claim IDs
to the original prepared-transfer SHA.

The original dispatch readiness service has a separate crop-binding branch;
the old machine-acquisition and Museum custody branches are unchanged. Crop
actions cannot mix those controls, change owners/quantities, lose the prepared
binding or downgrade leftover crop controls into unreserved legacy execution.
The existing execution-JSON/projection checks still run. Ordinary movement can
rebind the command source through the old dispatch path without resending the
preparation or revising the ledger.

## Evidence And Exact Next Work

New protocol tests exercise the actual original ledger service, compiler,
dispatch evaluator and durable repository. They cover one/multiple destination
slots, passive compaction, exact ownership, blocked native constraints,
corrupted complete replies, immutable inputs, parameter drift/downgrade,
repository restart and original reply recovery. The source fixtures are
synthetic protocol fixtures, not new native gameplay evidence. Final counts
and exact-head CI are recorded in the PR closeout.

Next, consume the retained preparation through the original native queue receipt,
native slot projection and `SettleSupportingTransition`. Verify the entire
before/after resource boundary, all destination allocations and passive claim
compaction; retain physical artifact hashes and independently replay the complete
settlement reply before reading new owned supply. Do not use the old
single-destination acquisition proof by relabeling crop seed transfer as machine
input or fabricating an acquisition graph. Use the original low-level receipt,
projector and settlement primitives instead; no second executor.

Then connect exact owned-slot planting consumption and lifecycle/cancellation
release. Finish the unique managed activation/resume owner and original journal
recovery; only after all consumers are connected may the explicitly versioned
managed entry run a small real native boundary sample. Joint calendar/aggregate
budget/cross-root continuity, Provider/model IO and formal manifest/preflight
remain afterward. Student fresh-save21/21 remains post-training evaluation.

Raw logs, test artifacts and `local-data` stay outside Git. No game, server,
worker, model API or training is required by this slice.
