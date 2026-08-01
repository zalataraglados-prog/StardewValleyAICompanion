using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.WorldModel;

namespace StardewAI.Core.Training;

public sealed class PolicyStateFeatureProjector
{
    public FeatureVector Project(WorldModelEnvelope worldModel)
    {
        if (worldModel is null)
            throw new ArgumentNullException(nameof(worldModel));
        return new FeatureVector
        {
            Numeric = new[]
            {
                Number("game.time", ReadDouble(worldModel.Facts.Game, "time")),
                Number("game.day", ReadDouble(worldModel.Facts.Game, "day")),
                Number("game.year", ReadDouble(worldModel.Facts.Game, "year")),
                Number("player.tile_x", ReadDouble(worldModel.Facts.Player, "tile_x")),
                Number("player.tile_y", ReadDouble(worldModel.Facts.Player, "tile_y")),
                Number("player.money", ReadDouble(worldModel.Facts.Player, "money")),
                Number("player.energy", ReadDouble(worldModel.Facts.Player, "energy")),
                Number("player.max_energy", ReadDouble(worldModel.Facts.Player, "max_energy")),
                Number("player.health", ReadDouble(worldModel.Facts.Player, "health")),
                Number("player.max_health", ReadDouble(worldModel.Facts.Player, "max_health")),
                Number("player.level", ReadDouble(worldModel.Facts.Player, "level")),
                Number("player.total_money_earned", ReadDouble(worldModel.Facts.Player, "total_money_earned")),
                Number("player.farmhouse_upgrade_level", ReadDouble(worldModel.Facts.Player, "farmhouse_upgrade_level")),
                Number("farm.crops_needing_watering", CountCropsNeedingWater(worldModel)),
                Number("completeness.unavailable_count", worldModel.Completeness.UnavailableCount),
                Number("completeness.required_readable_ratio", ReadableRatio(worldModel))
            },
            Categorical = new[]
            {
                Category("game.season", ReadString(worldModel.Facts.Game, "season")),
                Category("game.weather", ReadString(worldModel.Facts.Game, "weather")),
                Category("player.location_id", ReadString(worldModel.Facts.Player, "location_id")),
                Category("player.current_tool", ReadString(worldModel.Facts.Player, "current_tool")),
                Category("player.current_item_qualified_id", ReadString(worldModel.Facts.Player, "current_item_qualified_id")),
                Category("world.mode", worldModel.Mode),
                Category("goal.id", worldModel.UserGoal)
            },
            Boolean = new[]
            {
                Flag("player.has_skull_key", ReadBool(worldModel.Facts.Player, "has_skull_key")),
                Flag("player.has_rusty_key", ReadBool(worldModel.Facts.Player, "has_rusty_key")),
                Flag("player.married_or_roommate", ReadBool(worldModel.Facts.Player, "married_or_roommate")),
                Flag("completeness.all_required_facts_readable", worldModel.Completeness.AllRequiredFactsReadable),
                Flag("planner_inputs.blocked", worldModel.PlannerInputs.Blocked)
            }
        };
    }

    private static double ReadableRatio(WorldModelEnvelope worldModel) =>
        worldModel.Completeness.RequiredFactCount == 0
            ? 0
            : Math.Round(
                (double)worldModel.Completeness.ReadableRequiredFactCount /
                worldModel.Completeness.RequiredFactCount,
                6);

    private static double CountCropsNeedingWater(WorldModelEnvelope worldModel)
    {
        if (!worldModel.Facts.Farm.TryGetValue("crops", out var crops) ||
            crops.ValueKind != JsonValueKind.Array)
            return 0;
        return crops.EnumerateArray().Count(crop =>
            crop.ValueKind == JsonValueKind.Object &&
            crop.TryGetProperty("needs_watering", out var value) &&
            value.ValueKind == JsonValueKind.True);
    }

    private static double ReadDouble(IReadOnlyDictionary<string, JsonElement> facts, string key) =>
        facts.TryGetValue(key, out var value) && value.TryGetDouble(out var result) ? result : 0;

    private static bool ReadBool(IReadOnlyDictionary<string, JsonElement> facts, string key) =>
        facts.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.True;

    private static string ReadString(IReadOnlyDictionary<string, JsonElement> facts, string key) =>
        facts.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "unknown"
            : "unknown";

    private static NumericFeature Number(string name, double value) => new() { Name = name, Value = value };
    private static CategoricalFeature Category(string name, string value) => new()
    {
        Name = name,
        Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value
    };
    private static BooleanFeature Flag(string name, bool value) => new() { Name = name, Value = value };
}
