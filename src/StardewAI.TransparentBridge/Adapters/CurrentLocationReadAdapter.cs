using Microsoft.Xna.Framework;
using StardewModdingAPI;
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
        var unavailable = location is null
            ? new[]
            {
                "current_location.identity",
                "current_location.display_name",
                "current_location.flags",
                "current_location.objects",
                "current_location.chests",
                "current_location.terrain_features",
                "current_location.planting_context",
                "current_location.shop_action_tiles",
                "current_location.home_context",
                "current_location.route_context",
                "current_location.doors",
                "current_location.interior_doors",
                "current_location.warps",
                "current_location.map"
            }
            : Array.Empty<string>();

        return Section("current_location", new Dictionary<string, object>
        {
            ["identity"] = Field(location is null ? null : ReadIdentity(location), "Game1.currentLocation.Name/NameOrUniqueName", tick),
            ["display_name"] = Field(location is null ? null : ReadDisplayName(location), "Game1.currentLocation.GetDisplayName()/Name", tick),
            ["flags"] = Field(location is null ? null : ReadFlags(location), "Game1.currentLocation.IsOutdoors/IsFarm", tick),
            ["objects"] = Field(location is null ? null : ReadObjects(location), "Game1.currentLocation.objects", tick),
            ["chests"] = Field(location is null ? null : ReadChests(location), "Game1.currentLocation.objects[*] as Chest", tick),
            ["terrain_features"] = Field(location is null ? null : ReadTerrainFeatures(location), "Game1.currentLocation.terrainFeatures", tick),
            ["planting_context"] = Field(location is null ? null : ReadPlantingContext(location), "Game1.currentLocation planting APIs and HoeDirt terrain features", tick),
            ["shop_action_tiles"] = Field(location is null ? null : ReadShopActionTiles(location), "Game1.currentLocation map Action properties parsed by GameLocation.performAction", tick),
            ["home_context"] = Field(location is null ? null : ReadHomeContext(location), "Utility.getHomeOfFarmer(Game1.player); FarmHouse.getEntryLocation(); FarmHouse.GetPlayerBedSpot(); BedFurniture.IsBedHere", tick),
            ["route_context"] = Field(location is null ? null : ReadRouteContext(location), "Game1.currentLocation.isCollidingPosition local tile probes", tick),
            ["doors"] = Field(location is null ? null : ReadDoors(location), "Game1.currentLocation.doors", tick),
            ["interior_doors"] = Field(location is null ? null : ReadInteriorDoors(location), "Game1.currentLocation.interiorDoors", tick),
            ["warps"] = Field(location is null ? null : ReadWarps(location), "Game1.currentLocation.warps", tick),
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
            .Select(pair => ReadObject(pair.Key, pair.Value))
            .ToArray();
    }

    private static object ReadObject(Vector2 tile, StardewObject item)
    {
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
            type = item.GetType().FullName
        };
    }

    private static object[] ReadChests(GameLocation location)
    {
        return location.objects.Pairs
            .Where(pair => pair.Value is Chest)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
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
                quality = item.Quality
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

    private static object ReadPlantingContext(GameLocation location)
    {
        var seedCandidates = Game1.player?.Items
            .Select((item, index) => ReadSeedCandidate(item, index, location))
            .Where(item => item is not null)
            .Cast<object>()
            .ToArray() ?? Array.Empty<object>();

        return new
        {
            location_id = location.NameOrUniqueName,
            season = location.GetSeason().ToString().ToLowerInvariant(),
            is_outdoors = location.IsOutdoors,
            is_farm = location.IsFarm,
            is_greenhouse = location.IsGreenhouse,
            seeds_ignore_seasons_here = location.SeedsIgnoreSeasonsHere(),
            can_plant_here_default = location.GetData()?.CanPlantHere ?? location.IsFarm,
            candidate_seed_count = seedCandidates.Length,
            hoe_dirt_tiles = ReadHoeDirtPlantingTiles(location, seedCandidates)
        };
    }

    private static object? ReadSeedCandidate(Item? item, int index, GameLocation location)
    {
        if (item is null)
        {
            return null;
        }

        var seedId = Crop.ResolveSeedId(item.ItemId, location);
        var cropCatalogMatch = Game1.cropData.TryGetValue(seedId, out var cropData);
        if (item.Category != StardewObject.SeedsCategory && !cropCatalogMatch)
        {
            return null;
        }

        return new
        {
            slot_index = index,
            item_id = item.ItemId,
            qualified_item_id = item.QualifiedItemId,
            stack = item.Stack,
            category = item.Category,
            seed_id = seedId,
            crop_catalog_match = cropCatalogMatch,
            crop_seasons = cropData?.Seasons?.Select(season => season.ToString().ToLowerInvariant()).ToArray() ?? Array.Empty<string>()
        };
    }

    private static object[] ReadHoeDirtPlantingTiles(GameLocation location, object[] seedCandidates)
    {
        return location.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .Select(pair => ReadHoeDirtPlantingTile(location, pair.Key, (HoeDirt)pair.Value, seedCandidates))
            .ToArray();
    }

    private static object ReadHoeDirtPlantingTile(GameLocation location, Vector2 tile, HoeDirt dirt, object[] seedCandidates)
    {
        var isGardenPot = location.objects.TryGetValue(tile, out var tileObject) && tileObject is IndoorPot;
        var indoorPotBypass = isGardenPot && !location.IsOutdoors;
        return new
        {
            tile_x = (int)tile.X,
            tile_y = (int)tile.Y,
            has_crop = dirt.crop is not null,
            is_garden_pot = isGardenPot,
            indoor_pot_season_bypass = indoorPotBypass,
            occupied_object_qualified_id = tileObject?.QualifiedItemId,
            fertilizer_id = dirt.fertilizer.Value,
            fertilizer_speed_boost = dirt.GetFertilizerSpeedBoost(),
            agriculturist_speed_boost = Game1.player?.professions.Contains(Farmer.agriculturist) == true ? 0.1f : 0f,
            has_paddy_crop = dirt.hasPaddyCrop(),
            paddy_near_water_cache = dirt.nearWaterForPaddy.Value,
            paddy_water_scan_radius = 3,
            paddy_water_eligible = PaddyWaterEligible(location, tile, isGardenPot),
            paddy_speed_boost = dirt.hasPaddyCrop() && PaddyWaterEligible(location, tile, isGardenPot) ? 0.25f : 0f,
            seed_results = seedCandidates.Select(seed => ReadSeedTilePlantingResult(location, tile, dirt, seed, isGardenPot, indoorPotBypass)).ToArray()
        };
    }

    private static object ReadSeedTilePlantingResult(GameLocation location, Vector2 tile, HoeDirt dirt, object seedCandidate, bool isGardenPot, bool indoorPotBypass)
    {
        var seedType = seedCandidate.GetType();
        var seedId = (string?)seedType.GetProperty("seed_id")?.GetValue(seedCandidate) ?? string.Empty;
        var slotIndex = (int?)seedType.GetProperty("slot_index")?.GetValue(seedCandidate);
        var season = location.GetSeason();
        var cropDataFound = Game1.cropData.TryGetValue(seedId, out var cropData);
        var seasonAllowed = indoorPotBypass || location.SeedsIgnoreSeasonsHere() || (cropData?.Seasons?.Contains(season) ?? false);
        string? deniedMessage = null;
        var canPlantSeedsHere = indoorPotBypass || location.CanPlantSeedsHere(seedId, (int)tile.X, (int)tile.Y, isGardenPot, out deniedMessage);
        var baseGrowDays = cropData?.DaysInPhase?.Where(day => day < 99999).Sum();
        var isPaddyCrop = cropData is not null && ReadBool(cropData, "IsPaddyCrop") == true;
        var paddyEligible = isPaddyCrop && PaddyWaterEligible(location, tile, isGardenPot);
        var speedBoostWithoutPaddy = dirt.GetFertilizerSpeedBoost() + (Game1.player?.professions.Contains(Farmer.agriculturist) == true ? 0.1f : 0f);
        var speedBoostWithPaddy = speedBoostWithoutPaddy + (paddyEligible ? 0.25f : 0f);
        int? adjustedGrowDays = cropData is null
            ? null
            : AdjustedGrowDays(cropData.DaysInPhase, speedBoostWithPaddy);
        var daysRemainingInSeason = Math.Max(0, 28 - Game1.dayOfMonth);
        var seasonBypass = indoorPotBypass || location.SeedsIgnoreSeasonsHere();

        return new
        {
            slot_index = slotIndex,
            seed_id = seedId,
            crop_catalog_match = cropDataFound,
            can_plant_seeds_here = canPlantSeedsHere,
            denied_message_present = !string.IsNullOrWhiteSpace(deniedMessage),
            season_allowed = seasonAllowed,
            current_day_of_month = Game1.dayOfMonth,
            days_remaining_in_season = daysRemainingInSeason,
            is_paddy_crop = isPaddyCrop,
            paddy_water_eligible = paddyEligible,
            speed_boost_without_paddy = speedBoostWithoutPaddy,
            speed_boost_with_paddy = speedBoostWithPaddy,
            base_grow_days = baseGrowDays,
            adjusted_grow_days_with_paddy_if_eligible = adjustedGrowDays,
            can_mature_before_season_end_with_paddy_if_eligible = seasonBypass || (adjustedGrowDays.HasValue && adjustedGrowDays.Value <= daysRemainingInSeason),
            hard_rule_allows_planting = cropDataFound && canPlantSeedsHere && seasonAllowed
        };
    }

    private static bool PaddyWaterEligible(GameLocation location, Vector2 tile, bool isGardenPot)
    {
        if (isGardenPot)
        {
            return false;
        }

        const int radius = 3;
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                if (location.isWaterTile((int)tile.X + x, (int)tile.Y + y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool? ReadBool(object source, string propertyName)
    {
        return source.GetType().GetProperty(propertyName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(source) as bool?;
    }

    private static int AdjustedGrowDays(IReadOnlyList<int> daysInPhase, float speedBoost)
    {
        var phaseDays = daysInPhase.ToList();
        phaseDays.Add(99999);
        var totalGrowDays = phaseDays.Where(day => day != 99999).Sum();
        var daysToRemove = (int)Math.Ceiling(totalGrowDays * speedBoost);
        var passes = 0;
        while (daysToRemove > 0 && passes < 3)
        {
            for (var index = 0; index < phaseDays.Count; index++)
            {
                if ((index > 0 || phaseDays[index] > 1) && phaseDays[index] != 99999 && phaseDays[index] > 0)
                {
                    phaseDays[index]--;
                    daysToRemove--;
                }

                if (daysToRemove <= 0)
                {
                    break;
                }
            }

            passes++;
        }

        return phaseDays.Where(day => day != 99999).Sum();
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

    private static object[] ReadShopActionTiles(GameLocation location)
    {
        var map = location.map;
        if (map?.Layers is null || map.Layers.Count == 0)
        {
            return Array.Empty<object>();
        }

        var width = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerWidth);
        var height = map.Layers.Cast<xTile.Layers.Layer>().Max(layer => layer.LayerHeight);
        var tiles = new List<object>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.IsNullOrWhiteSpace(action))
                {
                    continue;
                }

                var parsed = ParseShopAction(location, action, x, y);
                if (parsed is not null)
                {
                    tiles.Add(new
                    {
                        tile_x = x,
                        tile_y = y,
                        action,
                        parsed,
                        owner_service_status = ReadOwnerServiceStatus(location, parsed),
                        service_time_status = ReadShopServiceTimeStatus(location, parsed, FindEntranceGateForCurrentLocation(location))
                    });
                }
            }
        }

        return tiles.ToArray();
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

        if (string.Equals(parts[0], "AnimalShop", StringComparison.OrdinalIgnoreCase))
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

        if (string.Equals(parts[0], "AdventureShop", StringComparison.OrdinalIgnoreCase))
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
