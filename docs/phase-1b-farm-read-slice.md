# Phase 1B Farm Read Slice

Status: implementation_ready_controller_integration_pending

Scope: Farm domain transparent read only. This slice adds a Bridge adapter draft/implementation that controller can register later. It does not modify `ModEntry`, backend shared ingest, command execution, game state, save files, or input.

## Adapter

New adapter: `src/StardewAI.TransparentBridge/Adapters/FarmReadAdapter.cs`

Section emitted: `farm`

Implemented fields:

- `farm_type`
- `farm_identity`
- `buildings`
- `crops`
- `terrain_features`
- `objects`
- `machines`
- `chests`
- `animals`
- `resource_clumps`
- `debris`
- `warps`

When `Context.IsWorldReady` is false or `Game1.getFarm()` is unavailable, every field is emitted as `status=unavailable`, `value=null`, `confidence=0`, and the field is listed in `unavailable_fields`.

## Contract And Schema Notes

No shared DTO change is required for this slice. `schemas/json/snapshot.schema.json` already defines `state.farm` as `farm_state`, allows additional field envelopes in that object, and includes the target Farm keys.

Suggested controller integration:

- Add `new FarmReadAdapter()` to the TransparentBridge adapter list after `PlayerReadAdapter`.
- Add a `read.farm` capability entry during controller integration, with `access_mode=read`, `required_permission=observer`, `can_write_game_state=false`.

Do not register this adapter until controller is ready to own shared capability and event integration.

## Decompiled Evidence

| Field/event | Decompiled path | Line/search pattern | Member path | Source kind | Runtime condition |
| --- | --- | --- | --- | --- | --- |
| farm_type | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs` | `792: public static int whichFarm` | `Game1.whichFarm` | public game object/static field | `Context.IsWorldReady`; farm snapshot only |
| farm_identity.location_name/location_id/is_farm | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Game1.cs`; `...\GameLocation.cs`; `...\Farm.cs` | `4712: public static Farm getFarm()`; `592: public bool IsFarm`; `29: public class Farm : GameLocation` | `Game1.getFarm().Name`, `Game1.getFarm().NameOrUniqueName`, `Game1.getFarm().IsFarm` | public game object | `Context.IsWorldReady` and `Game1.getFarm() != null` |
| farm_identity.greenhouse_unlocked | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farm.cs` | `113: [XmlElement("greenhouseUnlocked")]`; `114: public readonly NetBool greenhouseUnlocked` | `Game1.getFarm().greenhouseUnlocked.Value` | public game object/NetField | farm available |
| buildings | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\StardewValley.Buildings\Building.cs` | `185: public readonly NetCollection<Building> buildings`; `58-86: tileX/tileY/tilesWide/tilesHigh/daysOfConstructionLeft/buildingType`; `2065: public bool isUnderConstruction` | `Game1.getFarm().buildings[*].buildingType/tileX/tileY/tilesWide/tilesHigh/daysOfConstructionLeft/isUnderConstruction()` | public game object/NetField | farm available; no `GetIndoors()` call |
| crops | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\StardewValley.TerrainFeatures\HoeDirt.cs`; `...\Crop.cs` | `296: terrainFeatures`; `211: public Crop crop`; `465: public bool readyForHarvest()`; `46-95: phase/current/dead/forage fields` | `Game1.getFarm().terrainFeatures.Pairs[*] as HoeDirt.crop.*` | public game object/NetField plus read-only methods | farm available; only HoeDirt with non-null crop |
| terrain_features | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\StardewValley.TerrainFeatures\TerrainFeature.cs` | `296: terrainFeatures`; `33: public virtual Vector2 Tile` | `Game1.getFarm().terrainFeatures.Pairs[*]` | public game object | farm available |
| objects | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\Object.cs`; `...\Item.cs` | `256: public readonly OverlaidDictionary objects`; `382-415: bigCraftable/readyForHarvest/heldObject/minutesUntilReady`; `187: QualifiedItemId`; `200: DisplayName` | `Game1.getFarm().objects.Pairs[*]` | public game object/NetField | farm available |
| machines | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Object.cs` | `382: bigCraftable`; `391: readyForHarvest`; `403: heldObject`; `414: minutesUntilReady` | `Game1.getFarm().objects.Pairs[*]` filtered by machine-shaped state | derived from public game object fields | farm available; heuristic subset only |
| chests | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley.Objects\Chest.cs` | `20: public class Chest : Object`; `191: public Inventory Items`; `151: public SpecialChestTypes SpecialChestType` | `Game1.getFarm().objects.Pairs[*] as Chest` | public game object/NetField | farm available; reads `Items`, no mutation |
| animals | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\FarmAnimal.cs` | `191: public readonly NetLongDictionary<FarmAnimal...> animals`; `80-129: friendship/age/produceQuality/type/buildingTypeILiveIn/myID`; `268: displayName` | `Game1.getFarm().animals.Pairs[*]` | public game object/NetField | farm available; outside/current farm animals only |
| resource_clumps | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\Farm.cs`; `...\StardewValley.TerrainFeatures\ResourceClump.cs` | `367: resourceClumps.Add(...)`; `37-50: width/height/parentSheetIndex/health`; `444: Vector2 tile = Tile` | `Game1.getFarm().resourceClumps[*]` | public game object/NetField | farm available |
| debris | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\Debris.cs` | `299: public readonly NetCollection<Debris> debris`; `75/101/120/188/202: chunkType/debrisType/itemId/item/Chunks` | `Game1.getFarm().debris[*]` | public game object/NetField | farm available |
| warps | `I:\StardewValleyAICompanion-decompile\StardewValley\StardewValley\GameLocation.cs`; `...\Warp.cs` | `270: public readonly NetObjectList<Warp> warps`; `32-60: X/Y/TargetX/TargetY/TargetName` | `Game1.getFarm().warps[*]` | public game object/NetField | farm available |

