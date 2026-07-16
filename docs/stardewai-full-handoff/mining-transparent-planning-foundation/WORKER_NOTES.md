# Worker Notes

## 2026-07-15 Rolling Executor Closure

The blocked-executor statements below are historical. The current main branch compiles `mining.reach_depth` into one snapshot-bounded internal step and supports native stone mining, supported large resource-clump removal, native melee/slingshot/bomb combat, natural debris pickup, native food recovery, native ladder/shaft descent, and mandatory native exit for time, energy, or unrecoverable-health boundaries. Standing Mummies compile to melee knockdown before a fresh-snapshot targeted bomb finish; unsupported bomb receivers fail closed. The small model still emits only the high-level objective. Hidden/silent isolated run `runtime-mining-reach-depth-20260716-183347` closed levels 96 through 98 and terminal exit with five fresh, verified training rows. Hidden/silent isolated run `runtime-skull-cavern-shaft-20260716-221731` verified a native Skull Cavern shaft from 130 to 134 with exact health cost and one row. Quarry Mine uses the separate `mining.acquire_golden_scythe` objective, and Volcano Dungeon uses the separate `volcano.reach_caldera` objective; neither is an ordinary depth target. Remaining boundaries are arbitrary-depth/full-day time calibration, resource-clump and advanced-weapon runtime evidence, full Golden Scythe/Volcano loop evidence, and broader combat/loot combinations.

## 2026-07-13 Mining Transparent Planning Foundation

- Branch: `worker/mining-transparent-planning-foundation`.
- Scope implemented in this sandbox only.
- No game launch, SMAPI launch, build, test, smoke script, deployment, RNG mutation, mine generation, ladder discovery call, branch switch, reset, clean, rebase, push, or credential access was performed.
- No fishing-owned files or runtime harness files were edited.

## Completed

- Reworked `MiningReadAdapter` to avoid the mutating `Map` getter, read only already-loaded live fields, and mark incomplete map/collision/object/floor groups unavailable instead of complete.
- Registered `mining.reach_depth` as a parameterized mechanical option with bounded model parameters.
- Added reach-depth candidate generation gated on recursive transparent mining completeness, known target-depth validity, read elevator unlock progress, and an explicit unknown-cost block.
- At foundation-slice time, queue compilation preserved the target/resource/retreat envelope but intentionally blocked execution because cost and runtime support were unavailable. This historical boundary is superseded above.
- Added focused unit tests for option registration, recursive completeness, incomplete collision/object groups, exact action parsing, elevator unlock/continuation boundaries, optional reserve constraints, compiler boundary, and impossible target rejection.
- Replaced prior-slice handoff files with this slice's evidence, coverage, risk, and pending validation status.

## Validation

- Not run by instruction and active user-play constraint.
- Pending command list is recorded in `test-results.txt`.

## 2026-07-15 Controller Closure

- The original worker state above is historical. Codex subsequently replaced the unavailable collision/object/floor placeholders with decompile-backed loaded-floor reads.
- Collision rows now cover map geometry, objects, characters, terrain features, resource clumps, large terrain features, furniture, animals, and other farmers while excluding the controlled farmer. The cache is purpose-limited and refreshes no later than 30 ticks.
- Breakable stone/container durability, best-pickaxe hit counts, stone ladder previews, treasure-room state, floor gates, and current monster facts are available on a loaded `MineShaft`.
- `profile=mining` limits snapshot work to baseline and mining domains. Runtime profiling showed generic `locations` duplicated loaded-floor data and dominated payload/latency, so it is excluded upstream.
- Offline validation passed. No mining action runtime capability is claimed until the dynamic perfect executor is implemented.
- Controller follow-up completed the isolated E: smoke on a non-empty level 99 floor. Read-side serialization and latency now pass; only the action executor remains gated.

## 2026-07-15 Native Floor-Step Follow-up

- Added `MiningFloorStepPlanner`, which consumes only transparent mining groups and exact compact collision rows. It deterministically chooses a reachable ladder, kill-all monster, or stone and prefers a known ladder-producing stone.
- Added internal `executor.mine_stone`; it walks through the existing collision-safe input path and swings the equipped pickaxe through the native farmer tool lifecycle. It never invokes `Pickaxe.DoFunction` or removes a mine object directly.
- Runtime smoke `runtime-mining-snapshot-smoke-20260715-203940` verified two GoldPickaxe swings, health sequence `8,4,0`, natural object removal, and a matching after snapshot. A discovered feedback-order bug that initially recorded zero swings was fixed and rerun; the smoke now rejects non-positive swing counts or a health sequence without terminal zero.
- Do not expose `executor.mine_stone` to the small model. The model still emits `mining.reach_depth`; the generated executor owns mechanical floor steps and must re-read after each dynamic change.
- This was the boundary at the time of this slice. It is superseded by the rolling executor closure note above.
