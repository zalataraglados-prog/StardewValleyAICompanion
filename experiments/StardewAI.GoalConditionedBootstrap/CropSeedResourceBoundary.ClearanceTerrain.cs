using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static (int TileX, int TileY, string RuntimeType) VerifyClearanceTerrain(
        TrainingExecutionRequest request, SnapshotEnvelope before, SnapshotEnvelope after)
    {
        Require(request.OptionId == "executor.clear_obstacle" && request.TargetTileX is >= 0 && request.TargetTileY is >= 0,
            "Clearance terrain requires the original explicit nonnegative target tile and clear option.");
        int x = request.TargetTileX!.Value, y = request.TargetTileY!.Value;
        var expected = (JsonObject)Field(before, "farm", "terrain_features")!.DeepClone();
        var actual = Field(after, "farm", "terrain_features")!;
        var rows = Array(Value(expected), "clearance terrain before");
        var remaining = Array(Value(actual), "clearance terrain after");
        ValidateCoordinates(rows);
        ValidateCoordinates(remaining);
        var targets = rows.OfType<JsonObject>().Where(row => Int(row, "tile_x") == x && Int(row, "tile_y") == y).ToArray();
        Require(targets.Length == 1, "Clearance terrain original target is absent or ambiguous.");
        var target = targets[0];
        Require(target["type"] is JsonValue type && type.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
            "Clearance terrain original target type is unreadable.");
        string runtimeType = target["type"]!.GetValue<string>();
        Require(string.IsNullOrEmpty(request.TargetRuntimeType) || request.TargetRuntimeType == runtimeType,
            "Clearance terrain target type differs from the explicit request.");
        Require(!remaining.Any(row => Int(row!, "tile_x") == x && Int(row!, "tile_y") == y),
            "Clearance terrain target remains or was replaced.");
        rows.Remove(target);
        EqualField(expected, actual, "clearance terrain removal or unrelated terrain");
        return (x, y, runtimeType);

        static void ValidateCoordinates(JsonArray values)
        {
            var identities = new HashSet<(int X, int Y)>();
            foreach (var row in values)
            {
                Require(row is JsonObject, "Clearance terrain row is not an object.");
                Require(identities.Add((Int(row!, "tile_x"), Int(row!, "tile_y"))),
                    "Clearance terrain coordinate identity is duplicated.");
            }
        }
    }
}
