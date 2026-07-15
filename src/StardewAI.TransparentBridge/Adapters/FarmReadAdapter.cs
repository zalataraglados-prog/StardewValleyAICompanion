using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Machines;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewAI.TransparentBridge.State;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class FarmReadAdapter : ReadAdapterBase
{
    private const int MaxMachineRowsPerSnapshot = 64;
    private const int MaxMachineInputProbeSlotsPerMachine = 16;
    private const long MachineProbeCacheMaxAgeTicks = 240;

    private static readonly object MachineProbeCacheLock = new();
    private static object[] cachedMachineProbeRows = Array.Empty<object>();
    private static long cachedMachineProbeTick = -1;

    private static readonly string[] FarmFields =
    {
        "farm_type",
        "farm_identity",
        "crop_catalog",
        "shipping_bins",
        "buildings",
        "crops",
        "terrain_features",
        "objects",
        "machines",
        "chests",
        "animals",
        "resource_clumps",
        "debris",
        "warps",
        "grandpa_score"
    };

    public override string Domain => "farm";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        var farm = Context.IsWorldReady ? Game1.getFarm() : null;
        if (farm is null)
        {
            return Section(
                "farm",
                FarmFields.ToDictionary(
                    field => field,
                    field => (object)Unavailable("world_not_ready_or_farm_unavailable", "Context.IsWorldReady; Game1.getFarm()", tick, "vanilla_1_6_farm")),
                FarmFields.Select(field => "farm." + field).ToArray(),
                "unavailable");
        }

        if (string.Equals(SnapshotProfileContext.Current, "machine", StringComparison.OrdinalIgnoreCase))
        {
            return Section("farm", new Dictionary<string, object>
            {
                ["machines"] = Field(ReadCachedMachineProbeRowsOrFallback(farm), "FarmReadAdapter.RefreshMachineProbeCache on SMAPI UpdateTicked; Game1.getFarm().objects[*] machine-shaped objects", tick, "transparent_bridge_main_thread_cache")
            });
        }

        return Section("farm", new Dictionary<string, object>
        {
            ["farm_type"] = Field(Game1.whichFarm, "Game1.whichFarm", tick, "vanilla_1_6_farm"),
            ["farm_identity"] = Field(new
            {
                location_name = farm.Name,
                location_id = farm.NameOrUniqueName,
                is_farm = farm.IsFarm,
                greenhouse_unlocked = farm.greenhouseUnlocked.Value
            }, "Game1.getFarm().Name/NameOrUniqueName/IsFarm/greenhouseUnlocked", tick, "vanilla_1_6_farm"),
            ["crop_catalog"] = Field(ReadCropCatalog(), "Game1.cropData (Data\\Crops)", tick, "vanilla_1_6_crop_data"),
            ["shipping_bins"] = Field(ReadShippingBins(farm), "Game1.getFarm().buildings as ShippingBin", tick, "vanilla_1_6_farm"),
            ["grandpa_score"] = Field(farm.grandpaScore.Value, "Game1.getFarm().grandpaScore.Value", tick, "vanilla_1_6_farm"),
            ["buildings"] = Field(ReadBuildings(farm), "Game1.getFarm().buildings", tick, "vanilla_1_6_farm"),
            ["crops"] = Field(ReadCrops(farm), "Game1.getFarm().terrainFeatures[*] as HoeDirt.crop", tick, "vanilla_1_6_farm"),
            ["terrain_features"] = Field(ReadTerrainFeatures(farm), "Game1.getFarm().terrainFeatures", tick, "vanilla_1_6_farm"),
            ["objects"] = Field(ReadObjects(farm), "Game1.getFarm().objects", tick, "vanilla_1_6_farm"),
            ["machines"] = Field(ReadCachedMachineProbeRowsOrFallback(farm), "FarmReadAdapter.RefreshMachineProbeCache on SMAPI UpdateTicked; Game1.getFarm().objects[*] machine-shaped objects", tick, "transparent_bridge_main_thread_cache"),
            ["chests"] = Field(ReadChests(farm), "Game1.getFarm().objects[*] as Chest", tick, "vanilla_1_6_farm"),
            ["animals"] = Field(ReadAnimals(farm), "Game1.getFarm().animals", tick, "vanilla_1_6_farm"),
            ["resource_clumps"] = Field(ReadResourceClumps(farm), "Game1.getFarm().resourceClumps", tick, "vanilla_1_6_farm"),
            ["debris"] = Field(ReadDebris(farm), "Game1.getFarm().debris", tick, "vanilla_1_6_farm"),
            ["warps"] = Field(ReadWarps(farm), "Game1.getFarm().warps", tick, "vanilla_1_6_farm")
        });
    }

    public static void RefreshMachineProbeCache()
    {
        if (!Context.IsWorldReady || Game1.getFarm() is not { } farm)
        {
            SetMachineProbeCache(Array.Empty<object>(), unchecked((long)Game1.ticks));
            return;
        }

        var tick = unchecked((long)Game1.ticks);
        SetMachineProbeCache(ReadMachines(farm, includeLoadableInputs: true, minimalMachineProfile: true, machineProbeCacheTick: tick), tick);
    }

    private static object[] ReadBuildings(Farm farm)
    {
        return farm.buildings
            .Select(building => ReadBuildingRow(building))
            .Cast<object>()
            .OrderBy(building =>
            {
                var yObj = building.GetType().GetProperty("tile_y")?.GetValue(building);
                return yObj is int y ? y : 0;
            })
            .ThenBy(building =>
            {
                var xObj = building.GetType().GetProperty("tile_x")?.GetValue(building);
                return xObj is int x ? x : 0;
            })
            .ThenBy(building => building.GetType().GetProperty("type")?.GetValue(building) as string ?? string.Empty)
            .ToArray();
    }

    private static object ReadBuildingRow(Building building)
    {
        var type = building.buildingType.Value;
        var tileX = building.tileX.Value;
        var tileY = building.tileY.Value;
        var humanDoor = building.humanDoor;
        var indoors = building.GetIndoors();
        var hasIndoors = indoors is not null;
        var interiorWarps = hasIndoors ? indoors!.warps.ToArray() : Array.Empty<Warp>();

        int? humanDoorAbsoluteX = null;
        int? humanDoorAbsoluteY = null;
        int? exteriorStandX = null;
        int? exteriorStandY = null;
        int? exteriorEntryX = null;
        int? exteriorEntryY = null;
        string? indoorLocationId = null;
        int? indoorArrivalX = null;
        int? indoorArrivalY = null;

        if (humanDoor.X >= 0 && humanDoor.Y >= 0)
        {
            humanDoorAbsoluteX = tileX + humanDoor.X;
            humanDoorAbsoluteY = tileY + humanDoor.Y;
            exteriorEntryX = humanDoorAbsoluteX;
            exteriorEntryY = humanDoorAbsoluteY + 1;
            exteriorStandX = exteriorEntryX;
            exteriorStandY = exteriorEntryY;
        }

        if (hasIndoors)
        {
            indoorLocationId = indoors!.NameOrUniqueName;
            if (interiorWarps.Length > 0)
            {
                indoorArrivalX = interiorWarps[0].X;
                indoorArrivalY = interiorWarps[0].Y - 1;
            }
        }

        var constructionLeft = building.daysOfConstructionLeft.Value;
        var locked = building.daysOfConstructionLeft.Value > 0;
        var underConstruction = building.isUnderConstruction();

        object doorData;
        if (humanDoor.X >= 0 && humanDoor.Y >= 0)
        {
            doorData = new
            {
                human_door_relative_x = humanDoor.X,
                human_door_relative_y = humanDoor.Y,
                human_door_absolute_tile_x = humanDoorAbsoluteX,
                human_door_absolute_tile_y = humanDoorAbsoluteY,
                exterior_entry_tile_x = exteriorEntryX,
                exterior_entry_tile_y = exteriorEntryY,
                exterior_stand_tile_x = exteriorStandX,
                exterior_stand_tile_y = exteriorStandY,
                indoor_location_id = indoorLocationId,
                indoor_arrival_tile_x = indoorArrivalX,
                indoor_arrival_tile_y = indoorArrivalY,
                source_label = "Building.humanDoor; Building.GetIndoors(); Building.GetIndoors().warps[0]"
            };
        }
        else
        {
            doorData = new
            {
                human_door_unavailable = true,
                indoor_location_id = indoorLocationId,
                indoor_arrival_tile_x = indoorArrivalX,
                indoor_arrival_tile_y = indoorArrivalY,
                source_label = "Building.humanDoor absent; Building.GetIndoors()"
            };
        }

        return new
        {
            type,
            tile_x = tileX,
            tile_y = tileY,
            tiles_wide = building.tilesWide.Value,
            tiles_high = building.tilesHigh.Value,
            days_of_construction_left = constructionLeft,
            is_under_construction = underConstruction,
            is_locked_by_construction = locked,
            indoor_location_id = indoorLocationId,
            has_door_access_resolved = humanDoor.X >= 0 && humanDoor.Y >= 0 && hasIndoors,
            door_resolution_status = humanDoor.X >= 0 && humanDoor.Y >= 0
                ? (hasIndoors ? "resolved_building_door_connector" : "missing_indoor_location")
                : (hasIndoors ? "unresolved_human_door" : "unresolved_both_door_and_indoor"),
            door = doorData
        };
    }

    private static object[] ReadShippingBins(Farm farm)
    {
        var player = Game1.player;
        var playerId = player.UniqueMultiplayerID;
        var useSeparateWallets = player.team.useSeparateWallets.Value;
        var binInventory = farm.getShippingBin(player);
        var aggregateContents = ReadBinAggregateContents(binInventory);
        var contentsSignature = ComputeContentsSignature(aggregateContents);
        var contentsTotalCount = aggregateContents.Sum(c => c.count);
        var contentsDistinctCount = aggregateContents.Length;

        return farm.buildings
            .OfType<ShippingBin>()
            .Select(bin =>
            {
                var distanceToPlayer = Vector2.Distance(player.Tile, new Vector2(bin.tileX.Value + 0.5f, bin.tileY.Value));
                var completed = bin.daysOfConstructionLeft.Value <= 0;
                var standTiles = completed ? ComputeAllBinInteractionStandTiles(farm, bin, player) : Array.Empty<BinStandTileEntry>();
                var preferred = standTiles
                    .FirstOrDefault(t => t.map_passable && !t.blocked)
                    ?? standTiles.FirstOrDefault(t => t.map_passable);
                return new
                {
                    tile_x = bin.tileX.Value,
                    tile_y = bin.tileY.Value,
                    tiles_wide = bin.tilesWide.Value,
                    tiles_high = bin.tilesHigh.Value,
                    tile_width = bin.tilesWide.Value,
                    tile_height = bin.tilesHigh.Value,
                    days_of_construction_left = bin.daysOfConstructionLeft.Value,
                    completed,
                    distance_to_player = distanceToPlayer,
                    player_within_shipping_range = distanceToPlayer <= 2f,
                    interaction_stand_tile_x = preferred?.x,
                    interaction_stand_tile_y = preferred?.y,
                    interaction_stand_tile_blocked_reason = preferred?.blocked_reason,
                    stand_tiles = standTiles.Select(t => new { t.x, t.y, t.map_passable, t.blocked, t.blocked_reason }).ToArray(),
                    bin_scope = useSeparateWallets ? "personal" : "shared",
                    player_id = playerId,
                    contents = aggregateContents.Select(c => new
                    {
                        item_id = c.itemId,
                        qualified_item_id = c.qualifiedItemId,
                        count = c.count
                    }).ToArray(),
                    contents_total_count = contentsTotalCount,
                    contents_distinct_item_count = contentsDistinctCount,
                    contents_signature = contentsSignature,
                    contents_truncated = false
                };
            })
            .OrderBy(bin => bin.tile_y)
            .ThenBy(bin => bin.tile_x)
            .ToArray();
    }

    private sealed class BinStandTileEntry
    {
        public BinStandTileEntry(int x, int y, bool mapPassable, bool blocked, string? blockReason)
        {
            this.x = x;
            this.y = y;
            this.map_passable = mapPassable;
            this.blocked = blocked;
            this.blocked_reason = blockReason;
        }

        public readonly int x;
        public readonly int y;
        public readonly bool map_passable;
        public readonly bool blocked;
        public readonly string? blocked_reason;
    }

    private static BinStandTileEntry[] ComputeAllBinInteractionStandTiles(Farm farm, ShippingBin bin, Farmer player)
    {
        var binX = bin.tileX.Value;
        var binY = bin.tileY.Value;
        var binW = bin.tilesWide.Value;
        var binH = bin.tilesHigh.Value;
        var centerX = binX + 0.5;
        var centerY = (float)binY;

        var tileEntries = new List<BinStandTileEntry>();
        var minX = binX - 2;
        var maxX = binX + binW + 1;
        var minY = binY - 2;
        var maxY = binY + binH + 1;

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                if (IsTileInBuildingFootprint(x, y, binX, binY, binW, binH))
                    continue;

                var dx = x - centerX;
                var dy = y - centerY;
                if (Math.Sqrt(dx * dx + dy * dy) > 2.0)
                    continue;

                var mapPassable = IsTilePassableForInteraction(farm, x, y);
                var dynamicBlocked = IsTileDynamicallyBlocked(farm, x, y);
                var blocked = !mapPassable || dynamicBlocked;
                string? blockReason = null;
                if (!mapPassable && dynamicBlocked)
                    blockReason = "map_and_dynamic_blocked";
                else if (!mapPassable)
                    blockReason = "static_map_blocked";
                else if (dynamicBlocked)
                    blockReason = "dynamic_transient_blocked";

                tileEntries.Add(new BinStandTileEntry(x, y, mapPassable, blocked, blockReason));
            }
        }

        return tileEntries
            .OrderBy(t => Math.Abs(player.TilePoint.X - t.x) + Math.Abs(player.TilePoint.Y - t.y))
            .ThenBy(t => t.y)
            .ThenBy(t => t.x)
            .ToArray();
    }

    private static bool IsTileInBuildingFootprint(int x, int y, int binX, int binY, int binW, int binH)
    {
        return x >= binX && x < binX + binW && y >= binY && y < binY + binH;
    }

    private static bool IsTileDynamicallyBlocked(GameLocation location, int x, int y)
    {
        return location.isCollidingPosition(
            new Microsoft.Xna.Framework.Rectangle(x * 64 + 1, y * 64 + 1, 62, 62),
            Game1.viewport,
            isFarmer: true,
            damagesFarmer: 0,
            glider: false,
            Game1.player,
            pathfinding: true);
    }

    private sealed class BinContentEntry
    {
        public readonly string itemId;
        public readonly string qualifiedItemId;
        public readonly int count;

        public BinContentEntry(string itemId, string qualifiedItemId, int count)
        {
            this.itemId = itemId;
            this.qualifiedItemId = qualifiedItemId;
            this.count = count;
        }
    }

    private static BinContentEntry[] ReadBinAggregateContents(object? binInventory)
    {
        if (binInventory == null)
            return Array.Empty<BinContentEntry>();

        var items = binInventory as System.Collections.IEnumerable;
        if (items == null)
        {
            var itemsProp = binInventory.GetType().GetProperty("Items",
                BindingFlags.Instance | BindingFlags.Public);
            if (itemsProp == null)
                return Array.Empty<BinContentEntry>();
            items = itemsProp.GetValue(binInventory) as System.Collections.IEnumerable;
            if (items == null)
                return Array.Empty<BinContentEntry>();
        }

        var dict = new Dictionary<string, BinContentEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in items)
        {
            if (obj is not Item stardewItem || stardewItem.Stack <= 0)
                continue;
            var qId = stardewItem.QualifiedItemId ?? string.Empty;
            if (dict.TryGetValue(qId, out var existing))
            {
                dict[qId] = new BinContentEntry(existing.itemId, existing.qualifiedItemId, existing.count + stardewItem.Stack);
            }
            else
            {
                dict[qId] = new BinContentEntry(stardewItem.ItemId ?? string.Empty, qId, stardewItem.Stack);
            }
        }

        return dict.Values
            .OrderBy(e => e.qualifiedItemId, StringComparer.Ordinal)
            .ThenBy(e => e.itemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ComputeContentsSignature(BinContentEntry[] contents)
    {
        var sb = new StringBuilder();
        foreach (var entry in contents)
        {
            sb.Append(entry.qualifiedItemId);
            sb.Append('|');
            sb.Append(entry.count);
            sb.Append('\n');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool IsTilePassableForInteraction(GameLocation location, int x, int y)
    {
        if (x < 0 || y < 0 || x >= location.map.Layers[0].LayerWidth || y >= location.map.Layers[0].LayerHeight)
            return false;
        var tileLoc = new xTile.Dimensions.Location(x, y);
        return location.isTilePassable(tileLoc, Game1.viewport);
    }

    private static object[] ReadCropCatalog()
    {
        return Game1.cropData
            .Select(pair =>
            {
                var harvestItemId = ReadString(pair.Value, "HarvestItemId") ?? string.Empty;
                return new
                {
                    seed_id = pair.Key,
                    seasons = ReadStringList(pair.Value, "Seasons"),
                    days_in_phase = ReadIntList(pair.Value, "DaysInPhase"),
                    grow_days = ReadIntList(pair.Value, "DaysInPhase").Where(day => day < 99999).Sum(),
                    regrow_days = ReadIntNullable(pair.Value, "RegrowDays"),
                    harvest_item_id = harvestItemId,
                    harvest_item_qualified_id = QualifyObjectId(harvestItemId),
                    harvest_unit_sale_price = ReadHarvestUnitSalePrice(harvestItemId),
                    harvest_min_stack = ReadIntNullable(pair.Value, "HarvestMinStack"),
                    harvest_max_stack = ReadIntNullable(pair.Value, "HarvestMaxStack"),
                    harvest_max_increase_per_farming_level = ReadFloatNullable(pair.Value, "HarvestMaxIncreasePerFarmingLevel"),
                    extra_harvest_chance = ReadFloatNullable(pair.Value, "ExtraHarvestChance"),
                    harvest_min_quality = ReadIntNullable(pair.Value, "HarvestMinQuality"),
                    harvest_max_quality = ReadIntNullable(pair.Value, "HarvestMaxQuality"),
                    harvest_method = ReadString(pair.Value, "HarvestMethod"),
                    is_paddy_crop = ReadBoolNullable(pair.Value, "IsPaddyCrop"),
                    needs_watering = ReadBoolNullable(pair.Value, "NeedsWatering"),
                    plantable_location_rule_count = ReadCount(pair.Value, "PlantableLocationRules")
                };
            })
            .OrderBy(crop => crop.seed_id)
            .ToArray();
    }

    private static string QualifyObjectId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return ItemRegistry.QualifyItemId(itemId) ?? "(O)" + itemId;
    }

    private static int? ReadHarvestUnitSalePrice(string itemId)
    {
        var qualifiedId = QualifyObjectId(itemId);
        if (string.IsNullOrWhiteSpace(qualifiedId))
        {
            return null;
        }

        try
        {
            return ItemRegistry.Create(qualifiedId).salePrice();
        }
        catch
        {
            return null;
        }
    }

    private static object[] ReadCrops(Farm farm)
    {
        return farm.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt { crop: not null })
            .Select(pair =>
            {
                var dirt = (HoeDirt)pair.Value;
                var crop = dirt.crop;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    harvest_item_id = crop.indexOfHarvest.Value,
                    current_phase = crop.currentPhase.Value,
                    phase_count = crop.phaseDays.Count,
                    day_of_current_phase = crop.dayOfCurrentPhase.Value,
                    dead = crop.dead.Value,
                    forage_crop = crop.forageCrop.Value,
                    forage_crop_id = crop.whichForageCrop.Value,
                    fully_grown = crop.fullyGrown.Value,
                    ready_for_harvest = dirt.readyForHarvest(),
                    watered = dirt.isWatered(),
                    needs_watering = dirt.needsWatering()
                };
            })
            .OrderBy(crop => crop.tile_y)
            .ThenBy(crop => crop.tile_x)
            .ToArray();
    }

    private static object[] ReadTerrainFeatures(Farm farm)
    {
        return farm.terrainFeatures.Pairs
            .Select(pair => new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                type = pair.Value.GetType().FullName
            })
            .OrderBy(feature => feature.tile_y)
            .ThenBy(feature => feature.tile_x)
            .ToArray();
    }

    private static object[] ReadObjects(Farm farm)
    {
        return farm.objects.Pairs
            .Select(pair => new
            {
                tile_x = (int)pair.Key.X,
                tile_y = (int)pair.Key.Y,
                item_id = pair.Value.ItemId,
                qualified_item_id = pair.Value.QualifiedItemId,
                name = pair.Value.Name,
                display_name = pair.Value.DisplayName,
                stack = pair.Value.Stack,
                quality = pair.Value.Quality,
                big_craftable = pair.Value.bigCraftable.Value,
                ready_for_harvest = pair.Value.readyForHarvest.Value,
                minutes_until_ready = pair.Value.MinutesUntilReady,
                held_item = SummarizeItem(pair.Value.heldObject.Value)
            })
            .OrderBy(item => item.tile_y)
            .ThenBy(item => item.tile_x)
            .ToArray();
    }

    private static object[] ReadCachedMachineProbeRowsOrFallback(Farm farm)
    {
        var currentTick = unchecked((long)Game1.ticks);
        lock (MachineProbeCacheLock)
        {
            if (cachedMachineProbeRows.Length > 0 &&
                cachedMachineProbeTick >= 0 &&
                currentTick - cachedMachineProbeTick <= MachineProbeCacheMaxAgeTicks &&
                CachedMachineProbeRowsHaveLoadableInput())
            {
                return cachedMachineProbeRows;
            }
        }

        return ReadMachines(farm, includeLoadableInputs: false, minimalMachineProfile: true, machineProbeCacheTick: -1);
    }

    private static void SetMachineProbeCache(object[] rows, long tick)
    {
        lock (MachineProbeCacheLock)
        {
            cachedMachineProbeRows = rows;
            cachedMachineProbeTick = tick;
        }
    }

    private static bool CachedMachineProbeRowsHaveLoadableInput()
    {
        foreach (var row in cachedMachineProbeRows)
        {
            var loadableInputs = row.GetType().GetProperty("loadable_inputs")?.GetValue(row) as Array;
            if (loadableInputs is { Length: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    private static object[] ReadMachines(Farm farm)
    {
        var minimalMachineProfile = string.Equals(SnapshotProfileContext.Current, "machine", StringComparison.OrdinalIgnoreCase);
        return ReadMachines(farm, includeLoadableInputs: false, minimalMachineProfile, machineProbeCacheTick: -1);
    }

    private static object[] ReadMachines(Farm farm, bool includeLoadableInputs, bool minimalMachineProfile, long machineProbeCacheTick)
    {
        return farm.objects.Pairs
            .Where(pair => pair.Value.bigCraftable.Value && pair.Value.GetMachineData() is not null)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Take(MaxMachineRowsPerSnapshot)
            .Select(pair =>
            {
                object machineData = minimalMachineProfile
                    ? new
                    {
                        status = "blocked",
                        reason = "machine_profile_minimal_skips_machine_data"
                    }
                    : ReadMachineDataSummary(pair.Value.GetMachineData());
                var loadableInputs = includeLoadableInputs
                    ? ReadMachineLoadableInputs(pair.Value)
                    : Array.Empty<object>();
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    qualified_item_id = pair.Value.QualifiedItemId,
                    display_name = pair.Value.DisplayName,
                    ready_for_harvest = pair.Value.readyForHarvest.Value,
                    minutes_until_ready = pair.Value.MinutesUntilReady,
                    machine_row_snapshot_limit = MaxMachineRowsPerSnapshot,
                    loadable_input_probe_slot_limit = MaxMachineInputProbeSlotsPerMachine,
                    loadable_input_probe_status = includeLoadableInputs ? "available_main_thread_cache" : "blocked_requires_main_thread_cache",
                    machine_probe_cache_tick = machineProbeCacheTick,
                    machine_data = machineData,
                    held_item = SummarizeItem(pair.Value.heldObject.Value),
                    loadable_inputs = loadableInputs
                };
            })
            .Where(machine => minimalMachineProfile ||
                machine.minutes_until_ready > 0 ||
                machine.ready_for_harvest ||
                machine.held_item is not null ||
                machine.loadable_inputs.Length > 0)
            .ToArray();
    }

    private static object ReadMachineDataSummary(object? machineData)
    {
        if (machineData is null)
        {
            return new
            {
                source = "Object.GetMachineData()",
                status = "unavailable"
            };
        }

        return new
        {
            source = "Object.GetMachineData()",
            status = "available",
            has_input = ReadBoolNullable(machineData, "HasInput"),
            has_output = ReadBoolNullable(machineData, "HasOutput"),
            use_first_valid_output = ReadBoolNullable(machineData, "UseFirstValidOutput"),
            output_rule_count = ReadCount(machineData, "OutputRules"),
            output_rules = ReadMachineOutputRules(machineData)
        };
    }

    private static object[] ReadMachineOutputRules(object machineData)
    {
        var outputRules = ReadMemberValue(machineData, "OutputRules");
        if (outputRules is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        return enumerable
            .Cast<object?>()
            .Where(rule => rule is not null)
            .Take(12)
            .Select(rule => new
            {
                id = ReadString(rule!, "Id") ?? string.Empty,
                required_item_id = ReadString(rule!, "RequiredItemId") ?? string.Empty,
                condition = ReadString(rule!, "Condition") ?? string.Empty,
                per_item_condition = ReadString(rule!, "PerItemCondition") ?? string.Empty,
                output_method = ReadString(rule!, "OutputMethod") ?? string.Empty,
                trigger_count = ReadCount(rule!, "Triggers"),
                required_item_count = ReadCount(rule!, "RequiredItems"),
                max_items = ReadIntNullable(rule!, "MaxItems"),
                minutes_until_ready = ReadIntNullable(rule!, "MinutesUntilReady"),
                output_item = ReadMachineOutputItem(ReadMemberValue(rule!, "OutputItem")),
                output_items = ReadMachineOutputItemList(ReadMemberValue(rule!, "OutputItems")),
                additional_consumed_item_count = ReadCount(rule!, "AdditionalConsumedItems"),
                additional_consumed_items = ReadMachineAdditionalConsumedItems(ReadMemberValue(rule!, "AdditionalConsumedItems"))
            })
            .ToArray();
    }

    private static object[] ReadMachineAdditionalConsumedItems(object? items)
    {
        if (items is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        return enumerable
            .Cast<object?>()
            .Where(item => item is not null)
            .Take(8)
            .Select(item =>
            {
                var itemId = ReadString(item!, "ItemId") ?? string.Empty;
                var amount = ReadIntNullable(item!, "Amount") ?? 1;
                var salePrice = ReadItemSalePrice(itemId);
                return new
                {
                    item_id = itemId,
                    qualified_item_id = NormalizeObjectQualifiedId(itemId),
                    amount,
                    sale_price = salePrice,
                    total_value = salePrice.HasValue ? salePrice.Value * Math.Max(1, amount) : (int?)null
                };
            })
            .ToArray();
    }

    private static object? ReadMachineOutputItem(object? output)
    {
        if (output is null)
        {
            return null;
        }

        var itemId = ReadString(output, "ItemId") ?? ReadString(output, "Item") ?? string.Empty;
        return new
        {
            item_id = itemId,
            qualified_item_id = NormalizeObjectQualifiedId(itemId),
            stack = ReadIntNullable(output, "Stack"),
            min_stack = ReadIntNullable(output, "MinStack"),
            max_stack = ReadIntNullable(output, "MaxStack"),
            quality = ReadIntNullable(output, "Quality"),
            price = ReadIntNullable(output, "Price"),
            sale_price = ReadItemSalePrice(itemId),
            copy_price = ReadBoolNullable(output, "CopyPrice"),
            copy_quality = ReadBoolNullable(output, "CopyQuality"),
            copy_color = ReadBoolNullable(output, "CopyColor"),
            preserve_type = ReadString(output, "PreserveType") ?? string.Empty,
            preserve_id = ReadString(output, "PreserveId") ?? string.Empty
        };
    }

    private static object[] ReadMachineOutputItemList(object? outputs)
    {
        if (outputs is not System.Collections.IEnumerable enumerable)
        {
            return Array.Empty<object>();
        }

        return enumerable
            .Cast<object?>()
            .Where(output => output is not null)
            .Take(8)
            .Select(ReadMachineOutputItem)
            .Where(output => output is not null)
            .Cast<object>()
            .ToArray();
    }

    private static object[] ReadMachineLoadableInputs(StardewValley.Object machine)
    {
        if (Game1.player is null ||
            machine.GetMachineData() is null ||
            machine.readyForHarvest.Value ||
            machine.MinutesUntilReady > 0)
        {
            return Array.Empty<object>();
        }

        var predictedOutputCache = new Dictionary<string, object>();
        var inputs = new List<object>();
        for (var index = 0; index < Game1.player.Items.Count && index < MaxMachineInputProbeSlotsPerMachine; index++)
        {
            var item = Game1.player.Items[index];
            if (item is null || item is not StardewValley.Object)
            {
                continue;
            }

            bool accepts;
            try
            {
                accepts = machine.performObjectDropInAction(item, probe: true, Game1.player);
            }
            catch (Exception ex)
            {
                inputs.Add(new
                {
                    slot_index = index,
                    item_id = item.ItemId,
                    qualified_item_id = item.QualifiedItemId,
                    display_name = item.DisplayName,
                    stack = item.Stack,
                    quality = item.Quality,
                    sale_price = item.salePrice(),
                    predicted_output = new
                    {
                        status = "blocked",
                        reason = "machine_input_probe_exception",
                        exception_type = ex.GetType().Name
                    },
                    probe_source = "Object.performObjectDropInAction(probe:true)",
                    load_executor_status = "blocked_probe_exception"
                });
                continue;
            }

            if (!accepts)
            {
                continue;
            }

            inputs.Add(new
            {
                slot_index = index,
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                sale_price = item.salePrice(),
                predicted_output = ReadPredictedMachineOutputCached(machine, item, predictedOutputCache),
                probe_source = "Object.performObjectDropInAction(probe:true)",
                load_executor_status = "covered_for_runtime_load"
            });
        }

        return inputs.ToArray();
    }

    private static object ReadPredictedMachineOutputCached(StardewValley.Object machine, Item inputItem, IDictionary<string, object> cache)
    {
        var key = inputItem.QualifiedItemId + "|" + inputItem.ItemId + "|" + inputItem.Quality + "|" + inputItem.Stack + "|" + inputItem.salePrice();
        if (cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        object predicted;
        try
        {
            predicted = ReadPredictedMachineOutput(machine, inputItem);
        }
        catch (Exception ex)
        {
            predicted = new
            {
                status = "blocked",
                reason = "machine_native_output_probe_exception",
                exception_type = ex.GetType().Name
            };
        }
        cache[key] = predicted;
        return predicted;
    }

    private static object ReadPredictedMachineOutput(StardewValley.Object machine, Item inputItem)
    {
        var machineData = machine.GetMachineData();
        if (machineData is null || Game1.player is null || machine.Location is null)
        {
            return new
            {
                status = "unavailable",
                reason = "machine_context_unavailable"
            };
        }

        if (!MachineDataUtility.TryGetMachineOutputRule(
            machine,
            machineData,
            MachineOutputTrigger.ItemPlacedInMachine,
            inputItem,
            Game1.player,
            machine.Location,
            out var outputRule,
            out _,
            out _,
            out _))
        {
            return new
            {
                status = "unavailable",
                reason = "machine_output_rule_unavailable"
            };
        }

        var outputEntries = outputRule.OutputItem;
        if (outputEntries is null || outputEntries.Count == 0)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_item_unavailable"
            };
        }

        if (!outputRule.UseFirstValidOutput && outputEntries.Count > 1)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_random_choice_not_probed"
            };
        }

        var outputData = MachineDataUtility.GetOutputData(machine, machineData, outputRule, inputItem, Game1.player, machine.Location);
        if (outputData is null)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_data_unavailable"
            };
        }

        if (!string.IsNullOrWhiteSpace(outputData.OutputMethod))
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_custom_method_not_probed"
            };
        }

        var outputItem = MachineDataUtility.GetOutputItem(machine, outputData, inputItem, Game1.player, probe: true, out var overrideMinutesUntilReady);
        if (outputItem is null)
        {
            return new
            {
                status = "blocked",
                reason = "machine_output_probe_returned_null"
            };
        }

        var ruleMinutesUntilReady = ReadIntNullable(outputRule, "MinutesUntilReady");
        var effectiveMinutesUntilReady = overrideMinutesUntilReady ?? ruleMinutesUntilReady;
        return new
        {
            status = "available",
            source = "MachineDataUtility.GetOutputItem(probe:true)",
            matched_rule_id = outputRule.Id ?? string.Empty,
            required_item_id = ReadString(outputRule, "RequiredItemId") ?? string.Empty,
            use_first_valid_output = outputRule.UseFirstValidOutput,
            rule_minutes_until_ready = ruleMinutesUntilReady,
            effective_minutes_until_ready = effectiveMinutesUntilReady,
            item = SummarizeItem(outputItem),
            sale_price = outputItem.salePrice(),
            stack = outputItem.Stack,
            quality = outputItem.Quality,
            preserve_type = outputItem is StardewValley.Object outputObject && outputObject.preserve.Value.HasValue
                ? outputObject.preserve.Value.Value.ToString()
                : string.Empty,
            preserved_item_id = outputItem is StardewValley.Object preservedObject
                ? preservedObject.GetPreservedItemId() ?? string.Empty
                : string.Empty,
            override_minutes_until_ready = overrideMinutesUntilReady
        };
    }

    private static object[] ReadChests(Farm farm)
    {
        return farm.objects.Pairs
            .Where(pair => pair.Value is Chest)
            .Select(pair =>
            {
                var chest = (Chest)pair.Value;
                return new
                {
                    tile_x = (int)pair.Key.X,
                    tile_y = (int)pair.Key.Y,
                    qualified_item_id = chest.QualifiedItemId,
                    display_name = chest.DisplayName,
                    special_chest_type = chest.SpecialChestType.ToString(),
                    item_count = chest.Items.Count,
                    items = chest.Items
                        .Select((item, index) => new
                        {
                            slot_index = index,
                            item = SummarizeItem(item)
                        })
                        .ToArray()
                };
            })
            .OrderBy(chest => chest.tile_y)
            .ThenBy(chest => chest.tile_x)
            .ToArray();
    }

    private static object[] ReadAnimals(Farm farm)
    {
        return farm.animals.Pairs
            .Select(pair => new
            {
                animal_id = pair.Key,
                name = pair.Value.Name,
                display_name = pair.Value.displayName,
                type = pair.Value.type.Value,
                building_type_i_live_in = pair.Value.buildingTypeILiveIn.Value,
                age = pair.Value.age.Value,
                friendship_toward_farmer = pair.Value.friendshipTowardFarmer.Value,
                produce_quality = pair.Value.produceQuality.Value,
                tile_x = pair.Value.TilePoint.X,
                tile_y = pair.Value.TilePoint.Y
            })
            .OrderBy(animal => animal.animal_id)
            .ToArray();
    }

    private static object[] ReadResourceClumps(Farm farm)
    {
        return farm.resourceClumps
            .Select(clump => new
            {
                tile_x = (int)clump.Tile.X,
                tile_y = (int)clump.Tile.Y,
                runtime_type = clump.GetType().FullName,
                parent_sheet_index = clump.parentSheetIndex.Value,
                width = clump.width.Value,
                height = clump.height.Value,
                health = clump.health.Value,
                is_giant_crop = clump is GiantCrop,
                giant_crop_id = clump is GiantCrop giant ? giant.Id : string.Empty,
                required_tool = clump is GiantCrop ? "axe" : string.Empty,
                executor_status = clump is GiantCrop ? "blocked_requires_giant_crop_executor" : string.Empty
            })
            .OrderBy(clump => clump.tile_y)
            .ThenBy(clump => clump.tile_x)
            .ToArray();
    }

    private static object[] ReadDebris(Farm farm)
    {
        return farm.debris
            .Select((debris, index) => new
            {
                debris_index = index,
                debris_type = debris.debrisType.Value.ToString(),
                chunk_type = debris.chunkType.Value,
                item_id = debris.itemId.Value,
                qualified_item_id = debris.item?.QualifiedItemId ?? debris.itemId.Value,
                item_quality = debris.itemQuality,
                item = SummarizeItem(debris.item),
                chunk_count = debris.Chunks.Count,
                chunk_final_y_level = debris.chunkFinalYLevel,
                chunk_final_y_target = debris.chunkFinalYTarget,
                time_since_done_bouncing = debris.timeSinceDoneBouncing,
                is_sinking = debris.isSinking.Value,
                is_essential_item = debris.isEssentialItem(),
                pickup_executor_status = "covered_for_runtime_collect",
                chunks = debris.Chunks
                    .Select((chunk, chunkIndex) => new
                    {
                        chunk_index = chunkIndex,
                        pixel_x = (int)MathF.Round(chunk.position.X),
                        pixel_y = (int)MathF.Round(chunk.position.Y),
                        tile_x = (int)((chunk.position.X + 32f) / Game1.tileSize),
                        tile_y = (int)((chunk.position.Y + 32f) / Game1.tileSize),
                        x_velocity = chunk.xVelocity.Value,
                        y_velocity = chunk.yVelocity.Value,
                        bounces = chunk.bounces,
                        has_passed_resting_line_once = chunk.hasPassedRestingLineOnce.Value,
                        alpha = chunk.alpha
                    })
                    .ToArray()
            })
            .ToArray();
    }

    private static object[] ReadWarps(Farm farm)
    {
        return farm.warps
            .Select(warp => new
            {
                x = warp.X,
                y = warp.Y,
                target_name = warp.TargetName,
                target_x = warp.TargetX,
                target_y = warp.TargetY
            })
            .OrderBy(warp => warp.y)
            .ThenBy(warp => warp.x)
            .ToArray();
    }

    private static object? SummarizeItem(Item? item)
    {
        return item is null
            ? null
            : new
            {
                item_id = item.ItemId,
                qualified_item_id = item.QualifiedItemId,
                display_name = item.DisplayName,
                stack = item.Stack,
                quality = item.Quality,
                sale_price = item.salePrice()
            };
    }

    private static string NormalizeObjectQualifiedId(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return string.Empty;
        }

        return itemId.StartsWith("(", StringComparison.Ordinal) ? itemId : "(O)" + itemId;
    }

    private static int? ReadItemSalePrice(string itemId)
    {
        var qualifiedId = NormalizeObjectQualifiedId(itemId);
        if (string.IsNullOrWhiteSpace(qualifiedId))
        {
            return null;
        }

        try
        {
            return ItemRegistry.Create(qualifiedId).salePrice();
        }
        catch
        {
            return null;
        }
    }

    private static string[] ReadStringList(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        if (propertyValue is null)
        {
            return Array.Empty<string>();
        }

        return ((System.Collections.IEnumerable)propertyValue)
            .Cast<object?>()
            .Where(item => item is not null)
            .Select(item => item!.ToString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static int[] ReadIntList(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        if (propertyValue is null)
        {
            return Array.Empty<int>();
        }

        return ((System.Collections.IEnumerable)propertyValue)
            .Cast<object?>()
            .Select(item => item is null ? (int?)null : Convert.ToInt32(item))
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
    }

    private static string? ReadString(object value, string property)
    {
        return ReadMemberValue(value, property)?.ToString();
    }

    private static int? ReadIntNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToInt32(propertyValue);
    }

    private static bool? ReadBoolNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToBoolean(propertyValue);
    }

    private static float? ReadFloatNullable(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is null ? null : Convert.ToSingle(propertyValue);
    }

    private static int ReadCount(object value, string property)
    {
        var propertyValue = ReadMemberValue(value, property);
        return propertyValue is System.Collections.ICollection collection ? collection.Count : 0;
    }

    private static object? ReadMemberValue(object value, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        var type = value.GetType();
        var property = type.GetProperty(memberName, flags);
        if (property is not null)
        {
            return property.GetValue(value);
        }

        var field = type.GetField(memberName, flags);
        return field?.GetValue(value);
    }
}
