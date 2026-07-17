using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData;
using StardewValley.GameData.Locations;
using StardewValley.Internal;
using StardewValley.Locations;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FishingReadAdapter : ReadAdapterBase
{
    public override string Domain => "fishing";
    public override int Priority => 35;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        if (player is null || location is null)
        {
            return Section("fishing", new Dictionary<string, object>
            {
                ["location_context"] = Unavailable("world_not_ready", "Game1.currentLocation", tick),
                ["fishable_tiles"] = Unavailable("world_not_ready", "Game1.currentLocation.isTileFishable", tick),
                ["rod_inventory"] = Unavailable("world_not_ready", "Game1.player.Items as FishingRod", tick),
                ["rod_contexts"] = Unavailable("world_not_ready", "Game1.player.Items as FishingRod; Data/Locations Fish; GameLocation.getFish", tick),
                ["active_cast_state"] = Unavailable("world_not_ready", "Game1.player.CurrentTool as FishingRod", tick),
                ["spawn_rules"] = Unavailable("world_not_ready", "Data/Locations Fish and Data/Fish", tick),
                ["special_catch_sources"] = Unavailable("world_not_ready", "GameLocation.getFish", tick)
            }, new[]
            {
                "fishing.location_context",
                "fishing.fishable_tiles",
                "fishing.rod_inventory",
                "fishing.rod_contexts",
                "fishing.active_cast_state",
                "fishing.spawn_rules",
                "fishing.special_catch_sources"
            }, "unavailable");
        }

        var dimensions = MapDimensions(location);
        var canFishHere = location.canFishHere();
        var fishableTiles = dimensions.HasValue
            ? ReadFishableTiles(location, dimensions.Value.Width, dimensions.Value.Height, canFishHere)
            : null;
        var currentRod = player.CurrentTool as FishingRod;
        object? spawnRules = null;
        SpecialCatchSourcesProjection? specialCatchSources = null;
        RodFishingContextsProjection? rodContexts = null;
        string? spawnRuleFailure = null;
        string? specialCatchSourcesFailure = null;
        string? rodContextsFailure = null;
        if (fishableTiles is not null)
        {
            try
            {
                spawnRules = ReadSpawnRules(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                spawnRuleFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            try
            {
                specialCatchSources = ReadSpecialCatchSources(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                specialCatchSourcesFailure = $"{ex.GetType().Name}: {ex.Message}";
            }

            try
            {
                rodContexts = ReadRodContexts(location, player, currentRod, fishableTiles);
            }
            catch (Exception ex)
            {
                rodContextsFailure = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        var unavailable = new List<string>();
        if (fishableTiles is null)
        {
            unavailable.Add("fishing.fishable_tiles");
        }
        if (spawnRules is null)
        {
            unavailable.Add("fishing.spawn_rules");
        }
        if (specialCatchSources is null)
        {
            unavailable.Add("fishing.special_catch_sources");
        }
        else if (!specialCatchSources.Complete)
        {
            unavailable.Add("fishing.special_catch_sources.location_override");
        }
        if (rodContexts is null)
        {
            unavailable.Add("fishing.rod_contexts");
        }
        else if (!rodContexts.Complete)
        {
            unavailable.Add("fishing.rod_contexts.incomplete_rule_or_override_context");
        }
        object spawnRulesEnvelope;
        if (spawnRules is null)
        {
            spawnRulesEnvelope = Unavailable(
                spawnRuleFailure ?? "fishable_tile_scan_unavailable",
                "GameLocation.GetFishFromLocationData; Data/Locations Fish; Data/Fish",
                tick);
        }
        else
        {
            spawnRulesEnvelope = Field(
                spawnRules,
                "Game1.locationData[Default].Fish; GameLocation.GetData().Fish; DataLoader.Fish; GameStateQuery; ItemQueryResolver registry",
                tick);
        }
        object specialCatchSourcesEnvelope;
        if (specialCatchSources is null)
        {
            specialCatchSourcesEnvelope = Unavailable(
                specialCatchSourcesFailure ?? "fishable_tile_scan_unavailable",
                "GameLocation.getFish",
                tick);
        }
        else
        {
            specialCatchSourcesEnvelope = Field(
                specialCatchSources.Value,
                "GameLocation.getFish; FishPond fields; GameLocation.fishFrenzyFish/fishSplashPoint",
                tick);
        }
        object rodContextsEnvelope;
        if (rodContexts is null)
        {
            rodContextsEnvelope = Unavailable(
                rodContextsFailure ?? "fishable_tile_scan_unavailable",
                "Game1.player.Items as FishingRod; Data/Locations Fish; GameLocation.getFish",
                tick);
        }
        else
        {
            rodContextsEnvelope = Field(
                rodContexts.Rows,
                "Game1.player.Items as FishingRod; Data/Locations Fish; Data/Fish; GameLocation.getFish",
                tick);
        }

        return Section("fishing", new Dictionary<string, object>
        {
            ["location_context"] = Field(new
            {
                location_id = location.NameOrUniqueName,
                location_type = location.GetType().FullName,
                can_fish_here = canFishHere,
                map_width = dimensions?.Width,
                map_height = dimensions?.Height,
                fishable_tile_count = fishableTiles?.Length,
                fishing_level = player.FishingLevel,
                luck_level = player.LuckLevel,
                daily_luck = player.DailyLuck,
                scan_policy = "complete_current_map_no_cap"
            }, "Game1.currentLocation.canFishHere; Farmer.FishingLevel/LuckLevel/DailyLuck", tick),
            ["fishable_tiles"] = Field(fishableTiles, "GameLocation.isTileFishable; FishingRod.distanceToLand; GameLocation.TryGetFishAreaForTile", tick),
            ["rod_inventory"] = Field(ReadRodInventory(player, currentRod), "Game1.player.Items as StardewValley.Tools.FishingRod", tick),
            ["rod_contexts"] = rodContextsEnvelope,
            ["active_cast_state"] = Field(ReadActiveCastState(currentRod), "Game1.player.CurrentTool as FishingRod runtime fields", tick),
            ["spawn_rules"] = spawnRulesEnvelope,
            ["special_catch_sources"] = specialCatchSourcesEnvelope
        }, unavailable, unavailable.Count == 0 ? "complete" : "partial");
    }

}
