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
- `Spiker`, custom monster types, missing tools, stale identities, dynamic path drift, and unsafe obstacle windows -> fail closed with explicit reasons;
- unresolved topology -> fail closed.

The three new executors have independent Volcano state machines. They do not reuse ordinary-mine, Skull Cavern, or Quarry Mine runtime state. No direct gate open, lava mutation, object removal, monster damage, level assignment, player-position write, or warp is permitted.

## Current Exit Condition

This slice is read/plan/compiler/executor-implementation complete for switch touch, forward connectors, lava cooling, stone removal, container removal, and supported vanilla melee combat. Static builds pass with deployment disabled, but no game runtime claim is made for these Volcano primitives yet. Runtime closure still requires isolated primitive smokes, repeated fresh-snapshot replanning through level `9` into `Caldera`, after-snapshot training-row verification, and duration calibration. Full-objective duration remains unknown and therefore fails the day time-budget gate.
