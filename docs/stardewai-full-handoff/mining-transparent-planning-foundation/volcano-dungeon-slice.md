# Volcano Dungeon Slice

## Boundary

`VolcanoDungeon` is a separate runtime type and is never treated as a `MineShaft`. Its generated levels are `0..9`; level `5` is the rest/shop floor, and the level `9` forward warp targets `Caldera`. Ordinary mines, Skull Cavern, Quarry Mine, and Volcano Dungeon therefore remain four non-interchangeable execution families.

## Transparent Input

`VolcanoReadAdapter` publishes a dedicated `volcano` domain from the currently loaded live level:

- `current_level`: exact level, layout, generation seed, level kind, start/end positions, mushroom/monster flags, map dimensions, cooling legality, and progression mail gates.
- `tiles`: player tile, compact collision rows, exact `waterTiles`, `cooledLavaTiles`, currently coolable uncooled tiles, and dirt tiles. Level `5` is excluded from cooling candidates exactly as `VolcanoDungeon.performToolAction` requires.
- `connectors`: live native warps classified as forward, backward, `IslandNorth`, or `Caldera`, plus loaded tile-index actions `LeaveVolcano` and `VolcanoShop`.
- `gates`: every live `DwarfGate`, blocking tile, open state, pressed/required count, and every exact `DwarfSwitch` tile/state.
- `objects`, `monsters`, `debris`, and `player_resources`: current obstacle/threat/drop rows plus exact stone/container health, monster resilience and miss chance, executor support status, health, energy, inventory space, watering cans, pickaxes, melee weapons, and heavy-hitter container damage.

The adapter does not call generation, cooling, gate events, tool actions, direct warps, or monster AI.

## Model And Compiler

The model-facing option is `volcano.reach_caldera`. The compiler reads a fresh `profile=volcano` snapshot and emits one rolling current-level step:

- reachable unpressed switch -> `executor.move_to_tile`; native touch action remains responsible for pressing it;
- reachable forward warp -> `executor.traverse_connector`; expected target location and arrival tile are carried explicitly;
- lava bridge needed -> `executor.cool_volcano_lava`; runtime walks to the exact adjacent stand tile, selects the verified watering can, uses the native farmer tool lifecycle once, and accepts success only after the live target appears in `cooledLavaTiles`;
- breakable stone needed -> `executor.break_volcano_stone`; runtime locks the exact live object and pickaxe slot, walks to the compiler stand tile, uses the native pickaxe lifecycle, and accepts success only after the object disappears;
- breakable container needed -> `executor.break_volcano_container`; runtime locks the exact live container and heavy-hitter slot, uses normal tool input, leaves released contents to normal game debris handling, and records the deterministic tool-use count;
- supported monster needed -> `executor.combat_volcano_monster`; runtime locks the exact live runtime identity and melee weapon slot, pursues through current collision, attacks through normal input, and accepts success only after target health reaches zero;
- a pressed gate still opening -> `executor.wait_ticks`; the next fresh snapshot must show native gate progress before route planning continues;
- `Spiker`, custom monster types, missing tools, stale identities, dynamic path drift, and unsafe obstacle windows -> fail closed with explicit reasons;
- unresolved topology -> fail closed.

The Volcano executors have independent state machines. They do not reuse ordinary-mine, Skull Cavern, or Quarry Mine runtime state. No direct gate open, lava mutation, object removal, monster damage, level assignment, player-position write, or production warp is permitted.

Static and dynamic collision have separate roles. The weighted route search may price stones, containers, lava, gates, and monsters from static topology. Before dispatch, the selected stand tile must also be reachable in current dynamic collision. A reachable monster blocking that route is compiled first. A glider inside the three-tile danger window is intercepted even when it has no land stand, unless a native connector separates it from the player. Runtime movement, cooling, and obstacle actions yield to the same immediate-threat boundary and replan from a fresh snapshot.

Emergency food is a subordinate native action. While it is active, the combat no-progress clock is suspended. Completion requires the live food stack to decrease and health to increase. If those state changes are already verified but the native eating animation remains locked for more than 180 ticks, the harness clears only the stale farmer animation and restores the previous slot; it never awards health or removes food directly.

## Current Exit Condition

This slice is read/plan/compiler/executor/runtime-closed for switch touch, native gate wait, forward connectors, lava cooling, stone removal, container removal, supported vanilla melee combat, and native emergency food on the tested isolated generated run. `Invoke-RuntimeVolcanoReachCalderaLoop.ps1` loads a generated Volcano level through a debug-only native warp, reads `profile=volcano&fresh=true`, compiles one `volcano.reach_caldera` step, executes exactly one compiler-owned primitive, requires a fresh after-snapshot with a changed state hash, and repeats. The game clock is paused only while the external orchestrator is idle; actions run in the normal world update loop.

The loop succeeds only when `executor.traverse_connector` moves from level `9` to `Caldera`. It fails on stale snapshots, unexpected blocked primitives, cross-family option IDs, backward or skipped level transitions, leaving the Volcano family anywhere except Caldera, or exhausting the step limit. Explicit threat/path windows are safe replans only when the after-snapshot is fresh and its state hash changed. Per-step rows are collected without retraining; training is deferred until the run is complete.

Final hidden E-drive run `runtime-volcano-reach-caldera-20260730-114820-full-final` passed from level `0` through all ten generated levels to `Caldera`. It executed 106 rolling steps: 82 verified native actions and 24 fresh safe replans. Primitive totals were 10 lava cooling, 22 connector attempts, 38 combat attempts, 8 movement steps, 24 stone steps, and 4 gate waits. All after-snapshots were fresh, all state hashes changed, and 82 executor-calibration rows were written. This evidence supersedes the static-only status in EVD-100.

This proves one isolated generated 0-to-Caldera loop, not arbitrary generation seeds, modded monsters, multiplayer behavior, full-objective day-duration calibration, or trained-model quality. Those claims remain fail closed.
