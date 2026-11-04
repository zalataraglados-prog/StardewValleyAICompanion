# Rusty Key Time Budget

## Current Local Gates

Held museum donation and its one-step route/repair/wait continuation now pass the
remaining autonomous day budget to the original `DailyPlanCompiler`, before any
queue is offered to step preparation. The existing 19:00 recovery boundary is a
controller policy, not a claim about museum opening hours. A held item outside
this budget remains pending supply; it is not acquired again or marked donated.

`SnapshotGameClockBudgetReader` extracts the custody controller's existing strict
clock reader for reuse. Readable `time` / `time_of_day` fields must be integer,
valid Stardew HHMM in 06:00..26:00, and agree. Missing, unreadable, malformed or
conflicting clocks cannot become an unlimited donation budget. Ordinary held
work uses 19:00; the existing explicit mechanical custody transfer keeps its
26:00 limit. Backend's recovery policy and its safety buffer are unchanged.

The original `EventCandidateTimelineScheduler` now uses the same
`GameClockBudgetPolicy.TicksToGameMinutes` conversion as the day compiler and
execution budget validator. Previously it divided runtime ticks by 60, which
underestimated elapsed game minutes. Positive durations use the existing
700ms/game-minute, 60-update/second policy; zero remains zero, and unknown
duration cannot prove a closing window. The pinned 1.6.15 decompile was checked
at `Game1`'s `realMilliSecondsPerGameMinute = 700`, ten-minute accumulation and
`GameRunner`'s fixed-step setup. No new timing constant was introduced.

`WaitCost` retains its separate historical minute-times-60 encoding. This change
does not rescale it, extend waits, alter the close-bound comparison, generate a
second scheduler or remove the single bounded wait/fresh-replan barrier.

## Verification Scope

The original public held-donation fixture compiles to eight estimated minutes.
At 18:52 it fits; at 18:53 it is excluded with the original compiler budget
reason. Ten rolling checks include late work and five invalid clock shapes.
Twenty-four focused Core cases cover the shared clock reader, runtime-tick
closing boundaries, unknown/zero duration and unchanged deferred wait values.
Full Core governance passed 1180/1180, Backend 558/558 and the original
pre-commit rolling preparation suite 25/25. The rolling producer passed 44/44.
The reproduced pre-fix failures remain outside Git under
`I:\StardewAITraining\rolling-time-budget-20261104`.

This is synthetic local admission evidence, not a new native run, Teacher label,
criterion partition, fresh-save completion or training authorization. The
accepted native custody and geode/key traces remain accepted at their original
scope. `joint_execution_schedule_verified` and all frozen coverage counts are
unchanged.

## Current-Day Museum Route

The rolling held-donation branch now proves the complete route to the native
Gunther counter before offering a remote departure. It reuses the original
date-bound route producer and access-window resolver, including each later
connector's arrival, exclusive entry close, waits and the final adjacent stand.
The terminal interaction duration is the existing compiler budget, extracted to
`MuseumDonationBudgetPolicy`; it is not a new estimate. The counter completion
must fit the existing autonomous day budget. Native decompile confirms that
`LibraryMuseum.performAction` / `OpenDonationMenu` do not reapply the entrance's
closing clock inside the building, so the entry gate is not incorrectly imposed
on the entire indoor interaction.

The original museum candidate producer retains alternative first connectors.
The original route search can require the exact chosen first edge; that edge
must exist in the graph and match source/target/arrival coordinates. It cannot
borrow a feasible proof for another exit. Among legal remote candidates, rolling
uses full counter completion rather than the first connector's cost; this is an
explicit controller rule with its own versioned route-policy identity, not a
Teacher preference label. Unaffected local/acquisition policy IDs remain unchanged. Other consumers of
the shared route selector keep their original first-choice/lazy-sort behavior.

`selected_donation_route_schedule` is optional and bound into the independently
rebuilt original control report. The actual preparation/compiler/JIT still emit
only one current connector or bounded wait, then fresh-replan; no speculative future donation or
inventory is fabricated. Remote held routing requires the existing
`--route-timing-calibration` input but not acquisition axes. Local indoor donation
keeps its existing compiler/day-budget path. A blocked or unknown route retains
owned pending supply instead of creating another acquisition. Museum field
reads, movement calibration and date-map indexing happen once per invocation;
compact results are reused per first edge. No extra full snapshot, persistent
cache, native scanner, allocator or executor was added.

