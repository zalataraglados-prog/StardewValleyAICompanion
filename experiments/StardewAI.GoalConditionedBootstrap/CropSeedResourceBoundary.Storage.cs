using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void VerifyStorageEcho(SnapshotEnvelope before, SnapshotEnvelope after, JsonObject graph, JsonArray originalAccesses)
    {
        var expected = Field(before, "farm", "chests", required: false);
        var actual = Field(after, "farm", "chests", required: false);
        var projectedEcho = Readable(expected) && Readable(actual);
        if (projectedEcho)
        {
            var storage = Value(expected!).AsObject();
            var accesses = Array(graph["access_points"], "access points");
            Require(accesses.All(row => row is JsonObject) && accesses.OfType<JsonObject>().Select(row =>
                    row["access_point_id"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() == accesses.Count,
                "Owned seed transfer graph access identities are invalid.");
            var projectedById = accesses.OfType<JsonObject>().ToDictionary(row => row["access_point_id"]!.GetValue<string>(), StringComparer.Ordinal);
            var echoes = Array(storage["access_points"], "storage access points");
            var scope = storage["scope_location_id"]?.GetValue<string>() ?? string.Empty;
            var canonical = originalAccesses.OfType<JsonObject>().Where(row => row["access_kind"]?.GetValue<string>() is "placed_chest" or "built_in_fridge")
                .Where(row => scope.Length == 0 || string.Equals(row["location_id"]?.GetValue<string>(), scope, StringComparison.OrdinalIgnoreCase))
                .OrderBy(row => row["location_id"]!.GetValue<string>(), StringComparer.Ordinal)
                .ThenBy(row => row["tile_y"]?.GetValue<int>()).ThenBy(row => row["tile_x"]?.GetValue<int>())
                .ThenBy(row => row["access_point_id"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
            Require(echoes.Count == canonical.Length && echoes.Zip(canonical).All(pair => JsonNode.DeepEquals(pair.First, pair.Second)) &&
                    Int(storage, "access_point_count") == echoes.Count &&
                    Int(storage, "distinct_inventory_node_count") == canonical.Select(row => row["node_id"]!.GetValue<string>()).Distinct().Count() &&
                    Int(storage, "actor_authorized_access_point_count") == canonical.Count(row => row["actor_use_authorized"]?.GetValue<bool>() == true) &&
                    Int(storage, "locked_access_point_count") == canonical.Count(row => row["locked_by_other_player"]?.GetValue<bool>() == true),
                "Owned seed transfer source storage inventory/authority echo is inconsistent.");
            Require(Int(storage, "removable_empty_access_point_count") == echoes.OfType<JsonObject>().Count(row =>
                        row["relocation_status"]?.GetValue<string>() == "native_remove_available_empty") &&
                    Int(storage, "nonempty_shove_access_point_count") == echoes.OfType<JsonObject>().Count(row =>
                        row["relocation_status"]?.GetValue<string>() == "native_shove_available_nonempty"),
                "Owned seed transfer source storage counts are inconsistent.");
            for (var index = 0; index < echoes.Count; index++)
            {
                var row = echoes[index]!.AsObject();
                Require(projectedById.TryGetValue(row["access_point_id"]!.GetValue<string>(), out var projected),
                    "Owned seed transfer projected storage access is missing.");
                echoes[index] = projected!.DeepClone();
            }
            storage["removable_empty_access_point_count"] = echoes.OfType<JsonObject>().Count(row =>
                row["relocation_status"]?.GetValue<string>() == "native_remove_available_empty");
            storage["nonempty_shove_access_point_count"] = echoes.OfType<JsonObject>().Count(row =>
                row["relocation_status"]?.GetValue<string>() == "native_shove_available_nonempty");
        }
        EqualField(expected, actual, "storage infrastructure echo");

        // Only a comparison view substitutes the independently verified echo value. Real snapshots/hashes stay intact.
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion,
            State = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal) };
        if (projectedEcho)
        {
            var compared = Field(after, "farm", "chests")!;
            compared["value"] = Value(Field(before, "farm", "chests")!).DeepClone();
            // The original invariant excludes this already-verified graph; do not serialize it again in a comparison-only view.
            view.State["farm"] = JsonSerializer.SerializeToElement(after.State["farm"].EnumerateObject()
                .Where(row => row.Name != "material_inventory_graph").ToDictionary(
                row => row.Name, row => row.Name == "chests" ? JsonSerializer.SerializeToElement(compared) : row.Value));
        }
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(view, out var next) && first == next,
            "Owned seed transfer changed unrelated persistent state.");
    }
}
