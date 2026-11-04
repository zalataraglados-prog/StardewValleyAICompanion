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
Core route/museum/clock checks pass 61 cases; full Core governance passed 1192
before the added indoor-after-entry-close boundary case (final CI is pending).
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

## Remaining Joint Schedule

Continue the same Rusty Key primary. Reuse the existing target-date calendar,
route-date evidence producer, `FutureRouteAccessWindowResolver`, timing
calibration and day-budget compiler to join the selected acquisition's real
terminal position/completion time and any authorized custody withdrawal to this
museum route. Do not clone/mutate the snapshot to invent future stock or pretend
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
