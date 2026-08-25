using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FurniturePlacementNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Furniture.placementAction->Object.placementAction->location.furniture_or_table.heldObject";

    private TrainingExecutionResult ExecutePlaceFurniture(TrainingExecutionRequest request)
    {
        var requested = "current_location.furniture_endpoint=" + request.FurniturePlacementEndpoint +
            ";qualified_item_id=" + request.QualifiedItemId + ";desired_rotation=" + request.FurnitureDesiredRotation +
            ";player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.FurnitureInventoryRotationBefore.HasValue ||
            !request.FurnitureDesiredRotation.HasValue || !request.FurnitureRotationSteps.HasValue ||
            !request.FurnitureExpectedAnchorX.HasValue || !request.FurnitureExpectedAnchorY.HasValue ||
            !request.FurnitureFootprintWidth.HasValue || !request.FurnitureFootprintHeight.HasValue ||
            !request.FurnitureCanFreePlace.HasValue || !request.FurnitureExpectedPassable.HasValue)
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "typed_fields=missing",
                "place_furniture_typed_fields_required");
        }
        if (!string.Equals(request.NativeContract, FurniturePlacementNativeContract, StringComparison.Ordinal) ||
            request.FurniturePlacementEndpoint is not ("location_furniture" or "table_held_object"))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "native_contract_or_endpoint_mismatch",
                "place_furniture_native_contract_mismatch");
        }
        if (request.FurniturePlacementEndpoint == "table_held_object" &&
            (!request.FurnitureTableIndex.HasValue || !request.FurnitureTableTileX.HasValue || !request.FurnitureTableTileY.HasValue))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "table_endpoint_fields=missing",
                "place_furniture_table_endpoint_fields_required");
        }

        var location = Game1.currentLocation;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested,
                "location=" + (location?.NameOrUniqueName ?? "unavailable"), "place_furniture_location_mismatch");
        }
        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count || Game1.player.Items[slot] is not Furniture inventory ||
            !IsSupportedVanillaFurnitureType(inventory.GetType()) || inventory.Stack != request.ExpectedStackBefore.Value ||
            inventory.currentRotation.Value != request.FurnitureInventoryRotationBefore.Value ||
            !string.Equals(inventory.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "inventory_identity_or_rotation_mismatch",
                "place_furniture_inventory_identity_drifted");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var probe = Furniture.GetFurnitureInstance(inventory.ItemId);
        for (var attempts = 0; probe.currentRotation.Value != inventory.currentRotation.Value && attempts < 4; attempts++)
        {
            probe.rotate();
        }
        if (probe.currentRotation.Value != inventory.currentRotation.Value)
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "canonical_factory_cannot_match_inventory_rotation",
                "place_furniture_inventory_rotation_not_canonical");
        }
        for (var i = 0; i < request.FurnitureRotationSteps.Value; i++)
        {
            probe.rotate();
        }
        probe.InitializeAtTile(new Vector2(target.X, target.Y));
        if (probe.currentRotation.Value != request.FurnitureDesiredRotation.Value ||
            probe.furniture_type.Value != request.FurnitureType ||
            probe.getTilesWide() != request.FurnitureFootprintWidth.Value ||
            probe.getTilesHigh() != request.FurnitureFootprintHeight.Value ||
            probe.isPassable() != request.FurnitureExpectedPassable.Value ||
            !Utility.playerCanPlaceItemHere(location, probe, target.X * Game1.tileSize, target.Y * Game1.tileSize, Game1.player))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "rotation_geometry_or_native_probe_mismatch",
                "place_furniture_rotation_or_native_placement_drifted");
        }
        var endpointBefore = FindFurnitureEndpoint(location, probe);
        if (!string.Equals(endpointBefore.Kind, request.FurniturePlacementEndpoint, StringComparison.Ordinal) ||
            endpointBefore.TableIndex != request.FurnitureTableIndex.GetValueOrDefault(-1) ||
            endpointBefore.TableTileX != request.FurnitureTableTileX.GetValueOrDefault(-1) ||
            endpointBefore.TableTileY != request.FurnitureTableTileY.GetValueOrDefault(-1))
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "endpoint_mismatch",
                "place_furniture_endpoint_drifted");
        }

        var anchor = probe.TileLocation;
        if ((int)anchor.X != request.FurnitureExpectedAnchorX.Value ||
            (int)anchor.Y != request.FurnitureExpectedAnchorY.Value)
        {
            return BlockedWithPrimitive(request, "place_furniture", requested, "anchor_mismatch",
                "place_furniture_wall_or_anchor_projection_drifted:actual=" + (int)anchor.X + "," + (int)anchor.Y +
                ":expected=" + request.FurnitureExpectedAnchorX.Value + "," + request.FurnitureExpectedAnchorY.Value);
        }
        if (request.FurnitureCanFreePlace.Value)
        {
            if (!location.CanFreePlaceFurniture())
            {
                return BlockedWithPrimitive(request, "place_furniture", requested, "can_free_place=false",
                    "place_furniture_free_placement_drifted");
            }
        }
        else if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_furniture", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_furniture_player_not_adjacent");
        }

        var beforeFurniture = location.furniture.ToHashSet();
        var payloadBefore = FurniturePayloadFingerprint(inventory);
        for (var i = 0; i < request.FurnitureRotationSteps.Value; i++)
        {
            inventory.rotate();
        }
        inventory.InitializeAtTile(new Vector2(target.X, target.Y));
        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventory, slot, target);
        Furniture? placed = null;
        if (request.FurniturePlacementEndpoint == "table_held_object" &&
            request.FurnitureTableIndex.GetValueOrDefault(-1) >= 0 && request.FurnitureTableIndex.GetValueOrDefault(-1) < location.furniture.Count)
        {
            placed = location.furniture[request.FurnitureTableIndex.GetValueOrDefault()].heldObject.Value as Furniture;
        }
        else
        {
            placed = location.furniture.FirstOrDefault(item => !beforeFurniture.Contains(item) &&
                string.Equals(item.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal));
        }
        var identityVerified = placed is not null && string.Equals(placed.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal) &&
            string.Equals(placed.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) &&
            placed.currentRotation.Value == request.FurnitureDesiredRotation.Value &&
            (int)placed.TileLocation.X == request.FurnitureExpectedAnchorX.Value &&
            (int)placed.TileLocation.Y == request.FurnitureExpectedAnchorY.Value &&
            placed.getTilesWide() == request.FurnitureFootprintWidth.Value &&
            placed.getTilesHigh() == request.FurnitureFootprintHeight.Value &&
            placed.isPassable() == request.FurnitureExpectedPassable.Value &&
            string.Equals(FurniturePayloadFingerprint(placed), payloadBefore, StringComparison.Ordinal);
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value && attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && identityVerified && consumed;
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
            PrimitiveKind = "place_furniture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_Furniture_virtual_rotation_applied", "native_Utility_tryToPlaceItem_applied", "exact_runtime_anchor_footprint_endpoint_and_payload_verified", "inventory_stack_decreased_exactly_one" }
                : new[] { attempt.Placed ? "native_place_returned_true" : "native_place_returned_false", identityVerified ? "furniture_identity_verified" : "furniture_identity_mismatch", consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName + ";endpoint=" + request.FurniturePlacementEndpoint +
                ";runtime_type=" + (placed?.GetType().FullName ?? "null") + ";anchor=" + placed?.TileLocation.X + "," + placed?.TileLocation.Y +
                ";rotation=" + placed?.currentRotation.Value + ";footprint=" + placed?.getTilesWide() + "x" + placed?.getTilesHigh() +
                ";expected_runtime_type=" + request.TargetRuntimeType + ";expected_anchor=" + request.FurnitureExpectedAnchorX + "," + request.FurnitureExpectedAnchorY +
                ";expected_rotation=" + request.FurnitureDesiredRotation + ";expected_footprint=" + request.FurnitureFootprintWidth + "x" + request.FurnitureFootprintHeight +
                ";passable=" + placed?.isPassable() + ";expected_passable=" + request.FurnitureExpectedPassable +
                ";payload_before=" + payloadBefore + ";payload_after=" + (placed is null ? "null" : FurniturePayloadFingerprint(placed)) +
                ";inventory_stack_before=" + attempt.StackBefore + ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_furniture_post_state_mismatch" }
        };
    }

    private static FurnitureEndpoint FindFurnitureEndpoint(GameLocation location, Furniture probe)
    {
        var box = probe.GetBoundingBox();
        for (var index = 0; index < location.furniture.Count; index++)
        {
            var table = location.furniture[index];
            if (table.furniture_type.Value == 11 && table.heldObject.Value is null && table.GetBoundingBox().Intersects(box))
            {
                return new FurnitureEndpoint("table_held_object", index, (int)table.TileLocation.X, (int)table.TileLocation.Y);
            }
        }
        return new FurnitureEndpoint("location_furniture", -1, -1, -1);
    }

    private static bool IsSupportedVanillaFurnitureType(Type type) =>
        type == typeof(Furniture) || type == typeof(StorageFurniture) || type == typeof(FishTankFurniture) ||
        type == typeof(BedFurniture) || type == typeof(RandomizedPlantFurniture) || type == typeof(TV);

    private static string FurniturePayloadFingerprint(Furniture item)
    {
        var storage = item as StorageFurniture;
        return item.QualifiedItemId + "|" + item.GetType().FullName + "|" + item.heldObject.Value?.QualifiedItemId + "|" +
            item.IsOn + "|" + (storage is null ? string.Empty : string.Join(",", storage.heldItems.Select(entry =>
                entry?.GetType().FullName + ":" + entry?.QualifiedItemId + ":" + entry?.Stack + ":" + entry?.Quality)));
    }

    private sealed record FurnitureEndpoint(string Kind, int TableIndex, int TableTileX, int TableTileY);
}
