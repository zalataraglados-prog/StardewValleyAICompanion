# Current Production Snapshot Handoff

## R2 Shared Crop Resource Budget

PR502 passed all42 final CI steps and merged as e39dd1e5. Its E source checkpoint
remains6/6 and875g, with the original failure preserved; do not rerun13 days.
The original dependency graph hash is2b937d95dc0bc8da3fe30510c963ef6c529087b50be6db9cfa2d557faf5ca928.
Its global obligations remain distinct from the accepted conditional L1 methods.

One actual L3 integration gap was reproduced: the existing crop Teacher validated
BaseStrategyLedgerPath but evaluated seeds and purchases against unreserved stock
and total wallet money. Nine new assertions failed before the fix. The same
Teacher now passes its once-read, identity-checked ledger to the existing material
supply projector and acquisition currency reader/native currency supply projector.
All active reservations count, even with the same decision ID. Cancelled/completed
claims do not consume current supply; other currencies are validated separately,
not subtracted from gold. Overcommitted/missing-slot/missing-balance evidence fails
before a production decision. Only the player section is serialized for the reused
currency reader, never another full world snapshot. No new ledger/planner/executor.

The intentional active-crop/layout rejection introduced by864fef95 is unchanged;
its Git diff and historical JSON context were checked. No-base-ledger and no-active-
currency evidence requirements stay unchanged. The real E Teacher decision rebuilds
byte-identically as c683188e8a429b1680ae9f4862dee6656a761572f3c121f1fc856d9bd272fe29.
37 focused tests (14 new), all2428 Core tests and original complete hermetic pass,
with zero skipped tests. Final CI is recorded at PR closeout.
The first new currency fixture omitted the original reader's confidence field;
only that fixture was corrected, not the shared reader or its authority contract.

This fixes budget consumption at selection, not resource locking for the new plan.
Next after release: inspect/connect selected seed stock and purchase-cash ownership
through the original ReservationPortfolioLedgerService atomic commit, then compose
the original DailyPlanCompiler aggregate budget with joint-calendar/time-energy
and selected-source/continuity owners for L3. Do not add a second scheduler or
mistake a per-route cost vector for a fully scheduled day.
E's separate offline-finalization-summary is not
the original v2 passed runtime summary required by the stronger segment builder;
do not silently relabel/import it as that artifact or claim a Day-0 schedule.
Reusing its exact checkpoint/native sources must retain their actual scope.
Then replaceable Provider/model selection and formal data/manifest/preflight.
L1 remains19/19,11/11;formal=false. No game, worker/API, server or cleanup here;
all raw/test artifacts stay outside Git, and local-data is never staged.

## E Native Cycle And Bounded Offline Closeout

PR501 is accepted at c01ce0be;499/500 are closed and owned merged refs removed.
New hidden/silent E used the original current-v6 input, not D memory. It acquired5
seeds, prepared/planted5 tiles, watered50 times, crossed12 native growth-day save
boundaries and harvested5 cauliflower. Five original shipping deposits and one
more native sleep settled5 units at175g,875g total. Original source save remains
68ddc8dbbe09382f2e7ba1c9d5d1e8dd6d0ffaef59acf9367415b4678f59366d.

Original lifecycle/retention checks passed, but offline proof children reached
roughly3.2/3.4GiB while PowerShell retained about2.7GiB of frame graphs. Available
RAM approached0.5GiB. A temporary1.5GiB managed-heap cap made terminal rebuilding
fail explicitly with OOM; the original finally exited the isolated processes.
No attempted manual stop found its already-exited proof PID; no manual process
termination was executed. The temporary generated runtimeconfig change is restored.

The lifecycle builder now shares only fully loaded, whole-envelope canonical
content identities within that one call. Actual E has120 frame references but61
distinct logical frames (about539MiB duplicated logical data). No claimed state
hash, CAS index or path-only identity can authorize sharing. Original source
paths/SHA, receipts, continuity, calendar, resources and final outcomes still verify.
No global cache/parser/executor/Teacher is added. Only crop orchestration opts in
to1.5GiB through STARDEWAI_BOOTSTRAP_GC_HEAP_HARD_LIMIT. The original helper's
unconfigured default stays unchanged; an opted-in budget applies only when
neither explicit DOTNET nor COMPlus limit exists. Environment restores even on
failure. The game heap is not changed by this policy. Crop orchestration drops
unused full captures before proof reconstruction and collects once at that barrier,
never per frame. Its end-to-end memory improvement still needs future native sampling.