The rolling suite now has 49 named synthetic cases, including later-door
closure, a more expensive first connector with a better full route, missing
calibration, deferred opening, original preparation and raw schedule-tamper rejection. Focused
Core route/museum/clock/timeline checks pass 73 cases. The first PR head passed
full CI, including 1193 Core cases; final CI is pending after the prefix-boundary
fix below.
Backend passed 558/558 and the original pre-commit step preparation passed 25/25.
Initial diagnostic failures were a missing game version in the new fixture and
an incorrect boundary expectation: the original stand resolver already starts
adjacent to the first warp, so the second door is reached at 10:08, not 10:09.
The new deferred fixture also needed the existing `Buildings.Action` provenance
for a locked door. It then exposed a real integration omission: the held branch
discarded scheduled unavailable candidates before the existing wait compiler.
It now additionally accepts only the shared `ScheduledRouteWaitPolicy` predicate
already used by custody navigation. Source/calibration checks and all other
blocked-candidate rejection stay intact; no new wait executor or broad fallback
was added.

Self-review reproduced one required-prefix boundary: a target on the starting
map must not return a zero-edge proof before traversing the required first edge.
The original search now distinguishes its pre-prefix initial state from reached
states, allowing a real return to the same tile without admitting a bypass. Three
regressions cover unchanged unconstrained local routing, blocked one-way departure
and a valid two-edge round trip; the pre-fix bypass failed as expected.

Read-only replay of the accepted `custody-b3512d5f-1104` after-withdrawal snapshot
and revision-7 ledger passes both original public CLIs. Its native Blacksmith ->
Town -> ArchaeologyHouse evidence gives 09:34 at the counter and 09:40 completion,
with the original date's 08:00..18:00 museum entry gate. The emitted queue is still
one `executor.traverse_connector`, with no commit or execution/training authority.
Replay report SHA-256 is
`511a4127a3901d4a16f415be9a4899efc38be9b88d95785b3b6362974272a3a2`;
preparation SHA-256 is
`99db3113f65d81e83be987e7ffbfc1bffdf45cc624992c4d2a6b92f708b63a66`.
These artifacts remain under
`I:\StardewAITraining\donation-route-schedule-20261104`, outside Git. They prove
compatibility with retained native inputs, not a new native execution. Source
inventory/calibration hashes still match the original run.

## Complete Custody Interaction Budget

PR #426 merged as `68020b69` after exact-head CI (Core 1202/1202, Backend
558/558, rolling 56, preparation 27); its pending-CI notes below are historical.

Preparing the full withdrawal join exposed an existing underestimate: candidate
transfer timing used a 120-tick base, the primitive used `120 + quantity * 2`,
and the day plan treated transfer itself as one minute. The actual original
runtime already permits 180 updates waiting for the native chest menu and 120
waiting for mutex release. It checks successful completion before each strict
`> limit` timeout. Each wait can therefore succeed at `limit + 1`.

`Contracts.Execution.MaterialTransferBudgetPolicy` now shares these unchanged
runtime limits and the successful interaction bound `306 + quantity` ticks:
two inclusive waits, one click per item, and four state transitions (Move at
stand, Open, click-completion transition, Close). One unit is 307 ticks / eight
conservative game minutes. The primitive and candidate reuse this bound; the
day plan separates movement from interaction rather than charging the whole
candidate to movement plus a one-minute terminal. Invalid physical projections
retain unknown/blocked duration, and large positive budgets saturate instead
of overflowing into short/negative estimates.

The existing 16-tick-per-Manhattan-tile local movement estimate is unchanged;
it is not a proof of complete travel around obstacles. The next withdrawal join
must still use full calibrated date-bound routing to the chest, then this
interaction bound, then the museum route. Native clicks, menu/lock behavior,
global timeout, ownership/capacity checks, mechanical 26:00 boundary and
single-step fresh-replan protocol are unchanged.

The shared policy lives in Contracts because the runtime harness deliberately
does not reference Core. An initial local compilation caught a misplaced Core
reference; it was removed, and the original project dependency graph is intact.
Eight pre-fix budget counterexamples failed as expected; the final related
focused suite passed 109, including invalid-quantity and overflow boundaries.
Harness builds with zero warnings/errors against explicit `E:` and no mod
deployment. Final CI is pending. No native game run or training promotion is
claimed.

## Selected Acquisition Continuation

The current-day held-route slice passed exact-head CI (Core 1196/1196, Backend
558/558, rolling 49 and preparation 25) and merged as PR #425, `929686f3`.
Its earlier pending-CI wording above is historical.

The selected acquisition now retains `selected_acquisition_donation_schedule`.
It binds the original daily-time/energy evaluation hash, terminal location,
exact final stand and conservative completion time. A multi-interaction schedule
must agree with its last ordered terminal step. Missing endpoint/time evidence,
invalid HHMM, a different source clock or a mismatched final step cannot prove
the continuation. These are forecasts compiled from the original acquisition
axes, not observations that an item has already been acquired.

