using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupTreeTreatmentTarget(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_tree_treatment_target",
                "current_location.terrain_features[target].tree_treatment_native_allowed=true",
                "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var tile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        farm.objects.Remove(tile);
        farm.terrainFeatures.Remove(tile);
        var tree = new Tree("1", 5);
        tree.hasMoss.Value = true;
        tree.stopGrowingMoss.Value = false;
        farm.terrainFeatures[tile] = tree;

        var treatment = ItemRegistry.Create("(O)419", 2);
        if (!Game1.player.addItemToInventoryBool(treatment))
        {
            return BlockedWithPrimitive(request, "debug_setup_tree_treatment_target",
                "current_location.terrain_features[target].tree_treatment_native_allowed=true",
                "inventory_full", "fixture_vinegar_inventory_capacity_missing");
        }

        var slotIndex = -1;
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (Game1.player.Items[index] is { } item &&
                string.Equals(item.QualifiedItemId, "(O)419", StringComparison.Ordinal))
            {
                slotIndex = index;
                break;
            }
        }
        MoveFixtureFarmerToFarmAdjacent(new Point(request.TargetTileX.Value, request.TargetTileY.Value));
        var verified = slotIndex >= 0 && farm.terrainFeatures.TryGetValue(tile, out var feature) &&
            ReferenceEquals(feature, tree) && tree.hasMoss.Value && !tree.stopGrowingMoss.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_tree_treatment_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "exact_vanilla_tree_fixture_ready", "vinegar_inventory_source_ready" }
                : new[] { "fixture_tree_treatment_target_not_verified" },
            RequestedEffect = "current_location.terrain_features[" + request.TargetTileX.Value + "," + request.TargetTileY.Value + "].tree_treatment_native_allowed=true",
            ObservedEffect = "has_moss=" + tree.hasMoss.Value.ToString().ToLowerInvariant() +
                ";stop_growing_moss=" + tree.stopGrowingMoss.Value.ToString().ToLowerInvariant() +
                ";slot_index=" + slotIndex,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_tree_treatment_target_not_verified" }
        };
    }
}
