# Original Owned Crop Route Execution

## Accepted Parent And Scope

PR527 accepted completed-route consumption/rebind on the same owner. This successor
adds execution-before-result ownership and the actual original compiler/execution
adapter. It does not replace the executor, route finder, old default entry, resource
settlement service or original aggregate lifecycle validator.

The original compiler intentionally emits **one connector per fresh replan**
(`ActionQueueCompiler.Routing.cs`). A valid intermediate arrival is not the final
crop/home goal. The new versioned scope records both targets and binds the actual
hop to the independently replayed original compiler output. The old completed-route
entry still requires its original final destination and remains compatible.

## Preparation And Identity

`CompileAndAdmitOriginalRouteAsync` holds the existing owner gate, rejects pending
or unrestored heads before calling the original pure compiler, and admits on the
same sequence/OS lease. Its trusted callback must not re-enter owner APIs. The new
Program adapter refuses existing plan/queue/native output slots before compiling;
it cannot overwrite retained work when a caller repeats an iteration.

`Preparation.Open` shares the same original source/resource/layout reader and file
guard with the accepted completed-route consumer. It independently invokes the
original `ActionQueueCompiler`, compares the whole compilation except generated
queue ID, queue-item IDs and primitive step IDs, then pins the **actual** file and
IDs. Targets, actor, metadata, time budgets, command parameters, preconditions,
diagnostics and steps are not removed from comparison. Those opaque IDs cannot
change after admission. There is no second route algorithm.

The original plan and full before/ledger, current active covered commitment IDs,
model plan, actual queue, exact run/executor/candidate/day, final target, expected
connector destination and predeclared native receipt/after paths are bound before
dispatch. Active IDs are independently derived from the accepted original ledger
and original plan, not caller-selected; all resource owners still undergo the
original full supply validation. Completed/cancelled covered crops are not
incorrectly required to stay active forever.

This neutral ingress entry admits the ordinary connector kinds exposed by the
existing bridge route graph (`warp`, `door`, `action_warp`, `touch_action_warp`,
`locked_door_warp`, `magic_warp`). Native carriers with unproven costs require their
separate original resource/settlement scope **before** dispatch; they cannot spend
other roots' reservations and merely fail neutral verification afterwards. No
global option/candidate is deleted or labelled negatively by this local gate.
Procurement/owned-chest navigation remains in original purchase/transfer scopes.

## One Native Invocation And Original Loop

`ExecuteOriginalRouteAsync` checks the full original backend base and retained
inputs, writes the original intent and a create-new native invocation claim before
calling the original execution callback under the same gate. The claim explicitly
has `native_request_performed=null`: it is not proof of either zero or success.
An existing claim is never redispatched. The callback is trusted host orchestration,
not a sandbox or model/API permission grant.

The explicit Program adapter calls `BuildCropTeacherControlQueueCoreAsync` and
`ExecuteRuntimeTestHarnessCoreAsync`. Actual selected story/dialogue/sleep/capacity
interrupts cannot masquerade as the route. The old route snapshot default remains
unchanged. Only this new entry requests the actual `full` after boundary needed for
resource conservation, through the original capture path, not a route view padded
with remembered fields and not a per-frame capture loop.

Completed native data reuses original identity, queue receipt and resource-neutral
route validation. The expected end location is the **compiled connector's** target;
`route_target_reached` separately says whether it equals the original final goal.
Native outcome persistence verifies claim/intent/complete files and pins hashes;
flags alone are insufficient. It does not complete a crop cycle or emit a Teacher
preference label. A wrong path, bad receipt or resource drift never rebinds the ledger.
Incidental pickup or clearance with persistent resource deltas must use the original
attribution/settlement boundary (#236), not relax this neutral ingress validator.

## Recovery And Original Marker

- An admitted but unclaimed operation can invoke its original native loop once.
- An existing native claim with unknown/incomplete output is retained and blocked,
  not converted to zero or retried.
- Complete original receipt/after files can be independently verified after lost
  callback acknowledgement; a missing result record is appended without native
  replay. A retained bad result is not overwritten.
- Before marker invocation, `RunSupportingRouteAsync` can continue that verified
  native boundary and make its first original journaled marker call once.
- After marker invocation is claimed, pending/head recovery uses the original
  journal/retained reply or read-only lookup, independent full replay and final
  authority, exactly as the accepted parent. No marker resend or new fake proof.
- Acceptance pins native claim/result, original marker claim/reply and source head;
  it records intermediate/final arrival separately. Reopened accepted heads must
  restore original authority before the next admission.

The owner still owns every phase; no nested owner or replacement controller was
introduced. Shared source/file checks were extracted only from PR527's new consumer.
Old seed/lifecycle protocols, shared executors and old default read rules remain.

## Validation And Required Successor

The previous35 route checks initially remained green. New fixture-only failures
were missing navigation/strict provenance metadata and a required menus domain;
only the declared test source was completed, never the old fact gate. Original
compiler/owner tests then passed2/2; the actual original execution-loop HTTP fixture
passed with a constant bound of1-3 full reads for one native request. Expanded53/53
passed and the later two-leg/restart set passed19/19. Related regression passed
638/638; the final focused set passed57/57, including semantic/opaque-ID mutation
guards (zero failures/skips). The checkpoint matches EVD338. Exact-head CI/review
are reported separately in closeout; local checks do not substitute for them.

These are native-format fixtures and actual backend HTTP/original execution loop,
**not a real game run**. Automatic managed activation stays off. Next is original
maintenance (including refill/recovery) and native-save/day continuity, then the
explicit managed entry's bounded real native integration. Unknown zero-dispatch
route outcomes remain diagnostics, not fabricated native success. Original
calendar/budget/cross-root and paid-carrier ownership still belong to their layer,
then Provider/model selection and formal manifest/preflight. No global obligations
are waived or turned into local full-game prerequisites.

L1 remains19/19 criteria and11/11 roots; formal training remains false. Student
fresh-save21/21 remains post-training. No game/server/API worker or model training
was launched; logs and local-data remain outside Git.
