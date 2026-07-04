# Phase 1D NPC Read Slice

Scope: visible/current-location NPC read preparation only. This slice does not register the adapter in `ModEntry`; controller integration is separate.

Implemented adapter: `StardewAI.TransparentBridge.Adapters.NpcReadAdapter`.

## Supported fields

| field | status | source | notes |
| --- | --- | --- | --- |
| `npcs.positions` | `available` when world ready | `Game1.currentLocation.characters` plus `Character` read properties | Current-location NPCs only. Each record includes name, display name, location id, tile, facing direction, `visible_on_screen`, `is_villager`, and `is_monster`. |
| `npcs.schedules` | `unavailable` | `StardewValley.NPC.Schedule` | Schedules are intentionally not implemented. No schedule data is read or derived in this slice. |

`npcs.positions` is `unavailable` when `Context.IsWorldReady` is false or `Game1.currentLocation` is null.

## Decompiled Evidence

| field/event name | decompiled path | line or search pattern | member path | source kind | runtime null/readiness condition |
| --- | --- | --- | --- | --- | --- |
| `npcs.positions.location_id` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs`; `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs` | `Game1.cs:1328 currentLocation`; `GameLocation.cs:539 NameOrUniqueName` | `Game1.currentLocation.NameOrUniqueName` | public game object | `Context.IsWorldReady`; `Game1.currentLocation != null` |
| `npcs.positions[]` collection | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs` | `GameLocation.cs:247 public readonly NetCollection<NPC> characters`; `GameLocation.cs:768 AddField(characters, "characters")` | `Game1.currentLocation.characters` | public game object | `Context.IsWorldReady`; `Game1.currentLocation != null`; entry is not null |
| `npcs.positions.name` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `Character.cs:416 public string Name` | `npc.Name` | public game object | NPC entry is not null |
| `npcs.positions.display_name` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `Character.cs:290 public virtual string displayName`; `Character.cs:294 return _displayName...` | `npc.displayName` | public game object | NPC entry is not null |
| `npcs.positions.tile_x/tile_y` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `Character.cs:377 public Point TilePoint`; `Character.cs:383 Vector2 tile = Tile` | `npc.TilePoint.X/Y` | public game object | NPC entry is not null |
| `npcs.positions.facing_direction` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `Character.cs:403 public virtual int FacingDirection`; `Character.cs:407 return facingDirection.Value` | `npc.FacingDirection` | public game object | NPC entry is not null |
| `npcs.positions.current_location_filter` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Character.cs` | `Character.cs:500 public GameLocation currentLocation`; `Character.cs:504 return currentLocationRef.Value` | `ReferenceEquals(npc.currentLocation, Game1.currentLocation)` | public game object | NPC entry is not null; current location is not null |
| `npcs.positions.visible_on_screen` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Utility.cs` | `Utility.cs:7682 public static bool isOnScreen(Point positionTile, int acceptableDistanceFromScreenNonTile, GameLocation location = null)`; `Utility.cs:7684 location != Game1.currentLocation returns false` | `Utility.isOnScreen(npc.TilePoint, 128, Game1.currentLocation)` | public game object derived by game utility | NPC entry is not null; current location is not null |
| `npcs.positions.is_villager` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:528 public override bool IsVillager => true` | `npc.IsVillager` | public game object | NPC entry is not null |
| `npcs.positions.is_monster` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `GameLocation.cs:16291 if (!character.IsMonster)`; search pattern `IsMonster` | `npc.IsMonster` | public game object | NPC entry is not null |
| `npcs.schedules` | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\NPC.cs` | `NPC.cs:530 schedule of this NPC's movements...`; `NPC.cs:531 remarks set schedule using TryLoadSchedule` | unavailable only; no schedule member read | unavailable | Always unavailable in this slice |

## Read-Only Allowlist

Allowed source files:

- `src/StardewAI.TransparentBridge/Adapters/NpcReadAdapter.cs`
- `docs/phase-1d-npc-read-slice.md`
- `tests/StardewAI.Backend.Tests/NpcSnapshotPayloadTests.cs`

Allowed Stardew/SMAPI member paths:

- `Context.IsWorldReady`
- `Game1.currentLocation`
- `GameLocation.NameOrUniqueName`
- `GameLocation.characters`
- `NPC.Name`
- `NPC.displayName`
- `NPC.TilePoint`
- `NPC.FacingDirection`
- `NPC.currentLocation`
- `NPC.IsVillager`
- `NPC.IsMonster`
- `Utility.isOnScreen(Point, int, GameLocation)`

Allowed event subscriptions: none.

Forbidden domains for this slice:

- schedule loading or schedule parsing
- friendship, gift taste, dialogue, quest, mail, farm, inventory, movement, pathing, input, save, or game-state mutation

## Runtime Validation

鐪熷疄娓告垙杩愯鏃堕獙鏀跺皻鏈墽琛宍

Live SMAPI validation status: `not_executed`.
