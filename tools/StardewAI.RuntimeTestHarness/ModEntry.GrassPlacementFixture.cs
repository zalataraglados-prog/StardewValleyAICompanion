using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupGrassPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        var grassType = string.Equals(request.QualifiedItemId, "(O)297", StringComparison.Ordinal) ? 1 :
            string.Equals(request.QualifiedItemId, "(O)BlueGrassStarter", StringComparison.Ordinal) ? 7 : -1;
        if (grassType < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_grass_placement_target",
                "player.grass_placement.ready=true", "qualified_item_id=" + request.QualifiedItemId,
                "fixture_supported_grass_starter_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        var slot = EnsureInventoryItem(request.QualifiedItemId, 2);
        var inventoryGrass = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;
        if (inventoryGrass?.GetType() != typeof(StardewValley.Object) ||
            !string.Equals(inventoryGrass.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "debug_setup_grass_placement_target",
                "player.grass_placement.ready=true", "inventory_grass_starter_unavailable",
                "fixture_exact_base_grass_starter_required");
        }

        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var requested = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : new Point(width / 2, height / 2);
        var candidates = Enumerable.Range(2, Math.Max(0, height - 4))
            .SelectMany(y => Enumerable.Range(2, Math.Max(0, width - 4)).Select(x => new Point(x, y)))
            .OrderBy(tile => ManhattanDistance(tile, requested));

        var target = Point.Zero;
        var stand = Point.Zero;
        var nativeLegal = false;
        var moveReason = "fixture_no_native_legal_grass_layout";
        foreach (var candidate in candidates)
        {
            var vector = new Vector2(candidate.X, candidate.Y);
            if (farm.objects.ContainsKey(vector) || farm.terrainFeatures.ContainsKey(vector))
            {
                continue;
            }
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateMoveReason))
            {
                moveReason = candidateMoveReason;
                continue;
            }
            inventoryGrass.Location = farm;
            inventoryGrass.TileLocation = Vector2.Zero;
            if (!CanPlaceInventoryObjectNative(farm, inventoryGrass, slot, candidate))
            {
                continue;
            }

            target = candidate;
            stand = candidateStand;
            nativeLegal = true;
            break;
        }

        var verified = nativeLegal && Game1.player.TilePoint == stand && ManhattanDistance(stand, target) == 1;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_grass_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_base_inventory_grass_starter_ready",
                    "native_legal_empty_target_ready",
                    "shared_adjacent_path_fixture_ready",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y,
                    "expected_grass_type=" + grassType
                }
                : new[] { slot >= 0 ? "inventory_ready" : "inventory_unavailable", moveReason },
            RequestedEffect = "player.grass_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slot +
                ";inventory_qualified_item_id=" + inventoryGrass.QualifiedItemId +
                ";expected_grass_type=" + grassType +
                ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "grass_placement_fixture_not_ready" }
        };
    }
}
