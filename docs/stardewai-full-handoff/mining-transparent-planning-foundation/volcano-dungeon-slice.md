# Volcano Dungeon Slice

## Boundary

`VolcanoDungeon` is a separate runtime type and is never treated as a `MineShaft`. Its generated levels are `0..9`; level `5` is the rest/shop floor, and the level `9` forward warp targets `Caldera`. Ordinary mines, Skull Cavern, Quarry Mine, and Volcano Dungeon therefore remain four non-interchangeable execution families.

## Transparent Input

`VolcanoReadAdapter` publishes a dedicated `volcano` domain from the currently loaded live level:

- `current_level`: exact level, layout, generation seed, level kind, start/end positions, mushroom/monster flags, map dimensions, cooling legality, and progression mail gates.
- `tiles`: player tile, compact collision rows, exact `waterTiles`, `cooledLavaTiles`, currently coolable uncooled tiles, and dirt tiles. Level `5` is excluded from cooling candidates exactly as `VolcanoDungeon.performToolAction` requires.
- `connectors`: live native warps classified as forward, backward, `IslandNorth`, or `Caldera`, plus loaded tile-index actions `LeaveVolcano` and `VolcanoShop`.
- `gates`: every live `DwarfGate`, blocking tile, open state, pressed/required count, and every exact `DwarfSwitch` tile/state.
- `objects`, `monsters`, `debris`, and `player_resources`: current obstacle/threat/drop rows plus health, energy, inventory space, watering cans, pickaxes, and melee weapons.

The adapter does not call generation, cooling, gate events, tool actions, direct warps, or monster AI.

## Model And Compiler

The model-facing option is `volcano.reach_caldera`. The compiler reads a fresh `profile=volcano` snapshot and emits one rolling current-level step:

- reachable unpressed switch -> `executor.move_to_tile`; native touch action remains responsible for pressing it;
- reachable forward warp -> `executor.traverse_connector`; expected target location and arrival tile are carried explicitly;
- lava bridge needed -> `executor.cool_volcano_lava`; runtime walks to the exact adjacent stand tile, selects the verified watering can, uses the native farmer tool lifecycle once, and accepts success only after the live target appears in `cooledLavaTiles`;
- monster or breakable obstacle needed -> blocked with exact live identity until Volcano-specific native combat/tool lifecycle support exists;
- unresolved topology -> fail closed.

No direct gate open, lava mutation, object removal, damage, level assignment, or warp is permitted.

## Current Exit Condition

This slice is read/plan/compiler complete and includes the native cooling implementation, but no game runtime claim is made yet. Runtime closure still requires an isolated cooling smoke, Volcano-specific combat and obstacle lifecycles, and a repeated fresh-snapshot loop through level `9` into `Caldera`. Full-objective duration remains unknown and therefore fails the day time-budget gate.
