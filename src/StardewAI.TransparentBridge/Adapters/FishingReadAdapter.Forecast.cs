using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FishingReadAdapter
{
    private StateAdapterResult CollectForecast(long tick, Farmer player)
    {
        var locationId = SnapshotProfileContext.TargetFishingLocationId;
        var rodSlotIndex = SnapshotProfileContext.TargetFishingRodSlotIndex;
        var requestComplete = !string.IsNullOrWhiteSpace(locationId) &&
                              rodSlotIndex.HasValue;
        var request = new
        {
            profile = "fishing_forecast",
            target_location_id = locationId,
            rod_slot_index = rodSlotIndex,
            request_complete = requestComplete,
            current_player_location_id = player.currentLocation?.NameOrUniqueName,
            read_policy = "one_explicit_loaded_location_no_unrelated_domain_scan"
        };
        if (!requestComplete)
        {
            return ForecastUnavailable(
                tick,
                request,
                "fishing_forecast_location_or_rod_slot_missing");
        }

        var location = Game1.getLocationFromName(locationId!);
        if (location is null)
        {
            return ForecastUnavailable(
                tick,
                request,
                "fishing_forecast_loaded_location_not_found");
        }
        if (rodSlotIndex!.Value >= player.Items.Count ||
            player.Items[rodSlotIndex.Value] is not FishingRod selectedRod)
        {
            return ForecastUnavailable(
                tick,
                request,
                "fishing_forecast_rod_slot_not_fishing_rod");
        }

        var dimensions = MapDimensions(location);
        var canFishHere = location.canFishHere();
        var fishableTiles = dimensions.HasValue
            ? ReadFishableTiles(
                location,
                dimensions.Value.Width,
                dimensions.Value.Height,
                canFishHere)
            : null;
        if (fishableTiles is null)
        {
            return ForecastUnavailable(
                tick,
                request,
                "fishing_forecast_map_dimensions_unavailable");
        }

        object spawnRules;
        try
        {
            spawnRules = ReadSpawnRules(
                location,
                player,
                selectedRod,
                fishableTiles,
                deferPlayerPositionToTerminalStand: true);
        }
        catch (Exception ex)
        {
            return ForecastUnavailable(
                tick,
                request,
                $"fishing_forecast_spawn_projection_failed:{ex.GetType().Name}:{ex.Message}");
        }

        return Section(
            "fishing",
            new Dictionary<string, object>
            {
                ["forecast_request"] = Field(
                    request,
                    "SnapshotProfileContext fishing_forecast request",
                    tick,
                    "demand_only_fishing_forecast_v1"),
                ["location_context"] = Field(new
                {
                    location_id = location.NameOrUniqueName,
                    location_type = location.GetType().FullName,
                    can_fish_here = canFishHere,
                    map_width = dimensions?.Width,
                    map_height = dimensions?.Height,
                    fishable_tile_count = fishableTiles.Length,
                    rod_slot_index = rodSlotIndex,
                    selected_rod_qualified_item_id = selectedRod.QualifiedItemId,
                    scan_policy = "complete_single_requested_loaded_map_no_cap"
                }, "Game1.getLocationFromName existing loaded location; GameLocation.canFishHere", tick),
                ["fishable_tiles"] = Field(
                    fishableTiles,
                    "requested GameLocation.isTileFishable; FishingRod.distanceToLand; GameLocation.TryGetFishAreaForTile",
                    tick),
                ["spawn_rules"] = Field(
                    spawnRules,
                    "Game1.locationData[Default].Fish; requested loaded GameLocation.GetData().Fish; DataLoader.Fish; GameStateQuery; ItemQueryResolver registry",
                    tick)
            },
            Array.Empty<string>(),
            "complete");
    }

    private StateAdapterResult ForecastUnavailable(
        long tick,
        object request,
        string reason) => Section(
        "fishing",
        new Dictionary<string, object>
        {
            ["forecast_request"] = Field(
                request,
                "SnapshotProfileContext fishing_forecast request",
                tick,
                "demand_only_fishing_forecast_v1"),
            ["location_context"] = Unavailable(
                reason,
                "Game1.getLocationFromName existing loaded location",
                tick),
            ["fishable_tiles"] = Unavailable(
                reason,
                "requested GameLocation.isTileFishable",
                tick),
            ["spawn_rules"] = Unavailable(
                reason,
                "Data/Locations Fish and Data/Fish",
                tick)
        },
        new[]
        {
            "fishing.location_context",
            "fishing.fishable_tiles",
            "fishing.spawn_rules"
        },
        "unavailable");
}
