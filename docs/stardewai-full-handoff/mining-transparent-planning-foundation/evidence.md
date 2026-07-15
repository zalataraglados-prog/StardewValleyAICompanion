# Evidence

## Decompile Anchors

- EVD-MIN-001 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:207-217` exposes `MineShaft.mineLevel` as a live net field property.
- EVD-MIN-002 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:267-313` exposes live `isSlimeArea`, `isDinoArea`, `isMonsterArea`, and `isQuarryArea` flags.
- EVD-MIN-003 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:3343-3354` maps additional mine difficulty to quarry/no difficulty, Skull Cavern difficulty, or ordinary mines difficulty.
- EVD-MIN-004 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:3787-3858` maps mine level/area: Skull Cavern `>120 -> 121`, quarry `77377`, ordinary areas `0/10/40/80`.
- EVD-MIN-005 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:1956-1963` defines kill-all descent gates from slime, monster, and dino area flags.
- EVD-MIN-006 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:1974-1981` blocks ladder creation on floor `120` and quarry mine `77377`; this is not exposed as a future ladder preview because creation still depends on gameplay progression/RNG/event state.
- EVD-MIN-007 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:1983-2024` shows `doCreateLadderAt` calls `updateMap()` and writes building tile index `173`; this implementation does not call creation and does not claim tile-index appearance proves usability.
- EVD-MIN-008 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:3614-3637` shows stone break ladder discovery decrements `stonesLeftOnThisLevel`, consumes random values, and can call `createLadderDown`; this implementation does not call it.
- EVD-MIN-009 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Locations\MineShaft.cs:832` starts `findLadder`; task forbids calling it and adapter does not reference it.
- EVD-MIN-010 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs:239-256` exposes the already-loaded `map`, `characters`, and `objects` fields; `GameLocation.cs:570-575` shows the `Map` getter calls `updateMap()`, so the mining adapter uses only the public `map` field and avoids the getter.
- EVD-MIN-011 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs:9797-9804` handles exact `MineElevator` action semantics separately from other actions; this implementation parses exact first action tokens rather than prefix matching.
- EVD-MIN-012 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Object.cs:744-780` exposes object `Fragility` and `MinutesUntilReady`; `MinutesUntilReady` is not stone health/hits and is no longer surfaced as such.
- EVD-MIN-013 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Object.cs:920` and `Object.cs:1121-1138` identify breakable stones and pickaxe interaction; this implementation reads `IsBreakableStone()` but marks complete ore/container/hit classification unavailable.
- EVD-MIN-014 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Monsters\Monster.cs:163-197` exposes `DamageToFarmer`, `Health`, and `MaxHealth`.
- EVD-MIN-015 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Monsters\Monster.cs:254-282` exposes monster identity/net fields including drop list; this implementation does not read drop rolls or invoke AI.
- EVD-MIN-016 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farmer.cs:662-664`, `Farmer.cs:1230-1238`, `Farmer.cs:1242`, `Farmer.cs:1584-1606`, and `Farmer.cs:1647-1842` expose health, `deepestMineLevel`, stamina, inventory capacity, mining/combat levels, current item/tool, and max stamina.
- EVD-MIN-017 `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Menus\MineElevatorMenu.cs:16-40` builds elevator destinations from a deepest reached value capped at `120` and grouped by `5`; task-required candidate logic uses the read `Farmer.deepestMineLevel` checkpoint instead of inferring progress from target depth, and keeps the live current floor when it is deeper than the unlocked checkpoint.

## Repository Anchors

- `src\StardewAI.TransparentBridge\Adapters\MiningReadAdapter.cs` adds the read-only `mining` section and fails closed when `Game1.currentLocation` is not a loaded `MineShaft`.
- `src\StardewAI.TransparentBridge\ModEntry.cs` registers `MiningReadAdapter` in the transparent snapshot collector.
- `src\StardewAI.Core\OptionRegistry\OptionRegistry.cs` registers `mining.reach_depth` as `parameterized_mechanical` with transparent mining state requirements.
- `src\StardewAI.Core\OptionRegistry\MiningReachDepthCandidateBuilder.cs` fails closed on recursive required mining incompleteness, rejects known impossible target depths before runtime, keeps optional reserve constraints absent unless supplied, blocks unknown cost with `mining_cost_estimate_unavailable`, and uses read elevator unlock progress without moving backward from the live floor.
- `src\StardewAI.Core\Execution\ActionQueueCompiler.cs` preserves target/resource/retreat parameters, keeps cost estimates unknown, and always blocks `mining.reach_depth` with `mining_cost_estimate_unavailable` and `mining_perfect_executor_not_implemented`.
- `tests\StardewAI.Core.Tests\MiningReachDepthPlanningTests.cs` adds focused option, recursive completeness, exact action parsing, incomplete object/collision groups, elevator unlock/continuation, reserve-default, compiler, and impossible-target tests. Not executed in this active-user slice.
