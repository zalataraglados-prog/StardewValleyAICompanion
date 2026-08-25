using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceSign(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(sign_item_or_TextSign)->location.objects";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].sign_state.placement_kind=" + request.SignPlacementKind + ";player.inventory[" +
            request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.SignExpectedPassable.HasValue ||
            !request.SignExpectedDisplayItemEmpty.HasValue || !request.SignExpectedDisplayType.HasValue ||
            !request.SignExpectedShowNextIndex.HasValue ||
            request.SignPlacementKind is not ("display_item_sign" or "text_sign"))
        {
            return BlockedWithPrimitive(request, "place_sign", requested, "typed_target=missing",
                "place_sign_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            request.SignExpectedPassable.Value || !request.SignExpectedDisplayItemEmpty.Value ||
            request.SignExpectedDisplayType.Value != 0 || !string.IsNullOrEmpty(request.SignExpectedText) ||
            (request.SignPlacementKind == "display_item_sign" && request.SignExpectedShowNextIndex.Value) ||
            (request.SignPlacementKind == "text_sign" && !request.SignExpectedShowNextIndex.Value))
        {
            return BlockedWithPrimitive(request, "place_sign", requested, "native_contract_or_empty_state_mismatch",
                "place_sign_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_sign", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "place_sign_location_mismatch");
        }
        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object inventory ||
            inventory.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(inventory.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(inventory.ItemId, request.ItemId, StringComparison.Ordinal) ||
            !string.Equals(RuntimeSignPlacementKind(inventory), request.SignPlacementKind, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_sign", requested, "inventory_identity_mismatch",
                "place_sign_inventory_or_branch_identity_drifted");
        }
        var expectedType = request.SignPlacementKind == "display_item_sign" ? typeof(Sign).FullName : typeof(StardewValley.Object).FullName;
        if (!string.Equals(request.TargetRuntimeType, expectedType, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_sign", requested, "target_runtime_type=" + request.TargetRuntimeType,
                "place_sign_target_runtime_type_mismatch");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_sign", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_sign_player_not_adjacent");
        }
        if (!CanPlaceInventoryObjectNative(location, inventory, slot, target))
        {
            return BlockedWithPrimitive(request, "place_sign", requested, "native_placement_recheck=false",
                "place_sign_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventory, slot, target);
        var placed = attempt.PlacedObject;
        var identityVerified = placed is not null && placed.GetType().FullName == expectedType &&
            string.Equals(placed.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) &&
            placed.Location == location && placed.TileLocation == new Vector2(target.X, target.Y) &&
            string.Equals(RuntimePlacedSignKind(placed), request.SignPlacementKind, StringComparison.Ordinal) &&
            !placed.isPassable();
        var emptyStateVerified = request.SignPlacementKind switch
        {
            "display_item_sign" => placed is Sign sign && sign.displayItem.Value is null && sign.displayType.Value == 0,
            "text_sign" => placed is not null && placed.GetType() == typeof(StardewValley.Object) &&
                placed.IsTextSign() && string.IsNullOrEmpty(placed.SignText) && placed.showNextIndex.Value,
            _ => false
        };
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value &&
            attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && identityVerified && emptyStateVerified && consumed;

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
            PrimitiveKind = "place_sign",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_exact_native_sign_branch",
                    "placed_runtime_identity_and_empty_payload_verified",
                    "nonpassable_single_tile_result_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_sign_identity_verified" : "placed_sign_identity_mismatch",
                    emptyStateVerified ? "placed_sign_empty_state_verified" : "placed_sign_empty_state_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (placed?.GetType().FullName ?? "null") +
                ";placed_qualified_item_id=" + (placed?.QualifiedItemId ?? "null") +
                ";placement_kind=" + (placed is null ? "null" : RuntimePlacedSignKind(placed)) +
                ";display_item=" + ((placed as Sign)?.displayItem.Value?.QualifiedItemId ?? "null") +
                ";display_type=" + ((placed as Sign)?.displayType.Value.ToString() ?? "null") +
                ";sign_text=" + (placed?.SignText ?? string.Empty) +
                ";show_next_index=" + (placed?.showNextIndex.Value.ToString().ToLowerInvariant() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore + ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_sign_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId + ":" + expectedType + ":" + request.SignPlacementKind + ":empty"
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

    private static string? RuntimeSignPlacementKind(StardewValley.Object item)
    {
        if (!item.bigCraftable.Value)
        {
            return null;
        }
        if (item.HasContextTag("sign_item"))
        {
            return "display_item_sign";
        }
        return item.IsTextSign() ? "text_sign" : null;
    }

    private static string? RuntimePlacedSignKind(StardewValley.Object item) => item switch
    {
        Sign => "display_item_sign",
        _ when item.IsTextSign() => "text_sign",
        _ => null
    };
}
