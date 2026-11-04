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

## Remaining Joint Schedule

Continue the same Rusty Key primary. Reuse the existing target-date calendar,
route-date evidence producer, `FutureRouteAccessWindowResolver`, timing
calibration and day-budget compiler to compose acquisition completion, any
authorized custody withdrawal, museum arrival/entry and counter interaction.
Use native date-bound gate evidence, sequential arrival times and conservative
travel bounds; summing independent route costs or checking only the first
connector is not a joint certificate. Unknown later-map timing stays explicit,
without dropping items from the 95-item denominator.

Donation need not occur on the acquisition day. A legal later date must retain
custody and leave the real next-morning reward handoff before exclusive day 224.
No future inventory, gate, calendar or Teacher preference may be fabricated to
make that schedule pass. Then close this criterion's independent Teacher/native
partitions and actual pre-Year-3 outcome. Do not repeat accepted local/native or
owner-visual gates as prerequisites for this remaining work.
