using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFencePlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(request, "debug_setup_fence_placement_target",
                "player.fence_placement.ready=true", "qualified_item_id=missing", "qualified_item_id_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        var slot = EnsureInventoryItem(request.QualifiedItemId, 2);
        var inventoryFence = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;
        if (inventoryFence?.GetType() != typeof(StardewValley.Object) ||
            !inventoryFence.IsFenceItem() || !Fence.GetFenceLookup().ContainsKey(inventoryFence.ItemId))
        {
            return BlockedWithPrimitive(request, "debug_setup_fence_placement_target",
                "player.fence_placement.ready=true", "inventory_fence_unavailable", "fixture_exact_base_fence_item_required");
        }

        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var requested = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : new Point(width / 2, height / 2);
        var isGate = string.Equals(inventoryFence.ItemId, Fence.gateId, StringComparison.Ordinal);
        var candidates = Enumerable.Range(1, Math.Max(0, height - 2))
            .SelectMany(y => Enumerable.Range(1, Math.Max(0, width - 2)).Select(x => new Point(x, y)))
            .OrderBy(tile => ManhattanDistance(tile, requested));

        var target = Point.Zero;
        var stand = Point.Zero;
        var drawSum = -1;
        var nativeLegal = false;
        var moveReason = "fixture_no_native_legal_fence_layout";
        foreach (var candidate in candidates)
        {
            var supportTiles = isGate
                ? new[] { new Point(candidate.X - 1, candidate.Y), new Point(candidate.X + 1, candidate.Y) }
                : new[] { new Point(candidate.X + 1, candidate.Y) };
            var affected = supportTiles.Append(candidate)
                .Concat(new[]
                {
                    new Point(candidate.X - 1, candidate.Y),
                    new Point(candidate.X + 1, candidate.Y),
                    new Point(candidate.X, candidate.Y - 1),
                    new Point(candidate.X, candidate.Y + 1)
                })
                .Distinct()
                .ToArray();
            if (affected.Any(tile =>
                    farm.objects.ContainsKey(new Vector2(tile.X, tile.Y)) ||
                    farm.terrainFeatures.ContainsKey(new Vector2(tile.X, tile.Y))))
            {
                continue;
            }

            var supportItemId = isGate ? Fence.woodFenceId : inventoryFence.ItemId;
            foreach (var support in supportTiles)
            {
                farm.objects.Add(new Vector2(support.X, support.Y),
                    new Fence(new Vector2(support.X, support.Y), supportItemId, isGate: false));
            }
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateMoveReason))
            {
                foreach (var support in supportTiles)
                {
                    farm.objects.Remove(new Vector2(support.X, support.Y));
                }
                moveReason = candidateMoveReason;
                continue;
            }
            inventoryFence.Location = farm;
            inventoryFence.TileLocation = Vector2.Zero;
            if (!CanPlaceInventoryObjectNative(farm, inventoryFence, slot, candidate))
            {
                foreach (var support in supportTiles)
                {
                    farm.objects.Remove(new Vector2(support.X, support.Y));
                }
                continue;
            }

            target = candidate;
            stand = candidateStand;
            drawSum = ReadFenceDrawSumAt(farm, target, inventoryFence.ItemId);
            nativeLegal = !isGate || FunctionalFenceGateDrawSums.Contains(drawSum);
            if (nativeLegal)
            {
                break;
            }
            foreach (var support in supportTiles)
            {
                farm.objects.Remove(new Vector2(support.X, support.Y));
            }
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
            PrimitiveKind = "debug_setup_fence_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_base_inventory_fence_ready",
                    "native_legal_target_with_deterministic_neighbor_topology_ready",
                    "shared_adjacent_path_fixture_ready",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y,
                    "expected_draw_sum_after=" + drawSum
                }
                : new[] { slot >= 0 ? "inventory_ready" : "inventory_unavailable", moveReason },
            RequestedEffect = "player.fence_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slot +
                ";inventory_qualified_item_id=" + inventoryFence.QualifiedItemId +
                ";inventory_runtime_type=" + inventoryFence.GetType().FullName +
                ";is_gate=" + isGate.ToString().ToLowerInvariant() +
                ";expected_draw_sum_after=" + drawSum +
                ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fence_placement_fixture_not_ready" }
        };
    }
}
