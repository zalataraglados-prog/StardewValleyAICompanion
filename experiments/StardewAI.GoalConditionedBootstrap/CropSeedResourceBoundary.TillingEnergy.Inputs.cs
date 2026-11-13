using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private sealed record TillingEnergyInputs(float Before, int Maximum, int FarmingLevel, bool Efficient, bool Protected);

    internal static void ValidateTillingEnergyInputs(SnapshotEnvelope before, SnapshotEnvelope observation, int toolSlot, string toolId)
        => _ = ReadTillingEnergyInputs(before, observation, toolSlot, toolId);

    private static TillingEnergyInputs ReadTillingEnergyInputs(SnapshotEnvelope before, SnapshotEnvelope observation, int toolSlot, string toolId)
    {
        NativeExecutionSourceSupport.ValidateSnapshot(before); NativeExecutionSourceSupport.ValidateSnapshot(observation);
        Require(before.GameTick >= 0 && observation.GameTick == before.GameTick && before.BridgeVersion == observation.BridgeVersion &&
            NativeExecutionSourceSupport.Identity(before.PlayerId) == NativeExecutionSourceSupport.Identity(observation.PlayerId) &&
            NativeExecutionSourceSupport.Identity(before.SaveId) == NativeExecutionSourceSupport.Identity(observation.SaveId),
            "Crop till energy observation changed its original native frame/actor.");
        var context = Value(Field(observation, "current_location", "site_preparation_context")!);
        Require(context["schema_version"]?.GetValue<string>() == "farm_site_preparation_context.v2" &&
            context["projection_status"]?.GetValue<string>() == "complete_bounded_current_farm_region" &&
            context["location_id"]?.GetValue<string>() == "Farm" &&
            Int(context, "hoe_tool_slot_index") == toolSlot && context["hoe_tool_qualified_item_id"]?.GetValue<string>() == toolId &&
            toolSlot >= 0 && !string.IsNullOrWhiteSpace(toolId), "Crop till original Hoe identity/projection differs.");
        var efficient = TillingEnergyBoolean(context["hoe_is_efficient"], "Hoe efficiency");
        var inventory = Array(Value(Field(before, "player", "inventory")!), "till native tool inventory");
        Require(inventory.All(row => row is JsonObject && Int(row, "slot_index") >= 0) &&
            inventory.Select(row => Int(row!, "slot_index")).Distinct().Count() == inventory.Count,
            "Crop till original inventory slot identities are invalid.");
        var firstHoe = inventory.OfType<JsonObject>().Where(row => row["runtime_type"]?.GetValue<string>() == "StardewValley.Tools.Hoe")
            .OrderBy(row => Int(row, "slot_index")).FirstOrDefault();
        // The existing runtime FindTool<Hoe> selects the first native inventory Hoe, not an arbitrary planned slot.
        Require(firstHoe is not null && Int(firstHoe, "slot_index") == toolSlot &&
            firstHoe["qualified_item_id"]?.GetValue<string>() == toolId,
            "Crop till selected Hoe differs from the original runtime first-Hoe contract.");
        var skills = Array(Value(Field(before, "player", "skills_detail")!)["skills"], "till actor skill levels");
        var farming = skills.OfType<JsonObject>().Where(row => row["skill_id"]?.GetValue<string>() == "farming").ToArray();
        Require(farming is [var only] && Int(only, "skill_index") == 0, "Crop till native farming level is ambiguous.");
        var level = Int(farming[0], "effective_level");
        Require(level >= 0 && context["hoe_energy_cost_per_single_tile_action"] is JsonValue cost &&
            cost.TryGetValue<double>(out var projected) && double.IsFinite(projected) &&
            projected == (efficient ? 0d : Math.Max(0d, 2d - level * 0.1d)),
            "Crop till Hoe cost projection differs from its native efficiency/effective farming inputs.");
        var maximum = Value(Field(before, "player", "max_energy")!);
        Require(maximum is JsonValue maximumValue && maximumValue.TryGetValue<int>(out var max) && max >= 0,
            "Crop till maximum stamina is unavailable.");
        return new(TillingEnergyFloat(Value(Field(before, "player", "energy")!), "before"), maximum.GetValue<int>(), level,
            efficient, TillingEnergyProtection(before));
    }

    private static bool TillingEnergyProtection(SnapshotEnvelope snapshot)
    {
        var context = Value(Field(snapshot, "current_location", "statue_blessing")!);
        var buffs = Array(context["active_blessing_buffs"], "till native blessing buffs");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in buffs)
        {
            Require(row is JsonObject && row["buff_id"] is JsonValue value && value.TryGetValue<string>(out var id) &&
                !string.IsNullOrWhiteSpace(id) && id.StartsWith("statue_of_blessings_", StringComparison.Ordinal) && ids.Add(id),
                "Crop till native blessing identity is unavailable or ambiguous.");
        }
        return ids.Contains("statue_of_blessings_2");
    }

    private static bool TillingEnergyBoolean(JsonNode? node, string label) => node is JsonValue value && value.TryGetValue<bool>(out var flag)
        ? flag : throw new InvalidDataException("Crop till " + label + " is unavailable.");

    private static float TillingEnergyFloat(JsonNode? node, string label) => node is JsonValue value && value.TryGetValue<float>(out var number) && float.IsFinite(number)
        ? number : throw new InvalidDataException("Crop till energy " + label + " is not finite/readable.");
}