Using the same retained E sources with no game restart, terminal public Build and
the original checkpoint pass under1.5GiB managed heap. Checkpoint is6/6,pending0:
2bc8f05a7dfb0d548d20cf24dada231d647080122fc7aae69794cf80bac9aff5.
Terminal proof is46a8835343ffbe5b028a3adc46e5f684878ca9531d6ec424a712be2d69af175a.
Lifecycle public Build is byte-identical before/after optimization:
e9d092069378cd093c23efe42a92043726cc95dced3ec0a3f987e7ef854733ec.
78 focused policy/environment/source-lifetime checks, original snapshot20 and
control52 plus complete hermetic pass. Final CI is recorded at PR closeout.

Raw root: I:\StardewAITraining\money-resumption-20261107\current-production-runs\money-current-production-native-20261107-e.
evidence/offline-finalization-summary.json records recovery separately. Original
failure.json remains byte-identical,d8d37885d7b34fa0b2987743bfa7dfd1d1d01e978bf52c6433de584e71fa6be3.
Do not replace it with a passed runtime summary. This is complete selected-cycle
evidence, not an uninterrupted zero-failure run, historical v22 conversion,50k
threshold, fresh-save Teacher21 or Student training. No raw artifacts/local-data
or external recovery callers enter Git; L1 remains19/19,11/11;formal=false.

After accepting this patch, next is R2 L3 composition of accepted local capabilities
and selected production: cross-root reservations/cash/materials, joint calendars,
continuity and selected-source schedules/independent replay. Reuse original owners;
do not recollect L1 or turn an optional route into a universal prerequisite. Then
verify replaceable Provider/model experiments and freeze formal dataset/manifest/
preflight. Student fresh-save21/21 is a post-training evaluation, not its own entry
condition. Stage2 Perfection and humanization remain later product stages.

## Verified Audit Repairs499/500

PR498 passed all40 final CI steps at42354205809 and merged as50349056;
the merged tree matches its tested head. Its original native-control route
proof compatibility is complete, not a native sleep/rollover pass.

Issue499 adds Number kind guards before the three TryGetInt32 calls in the
original shared fish-progress reader. Wrong types now reach the existing
unavailable_exact_denominator structured rejection. Exact72, species identities,
aggregate/missing-list agreement and previously-caught non-regression remain.
Issue500 aligns only the corpus phase gate with its existing native verifier:
sleep advances exactly one day, automatic wedding story stays on the same day.
BuildRun still independently rebuilds the complete native source; exact
queue/item/nonce/option, complete wedding, original partitions and non-label
control roles remain. No executor/planner or raw evidence changes.

The147 focused tests pass with no skips, including24 type cases, both wedding
phases and nine story-identity/native-proof negatives. The old three-partition
corpus replay and final exact-head CI are recorded at PR closeout. Fixtures are
not new native evidence. Close499/500 only after acceptance, then resume the
original current-v6 production/first rollover/maintenance/retention/settlement
and R2 integration. No accepted L1 recollection;19/19,11/11 and formal=false
remain unchanged. Raw data/temp stay outside Git, no worker/API/game started.

## D First-Day Progress And Control Preflight

PR497 is accepted at `5cc3b42c`, exact-head CI42351397889 all39 steps passed;
owned refs were removed after tree verification. New hidden/silent D used the
original current-v6 input, not C memory. It independently acquired5 seeds, routed
home, prepared the site and executed5 plant plus5 water steps, both batches
applied/success/verified. No full lifecycle, harvest or settlement pass is claimed.

Its first sleep source was captured during map-transition control settling.
The original Loop correctly substituted one30-tick verified wait, reported
primary_training_target_not_met, required_verified_actions1/verified_actions0 and
exit2. The fixed sleep caller then rejected it. Original save SHA is unchanged;
owned game/backend exited and D's raw/failure artifacts remain outside Git.

