# Social Transparent Planning Foundation

> Controller review added during implementation. Read and satisfy every blocker in `CONTROLLER_REVIEW.md` before committing.

Task ID: `social-transparent-planning-foundation`

## Goal

Implement a bounded C# social transparency and planning foundation for `social.talk_npc` and `social.gift_npc`. The model may choose the social target and, for gifting, an exact owned item; the compiler must derive mechanical detail and reject illegal or incomplete choices. Do not pretend the native social executor exists.

Work only in this sandbox. Use `I:\StardewValleyAICompanion-decompile` as primary evidence and the Wiki only as secondary confirmation. Read `APPROVED_SOCIAL_AUDIT.md` as corrected scout input, not as authority. Do not edit the real repository, launch the game/SMAPI, deploy, build, test, run smoke scripts, mutate state/RNG, invoke NPC schedule loaders or dialogue/gift side effects, access credentials, push, reset, clean, rebase, switch branches, or touch user processes. The user is actively playing.

## File ownership

- Primary: `NpcReadAdapter.cs`, social candidate builder/evaluator files, option registration, C# `ActionQueueCompiler`, focused C# tests, and task handoff documents.
- Do not edit fishing/mining adapters or candidate builders.
- Do not edit `tools/StardewAI.RuntimeTestHarness/ModEntry.cs` or `tools/StardewAI.LiveTrainingLoop/Program.cs`; runtime social execution is outside this slice.
- There is no Python action compiler. All production changes are C#.

## Transparent input surface

Inventory and explicitly classify, with field-level source/status/completeness and fail-closed unknown/modded behavior:

- socializable NPC identity/master-data presence and current loaded instance/location/tile/bounding box/facing/movement/controller/busy/invisible/sleeping/event/festival/shop-service states needed for interaction now;
- exact current `CanSocialize` and `CanReceiveGifts` inputs/results only where the decompile proves the read is side-effect-free; otherwise reproduce the pure rule from transparent inputs or mark unavailable;
- friendship points/hearts/status, talked today, gifts today/week, last gift date, birthday, dating/bouquet, engaged/married/roommate/divorced/proposal and other relationship gates relevant to talk/gift legality;
- loaded schedule identity, control flags, entries, path/end behavior/provenance and whether it is usable for the current day; raw precedence inputs for green rain, island, passive festival, marriage/divorce, rain, season/day/week and special overrides;
- never call `TryLoadSchedule`, `checkSchedule`, `ClearSchedule`, path controllers, dialogue methods, gift methods, or any helper which mutates NPC/game state. If future schedule resolution cannot be projected purely and completely, keep future interaction windows unavailable and block those candidates;
- gift legality/effect inputs for every eligible owned inventory item: identity, slot, quality, stack, protection, quest-delivery ambiguity, universal and NPC-specific tastes, context-tag and exception precedence, birthday/Feast multipliers where applicable, daily/weekly limits, rejection/jealousy/divorce/special branches, expected friendship delta only when deterministic and evidence-backed;
- explicit required versus optional groups and recursive completeness. An available top-level domain may not contain an unavailable required child.

## Planning and compiler surface

- Replace/extend the preview-only social registry with parameterized current-state candidates for talk and gift. Candidate parameters must preserve exact NPC identity and gift slot/item/quality where applicable.
- Exclude impossible, illegal, unreachable, protected-item, ambiguous quest-delivery, exhausted-limit, sleeping/invisible/busy, incomplete-taste, incomplete-schedule, and incomplete-route cases upstream before ranking.
- Do not emit future schedule candidates unless their complete time/location/route window is derived from non-mutating transparent data. Waiting or route costs must not be guessed.
- The model output is strategic: action kind, NPC, exact gift choice when gifting, and bounded time/resource constraints. Mechanical stand tile, facing, native interaction, dialogue advance, and verification belong to compiler/executor.
- Compile a structured social plan envelope preserving requested target/item, live legality/taste evidence, time/route constraints, expected deterministic counters/delta, and required executor profile.
- The runtime queue must remain explicitly blocked with `social_native_executor_not_implemented`. Do not emit fake low-level actions or training rows.
- Remove guessed fixed social duration where this option consumes it; represent duration unknown until route and executor timing are available.

## Output recording contract

Define the fields a later native executor must record before policy training: item before/after and decrement, friendship points/delta, talked/gift counters before/after, dialogue/menu/event side effects, NPC/player location and tick/time, precise accepted/rejected/blocked category, primitive verification, freshness/state hash, and calibration-versus-policy label. This slice may define contracts/tests but must not claim runtime verification.

## Deliverables

- Focused C# tests for complete current talk/gift candidates and fail-closed limits, tastes, schedules, routes, unknown NPCs, protected/quest items, and compiler boundary. Add but do not run them.
- `evidence.md` with exact local decompile paths/lines for every rule/API claim.
- `transparency-coverage.md`, `WORKER_NOTES.md`, `test-results.txt`, and `risk.md` with honest static-only status.
- Static review and a bounded sandbox commit.