## Read-Only Allowlist

Allowed source files:

- `src/StardewAI.TransparentBridge/Adapters/FarmReadAdapter.cs`
- `docs/phase-1b-farm-read-slice.md`
- `tests/StardewAI.Backend.Tests/FarmSnapshotIngestTests.cs`

Allowed Stardew/SMAPI member paths:

- `Context.IsWorldReady`
- `Game1.getFarm()`
- `Game1.whichFarm`
- `Farm.Name`
- `Farm.NameOrUniqueName`
- `Farm.IsFarm`
- `Farm.greenhouseUnlocked.Value`
- `Farm.buildings`
- `Farm.terrainFeatures`
- `Farm.objects`
- `Farm.animals`
- `Farm.resourceClumps`
- `Farm.debris`
- `Farm.warps`
- `Building.buildingType/tileX/tileY/tilesWide/tilesHigh/daysOfConstructionLeft/isUnderConstruction()`
- `HoeDirt.crop/readyForHarvest()/isWatered()/needsWatering()`
- `Crop.indexOfHarvest/currentPhase/phaseDays/dayOfCurrentPhase/dead/forageCrop/whichForageCrop/fullyGrown`
- `Object.ItemId/QualifiedItemId/Name/DisplayName/Stack/Quality/bigCraftable/readyForHarvest/minutesUntilReady/heldObject`
- `Chest.SpecialChestType/Items`
- `FarmAnimal.Name/displayName/type/buildingTypeILiveIn/age/friendshipTowardFarmer/produceQuality/TilePoint`
- `ResourceClump.Tile/parentSheetIndex/width/height/health`
- `Debris.debrisType/chunkType/itemId/item/Chunks`
- `Warp.X/Y/TargetName/TargetX/TargetY`

Allowed event subscriptions: none in this slice.

Explicitly forbidden domains for this slice:

- command execution
- pathing
- UI automation
- buying/selling
- inventory mutation
- save editing
- full runtime acceptance
- current-location/NPC/quest/progress expansion beyond Farm fields above

## Remaining Gaps

- `FarmReadAdapter` is not registered in `ModEntry` by design; controller will integrate.
- Capability manifest entry for `read.farm` is not added by design; controller will integrate shared capability surface.
- Indoor animal-house animals are not read because this slice avoids `Building.GetIndoors()` to prevent location instantiation side effects. Controller can add a separately evidenced indoor-animal read if it confirms a safe path.
- Machine detection is conservative and derived from object fields; it is not a full `Data/Machines` classification.
- Real SMAPI/game validation was not run.

Build status note: Farm slice files and backend tests are prepared, but the current full repository build is blocked by an unrelated untracked `src/StardewAI.TransparentBridge/Adapters/ProgressReadAdapter.cs` compile error in the shared worktree.

Live SMAPI validation status: `not_executed`

鐪熷疄娓告垙杩愯鏃堕獙鏀跺皻鏈墽琛宍
