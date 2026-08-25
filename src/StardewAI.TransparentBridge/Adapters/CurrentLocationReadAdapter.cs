using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewAI.Contracts.State;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter : ReadAdapterBase
{
    public override string Domain => "current_location";
    public override int Priority => 30;

    public override StateAdapterResult Collect(long tick)
    {
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        var actionIndex = location is null ? null : ReadMapActionIndex(location);
        var storageRequested =
            SnapshotProfileContext
                .IncludesPersistentMaterialInventoryGraph;
        var farm =
            location is null || !storageRequested
                ? null
                : Game1.getFarm();
        var storageInfrastructure =
            location is null || farm is null
                ? null
                : FarmReadAdapter.ReadStorageInfrastructure(
                    FarmReadAdapter.ReadCachedMaterialInventoryGraph(
                        farm,
                        Game1.player,
                        tick),
                    location.NameOrUniqueName);
        object chestsField =
            location is null
                ? Field(
                    (StorageInfrastructureProjection?)null,
                    "Game1.currentLocation unavailable",
                    tick,
                    "vanilla_1_6_storage_infrastructure")
                : !storageRequested
                    ? Unavailable(
                        "not_requested_by_snapshot_profile",
                        "SnapshotProfileContext.IncludesPersistentMaterialInventoryGraph",
                        tick,
                        "snapshot_profile")
                    : Field(
                        storageInfrastructure,
                        "farm.material_inventory_graph access_points filtered to Game1.currentLocation.NameOrUniqueName; canonical contents remain farm.material_inventory_graph.inventory_nodes[node_id]",
                        tick,
                        "vanilla_1_6_storage_infrastructure");
        var unavailable = location is null
            ? new[]
            {
                "current_location.identity",
                "current_location.display_name",
                "current_location.flags",
                "current_location.objects",
                "current_location.furniture",
                "current_location.debris",
                "current_location.chests",
                "current_location.terrain_features",
                "current_location.large_terrain_features",
                "current_location.resource_clumps",
                "current_location.crops",
                "current_location.planting_context",
                "current_location.shop_action_tiles",
                "current_location.mine_elevator_action_tiles",
                "current_location.drop_box_action_tiles",
                "current_location.arcade_action_tiles",
                "current_location.home_context",
                "current_location.route_context",
                "current_location.doors",
                "current_location.interior_doors",
                "current_location.warps",
                "current_location.panning",
                "current_location.map"
            }
            : Array.Empty<string>();

        return Section("current_location", new Dictionary<string, object>
        {
            ["identity"] = Field(location is null ? null : ReadIdentity(location), "Game1.currentLocation.Name/NameOrUniqueName", tick),
            ["display_name"] = Field(location is null ? null : ReadDisplayName(location), "Game1.currentLocation.GetDisplayName()/Name", tick),
            ["flags"] = Field(location is null ? null : ReadFlags(location), "Game1.currentLocation.IsOutdoors/IsFarm", tick),
            ["objects"] = Field(location is null ? null : ReadObjects(location), "Game1.currentLocation.objects", tick),
            ["furniture"] = Field(location is null ? null : ReadFurniture(location), "Game1.currentLocation.furniture live placement, rotation, footprint, held object and storage state", tick),
            ["debris"] = Field(location is null ? null : ReadCurrentLocationDebris(location), "Game1.currentLocation.debris live item and chunk fields", tick),
            ["chests"] = chestsField,
            ["terrain_features"] = Field(location is null ? null : ReadTerrainFeatures(location), "Game1.currentLocation.terrainFeatures", tick),
            ["large_terrain_features"] = Field(location is null ? null : ReadLargeTerrainFeatures(location), "Game1.currentLocation.largeTerrainFeatures; Bush native harvest projection", tick),
            ["resource_clumps"] = Field(location is null ? null : ReadCurrentLocationResourceClumps(location, Game1.player), "Game1.currentLocation.resourceClumps; ResourceClump.performToolAction/destroy decompiled projections", tick),
            ["crops"] = Field(location is null ? null : FarmReadAdapter.ReadCrops(location), "Game1.currentLocation terrain HoeDirt and IndoorPot.hoeDirt live crop state", tick, "vanilla_1_6_current_location_crops"),
            ["planting_context"] = Field(location is null ? null : ReadPlantingContext(location), "Game1.currentLocation planting APIs and HoeDirt terrain features", tick),
            ["shop_action_tiles"] = Field(actionIndex?.ShopActionTiles, "Game1.currentLocation map Action properties parsed by GameLocation.performAction", tick),
            ["mine_elevator_action_tiles"] = Field(actionIndex?.MineElevatorActionTiles, "GameLocation Buildings Action first token MineElevator or MineShaft Buildings/mine tile index 112; native performAction/checkAction elevator dispatch", tick),
            ["drop_box_action_tiles"] = Field(actionIndex?.DropBoxActionTiles, "Game1.currentLocation map Action=DropBox <box_id>; GameLocation.performAction native drop-box dispatch", tick),
            ["arcade_action_tiles"] = Field(actionIndex?.ArcadeActionTiles, "Game1.currentLocation map Action=Arcade_*; GameLocation.performAction native arcade dispatch", tick),
            ["home_context"] = Field(location is null ? null : ReadHomeContext(location), "Utility.getHomeOfFarmer(Game1.player); FarmHouse.getEntryLocation(); FarmHouse.GetPlayerBedSpot(); BedFurniture.IsBedHere", tick),
            ["route_context"] = Field(location is null ? null : ReadRouteContext(location), "Game1.currentLocation.isCollidingPosition local tile probes", tick),
            ["doors"] = Field(location is null ? null : ReadDoors(location), "Game1.currentLocation.doors", tick),
            ["interior_doors"] = Field(location is null ? null : ReadInteriorDoors(location), "Game1.currentLocation.interiorDoors", tick),
            ["warps"] = Field(location is null ? null : ReadWarps(location), "Game1.currentLocation.warps", tick),
            ["panning"] = Field(location is null ? null : ReadPanning(location, Game1.player), "GameLocation.orePanPoint; Pan.beginUsing/getPanItems on a detached NetFarmerRoot clone", tick),
            ["map"] = Field(location is null ? null : ReadMap(location), "Game1.currentLocation.map.Layers", tick)
        }, unavailable, location is null ? "unavailable" : "partial");
    }

    private static object ReadIdentity(GameLocation location)
    {
        return new
        {
            name = location.Name,
            name_or_unique_name = location.NameOrUniqueName,
            type = location.GetType().FullName
        };
    }

    private static string ReadDisplayName(GameLocation location)
    {
        return location.GetDisplayName() ?? location.Name;
    }

    private static object ReadFlags(GameLocation location)
    {
        return new
        {
            is_outdoors = location.IsOutdoors,
            is_farm = location.IsFarm
        };
    }

    private static object[] ReadObjects(GameLocation location)
    {
        return location.objects.Pairs
            .OrderBy(pair => pair.Key.X)
            .ThenBy(pair => pair.Key.Y)
            .Select(pair => ReadObject(location, pair.Key, pair.Value, Game1.player))
            .ToArray();
    }

    private static object ReadObject(GameLocation location, Vector2 tile, StardewObject item, Farmer player)
    {
        var harvest = ReadSpawnedObjectHarvest(location, tile, item, player);
        var clearance = ReadObjectClearance(location, tile, item, player);
        var crabPot = ReadCrabPotHarvest(tile, item, player);
        var crabPotBaitLoad = ReadCrabPotBaitLoad(item, player);
        var fence = ReadFenceState(item);
        var sign = ReadSignState(location, tile, item, player);
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            name = item.Name,
            display_name = item.DisplayName,
            stack = item.Stack,
            quality = item.Quality,
            type = item.GetType().FullName,
            object_type = item.Type,
            is_spawned_object = item.IsSpawnedObject,
            is_forage = item.isForage(),
            special_variable = item.SpecialVariable,
            is_quest_item = item.questItem.Value,
            quest_id = item.questId.Value ?? string.Empty,
            spawned_object_pickup_status = harvest.Status,
            projected_harvest_quality = harvest.Quality,
            projected_primary_quantity = harvest.PrimaryQuantity,
            projected_gatherer_duplicate = harvest.GathererDuplicate,
            projected_total_quantity = harvest.TotalQuantity,
            foraging_experience_on_success_min = harvest.ForagingExperience,
            foraging_experience_on_success_max = harvest.ForagingExperience,
            farming_experience_on_success_min = harvest.FarmingExperience,
            farming_experience_on_success_max = harvest.FarmingExperience,
            harvest_experience_status = harvest.ExperienceStatus,
            harvest_experience_basis = harvest.ExperienceBasis,
            clear_kind = clearance.ClearKind,
            clear_obstacle_executor_status = clearance.Status,
            required_tool_kind = clearance.RequiredToolKind,
            tool_slot_index = clearance.ToolSlotIndex,
            expected_tool_hits_to_clear = clearance.ExpectedToolHits,
            harvest_experience_skill_id = clearance.SkillId,
            harvest_experience_skill_index = clearance.SkillIndex,
            harvest_experience_on_success_min = clearance.Experience,
            harvest_experience_on_success_max = clearance.Experience,
            harvest_experience_condition = clearance.ExperienceCondition,
            harvest_experience_projection_status = clearance.ExperienceStatus,
            clear_output_projection_status = clearance.OutputStatus,
            clear_output_items = clearance.OutputItems,
            clear_output_items_json = System.Text.Json.JsonSerializer.Serialize(clearance.OutputItems),
            clear_output_qualified_item_id = clearance.OutputQualifiedItemId,
            clear_output_quantity_min = clearance.OutputQuantity,
            clear_output_quantity_max = clearance.OutputQuantity,
            clear_bonus_output_qualified_item_id = clearance.BonusOutputQualifiedItemId,
            clear_bonus_output_quantity_min = clearance.BonusOutputQuantity,
            clear_bonus_output_quantity_max = clearance.BonusOutputQuantity,
            artifact_spots_dug_before = clearance.ArtifactSpotsDugBefore,
            artifact_spots_dug_delta = clearance.ArtifactSpotsDugDelta,
            artifact_spots_dug_expected_after = clearance.ArtifactSpotsDugExpectedAfter,
            clear_terrain_feature_expected_after = clearance.TerrainFeatureExpectedAfter,
            defense_book_mail_before = clearance.DefenseBookMailBefore.HasValue
                ? clearance.DefenseBookMailBefore.Value ? 1 : 0
                : (int?)null,
            defense_book_mail_expected_after = clearance.DefenseBookMailExpectedAfter.HasValue
                ? clearance.DefenseBookMailExpectedAfter.Value ? 1 : 0
                : (int?)null,
            crab_pot_collect_status = crabPot.Status,
            crab_pot_tile_index = crabPot.TileIndex,
            crab_pot_ready_for_harvest = crabPot.ReadyForHarvest,
            crab_pot_owner_id = crabPot.OwnerId,
            crab_pot_bait_qualified_item_id = crabPot.BaitQualifiedItemId,
            crab_pot_bait_unit_state_sha256 = crabPot.BaitUnitStateSha256,
            crab_pot_bait_load_status = crabPotBaitLoad.Status,
            crab_pot_needs_bait = crabPotBaitLoad.NeedsBait,
            crab_pot_owner_has_luremaster = crabPotBaitLoad.OwnerHasLuremaster,
            crab_pot_owner_player_id_before_bait = crabPotBaitLoad.OwnerPlayerIdBefore,
            crab_pot_expected_owner_player_id_after_bait = crabPotBaitLoad.ExpectedOwnerPlayerIdAfter,
            crab_pot_current_bait_runtime_type = crabPotBaitLoad.CurrentBaitRuntimeType,
            crab_pot_current_bait_quality = crabPotBaitLoad.CurrentBaitQuality,
            crab_pot_bait_load_inventory_rows = crabPotBaitLoad.InventoryBaitRows,
            crab_pot_bait_load_inventory_rows_json = System.Text.Json.JsonSerializer.Serialize(crabPotBaitLoad.InventoryBaitRows),
            crab_pot_bait_load_native_contract = crabPotBaitLoad.NativeContract,
            crab_pot_output_runtime_type = crabPot.OutputRuntimeType,
            crab_pot_output_qualified_item_id = crabPot.OutputQualifiedItemId,
            crab_pot_output_quality = crabPot.OutputQuality,
            crab_pot_output_unit_state_sha256 = crabPot.OutputUnitStateSha256,
            crab_pot_output_state_context = string.IsNullOrWhiteSpace(crabPot.OutputRuntimeType) ? "not_applicable" : "post_inventory_receive",
            crab_pot_expected_output_items_json = crabPot.OutputItemsJson,
            crab_pot_output_stack_before = crabPot.OutputStackBefore,
            crab_pot_output_stack_on_collect = crabPot.OutputStackOnCollect,
            crab_pot_book_double_roll_succeeded = crabPot.BookDoubleRollSucceeded,
            crab_pot_book_crabbing_owned = crabPot.BookCrabbingOwned,
            crab_pot_book_double_applied = crabPot.BookDoubleApplied,
            crab_pot_inventory_accepts_base_stack = crabPot.InventoryAcceptsBaseStack,
            crab_pot_inventory_accepts_collect_stack = crabPot.InventoryAcceptsCollectStack,
            crab_pot_fishing_experience_on_success_min = crabPot.FishingExperience,
            crab_pot_fishing_experience_on_success_max = crabPot.FishingExperience,
            crab_pot_experience_projection_status = crabPot.ExperienceStatus,
            crab_pot_fish_collection_eligible = crabPot.FishCollectionEligible,
            crab_pot_fish_caught_count_before = crabPot.FishCaughtCountBefore,
            crab_pot_fish_caught_count_after = crabPot.FishCaughtCountAfter,
            crab_pot_fish_caught_max_size_before = crabPot.FishCaughtMaxSizeBefore,
            crab_pot_catch_size_min = crabPot.CatchSizeMin,
            crab_pot_catch_size_max = crabPot.CatchSizeMax,
            crab_pot_catch_size_projection_status = crabPot.CatchSizeProjectionStatus,
            fence_state = fence,
            sign_state = sign
        };
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
                context_tags = item.GetContextTags()
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToArray()
            };
    }

    private static object[] ReadTerrainFeatures(GameLocation location)
    {
        return location.terrainFeatures.Pairs
            .OrderBy(pair => pair.Key.X)
            .ThenBy(pair => pair.Key.Y)
            .Select(pair => ReadTerrainFeature(pair.Key, pair.Value))
            .ToArray();
    }

    private static object ReadTerrainFeature(Vector2 tile, TerrainFeature feature)
    {
        return ReadTerrainFeatureDetails(tile, feature);
    }

    private static object[] ReadWarps(GameLocation location)
    {
        return location.warps
            .OrderBy(warp => warp.X)
            .ThenBy(warp => warp.Y)
            .ThenBy(warp => warp.TargetName, StringComparer.Ordinal)
            .Select(warp => new
            {
                x = warp.X,
                y = warp.Y,
                target_name = warp.TargetName,
                target_x = warp.TargetX,
                target_y = warp.TargetY,
                flip_farmer = warp.flipFarmer.Value,
                npc_only = warp.npcOnly.Value
            })
            .ToArray();
    }

    private static object[] ReadDoors(GameLocation location)
    {
        return location.doors.Pairs
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => new
            {
                tile_x = pair.Key.X,
                tile_y = pair.Key.Y,
                target = pair.Value,
                action = location.doesTileHaveProperty(pair.Key.X, pair.Key.Y, "Action", "Buildings")
            })
            .ToArray();
    }

    private static object[] ReadInteriorDoors(GameLocation location)
    {
        return location.interiorDoors.Doors
            .OrderBy(door => door.Position.Y)
            .ThenBy(door => door.Position.X)
            .Select(door => new
            {
                tile_x = door.Position.X,
                tile_y = door.Position.Y,
                open = door.Value,
                action = location.doesTileHaveProperty(door.Position.X, door.Position.Y, "Action", "Buildings"),
                touch_action = location.doesTileHaveProperty(door.Position.X, door.Position.Y, "TouchAction", "Back")
            })
            .ToArray();
    }

    private sealed class MapActionIndex
    {
        public object[] ShopActionTiles { get; set; } = Array.Empty<object>();
        public object[] MineElevatorActionTiles { get; set; } = Array.Empty<object>();
        public object[] DropBoxActionTiles { get; set; } = Array.Empty<object>();
        public object[] ArcadeActionTiles { get; set; } = Array.Empty<object>();
    }

    private static MapActionIndex ReadMapActionIndex(GameLocation location)
    {
        var map = location.map;
        if (map?.Layers is null || map.Layers.Count == 0)
        {
            return new MapActionIndex
            {
                ShopActionTiles = Array.Empty<object>(),
                MineElevatorActionTiles = Array.Empty<object>(),
                DropBoxActionTiles = Array.Empty<object>(),
                ArcadeActionTiles = Array.Empty<object>()
            };
        }

        var width = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerWidth);
        var height = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerHeight);
        var shopTiles = new List<object>();
        var mineElevatorTiles = new List<object>();
        var dropBoxTiles = new List<object>();
        var arcadeTiles = new List<object>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (location is MineShaft mine && mine.getTileIndexAt(x, y, "Buildings", "mine") == 112)
                {
                    mineElevatorTiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action = (string?)null,
                        action_type = "MineElevator",
                        native_dispatch = "MineShaft.checkAction Buildings/mine tile index 112",
                        mine_tile_index = 112,
                        lowest_level_reached = MineShaft.lowestLevelReached,
                        menu_available = MineShaft.lowestLevelReached >= 5
                    });
                }
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                var parsed = ParseShopAction(location, action, x, y);
                if (parsed is not null)
                {
                    shopTiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action,
                        parsed,
                        owner_service_status = ReadOwnerServiceStatus(location, parsed),
                        service_time_status = ReadShopServiceTimeStatus(location, parsed, FindEntranceGateForCurrentLocation(location))
                    });
                }

                var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && string.Equals(parts[0], "MineElevator", StringComparison.Ordinal))
                {
                    mineElevatorTiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action,
                        action_type = parts[0],
                        native_dispatch = "GameLocation.performAction Action=MineElevator",
                        mine_tile_index = (int?)null,
                        lowest_level_reached = MineShaft.lowestLevelReached,
                        menu_available = MineShaft.lowestLevelReached >= 5
                    });
                }

                if (parts.Length > 0 && string.Equals(parts[0], "DropBox", StringComparison.OrdinalIgnoreCase))
                {
                    dropBoxTiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action,
                        action_type = parts[0],
                        box_id = Part(parts, 1) ?? string.Empty
                    });
                }

                if (parts.Length > 0 && parts[0].StartsWith("Arcade_", StringComparison.OrdinalIgnoreCase))
                {
                    arcadeTiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action,
                        action_type = parts[0],
                        unlocked = !string.Equals(parts[0], "Arcade_Minecart", StringComparison.OrdinalIgnoreCase) || Game1.player.hasSkullKey,
                        unlock_requirement = string.Equals(parts[0], "Arcade_Minecart", StringComparison.OrdinalIgnoreCase) ? "player.has_skull_key" : string.Empty
                    });
                }
            }
        }

        return new MapActionIndex
        {
            ShopActionTiles = shopTiles.ToArray(),
            MineElevatorActionTiles = mineElevatorTiles.ToArray(),
            DropBoxActionTiles = dropBoxTiles.ToArray(),
            ArcadeActionTiles = arcadeTiles.ToArray()
        };
    }

    private static object ReadShopServiceTimeStatus(GameLocation location, object parsed, EntranceGate? entranceGate)
    {
        var directOpenTime = ReadNullableIntProperty(parsed, "open_time");
        var directCloseTime = ReadNullableIntProperty(parsed, "close_time");
        var openTime = directOpenTime ?? entranceGate?.OpenTime;
        var closeTime = directCloseTime ?? entranceGate?.CloseTime;
        var timeGateKnown = openTime.HasValue && closeTime.HasValue;
        bool? timeAllowed = null;
        if (openTime is int open && closeTime is int close)
        {
            timeAllowed = Game1.timeOfDay >= open && Game1.timeOfDay < close;
        }
        var storesClosedForFestival = entranceGate?.FestivalClosed ?? (location.InValleyContext() && GameLocation.AreStoresClosedForFestival());
        var seedShopWednesdayClosed = entranceGate?.SeedShopWednesdayClosed == true;
        var friendshipAllowed = entranceGate?.FriendshipAllowed;
        var greenRainOverride = entranceGate?.GreenRainOverride == true;
        var blockReasons = new List<string>();
        if (storesClosedForFestival)
        {
            blockReasons.Add("stores_closed_for_festival");
        }

        if (seedShopWednesdayClosed)
        {
            blockReasons.Add("seed_shop_wednesday_closed_before_community_center_event");
        }

        if (timeGateKnown && timeAllowed == false)
        {
            blockReasons.Add(Game1.timeOfDay < openTime!.Value ? "shop_not_open_yet" : "shop_closed_for_day");
        }

        if (friendshipAllowed == false)
        {
            blockReasons.Add("shop_entrance_friendship_gate_blocked");
        }

        return new
        {
            current_time = Game1.timeOfDay,
            current_day = Game1.dayOfMonth,
            current_day_name = Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth),
            current_season = Game1.currentSeason,
            festival_day = Utility.isFestivalDay(),
            stores_closed_for_festival = storesClosedForFestival,
            raining_here = location.IsRainingHere(),
            green_raining_here = location.IsGreenRainingHere(),
            time_gate_known = timeGateKnown,
            time_gate_source = directOpenTime.HasValue && directCloseTime.HasValue
                ? "shop_action"
                : (entranceGate is not null ? "entrance_locked_door_warp" : null),
            entrance_location_id = entranceGate?.FromLocation,
            entrance_tile_x = entranceGate?.FromX,
            entrance_tile_y = entranceGate?.FromY,
            entrance_action = entranceGate?.RawAction,
            open_time = openTime,
            effective_open_time = entranceGate?.EffectiveOpenTime ?? openTime,
            close_time = closeTime,
            has_town_key = entranceGate?.HasTownKey ?? (Game1.player?.HasTownKey ?? false),
            entrance_npc_name = entranceGate?.NpcName,
            entrance_min_friendship = entranceGate?.MinFriendship,
            entrance_friendship_points = entranceGate?.FriendshipPoints,
            entrance_friendship_allowed = friendshipAllowed,
            seed_shop_wednesday_closed = seedShopWednesdayClosed,
            entrance_green_rain_override = greenRainOverride,
            time_allowed = entranceGate?.TimeAllowed ?? timeAllowed,
            allowed_now = blockReasons.Count == 0 || greenRainOverride,
            block_reasons = blockReasons.ToArray()
        };
    }

    private static EntranceGate? FindEntranceGateForCurrentLocation(GameLocation currentLocation)
    {
        var currentLocationId = currentLocation.NameOrUniqueName;
        return Game1.locations
            .Where(location => location is not null && location.map?.Layers is not null)
            .SelectMany(location => ReadLockedDoorWarpEntranceGates(location, currentLocationId))
            .OrderBy(gate => gate.AllowedNow ? 0 : 1)
            .ThenBy(gate => string.IsNullOrWhiteSpace(gate.NpcName) ? 0 : 1)
            .ThenBy(gate => gate.MinFriendship)
            .ThenBy(gate => gate.OpenTime ?? int.MaxValue)
            .FirstOrDefault();
    }

    private static IEnumerable<EntranceGate> ReadLockedDoorWarpEntranceGates(GameLocation location, string targetLocationId)
    {
        var map = location.map;
        if (map?.Layers is null || map.Layers.Count == 0)
        {
            yield break;
        }

        var width = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerWidth);
        var height = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerHeight);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                var gate = ParseLockedDoorWarpEntranceGate(location, x, y, action, targetLocationId);
                if (gate is not null)
                {
                    yield return gate;
                }
            }
        }
    }

    private static EntranceGate? ParseLockedDoorWarpEntranceGate(GameLocation fromLocation, int x, int y, string action, string targetLocationId)
    {
        var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !string.Equals(parts[0], "LockedDoorWarp", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var targetLocation = Part(parts, 3);
        if (!string.Equals(targetLocation, targetLocationId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var openTime = ParseIntPart(parts, 4);
        var closeTime = ParseIntPart(parts, 5);
        var npcName = Part(parts, 6);
        var minFriendship = ParseIntPart(parts, 7) ?? 0;
        var friendshipPoints = ReadFriendshipPoints(npcName);
        var friendshipAllowed = minFriendship <= 0 || fromLocation.IsWinterHere() || (friendshipPoints.HasValue && friendshipPoints.Value >= minFriendship);
        var effectiveOpenTime = string.Equals(targetLocation, "FishShop", StringComparison.OrdinalIgnoreCase) && Game1.player?.mailReceived.Contains("willyHours") == true
            ? 800
            : openTime;
        var inValleyContext = fromLocation.InValleyContext();
        var hasTownKey = Game1.player?.HasTownKey == true && inValleyContext && !(fromLocation.GetType().Name == "BeachNightMarket" && targetLocation != "FishShop");
        var seedShopWednesdayClosed = string.Equals(targetLocation, "SeedShop", StringComparison.OrdinalIgnoreCase)
            && string.Equals(Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth), "Wed", StringComparison.OrdinalIgnoreCase)
            && !Utility.HasAnyPlayerSeenEvent("191393")
            && !hasTownKey;
        var timeAllowed = hasTownKey || (effectiveOpenTime.HasValue && closeTime.HasValue && Game1.timeOfDay >= effectiveOpenTime.Value && Game1.timeOfDay < closeTime.Value);
        var greenRainOverride = fromLocation.IsGreenRainingHere()
            && Game1.year == 1
            && fromLocation.GetType().Name != "Beach"
            && fromLocation.GetType().Name != "Forest"
            && !string.Equals(targetLocation, "AdventureGuild", StringComparison.OrdinalIgnoreCase);
        var storesClosedForFestival = fromLocation.InValleyContext() && GameLocation.AreStoresClosedForFestival();
        var allowedNow = !storesClosedForFestival && !seedShopWednesdayClosed && ((timeAllowed && friendshipAllowed) || greenRainOverride);

        return new EntranceGate(
            fromLocation.NameOrUniqueName,
            x,
            y,
            action,
            openTime,
            effectiveOpenTime,
            closeTime,
            npcName,
            minFriendship,
            friendshipPoints,
            hasTownKey,
            storesClosedForFestival,
            seedShopWednesdayClosed,
            timeAllowed,
            friendshipAllowed,
            greenRainOverride,
            allowedNow);
    }

    private static int? ReadFriendshipPoints(string? npcName)
    {
        if (string.IsNullOrWhiteSpace(npcName) ||
            Game1.player is null ||
            !Game1.player.friendshipData.TryGetValue(npcName, out var friendship) ||
            friendship is null)
        {
            return null;
        }

        return friendship.Points;
    }

    private static object ReadOwnerServiceStatus(GameLocation location, object parsed)
    {
        var ownerNpc = ReadStringProperty(parsed, "owner_npc");
        if (string.IsNullOrWhiteSpace(ownerNpc))
        {
            return new
            {
                owner_required = false,
                owner_npc = (string?)null,
                owner_found = (bool?)null,
                in_service_area = (bool?)null,
                block_reason = (string?)null
            };
        }

        var owner = location.characters.FirstOrDefault(npc =>
            npc is not null &&
            string.Equals(npc.Name, ownerNpc, StringComparison.OrdinalIgnoreCase) &&
            ReferenceEquals(npc.currentLocation, location));
        if (owner is null)
        {
            return new
            {
                owner_required = true,
                owner_npc = ownerNpc,
                owner_found = false,
                owner_tile_x = (int?)null,
                owner_tile_y = (int?)null,
                in_service_area = false,
                block_reason = "owner_npc_not_in_current_location"
            };
        }

        var serviceArea = ReadObjectProperty(parsed, "owner_service_area");
        var inServiceArea = IsTileInArea(owner.TilePoint.X, owner.TilePoint.Y, serviceArea);
        return new
        {
            owner_required = true,
            owner_npc = ownerNpc,
            owner_found = true,
            owner_tile_x = owner.TilePoint.X,
            owner_tile_y = owner.TilePoint.Y,
            in_service_area = inServiceArea,
            block_reason = inServiceArea ? null : "owner_npc_not_at_service_counter"
        };
    }

    private static bool IsTileInArea(int tileX, int tileY, object? area)
    {
        if (area is null)
        {
            return false;
        }

        var x = ReadIntProperty(area, "x");
        var y = ReadIntProperty(area, "y");
        var width = ReadIntProperty(area, "width");
        var height = ReadIntProperty(area, "height");
        return width > 0 && height > 0 &&
            tileX >= x && tileX < x + width &&
            tileY >= y && tileY < y + height;
    }

    private static object ReadHomeContext(GameLocation currentLocation)
    {
        var home = Game1.player is null ? null : Utility.getHomeOfFarmer(Game1.player);
        if (home is null)
        {
            return new
            {
                home_available = false,
                reason = "home_location_unavailable"
            };
        }

        var entry = home.getEntryLocation();
        var bed = home.GetPlayerBedSpot();
        return new
        {
            home_available = true,
            home_location_id = home.NameOrUniqueName,
            home_type = home.GetType().FullName,
            current_location_id = currentLocation.NameOrUniqueName,
            current_location_is_home = string.Equals(currentLocation.NameOrUniqueName, home.NameOrUniqueName, StringComparison.Ordinal),
            entry_tile_x = entry.X,
            entry_tile_y = entry.Y,
            bed_tile_x = bed.X,
            bed_tile_y = bed.Y,
            bed_tile_has_bed = BedFurniture.IsBedHere(home, bed.X, bed.Y),
            sleep_executor_enabled = true
        };
    }

    private static object ReadRouteContext(GameLocation location)
    {
        const int radius = 6;
        var playerTile = Game1.player.TilePoint;
        var probes = new List<object>();
        for (var y = playerTile.Y - radius; y <= playerTile.Y + radius; y++)
        {
            for (var x = playerTile.X - radius; x <= playerTile.X + radius; x++)
            {
                if (!location.isTileOnMap(new Vector2(x, y)))
                {
                    probes.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        on_map = false,
                        collision_blocked = true,
                        action = (string?)null,
                        warp_target = (string?)null
                    });
                    continue;
                }

                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var warp = location.warps.FirstOrDefault(candidate => candidate.X == x && candidate.Y == y);
                var collision = location.isCollidingPosition(
                    new Rectangle(x * 64 + 1, y * 64 + 1, 62, 62),
                    Game1.viewport,
                    Game1.player);

                probes.Add(new
                {
                    tile_x = x,
                    tile_y = y,
                    on_map = true,
                    collision_blocked = collision,
                    action,
                    warp_target = warp?.TargetName
                });
            }
        }

        return new
        {
            player_tile_x = playerTile.X,
            player_tile_y = playerTile.Y,
            probe_radius = radius,
            probe_rect_offset_x = 1,
            probe_rect_offset_y = 1,
            probe_rect_width = 62,
            probe_rect_height = 62,
            probe_count = probes.Count,
            probes
        };
    }

    private static object? ParseShopAction(GameLocation location, string action, int tileX, int tileY)
    {
        var parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        if (string.Equals(parts[0], "OpenShop", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "open_shop",
                shop_id = Part(parts, 1),
                required_direction = Part(parts, 2),
                open_time = ParseIntPart(parts, 3),
                close_time = ParseIntPart(parts, 4),
                owner_area = ParseOwnerArea(parts)
            };
        }

        if (string.Equals(parts[0], "Buy", StringComparison.OrdinalIgnoreCase))
        {
            var legacyShopId = Part(parts, 1);
            return new
            {
                kind = "legacy_buy",
                legacy_shop_id = legacyShopId,
                shop_id = ShopIdResolver.ResolveLegacyBuy(location, legacyShopId)
            };
        }

        if (string.Equals(parts[0], "JojaShop", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "joja_shop",
                shop_id = "Joja"
            };
        }

        if (string.Equals(parts[0], "Blacksmith", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "dialogue_shop",
                shop_id = "Blacksmith",
                owner_npc = "Clint",
                owner_service_area = CounterServiceArea(tileX, tileY),
                dialogue_key = "Blacksmith",
                shop_response_key = "Shop",
                opens_menu_after_response = true
            };
        }

        if (string.Equals(parts[0], "Carpenter", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "dialogue_shop",
                shop_id = "Carpenter",
                owner_npc = "Robin",
                owner_service_area = CounterServiceArea(tileX, tileY),
                dialogue_key = "carpenter",
                shop_response_key = "Shop",
                opens_menu_after_response = true
            };
        }

        if (string.Equals(parts[0], "AnimalShop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[0], "Marnie", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "dialogue_shop",
                shop_id = "AnimalShop",
                owner_npc = "Marnie",
                owner_service_area = CounterServiceArea(tileX, tileY),
                dialogue_key = "Marnie",
                shop_response_key = "Supplies",
                opens_menu_after_response = true
            };
        }

        if (string.Equals(parts[0], "AdventureShop", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parts[0], "AdventureGuild", StringComparison.OrdinalIgnoreCase))
        {
            return new
            {
                kind = "direct_or_dialogue_shop",
                shop_id = "AdventureShop",
                owner_npc = "Marlon",
                owner_service_area = CounterServiceArea(tileX, tileY),
                dialogue_key = "adventureGuild",
                shop_response_key = "Shop",
                opens_menu_after_response = true
            };
        }

        return null;
    }

    private static object CounterServiceArea(int actionTileX, int actionTileY)
    {
        return new
        {
            x = actionTileX - 2,
            y = actionTileY - 2,
            width = 5,
            height = 3,
            source = "derived_from_dialogue_shop_action_tile",
            rule = "owner must be within two tiles horizontally and not below the counter action tile"
        };
    }

    private static object? ParseOwnerArea(string[] parts)
    {
        var x = ParseIntPart(parts, 5);
        var y = ParseIntPart(parts, 6);
        var width = ParseIntPart(parts, 7);
        var height = ParseIntPart(parts, 8);
        return x.HasValue || y.HasValue || width.HasValue || height.HasValue
            ? new { x, y, width, height }
            : null;
    }

    private static string? Part(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length ? parts[index] : null;
    }

    private static int? ParseIntPart(string[] parts, int index)
    {
        return index >= 0 && index < parts.Length && int.TryParse(parts[index], out var value)
            ? value
            : null;
    }

    private static string? ReadStringProperty(object source, string propertyName)
    {
        return source.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.GetValue(source) as string;
    }

    private static object? ReadObjectProperty(object source, string propertyName)
    {
        return source.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.GetValue(source);
    }

    private static int ReadIntProperty(object source, string propertyName)
    {
        var value = source.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.GetValue(source);
        return value is int number ? number : 0;
    }

    private static int? ReadNullableIntProperty(object source, string propertyName)
    {
        var value = source.GetType()
            .GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.GetValue(source);
        return value is int number ? number : null;
    }

    private sealed record EntranceGate(
        string FromLocation,
        int FromX,
        int FromY,
        string RawAction,
        int? OpenTime,
        int? EffectiveOpenTime,
        int? CloseTime,
        string? NpcName,
        int MinFriendship,
        int? FriendshipPoints,
        bool HasTownKey,
        bool FestivalClosed,
        bool SeedShopWednesdayClosed,
        bool TimeAllowed,
        bool FriendshipAllowed,
        bool GreenRainOverride,
        bool AllowedNow);

    private static object ReadMap(GameLocation location)
    {
        var layers = location.map?.Layers
            .Cast<xTile.Layers.Layer>()
            .Select((layer, index) => new
            {
                index,
                id = layer.Id,
                width = layer.LayerWidth,
                height = layer.LayerHeight
            })
            .OrderBy(layer => layer.index)
            .ToArray();

        return new
        {
            id = location.map?.Id,
            width = layers?.Length > 0 ? layers.Max(layer => layer.width) : (int?)null,
            height = layers?.Length > 0 ? layers.Max(layer => layer.height) : (int?)null,
            layer_count = layers?.Length ?? 0,
            layers = layers ?? Array.Empty<object>()
        };
    }
}
