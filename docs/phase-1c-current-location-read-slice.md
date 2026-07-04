# Phase 1C Current Location Read Slice

Scope: `current_location` only. This slice adds a read-only bridge adapter for current location identity/display, outdoor/farm flags, safe object summaries, terrain feature summaries, warp metadata, and map/layer metadata. NPCs, pathing, map graph execution, mutation, and full runtime validation remain out of scope.

## Decompiled Evidence

| Field | Decompiled path | Line/search evidence | Member path | Source kind | Runtime null/readiness condition |
| --- | --- | --- | --- | --- | --- |
| world readiness | `SMAPI/StardewModdingAPI/Context.cs` | `IsWorldReady` getter returns `IsWorldReadyForScreen.Value`; `IsPlayerFree` additionally checks `Game1.currentLocation != null` | `Context.IsWorldReady` | SMAPI public API | Required before reading `Game1.currentLocation`; otherwise fields are unavailable |
| current location | `StardewValley/StardewValley/Game1.cs` | `public static GameLocation currentLocation` getter returns `game1.instanceGameLocation` | `Game1.currentLocation` | public game object | May be null even when not player-free; adapter checks null |
| identity name | `StardewValley/StardewValley/GameLocation.cs` | `public string Name => name.Value;` | `Game1.currentLocation.Name` | public game object | Requires non-null location |
| identity unique name | `StardewValley/StardewValley/GameLocation.cs` | `NameOrUniqueName` returns `uniqueName.Value` when set, otherwise `name.Value` | `Game1.currentLocation.NameOrUniqueName` | public game object | Requires non-null location |
| display name | `StardewValley/StardewValley/GameLocation.cs` | `DisplayName` getter caches `_displayName`; `GetDisplayName()` reads location data display name and parses token text | `Game1.currentLocation.GetDisplayName() ?? Game1.currentLocation.Name` | public game object | Requires non-null location; adapter avoids `DisplayName` getter cache write |
| farm flag | `StardewValley/StardewValley/GameLocation.cs` | `IsFarm` getter returns `isFarm.Value` | `Game1.currentLocation.IsFarm` | public game object | Requires non-null location |
| outdoor flag | `StardewValley/StardewValley/GameLocation.cs` | `IsOutdoors` getter returns `isOutdoors.Value` | `Game1.currentLocation.IsOutdoors` | public game object | Requires non-null location |
| safe objects | `StardewValley/StardewValley/GameLocation.cs` | `[XmlElement("objects")] public readonly OverlaidDictionary objects;`; `Objects => objects` | `Game1.currentLocation.objects.Pairs` | public game object | Requires non-null location |
| object identity | `StardewValley/StardewValley/Item.cs` | `ItemId`, `QualifiedItemId`, `DisplayName`, and `Name` public getters | object item `ItemId/QualifiedItemId/Name/DisplayName/Stack/Quality` | public game object | Requires object entry from current location |
| terrain features | `StardewValley/StardewValley/GameLocation.cs` | `[XmlElement("terrainFeatures")] public readonly NetVector2Dictionary<TerrainFeature, NetRef<TerrainFeature>> terrainFeatures` | `Game1.currentLocation.terrainFeatures.Pairs` | public game object | Requires non-null location |
| terrain feature type | `StardewValley/StardewValley.TerrainFeatures/TerrainFeature.cs` | `public virtual Vector2 Tile`; `public ModDataDictionary modData`; type is concrete subclass | `terrainFeature.GetType().FullName` | public game object | Requires terrain feature entry from current location |
| warps | `StardewValley/StardewValley/GameLocation.cs` | `public readonly NetObjectList<Warp> warps` | `Game1.currentLocation.warps` | public game object | Requires non-null location |
| warp metadata | `StardewValley/StardewValley/Warp.cs` | `X`, `Y`, `TargetX`, `TargetY`, `TargetName` public getters; `flipFarmer` and `npcOnly` public NetBool fields | `warp.X/Y/TargetName/TargetX/TargetY/flipFarmer.Value/npcOnly.Value` | public game object | Requires warp entry from current location |
| map reference | `StardewValley/StardewValley/GameLocation.cs` | `[XmlIgnore] public Map map;`; `Map` getter calls `updateMap()` | `Game1.currentLocation.map` | public game object | Requires non-null location; adapter avoids `Map` getter to prevent update side effects |
| map size and layers | `StardewValley/StardewValley/GameLocation.cs` | `isTileOnMap` checks `map.Layers[0].LayerWidth` and `LayerHeight`; `SortLayers()` iterates `foreach (Layer layer in map.Layers)` and reads `layer.Id` | `Game1.currentLocation.map.Layers[*].Id/LayerWidth/LayerHeight` | public game object | Requires non-null location and non-null map |

## Implemented Fields

- `current_location.identity`: `name`, `name_or_unique_name`, `type`.
- `current_location.display_name`: `GetDisplayName() ?? Name`; avoids the `DisplayName` getter because it writes `_displayName`.
- `current_location.flags`: `is_outdoors`, `is_farm`.
- `current_location.objects`: ordered safe summaries with tile, item IDs, display/name, stack, quality, runtime type.
- `current_location.terrain_features`: ordered tile/type summaries only.
- `current_location.warps`: ordered source/target metadata and read-only flags.
- `current_location.map`: map id, max layer width/height, layer count, ordered layer id/width/height.

## Read-Only Allowlist

Allowed source files:

- `src/StardewAI.TransparentBridge/Adapters/CurrentLocationReadAdapter.cs`
- `tests/StardewAI.Backend.Tests/CurrentLocationSnapshotIngestTests.cs`
- `docs/phase-1c-current-location-read-slice.md`

Allowed Stardew/SMAPI member paths:

- `Context.IsWorldReady`
- `Game1.currentLocation`
- `GameLocation.Name`
- `GameLocation.NameOrUniqueName`
- `GameLocation.GetDisplayName()`
- `GameLocation.IsOutdoors`
- `GameLocation.IsFarm`
- `GameLocation.objects.Pairs`
- `GameLocation.terrainFeatures.Pairs`
- `GameLocation.warps`
- `GameLocation.map.Layers`
- `Item.ItemId`, `Item.QualifiedItemId`, `Item.Name`, `Item.DisplayName`, `Item.Stack`, `Item.Quality`
- `Warp.X`, `Warp.Y`, `Warp.TargetName`, `Warp.TargetX`, `Warp.TargetY`, `Warp.flipFarmer.Value`, `Warp.npcOnly.Value`

Allowed event subscriptions: none.

Explicitly forbidden for this slice:

- NPC/character summaries.
- Pathfinding, collision checks, route graph execution, or warp execution.
- Game state mutation, input simulation, save writes, config writes, and runtime full acceptance.

Live SMAPI validation status: `not_executed`.

鐪熷疄娓告垙杩愯鏃堕獙鏀跺皻鏈墽琛宍
