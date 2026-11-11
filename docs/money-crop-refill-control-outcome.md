# Original Native Refill Control Outcome

## Accepted Parent And Current Native Source

PR531 accepted original watering compilation/one execution-loop invocation on the
same persistent owner. Headc1ce27b7 and mainec08ea9f share tree33102d0f;
CI42453157961 completed42/42, Core2443/Backend1357, no failures/skips, and
same-head on-open review6718021603 completed without findings.

The current E runtime DLL was hashed and `WateringCan` was freshly decompiled for
this slice. SHA256 is
`7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee`.
In native `WateringCan.DoFunction`, a valid refill tile fills `WaterLeft` to native
capacity; the stamina charge belongs to the separate non-refill watering branch.
Original `StartWaterCrop`/`ContinueWaterCropAfterRefill` can therefore refill a can,
then return blocked when the subsequent crop path fails, without watering a crop.
No original executor, tool animation, movement or runtime-default rule is changed.

## Distinct Native Side Effect, Not Zero Dispatch

The original positive planting/watering prefix rule still requires at least one
verified applied item. `Open` is unchanged in meaning and does not accept zero.
`OpenRefillControl` is an explicit outcome consumer using the same original source,
layout, receipt-step identity, watering/resource kernel and marker-rebind machinery.
The original owned preparation selects it only for a non-success receipt containing
one blocked native attempt; all other outcomes retain the original positive path.

Its separate header gate requires one actual blocked step, exact queue/run/executor/
candidate/source identity, attempted/pending counts, fresh tick/after, incomplete
candidate and exact terminal block reasons. Empty steps, a success/completed flag
or multiple blocked attempts are rejected. It never uses seed/controller
zero-dispatch evidence or infers that an unknown native claim made no request.

The physical gate proves an originally empty unenchanted can changed to the exact
unchanged native capacity. Original common resource verification then runs once:
no crop receives water/growth credit, stamina and all unrelated resource/unknown
fields remain unchanged, while original passive bowl projections must exactly
reflect actual water/tool/energy/status. Neither partial/absent refill nor a
nonempty/enchanted precondition can satisfy this witness. No ignore list is widened.

## Same Owner And Evidence Flags

The existing maintenance claim/result, marker commit, journal/lookup, whole-ledger
readback and accepted-head recovery are reused without another phase controller.
For this outcome only:

- `native_refill_control_verified=true`;
- `native_watering_prefix_verified=false`;
- applied count/tiles remain zero/empty and `complete_batch=false`;
- crop-cycle verification and preference/formal-training authorization remain false.

Ordinary positive outcomes keep their existing serialized fields. A forged refill
flag on a positive accepted summary is rejected. Restart first reconstructs the
actual native outcome before comparing all zero-credit flags/counts. Lost native
or marker acknowledgements recover from original retained evidence, never another
native call. The refreshed source can compile the next original watering batch on
the same owner sequence without pretending the previous crop work was completed.

## Validation And Required Successor

Initial related focus75/75 and expanded87/87 passed. The new checks cover physical
refill/bottomless behavior, missing/unattributed effects, false zero-dispatch and
completion shapes, native/marker acknowledgement loss, restart summary corruption,
the next original compilation and original execution-loop HTTP blocked receipt with
bounded full-snapshot action-boundary reads. Related regression/final CI/review are
recorded in closeout. Related regression was751/752: the only exception was
UnauthorizedAccess in unchanged backend File.Move during an old planting fixture's
marker setup, before this new receipt path. Final focused93/93 (including that
six-case old group and24 new checks) passed after the internal helper rename. The
permission anomaly did not reproduce; its origin remains unconfirmed and the
original failure log is retained. No storage retry/permission behavior was changed.
These are declared native-format fixtures and actual original
compiler/backend/loop protocols, not a real game run or Teacher supervision.

Next continue original refill-approach/control continuity, including its existing
route repair and attributed clearance/pickup effects, then harvest attribution and
native-save/day continuity. Do not call a resource-changing repair a neutral move,
disable the old repair to simplify tests or rewrite old full-cycle/idle/binder rules.

## Resource-Neutral Approach On The Same Owner

The explicit approach preparation reuses CropTeacherRefillApproachCompilation and
the SAME native trace/whole persistent-resource projector. It does not call a
resource-changing move neutral: compiled clear items reject BEFORE dispatch;
each admitted native move frame must independently preserve the whole original
inventory/currency/persistent projection. Unknown drift, water/crop/energy changes
and full-slot metadata changes reject, rather than widening a volatile-field list.
Existing repair planning stays enabled with its original budget. Repair/pickup
settlement and producer credit remain separate pending work, not silently skipped.

When observations were not requested, original raw request/journal/gzip/native
receipts are still required but optional calibration fields are not made mandatory.
If observations were requested, their original quantity/snapshot denominator must
validate; a missing observation or an unsolicited attached one is rejected.

Only this original owned preparation records maintenance_control_kind=refill_approach.
The original six-argument compilation constructor and ordinary watering intent
shape are retained. Dispatch/result/marker/lookup and restart use the SAME owner;
no replacement executor or second controller is introduced. New outcome evidence
is native_refill_approach_verified=true, native_watering_prefix_verified=false,
zero applied tiles, incomplete batch, and preference/formal=false. It has NOT yet
filled the watering can. A subsequent original watering/refill phase is still due.
Forged approach evidence on a different accepted outcome is rejected on recovery.

The owned helpers are adapters, not automatic proof that the top-level loop uses
them. Current DispatchCropTeacherAsync still explicitly validates UNMANAGED dispatch.
Joining these adapters to the original top-level run, exact resourceful clearing,
harvest/native save and a bounded real managed cycle remain required under PR551.
Declared HTTP/native-format fixture success must not be reported as that real run.

Latest focused integration79/79 passed, including original wire dispatch, native/
marker/acceptance acknowledgement loss, read-only restart, next watering compile,
forged approach flags and the14-case Museum recovery group. Complete local Backend
was1874passed/1failed/0skipped: that old Museum malformed-reply case exceeded its
15-second bound. The failure is retained; focused reproduction passed without
changing Museum, timeouts or filters in CI. Its cause remains unconfirmed, and
final-head complete CI is still required. The new outcome reuses the trace-owned
artifact guard rather than decoding/hashing the same full snapshots twice.
Only after these are joined explicitly enable bounded real managed integration.
Global calendar/budget/cross-root/carrier, Provider/model and manifest/preflight
remain; L1 stays19/19 and11/11, formal=false; Student21/21 is post-training.
No worker/game/server/model training launched; raw/local-data remain outside Git.
