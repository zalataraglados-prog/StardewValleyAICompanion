# Opt-In Native Output Observation

## Accepted Parent And Native Source

PR534 accepted the original refill compilation boundary at main3270e2d3,
tree6471553b,CI42459220203 all42,Core2458/Backend1395,no failures/skips;
same-head on-open review6718779948 had no findings and owned refs were removed.
That compilation remains read-only and does not grant native output credit.

The current E DLL retains SHA256
7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee.
Fresh Debris/Farmer/Item/Object decompilation establishes distinct native branches:
collect can succeed without inventory admission;Farmer can receive special items
without ordinary inventory space. In the actual implementation a partial merge can
return false even though inventory increased. A complete empty-slot insertion can
return true while the offered item retains its original Stack. The native comment
is not sufficient authority for either case. Neither boolean nor post-call Stack
alone proves the accepted resource quantity.

## Same Capture, Optional Read-Only Witness

The existing execute endpoint accepts optional action_origin_observation_scope_id.
It is restricted to original move_to_tile/clear_obstacle in training_singleplayer,
with bounded scope text and all run/queue/item/nonce/source/save identities.
The original live request validator must also admit it before observation begins.
There is no new endpoint,action,candidate,compiler,drop engine or runtime controller.
Default requests/results omit these null fields and keep their original response
task/exception semantics;no default observation completion task is allocated.

The original action-origin before capture is reused. Acquisition retention is
pinned by its original full allowlist/identity predicate,not enabled by observing
an ordinary repair. Observation-only finalization bypasses unnecessary acquisition
record construction. Original attribution/ambiguity/required-quantity rules stay.

Only the first admitted explicit scope installs two owner-scoped,read-only Harmony
hooks:Debris.collect and Farmer.addItemToInventoryBool. Outside an active scope the
hooks do no game reads. They never suppress native calls,replace arguments/results,
collect debris,grant items,move actors or change RNG. Installation failure is
unavailable and is not repeatedly retried;rollback targets only this observation ID.

The receipt binds a process capture-session ID and weak reference IDs for actual
debris/chunks,original request identity and capture ticks,before/after debris and
inventory aggregates,and actual nested collection/admission calls. Offered amount,
item_stack_after_call,inventory before/after and native returns are recorded as
observations,not converted into invented production or pickup credit.

The neutral post-call stack name intentionally does not mean unaccepted remainder.
For an offered stack of six,ordinary complete merge can leave Stack zero and add
six;complete empty-slot insertion can leave Stack six and add six;partial merge can
leave Stack four,add two and return false. No-inventory receive behavior can return
true with no ordinary inventory delta. Protocol fixtures preserve these independent
observations;they are not executions of those native branches.

## Original Client Opt-In

LiveTrainingLoop accepts --observe-native-outputs,off by default. It requires
--skip-training,training_singleplayer,the original runtime-test-harness executor
and executor feedback. Product/sandbox/coop/formal configurations fail before
dispatch. Only original move_to_tile/clear_obstacle requests get a scope;other
items in the same compiled queue retain their original unobserved behavior.

The scope is a bounded versioned SHA256 of a structured array of the complete
original run/queue/item/nonce/before/source/save/option identity. Long IDs and
delimiter collisions cannot overflow or ambiguously concatenate the scope. The
original request factory,journal and HTTP dispatcher retain the same endpoint,
nonce,body,action parameters and action/observation statuses independently. There
is no new dispatcher,automatic retry,policy candidate or native-success inference.
Directly constructed opt-in options are checked at request construction too.

One same-thread scope owns the hook context. Native RPC response waits for both
original action completion and final capture publication under the old timeout;
the original native task is not replaced. Capture faults,missing returns,context
drift,unknown item/runtime/nested item graphs and overflow are explicitly unavailable.
Limits:256 collection/admission calls,512 debris/inventory rows,4096 total exported
chunk references,64Ki characters per live item XML and1Mi characters per receipt.
No full world snapshot or per-frame scan is added. Completed records stop accepting
callbacks before publication. These observations end at native capture finalization;
later snapshot continuity/resource changes still require independent verification.

## Item Payload Purity And Existing Issue29

