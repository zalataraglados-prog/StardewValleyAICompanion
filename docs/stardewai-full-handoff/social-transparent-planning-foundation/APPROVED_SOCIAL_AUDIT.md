# Approved Social Audit Input

The scout correctly identified these implementation blockers, subject to fresh decompile verification:

- Existing `NpcReadAdapter` exposes current-location positions, friendship summaries, and loaded schedules, but not complete interaction-now or future-schedule truth.
- Required schedule/control facts include loaded schedule identity/provenance, `ignoreScheduleToday`, `followSchedule`, route/end behavior, and green-rain/island/festival/marriage/divorce/rain/season/day fallback inputs.
- Required interaction-now facts include invisible/sleeping/controller/busy state and pure `CanSocialize`/`CanReceiveGifts` rule inputs.
- Required gift facts include universal/NPC taste precedence, tags/exceptions, item quality/protection, limits, birthday/special multipliers, rejected/jealous/divorced branches, and deterministic effect inputs.
- A later executor must record item decrement, friendship delta, talked/gift counters, dialogue/menu/event effects, precise result category, time/location/tick, freshness, verification, and training label.
- Unknown/modded NPC data, missing tastes, and unresolved schedules must fail closed.

Controller corrections:

- Ignore the scout's `backend/action_compiler.py` recommendation; it is a hallucinated path. The repository uses C# `ActionQueueCompiler` and C# option/candidate classes.
- The scout inventory is not exhaustive. Re-check every required field against current code and decompiled vanilla source; do not rely on this summary or old docs as a complete list.
- Do not call vanilla schedule/gift/dialogue methods merely to obtain a value unless the decompile proves they are pure and side-effect-free.
