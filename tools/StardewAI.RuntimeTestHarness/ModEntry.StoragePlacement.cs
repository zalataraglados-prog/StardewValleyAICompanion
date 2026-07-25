using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceStorage(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested =
            "current_location.chests[" +
            request.LocationId + ":" +
            request.TargetTileX + "," +
            request.TargetTileY +
            "].qualified_item_id=" +
            request.QualifiedItemId +
            ";player.inventory[" +
            request.InventorySlotIndex +
            "].stack_decreases=1";
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "typed_target=missing",
                "place_storage_typed_target_fields_required");
        }
        if (string.IsNullOrWhiteSpace(
                request.NativeStorageBranch) ||
            string.IsNullOrWhiteSpace(
                request.SpecialChestType) ||
            !request.ExpectedStorageCapacity.HasValue ||
            request.ExpectedStorageCapacity.Value <= 0 ||
            string.IsNullOrWhiteSpace(
                request.StorageRole))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "storage_projection_fields=missing_or_invalid",
                "place_storage_projection_fields_required");
        }

        var location = Game1.currentLocation;
        if (location is null ||
            string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(
                location.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "location_id=" +
                (location?.NameOrUniqueName ?? "unavailable"),
                "place_storage_location_mismatch");
        }

        var slotIndex = request.InventorySlotIndex.Value;
        if (slotIndex < 0 ||
            slotIndex >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_slot=" + slotIndex,
                "place_storage_inventory_slot_out_of_range");
        }
        if (Game1.player.Items[slotIndex] is not
                StardewValley.Object storageItem ||
            !TryClassifyNativePlayerStorageItem(
                storageItem,
                out var nativeBranch))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_slot_item=not_player_storage",
                "place_storage_inventory_slot_not_storage");
        }
        if (!string.Equals(
                storageItem.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                nativeBranch,
                request.NativeStorageBranch,
                StringComparison.Ordinal) ||
            (!string.IsNullOrWhiteSpace(request.ItemId) &&
             !string.Equals(
                 storageItem.ItemId,
                 request.ItemId,
                 StringComparison.Ordinal)))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "inventory_item=" +
                storageItem.QualifiedItemId,
                "place_storage_inventory_identity_mismatch");
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) +
                Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "player_tile=" + playerTile.X +
                "," + playerTile.Y,
                "place_storage_player_not_adjacent");
        }

        var targetVector = new Vector2(
            target.X,
            target.Y);
        var pixelX = target.X * Game1.tileSize;
        var pixelY = target.Y * Game1.tileSize;
        if (location.objects.ContainsKey(targetVector) ||
            !Utility.playerCanPlaceItemHere(
                location,
                storageItem,
                pixelX,
                pixelY,
                Game1.player))
        {
            return BlockedWithPrimitive(
                request,
                "place_storage",
                requested,
                "native_placement_recheck=false",
                "place_storage_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var selectedSlotBefore =
            Game1.player.CurrentToolIndex;
        var stackBefore = storageItem.Stack;
        Game1.player.CurrentToolIndex = slotIndex;
        var placed = Utility.tryToPlaceItem(
            location,
            storageItem,
            pixelX,
            pixelY);
        if (selectedSlotBefore >= 0 &&
            selectedSlotBefore <
                Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex =
                selectedSlotBefore;
        }

        location.objects.TryGetValue(
            targetVector,
            out var placedObject);
        var afterSlot =
            slotIndex < Game1.player.Items.Count
                ? Game1.player.Items[slotIndex]
                : null;
        var stackAfter = afterSlot?.Stack ?? 0;
        var inventoryConsumed =
            stackAfter == stackBefore - 1;
        var placedStorageMatches =
            placedObject is Chest placedChest &&
            placedChest.playerChest.Value &&
            string.Equals(
                placedChest.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                placedChest.SpecialChestType.ToString(),
                request.SpecialChestType,
                StringComparison.Ordinal) &&
            placedChest.GetActualCapacity() ==
                request.ExpectedStorageCapacity &&
            StorageRoleMatches(
                placedChest,
                request.StorageRole);
        var verified = placed &&
            placedStorageMatches &&
            inventoryConsumed;

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
            CompletedAt =
                DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_storage",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "Utility.playerCanPlaceItemHere_rechecked",
                    "Utility.tryToPlaceItem_applied_native_callbacks",
                    "placed_player_chest_identity_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    placed
                        ? "native_place_returned_true"
                        : "native_place_returned_false",
                    placedStorageMatches
                        ? "placed_storage_identity_matches"
                        : "placed_storage_missing_or_mismatched",
                    inventoryConsumed
                        ? "inventory_consumed_one"
                        : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect =
                "location_id=" +
                location.NameOrUniqueName +
                ";target_tile=" + target.X +
                "," + target.Y +
                ";placed_qualified_item_id=" +
                (placedObject?.QualifiedItemId ?? "null") +
                ";placed_runtime_type=" +
                (placedObject?.GetType().FullName ?? "null") +
                ";placed_special_chest_type=" +
                (placedObject is Chest observedChest
                    ? observedChest.SpecialChestType.ToString()
                    : "null") +
                ";placed_capacity=" +
                (placedObject is Chest capacityChest
                    ? capacityChest.GetActualCapacity().ToString()
                    : "null") +
                ";expected_storage_role=" +
                request.StorageRole +
                ";inventory_stack_before=" +
                stackBefore +
                ";inventory_stack_after=" +
                stackAfter,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[]
                {
                    "place_storage_post_state_mismatch"
                },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path =
                            "current_location.chests[" +
                            location.NameOrUniqueName +
                            ":" + target.X + "," +
                            target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId
                    },
                    new SimulatedFactChange
                    {
                        Path =
                            "player.inventory[" +
                            slotIndex + "].stack",
                        Before = stackBefore.ToString(),
                        After = stackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static bool TryClassifyNativePlayerStorageItem(
        StardewValley.Object item,
        out string branch)
    {
        branch = item.QualifiedItemId switch
        {
            "(BC)130" or "(BC)232" =>
                "native_object_placement_normal_chest",
            "(BC)BigChest" or "(BC)BigStoneChest" =>
                "native_object_placement_big_chest",
            "(BC)216" =>
                "native_object_placement_mini_fridge",
            "(BC)248" =>
                "native_object_placement_mini_shipping_bin",
            "(BC)256" =>
                "native_object_placement_junimo_chest",
            _ => string.Empty
        };
        if (branch.Length > 0)
        {
            return true;
        }
        if (item is Chest chest &&
            chest.playerChest.Value)
        {
            branch =
                "inventory_runtime_chest_placement";
            return true;
        }
        return false;
    }

    private static bool StorageRoleMatches(
        Chest chest,
        string storageRole)
    {
        return storageRole switch
        {
            "ordinary_material" =>
                (chest.SpecialChestType is
                    Chest.SpecialChestTypes.None or
                    Chest.SpecialChestTypes.BigChest) &&
                !chest.fridge.Value,
            "shared_global" =>
                chest.SpecialChestType ==
                    Chest.SpecialChestTypes.JunimoChest,
            "shipping" =>
                chest.SpecialChestType ==
                    Chest.SpecialChestTypes.MiniShippingBin,
            "fridge" => chest.fridge.Value,
            "special_storage" => true,
            _ => false
        };
    }
}
