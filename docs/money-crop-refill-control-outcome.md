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
Only after these are joined explicitly enable bounded real managed integration.
Global calendar/budget/cross-root/carrier, Provider/model and manifest/preflight
remain; L1 stays19/19 and11/11, formal=false; Student21/21 is post-training.
No worker/game/server/model training launched; raw/local-data remain outside Git.
