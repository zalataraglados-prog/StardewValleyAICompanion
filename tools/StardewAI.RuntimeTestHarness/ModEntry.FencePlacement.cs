using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly HashSet<int> FunctionalFenceGateDrawSums = new() { 10, 100, 500, 1000, 110, 1500 };

    private TrainingExecutionResult ExecutePlaceFence(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFenceItem)->Fence(tile,item_id,is_gate)";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].runtime_type=StardewValley.Fence;current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].draw_sum=" + request.ExpectedFenceDrawSum + ";player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.ExpectedFenceIsGate.HasValue ||
            !request.ExpectedFenceDrawSum.HasValue || !request.ExpectedFenceGateFunctional.HasValue ||
            !request.ExpectedFenceHealthMin.HasValue || !request.ExpectedFenceHealthMax.HasValue ||
            !request.ExpectedFenceMaxHealthMin.HasValue || !request.ExpectedFenceMaxHealthMax.HasValue)
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "typed_target=missing", "place_fence_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(Fence).FullName, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "native_contract_or_target_runtime_mismatch", "place_fence_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "place_fence_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object inventoryFence ||
            !inventoryFence.IsFenceItem() || inventoryFence.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(inventoryFence.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(inventoryFence.ItemId, request.FenceDataKey, StringComparison.Ordinal) ||
            !Fence.GetFenceLookup().ContainsKey(inventoryFence.ItemId))
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "inventory_identity_mismatch", "place_fence_inventory_identity_drift");
        }

        var isGate = string.Equals(inventoryFence.ItemId, Fence.gateId, StringComparison.Ordinal);
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var drawSum = ReadFenceDrawSumAt(location, target, inventoryFence.ItemId);
        var gateFunctional = isGate && FunctionalFenceGateDrawSums.Contains(drawSum);
        if (isGate != request.ExpectedFenceIsGate.Value || drawSum != request.ExpectedFenceDrawSum.Value ||
            gateFunctional != request.ExpectedFenceGateFunctional.Value || (isGate && !gateFunctional))
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "draw_sum=" + drawSum + ";gate_functional=" + gateFunctional.ToString().ToLowerInvariant(),
                "place_fence_neighbor_topology_drifted");
        }
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_fence_player_not_adjacent");
        }
        if (!CanPlaceInventoryObjectNative(location, inventoryFence, slot, target))
        {
            return BlockedWithPrimitive(request, "place_fence", requested,
                "native_placement_recheck=false", "place_fence_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventoryFence, slot, target);
        var placedFence = attempt.PlacedObject as Fence;
        var identityVerified = placedFence?.GetType() == typeof(Fence) &&
            string.Equals(placedFence.ItemId, request.FenceDataKey, StringComparison.Ordinal) &&
            string.Equals(placedFence.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) &&
            placedFence.Location == location && placedFence.TileLocation == new Vector2(target.X, target.Y);
        var topologyVerified = placedFence is not null &&
            placedFence.isGate.Value == request.ExpectedFenceIsGate.Value &&
            placedFence.gatePosition.Value == 0 && !placedFence.isPassable() &&
            placedFence.getDrawSum() == request.ExpectedFenceDrawSum.Value &&
            (!placedFence.isGate.Value || FunctionalFenceGateDrawSums.Contains(placedFence.getDrawSum()));
        var healthVerified = placedFence is not null &&
            InFenceRange(placedFence.health.Value, request.ExpectedFenceHealthMin.Value, request.ExpectedFenceHealthMax.Value) &&
            InFenceRange(placedFence.maxHealth.Value, request.ExpectedFenceMaxHealthMin.Value, request.ExpectedFenceMaxHealthMax.Value);
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value &&
            attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && identityVerified && topologyVerified && healthVerified && consumed;

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
            PrimitiveKind = "place_fence",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_Object_placementAction_IsFenceItem",
                    "placed_exact_base_Fence_identity_and_Data_Fences_health_verified",
                    "neighbor_draw_sum_gate_closed_and_nonpassable_initial_state_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_fence_identity_verified" : "placed_fence_identity_mismatch",
                    topologyVerified ? "placed_fence_topology_verified" : "placed_fence_topology_mismatch",
                    healthVerified ? "placed_fence_health_verified" : "placed_fence_health_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (attempt.PlacedObject?.GetType().FullName ?? "null") +
                ";placed_qualified_item_id=" + (attempt.PlacedObject?.QualifiedItemId ?? "null") +
                ";is_gate=" + (placedFence?.isGate.Value.ToString().ToLowerInvariant() ?? "null") +
                ";gate_position=" + (placedFence?.gatePosition.Value.ToString() ?? "null") +
                ";draw_sum=" + (placedFence?.getDrawSum().ToString() ?? "null") +
                ";health=" + (placedFence?.health.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null") +
                ";max_health=" + (placedFence?.maxHealth.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_fence_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId + ":StardewValley.Fence:draw_sum=" + request.ExpectedFenceDrawSum
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "].stack",
                        Before = attempt.StackBefore.ToString(),
                        After = attempt.StackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static int ReadFenceDrawSumAt(GameLocation location, Point target, string itemId)
    {
        var sum = 0;
        AddFenceDrawWeight(location, new Vector2(target.X + 1, target.Y), itemId, 100, ref sum);
        AddFenceDrawWeight(location, new Vector2(target.X - 1, target.Y), itemId, 10, ref sum);
        AddFenceDrawWeight(location, new Vector2(target.X, target.Y + 1), itemId, 500, ref sum);
        AddFenceDrawWeight(location, new Vector2(target.X, target.Y - 1), itemId, 1000, ref sum);
        return sum;
    }

    private static void AddFenceDrawWeight(GameLocation location, Vector2 tile, string itemId, int weight, ref int sum)
    {
        if (location.objects.TryGetValue(tile, out var neighbor) &&
            neighbor is Fence fence && fence.countsForDrawing(itemId))
        {
            sum += weight;
        }
    }

    private static bool InFenceRange(float value, double minimum, double maximum) =>
        value >= minimum - 0.001d && value <= maximum + 0.001d;
}
