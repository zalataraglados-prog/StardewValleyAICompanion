using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyUnconsumedPlanting(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        foreach (var name in new[] { "inventory", "inventory_capacity", "seed_inventory", "money", "shop_currency_balances", "multiplayer_wallet" })
            SameField(before, after, "player", name, required: name != "multiplayer_wallet");
        foreach (var name in new[] { "crops", "material_inventory_graph" }) SameField(before, after, "farm", name, required: true);
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(after, out var next) && first == next,
            "Owned planting failed suffix changed persistent state.");
    }

    private static void VerifyPlantingPersistentState(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        var prior = Field(before, "farm", "crops")!;
        // The full crop delta was verified above; normalize only that value in a comparison view.
        var comparedCrops = Field(after, "farm", "crops")!;
        comparedCrops["value"] = Value(prior).DeepClone();
        state["farm"] = JsonSerializer.SerializeToElement(after.State["farm"].EnumerateObject()
            .Where(row => row.Name != "material_inventory_graph").ToDictionary(row => row.Name,
                row => row.Name == "crops" ? JsonSerializer.SerializeToElement(comparedCrops, JsonDefaults.Options) : row.Value), JsonDefaults.Options);
        const string field = "game_state_query_unlock_state";
        var expectedStats = Field(before, "world_progress", field, required: false);
        var actualStats = Field(after, "world_progress", field, required: false);
        if (Readable(expectedStats) && Readable(actualStats))
        {
            var actors = Array(Value(expectedStats!)["players"], "GSQ players");
            var actor = Unique(actors, "player_id", before.PlayerId.Value!);
            var stats = actor["stats"] as JsonObject ?? throw new InvalidDataException("Owned planting actor stats are unavailable.");
            var count = 0u;
            Require(!stats.ContainsKey("seedsSown") || stats["seedsSown"] is JsonValue value && value.TryGetValue(out count),
                "Owned planting seed statistic has invalid type.");
            stats["seedsSown"] = unchecked(count + 1u);
            foreach (var envelope in new[] { expectedStats, actualStats })
                foreach (var player in Array(Value(envelope!)["players"], "GSQ players").OfType<JsonObject>())
                    if (player["stats"] is JsonObject playerStats) playerStats.Remove("stepsTaken");
            EqualField(expectedStats, actualStats, "owned planting actor seedsSown statistic");
            var progress = JsonNode.Parse(after.State["world_progress"].GetRawText())!.AsObject();
            progress[field]!["value"] = Value(Field(before, "world_progress", field)!).DeepClone();
            state["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonDefaults.Options);
        }
        else EqualField(expectedStats, actualStats, "owned planting actor stats availability");
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(view, out var next) && first == next,
            "Owned planting changed unrelated persistent state.");
    }
}
