# Managed Seed Scope Selection

## Accepted Parent

PR517 merged as6c83d3d909af6e842bfa4e9e226b6d39c068cf65. Final head c55072dd
passed CI42414916363, all42 steps, Core2443/Backend1044 with no failures/skips;
tested/main tree matched447e03f8. Its automatic review on2df783d identified the
new callback-origin issue, fixed and controller-reviewed with34/34 final runtime
tests. That earlier review is not a new automatic review on the final head.
The accepted lifecycle runtime does not implement cross-process phase ownership.

## One Selection Boundary, Original Implementations

CropTeacherManagedSeedScopes.Open binds an original CropTeacherRunStep to exactly
one existing support type: purchase, chest transfer or owned-slot planting.
It does not evaluate/rank candidates, construct another plan, issue HTTP, mutate
the ledger, copy a world snapshot, or introduce a second executor/recovery path.
Route/site/watering/harvest/sleep/closure steps are not silently reinterpreted
as seed actions. Lifecycle closure retains its separate explicit reason/mode.

Purchase uses the existing cached original acquisition intent and original
unallocated seed capacity; day, seed, shop, price and positive bounded quantity
must match, without attached foreign claim/batch/transfer fields. The old scope
exposes detached intent and quantity accessors only; original rules do not change.
There is no second parse of the full request or duplicate resource projection.

Transfer requires the selected original chest reservation and exact complete
transfer projection, original day/seed and sufficient remaining quantity.
Planting requires the original settled current source, crop/day/seed/batch;
it cannot borrow purchase/transfer's idle-recapture argument. The existing runtime
still performs the one original compiler/growth/owned-slot preflight before
mutation. Scope selection does not repeat that expensive preflight.

The selected original step is frozen as JSON, including its claim, transfer and
tile array. ValidateStep rechecks original physical input hashes and exact step
identity before control. Later mutation of nested arrays/intents cannot silently
change the scope. Existing support objects retain original input paths/hashes;
this is not a replacement for durable cross-process owner records.

BuildCropTeacherManagedControlQueueAsync validates this binding and forwards the
three typed optional scopes to the SAME BuildCropTeacherControlQueueCoreAsync.
Original story/menu preemption, compiler, runtime transaction, native prefix,
maximum eight-tile batch, settlement and lookup implementations are reused.
The unmanaged compatibility entry and main-loop defaults remain unchanged.

## Verification And Exact Remaining Owner Work

The final focused group passed20/20, zero failures/skips. Tests include intent
and capacity drift, changed physical source or nested step, exact original
chest projection, and selected planting batch. Real original HTTP/file repository
tests exercise selected purchase with all three lost replies, selected transfer
through the original control/compiler/settlement, and selected eight-tile planting
through the original control/compiler/native-format loop/settlement. The old
transfer and planting entry variants remain covered, not replaced.
These are synthetic protocol/native-format fixtures, not new game outcomes.
Full exact-head CI/review/merge results belong to PR closeout.

The [original runtime recovery boundary](money-crop-managed-runtime-recovery.md)
is the immediate consumer needed to accept a recovered phase without trusting
summary flags. Then connect the UNIQUE managed phase owner: durable run/plan/input identity, original
before retention, one pending operation admission, partial execution and restart
recovery across purchase/transfer/planting/lifecycle. Recovery must reconstruct
original proofs and query original journals/lookup before any new operation;
unknown native dispatch cannot be cleared by a fresh scope/output or blind resend.

The existing CropTeacherLedgerSourceBinder intentionally rejects active crops.
Do not weaken it to activate this selector. Original support idle recapture is
not a travel/maintenance/day-boundary proof. The owner must advance those states
through their original native evidence/settlement, preserving all foreign claims
and the last verified before source; changing a state hash is not a substitute.
Only after all paths connect should the versioned managed entry be opted in and
a small native boundary sample run, without repeating accepted13-day/L1 evidence.

Then original joint calendars/aggregate budgets/cross-root continuity,
Provider/model IO and manifest/preflight. L1 remains19/19 and11/11, formal=false;
Student fresh-save21/21 is post-training. No game/server/worker/model API/training
starts here. No local-data/raw artifacts are committed.