The same `MuseumDonationRouteScheduleBuilder` accepts that typed continuation
origin and reuses its original date context, calibration, route producer, later
entry gates, counter approach and interaction budget. It rejects a different day
or a departure before the source clock. Its per-invocation cache now binds the
entire start position/time/prefix request, so two acquisition completion times
cannot borrow one result. No snapshot/inventory mutation or synthetic held-item
candidate is used, and existing held-route output remains unchanged.

Only the already selected acquisition is evaluated; the original safe
elapsed/energy/stable-ID controller ordering and atomic admission stay intact.
An unproved same-day donation is not evidence that acquisition itself is
illegal: authorized custody and a later donation date remain possible. The
report records this gap without granting a joint certificate or silently
filtering the acquisition pool. Original step preparation independently rebuilds
this field and rejects caller-edited timing. Native acquisition and full joint
verification remain false; no additional execution/commit authority is added.

Focused Core passed 55 cases, rolling passed 56 named cases and original step
preparation passed 27, including two public precommit forecast-tamper checks.
These synthetic cases cover the final multi-step endpoint, missing/changed
clock or stand, a closed later gate and missing calibration. Final CI is pending.

Both original public CLIs also pass retained native acquisition-prefix inputs
from `custody-b3512d5f-1104`: the actual source is day 6 / 09:00, while the
admitted acquisition budget ends at Blacksmith (3,15) at 09:10. The continuation
starts there, reaches Gunther at 09:45 and finishes the original six-minute
donation interaction at 09:51. It does not sum a second route starting at 09:00.
The preparation still selects only original `terminal_acquisition`, with no
compiled donation queue or execution/training authority. All retained JSON
source bindings match the original run; there is no new native execution.
The previously accepted held replay is byte-identical after the shared builder
extension (`511a4127a3901d4a16f415be9a4899efc38be9b88d95785b3b6362974272a3a2`).

Artifacts remain outside Git in
`I:\StardewAITraining\acquisition-donation-schedule-20261104`:
`native-acquisition-replay-01.json` SHA-256
`0a18178effcb2f28a5092db16f949b1a3d847efa4b25767e96d12c472074595d`,
`native-acquisition-preparation-01.json` SHA-256
`66f16609a2969507e0fa00d3b3b3712caa03c6cee43fa199bb9fc5510af5c99a`.

## Selected Custody Continuation

PR #427 passed exact-head CI (Core 1214/1214, Backend 558/558, rolling 56,
preparation 27) and merged as `d3e5b75d`. Its earlier pending-CI notes are
historical. The selected stored-output control now retains
`selected_custody_donation_schedule`, binding the original reservation,
position-history hash, destination and actual selected candidate.

The same museum schedule builder composes three sequential stages using one
original date context/calibration: complete chest approach, shared native
withdrawal interaction, then original museum continuation. Local transfer
must use the exact original compiled adjacent stand. Remote navigation must
traverse the actual selected first edge; an easier unselected exit cannot lend
its proof. The original producer's exact-stand mode handles local arrival;
remote arrival uses its ordinary adjacent-stand mode. Only the shared interaction
ticks are added after arrival, not the candidate's whole movement estimate.

Later doors and counter completion are evaluated at the resulting sequential
times. The original execution still contains only its immediate transfer request,
one connector or one bounded wait, then fresh-replans; no forecast is converted
to a multi-map queue or inventory mutation. Original whole-control precommit
rebuilding rejects edits to the new report. Only the selected custody is analyzed,
reusing the existing parsed material graph without extra snapshots or scanners.

This is a conditional current-day **route/timing** proof, not future physical
transfer authorization, a native withdrawal receipt or the complete joint/key
certificate. `requires_fresh_transfer_preflight=true` and native verification
remain false. The original capacity, authorization, reservation commit and native
settlement checks remain mandatory. Missing timing/date/calibration, or a selected
clearance step whose post-clearance geometry has not been freshly observed, stays
explicitly unproved. Existing legal withdrawal/navigation may still proceed and
fresh-replan; this does not manufacture another acquisition or conclude that a
later donation date is impossible.

Focused route/museum checks passed 63 cases; original preparation passed 34,
including local, remote, deferred first door, later closed door, missing date/
calibration and raw-control tamper cases. The critical counterexample admits a
direct museum trip but rejects the same trip after the extra withdrawal delay.
Final CI is pending.

Integration review also reproduced a real runtime omission: the first lightweight
`New-RuntimeRustyKeyPlanning` call knew `RouteTimingPath` but did not forward it,
so remote held stock could stall as calibration-missing without entering the
acquisition pipeline. Its common build/preparation arguments now include the
existing calibration path. Four actual lightweight CLI paths (remote held,
local custody, remote custody, deferred custody) require ready route evidence
without rebuilding acquisition axes. The pre-fix held case failed as expected;
the full runtime orchestration suite now passes 39 checks without a game.

