using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceFlooring(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(IsFloorPathItem)->terrainFeatures.Add(Flooring)";
        var requested = "current_location.terrain_features[" + request.TargetTileX + "," + request.TargetTileY +
            "].runtime_type=StardewValley.TerrainFeatures.Flooring;floor_data_key=" + request.FloorDataKey +
            ";neighbor_mask=" + request.ExpectedFlooringNeighborMask +
            ";player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.ExpectedFlooringNeighborMask.HasValue ||
            !request.ExpectedFlooringViewMin.HasValue || !request.ExpectedFlooringViewMax.HasValue)
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "typed_target=missing", "place_flooring_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(Flooring).FullName, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "native_contract_or_target_runtime_mismatch", "place_flooring_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "place_flooring_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        var lookup = Flooring.GetFloorPathItemLookup();
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object inventoryFlooring ||
            !inventoryFlooring.IsFloorPathItem() || inventoryFlooring.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(inventoryFlooring.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !lookup.TryGetValue(inventoryFlooring.ItemId, out var floorDataKey) ||
            !string.Equals(floorDataKey, request.FloorDataKey, StringComparison.Ordinal) ||
            !Game1.floorPathData.TryGetValue(floorDataKey, out var floorData) ||
            !string.Equals(floorData.ItemId, inventoryFlooring.ItemId, StringComparison.Ordinal) ||
            !string.Equals(floorData.ConnectType.ToString(), request.FlooringConnectType, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "inventory_or_floor_data_identity_mismatch", "place_flooring_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var neighborMask = ReadFlooringNeighborMaskAt(location, target, request.FloorDataKey);
        if (neighborMask != request.ExpectedFlooringNeighborMask.Value)
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "neighbor_mask=" + neighborMask, "place_flooring_neighbor_topology_drifted");
        }
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_flooring_player_not_adjacent");
        }
        if (location.terrainFeatures.ContainsKey(new Vector2(target.X, target.Y)) ||
            !CanPlaceInventoryObjectNative(location, inventoryFlooring, slot, target))
        {
            return BlockedWithPrimitive(request, "place_flooring", requested,
                "native_placement_recheck=false", "place_flooring_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventoryFlooring, slot, target);
        var placedFlooring = attempt.PlacedTerrainFeature as Flooring;
        var identityVerified = placedFlooring?.GetType() == typeof(Flooring) &&
            string.Equals(placedFlooring.whichFloor.Value, request.FloorDataKey, StringComparison.Ordinal) &&
            placedFlooring.Location == location && placedFlooring.Tile == new Vector2(target.X, target.Y);
        var topologyVerified = placedFlooring is not null && placedFlooring.isPassable() &&
            ReadFlooringNeighborMaskAt(location, target, request.FloorDataKey) == request.ExpectedFlooringNeighborMask.Value;
        var viewVerified = placedFlooring is not null &&
            placedFlooring.whichView.Value >= request.ExpectedFlooringViewMin.Value &&
            placedFlooring.whichView.Value <= request.ExpectedFlooringViewMax.Value;
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value &&
            attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && attempt.PlacedObject is null && identityVerified && topologyVerified && viewVerified && consumed;

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
            PrimitiveKind = "place_flooring",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_Object_placementAction_IsFloorPathItem",
                    "placed_exact_base_Flooring_identity_Data_FloorsAndPaths_and_passability_verified",
                    "same_floor_eight_neighbor_mask_and_native_whichView_domain_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_flooring_identity_verified" : "placed_flooring_identity_mismatch",
                    topologyVerified ? "placed_flooring_topology_verified" : "placed_flooring_topology_mismatch",
                    viewVerified ? "placed_flooring_view_domain_verified" : "placed_flooring_view_domain_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (attempt.PlacedTerrainFeature?.GetType().FullName ?? "null") +
                ";floor_data_key=" + (placedFlooring?.whichFloor.Value ?? "null") +
                ";which_view=" + (placedFlooring?.whichView.Value.ToString() ?? "null") +
                ";neighbor_mask=" + ReadFlooringNeighborMaskAt(location, target, request.FloorDataKey) +
                ";is_passable=" + (placedFlooring?.isPassable().ToString().ToLowerInvariant() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_flooring_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.terrain_features[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = request.FloorDataKey + ":StardewValley.TerrainFeatures.Flooring:neighbor_mask=" + request.ExpectedFlooringNeighborMask
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

    private static int ReadFlooringNeighborMaskAt(GameLocation location, Point target, string floorDataKey)
    {
        var mask = 0;
        AddFlooringNeighbor(location, new Vector2(target.X, target.Y - 1), floorDataKey, Flooring.N, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X + 1, target.Y), floorDataKey, Flooring.E, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X, target.Y + 1), floorDataKey, Flooring.S, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X - 1, target.Y), floorDataKey, Flooring.W, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X + 1, target.Y - 1), floorDataKey, Flooring.NE, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X - 1, target.Y - 1), floorDataKey, Flooring.NW, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X + 1, target.Y + 1), floorDataKey, Flooring.SE, ref mask);
        AddFlooringNeighbor(location, new Vector2(target.X - 1, target.Y + 1), floorDataKey, Flooring.SW, ref mask);
        return mask;
    }

    private static void AddFlooringNeighbor(
        GameLocation location, Vector2 tile, string floorDataKey, byte direction, ref int mask)
    {
        if ((location.map is not null && !location.isTileOnMap(tile)) ||
            (location.terrainFeatures.TryGetValue(tile, out var feature) && feature is Flooring flooring &&
             string.Equals(flooring.whichFloor.Value, floorDataKey, StringComparison.Ordinal)))
        {
            mask |= direction;
        }
    }
}