Only admitted exact vanilla Object payloads without nested item graphs are directly
serialized,with a bounded writer and UTF-8 text SHA256. This is actual serialized
item state,NOT the old normalized unit-state hash. No item is constructed or cloned.

Fresh native source exposed getOne -> Object constructor -> live NextBool in the
old unit-hash helper. Its original normalization intent is recorded by f3133e72,
6f21e240 and e847e04f. The new observer therefore reuses the native serializer but
does not call that clone helper. Existing helpers are untouched. The concrete path,
Panning sandbox boundary and required history/native equivalence work are recorded
in [existing issue29 follow-up](https://github.com/zalataraglados-prog/StardewValleyAICompanion/issues/29#issuecomment-6719205294).
That is not a completed global probe audit or grounds to discard frozen L1 evidence.

## Validation And Successor

Initial focused36/36,related97/97 and final incremental37/37 passed,zero failures/
skips;the final explicit-E native module builds with zero warnings/errors. Initial
build caught the installed Harmony API mismatch;rollback now uses its documented
method/owner-scoped Unpatch overload,not an assumed UnpatchSelf. Pure tests cover
scope/default transport,forged credit flags,partial/zero-admission shapes,bounds,
reference identity and both completion/failure orderings;source guards cover game
glue. These are not native hook/RNG equivalence evidence. Exact-head CI/review and
release are recorded in closeout. No game/server/worker/model training launched.

PR535 was accepted at main6774d22b,tree73290565,CI42463019944 all42,
Core2476/Backend1395,zero failures/skips;same-head review6719263449 had no
findings. The client successor corrects the new observation vocabulary/fixtures
against the same E binary and tests the original request factory/journal/HTTP
path. Its validation and release are recorded separately;it does not itself
establish native hook equivalence or activate managed production.

### Client And Native Calibration

Client focused/journal39 passed,then the final related Backend152 passed after
three direct request/options drift cases were added;Core observation22 passed.
Release Backend/LiveTrainingLoop/Bridge/Harness builds had zero warnings/errors,
explicit E GamePath,no deployment to shared runtime Mods. The initial Core test
placement was rejected by the game-free profile that deliberately excludes the
client reference;the new client tests now live in the existing Backend test group.

External output-client-native-b exercised original compiled clear/move with the
flag off/on:all four native primitives applied and verified;default fields stayed
omitted. Those scopes had no collection callbacks;pickup occurred between commands.
The first external launcher attempt omitted the original required-verified-actions
argument and was rejected before action dispatch;only that launcher was corrected.

The existing chop-wild-tree DailyPlan candidate in output-client-native-tree-b,
followed by an original compiled move toward a real remaining wood-drop tile,
exercised two more applied/verified primitives. The move scope captured16 actual
Debris.collect and16 nested Farmer admission calls,inventory wood5->16 and sap1->6.
Raw-wire journal hashes/nonces,receipt identities/tick intervals,weak debris/chunk
links and all16 actual XML digests were independently checked. Original source save
tree68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d
stayed unchanged,only two allowlisted Mods were loaded,and owned processes exited.
The earlier tree-only attempt had no callback coverage and is retained as such.
An external audit corrects a PowerShell null-array count in the raw B summary;
control callbacks are zero,not one. No source artifact was rewritten to claim success.

This is bounded native transport/callback calibration on copied fixture saves,
NOT paired RNG/item/world equivalence,partial-admission native coverage,production
resource admission or Teacher/training credit. In particular,the wood/sap already
present before the move must not be relabeled as receipts from that move. Logs,
raw snapshots,copies and audit stay outside the repository;formal remains false.

Next complete the remaining isolated item/world/RNG control verification and
inspect/fix the concrete legacy issue29 path with canonical identity preserved.
Do not repeat the now-calibrated transport or tree-to-drop callback sample merely
because the residual purity proof is separate.
Then consume the same pinned original refill compilation and actual resource/
provenance witnesses in the single persistent owner. Do not let observed status
replace the independent resource boundary or expand old neutral/watering gates.
Continue harvest/native save/day,bounded managed integration,existing global
calendar/shared budgets/cross-root/carrier,Provider/model and manifest/preflight.
L1 remains19/19 and11/11;formal=false;Student21/21 stays post-training. No new
criterion-local Day-0 gate is introduced. Raw/local-data are never committed.