`18b59881`/`40dad9f6` and restored Oct28 JSON around113500-113600 explain why
observation is not a training work success. Do not change that accounting,
require zero work actions, broadly accept exit2 or force actor control. The
existing Rusty save-boundary selector is extracted once into RuntimeNativeControl;
its compatibility wrapper retains selection/error behavior. Only the two crop
sleep callers add bounded preflight through that selector, original ranking and
original exact-observation DailyPlan helper. At most4 observations, unique safe
candidate, same day and source progress are required. Every wait is appended to
the original supporting/settlement route before rebinding sleep's source.
Original sleep command IDs, compiler and assertions remain. Review caught that
both original route proof paths required movement in every queue. They now
recognize only the original NativeControlSettleRecovery's exact30-tick control
observation at unchanged readable location/tile; generic waits remain rejected.
Full original receipt, calendar, persistent-resource and destination gates remain.
Both proof paths pass a positive and seven negative fixtures. The complete
hermetic suite and retained actual D receipt/lifecycle/home-ingress replay pass;
all original D source bytes are unchanged, with no game restarted.

52 new protocol/source-chain checks,41 original Rusty evidence checks and50
original rolling runtime checks (including D's real receipt/report), plus20
snapshot and10 goal-forwarding regressions pass. A real backend contrast on
immutable D before/after sources compiles only control wait before, then exactly
one original executor.sleep after. It does not execute sleep or certify rollover.
Final CI/publication is recorded at closeout. No new local large test/game is
started merely to repeat the already-accepted acquisition while this patch runs.

Next: publish this tested caller fix, then fix verified issues499/500 (fish
aggregate type/error contract and original wedding-story corpus compatibility)
without native recollection. Only then resume the original current-v6 production run
for first native rollover, daily maintenance, retention and settlement. Do not
splice D's unsaved memory or modify original receipts. R2 global integration
follows; L1 remains19/19 and11/11, formal=false, no workers/APIs/training/server
or cleanup. Raw callers/data/temp stay outside Git.

## Known Context In The Shared Boundary

PR496 is accepted at `416ded1f`, exact-head CI42350460137 all39 steps passed.
Retained C was audited in one bounded pass:9 transitions and31 native steps,
no game or backend restart. Known non-persistent changes were tool-upgrade
location/control/service/derived fingerprint, Gourmand's global menu-clear flag,
and Community Center event playback/derived lifecycle stage. The existing
verified story action still owns actual mail/quest/unlock changes.

`1f2bc568` bridge code and restored chat distinguish implementing tool-upgrade
read/execution from obtaining native upgrade/claim evidence. `167eb5f6` and its
restored context explain the existing allowlisted runtime normalization: unknown
fields remain persistent. Neither source intent calls for removing tool progress
or forcing the executor to freeze unrelated menus/NPCs. The fix stays in the
original boundary projector, not the bridge, executor or raw snapshot.

Only known tool_upgrade.v1 route/control/Clint schedule and derived stage/hash
fields are normalized. Tool identity, level, source slot, pending tool/days,
upgrade price/cost, contracts and unknown fields remain. Strict comparison also
retains free slots/money and resource-derived capacity. Only the existing
expected-resource-mutation view removes their duplicate projections; its caller
still proves exact cash/inventory changes. Gourmand only loses menu_clear under
its known schema, not request/reward/mutex/crop facts. Community Center v1
lifecycle loses only derived stage/event_active, not seen, assets, mail,
completion or unknown fields.

Shared consumers are Full Shipment recovery, crop acquisition/lifecycle, Money
resource-neutral routes, counterfactual source audit and replay source binding.
No other domain is exempted; no new hash cache, execution mode or second proof
system is introduced. These boundary hashes are internal comparison values, not
a new serialized authority. Existing hermetic cases cover these consumers;21
new cases compare both strict and expected-resource views, including real tool,
cost, mail/event/completion, request/reward/lock, unknown and unsupported-schema
changes. Explicit E build is zero warnings/errors; final full suite/CI is recorded
at closeout.

The original public Build now accepts the entire retained C acquisition:
`verified_exact_seed_shortfall_native_acquisition`, seeds0->5, cash450->50,
9 transitions/4 supporting/5 purchase slices, formal=false. Output SHA is
`413feac75f5ae7b026392119dc28e6aa1bce78b7bada7a1f3b033556b932183e`.
Original failure/raw receipts and source save are unchanged. This is not native
planting, retention, settlement or a full fresh-save Teacher21 proof.

Next: publish this tested shared repair, then use the original current-v6 full
production entry point for planting/daily maintenance/retention/settlement. Do not
splice C's unsaved memory or silently restore snapshot state. Keep all A/B/C
diagnostics, no local-root recollection, worker/API or training. R2 global
capability/resource/calendar integration follows; L1 stays19/19 and11/11.

## Applied Progress Follow-Up

PR495 is accepted at `6e5616e0`, exact-head CI42349146093 passed. C exited
after nine real same-day acquisition transitions: four opening waits and five
one-unit cauliflower purchases, total400g, inventory0->5, cash450->50. The
original save SHA stayed unchanged; owned game/backend exited. Its original
failure and raw data remain outside Git. This is not a full production pass.

The proof initially rejected the first wait. `b95c4ca8` and the restored Oct28
JSON around lines9530-9590 explain the intentional Loop contract: verified
partial progress stays `applied`, while candidate completion/success stays false,
so native story evidence is not discarded. Do not revert that producer or
rewrite historical receipt flags. Only the crop acquisition consumer opts into
this exact progress form; default strict validation and legacy blocked
continuations retain their original behavior. All original per-step identity,
freshness, tick, state, effective-item and resource checks still run. Progress
requires explicit verification and pending continuation metadata; any recorded
completed objective must match the still-active identity. Exact purchase deltas
and the entire seed shortfall remain independently required by the old builder.

Explicit E build passed with zero warnings/errors. The first full hermetic pass
includes25 new progress/default/forgery checks plus the old acquisition and
continuation cases. Public replay of retained C now reaches transition4/step1,
then rejects `player.tool_upgrade`: observed FarmHouse->Farm, actor-control and
service status changed, with a derived fingerprint; pending/current tool,
inventory capacity and money did not change there. No shared persistent-boundary
rule has been modified or bypassed. Next: finish this patch's CI/publication,
audit that projection's historical contract, repair actual contextual false
positives without ignoring persistent or unknown fields, then the same current-v6
production/retention/settlement and R2 global integration. No local-root recollection,
worker/API or training. L1 stays19/19 and11/11, formal=false.

## Current Bounded-Goal Follow-Up

PR #494 is accepted at `35b83485`, exact-head CI42347958326 all39 steps.
Its I/O fix passed repeated native imports. B was deliberately stopped after
exposing a separate ownership conflict, not a renewed422: its one-step purchase
Loop inherited `grandpa_max_score_year3`, produced a new crop Teacher commitment
and entered many control iterations instead of returning the bound purchase.
Owned Loop/backend processes were revalidated and stopped; the parent's normal
finally stopped its game. Original save and A/B evidence remain unchanged.
The abort audit/PR494 closeout supersede the older running-B status below.

The existing Loop default dates to `2666b1f8` and is correct for normal companion
operation. Do not change that default, automatic Teacher policy or executor.
`Invoke-RuntimeDailyPlanStep` now has an optional trailing `GoalId` argument:
omitted calls keep the same parameters/default behavior, while crop seed
acquisition explicitly passes `daily.closed_loop`, binding execution to the outer
production owner. Its purchase ranking still uses `earn_money`; the goals are
not required to be textually identical. No second scheduler is added.

Ten argument/AST checks prove forwarding, old parameter positions/defaults and
the unique crop call path. Original20 snapshot I/O checks still pass. A real
backend comparison on the same immutable B source SHA
`102bd3debf2ce8c543cb540610ee4b31158f9aca03c1ad305a96d588a94a61a3`
reproduces the global Teacher handoff, while the explicit daily goal produces
no handoff and compiles the exact counter interaction. These are routing/compile
facts, not a native production outcome.

Fresh isolated C uses the same immutable current-v6 input and unchanged original
save, not stitched A/B memory. Its full native outcome remains pending until
production, retention and settlement verify. Continue this run and genuine
consumer failures, then the existing R2 global resource/calendar integration.
L1 remains19/19 and11/11; formal training stays false. Final C/CI results are
recorded at closeout; do not resume the intentionally terminated B process.

## Scope And Reason

PR #493 is accepted at `709bfe6c`, exact-head CI42344762784 all38 steps.
Accepted L1 union remains19/19 executable/split-complete/ready and11/11 roots.
It is not fresh-save Teacher21, global production/acquisition/calendar budget
completion, Student learning or formal training.

The next work audits retained production before reusing it globally. Historical
v22 has a passed offline finalization and5/6 checkpoint, with terminal settlement
pending. Historical pricefix-v10 stores48 transitions and6/6, plus875g outcome.
Those original files are retained. A current read-only replay is not identical
to their historical acceptance: current Teacher rejects the v1 layout where it
requires v2, and native checkpoint replay rejects transition16's continuation
terminal marker. Neither rejection is the old recursive tick-boundary bug.

`2c8dc42b` introduced real refill-source and standability facts for v2. Its diff
and restored chat explain why the new long-run dispatch must not invent those
facts from v1. `cb7bbb07` changed continuation completion-marker matching while
handling native story interrupts. Do not rewrite historical receipts, relabel
them as current supervision, weaken shared validators or discard their valid
historical outcomes. #338/#339 remain separate until their own scope is settled.

## Actual Current Boundary

The existing capture/initialization scripts collected a real current boundary:
`money-current-production-20261107-a`, source ProofFarm day6/time600,450g cash,
zero cumulative earnings, nine complete bounded v2 layouts for an80x65 Farm.
Maximum tile count was64; the existing v6 Teacher itself selected5 cauliflower
tiles with the real budget. No fixture inventory, price or earnings was added.
Source-save, isolated-save and Mod preservation checks passed; the capture game
exited. Callers/data are outside Git under
`I:\StardewAITraining\money-resumption-20261107`.

Current native run A executed its first original seed-acquisition queue, then
failed re-ingesting the next source. The retained file was a7,227-byte
`stardewai.content_addressed_json.v1` index, not the6.5MB logical snapshot. The
old PowerShell importer submitted that index to the backend's full-snapshot API.
Its original200/422 gate correctly rejected the wrong envelope. HTTP handling
then obscured the response by reading already-disposed response content.

## Fix Ownership

`3e79b3a7` introduced the importer for inline snapshots. Existing
`Read-RuntimeLogicalJsonArtifact` and the original Core reader already verify
compressed content-addressed artifacts. This patch adapts the I/O consumers to
that contract, not the backend, bridge, native queue or domain rules:

- Import uses the existing verified logical reader. The backend still checks
  acceptance and exact state hash; returned evidence path remains the original
  manifest, whose bytes are not changed.
- Crop callers explicitly select the Core assembly belonging to their Loop or
  Bootstrap. No ambient F/game-path discovery or second decoder is introduced.
- Relocated daily/layout snapshot copies contain verified logical JSON instead
  of a dangling relative blob reference. Inline text/lexemes remain intact;
  copying a file to itself verifies it without overwriting its manifest.
- Reservation, input, initial and final snapshot reads use the same reader.
  Decision/plan/proof JSON contracts remain unchanged.
- HTTP failure handling preserves PowerShell's existing ErrorDetails first and
  retains the original exception/status; fallback stream handling is unchanged.

## Verification And Continuation

Twenty focused checks cover actual original writer/reader use, inline lexical
compatibility, manifest identity, relocation/self-copy, corrupt input rejected
before HTTP/output, backend rejection/hash drift and disposed422 response details.
Original material-transfer20 checks and Core2380/2380 pass with no skips.

A real isolated backend probe reuses native run A's immutable manifest SHA
`ed1e66cf0d3af4875a3fe8876f0b6eae0a5a3e893901697781c6b3c9296564e4`:
raw index rejected, verified logical state
`25394a95c456ef23799e62c39a0a5133744df4cd7fd866878628acafae5fc789`
accepted, forged state hash still rejected. Original bytes are unchanged.

Run A's failure is preserved. New isolated B uses the same current v6 decision
and existing production/compiler/executor; it has passed repeated CAS imports
and continues native acquisition. This is not yet a completed production cycle
or a new global/threshold/Teacher21 result. Native games and heavy local work
remain serial, hidden and silent; no worker/API or model training is enabled.
Performance observation uses the lightweight endpoint, not repeated full scans.

Next finish the current-version production/retention/settlement evidence and
only its genuine consumer failures, then resume R2 global capability/resource/
calendar integration. Do not repeat accepted L1 sources, revive old blockers by
title, or treat a bounded production sample as a full Year3 plan. Final native,
CI and publication outcomes are recorded at this slice's closeout.
