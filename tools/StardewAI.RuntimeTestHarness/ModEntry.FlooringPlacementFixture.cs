using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFlooringPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(request, "debug_setup_flooring_placement_target",
                "player.flooring_placement.ready=true", "qualified_item_id=missing", "qualified_item_id_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        var slot = EnsureInventoryItem(request.QualifiedItemId, 2);
        var inventoryFlooring = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;
        var lookup = Flooring.GetFloorPathItemLookup();
        if (inventoryFlooring?.GetType() != typeof(StardewValley.Object) ||
            !inventoryFlooring.IsFloorPathItem() || !lookup.TryGetValue(inventoryFlooring.ItemId, out var floorDataKey) ||
            !Game1.floorPathData.ContainsKey(floorDataKey))
        {
            return BlockedWithPrimitive(request, "debug_setup_flooring_placement_target",
                "player.flooring_placement.ready=true", "inventory_flooring_unavailable", "fixture_exact_base_floor_path_item_required");
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
        var neighborMask = -1;
        var nativeLegal = false;
        var moveReason = "fixture_no_native_legal_flooring_layout";
        foreach (var candidate in candidates)
        {
            var support = new Point(candidate.X + 1, candidate.Y);
            var affected = new[]
            {
                candidate, support,
                new Point(candidate.X - 1, candidate.Y), new Point(candidate.X, candidate.Y - 1),
                new Point(candidate.X, candidate.Y + 1), new Point(candidate.X + 1, candidate.Y - 1),
                new Point(candidate.X - 1, candidate.Y - 1), new Point(candidate.X + 1, candidate.Y + 1),
                new Point(candidate.X - 1, candidate.Y + 1)
            };
            if (affected.Any(tile => farm.objects.ContainsKey(new Vector2(tile.X, tile.Y)) ||
                    farm.terrainFeatures.ContainsKey(new Vector2(tile.X, tile.Y))))
            {
                continue;
            }

            farm.terrainFeatures.Add(new Vector2(support.X, support.Y), new Flooring(floorDataKey));
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateMoveReason))
            {
                farm.terrainFeatures.Remove(new Vector2(support.X, support.Y));
                moveReason = candidateMoveReason;
                continue;
            }
            inventoryFlooring.Location = farm;
            inventoryFlooring.TileLocation = Vector2.Zero;
            if (!CanPlaceInventoryObjectNative(farm, inventoryFlooring, slot, candidate))
            {
                farm.terrainFeatures.Remove(new Vector2(support.X, support.Y));
                continue;
            }

            target = candidate;
            stand = candidateStand;
            neighborMask = ReadFlooringNeighborMaskAt(farm, target, floorDataKey);
            nativeLegal = neighborMask == Flooring.E;
            if (nativeLegal)
            {
                break;
            }
            farm.terrainFeatures.Remove(new Vector2(support.X, support.Y));
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
            PrimitiveKind = "debug_setup_flooring_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_base_inventory_floor_path_item_ready",
                    "native_legal_target_with_deterministic_same_floor_east_neighbor_ready",
                    "shared_adjacent_path_fixture_ready",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y,
                    "expected_neighbor_mask_after=" + neighborMask
                }
                : new[] { slot >= 0 ? "inventory_ready" : "inventory_unavailable", moveReason },
            RequestedEffect = "player.flooring_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slot +
                ";inventory_qualified_item_id=" + inventoryFlooring.QualifiedItemId +
                ";floor_data_key=" + floorDataKey +
                ";expected_neighbor_mask_after=" + neighborMask +
                ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "flooring_placement_fixture_not_ready" }
        };
    }
}