Retained native `custody-b3512d5f-1104` before-withdrawal snapshot and revision-5
ledger pass both original public CLIs. The bound Blacksmith (5,17) chest uses
stand (5,16): arrival 09:00, withdrawal complete by 09:08, counter by 09:42,
donation complete by 09:48. Preparation remains `custody_transfer`, with the
original request and no compiled donation queue or execution/training authority.
The original atomic request and source state match the retained control exactly;
the held-route public replay also remains byte-identical at SHA-256
`511a4127a3901d4a16f415be9a4899efc38be9b88d95785b3b6362974272a3a2`.
No new game run is claimed. Local artifacts outside Git:
`I:\StardewAITraining\custody-donation-schedule-20261104`, replay SHA-256
`c7fe56180f10c438d4a572718d2775583873a970d4d590745b07708bf318a8f1`,
preparation SHA-256
`75732df09a8e3496b6132f8535eca243deafce92d78894c4422dd2c110e3827f`.

## Owned Supply Evening Continuation

The rolling controller can select `owned_supply_deferral`, not a new executor,
when enough distinct missing museum requirements are already owned and the
original current-date donation/compiler proves a time-only exclusion at or after
19:00. Eligible exclusions are the original aggregate autonomous budget, the
complete-route day budget, or resolved connector closure before arrival. A
closed first door must also have its native close time and the same fully bound
date/calibration route evidence. Unknown geometry, unresolved gates, missing
calibration/date, changed museum progress and ownership failures do not grant a
recovery fallback. Another useful acquisition is not suppressed when owned stock
cannot cover the live remaining threshold.

Selection reuses `recovery.stabilize_day`: one original return-home/repair step,
ordinary terminal sleep, an exact already-open Sleep prompt, or the existing
30-tick native-control-settle observer. It never supplies
`control_plane.native_save_boundary=true`, closes an arbitrary menu, chooses a
profession, or fabricates a next-day map. The same daily-plan/action compilers
produce the bounded queue; preparation independently rebuilds the whole control.
Runtime uses the existing daily-plan adapter, retains the latest ledger and
fresh-replans after each receipt. Its special verified-observer handling remains
restricted to the existing exact native-control contract; failures stop without
native retry.

Deferral binds the source day, currently owned requirement IDs and exact recovery
candidate. It explicitly reports `future_date_route_verified=false`. Ordinary
overnight recovery on day D must leave donation on D+1 and the real key reward on
D+2 before exclusive day 224 (D <= 221). A same-day native-control observer does
not consume an extra night. These are earliest-resume/deadline checks, not proof
that the future museum route will be open. A fresh next-day source must rebuild
that route and the original physical/custody authorization.

Local verification passes 54 preparation cases, 45 actual-CLI/synthetic runtime
checks and 137 focused shared museum/recovery regressions. It covers resolved
first/later door closure, missing date/calibration, changed progress, insufficient
owned stock, unknown/foreign menus, the last legal overnight day, exact sleep/
native-control continuation, deterministic rebuilding/tamper rejection and an
independent synthetic next-day route source. Explicit E-runtime Release build
has zero warnings/errors; EVD-338 is unchanged. No game, HTTP server, new native
evidence, worker/API or training was run. Raw artifacts stay outside Git at
`I:\StardewAITraining\owned-supply-deferral-20261105`.

## Remaining Joint Schedule

Continue the same Rusty Key primary. Reuse the existing target-date calendar,
route-date evidence producer, `FutureRouteAccessWindowResolver`, timing
calibration and day-budget compiler to join the selected acquisition's real
terminal position/completion time and any authorized custody withdrawal to this
museum route. The selected current-day acquisition forecast now supplies the
first of those joins, and the selected custody branch now supplies the complete
current-day withdrawal route/timing join. Fresh physical transfer/native outcomes,
post-clearance fresh geometry, retained custody across a legal later donation
date and the actual following-morning handoff remain.
Do not clone/mutate the snapshot to invent future stock or pretend
the current-date map projection is a future-date source.
Use native date-bound gate evidence, sequential arrival times and conservative
travel bounds; summing independent route costs or checking only the first
connector is not a joint certificate. Unknown later-map timing stays explicit,
without dropping items from the 95-item denominator.

This is the selected rolling continuation's scope, not a requirement that all 95
acquisition routes be available today. An unrelated blocked route cannot prohibit
one valid next step. Current-day route readiness is not the full acquisition,
custody, donation, overnight and reward certificate; the existing full-threshold
`joint_execution_schedule_verified` flag remains false.

Donation need not occur on the acquisition day. A legal later date must retain
custody and leave the real next-morning reward handoff before exclusive day 224.
No future inventory, gate, calendar or Teacher preference may be fabricated to
make that schedule pass. Then close this criterion's independent Teacher/native
partitions and actual pre-Year-3 outcome. Do not repeat accepted local/native or
owner-visual gates as prerequisites for this remaining work.
