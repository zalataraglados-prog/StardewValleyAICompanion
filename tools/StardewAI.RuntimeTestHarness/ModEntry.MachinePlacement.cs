using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceMachine(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var requested = "farm.machines[" + request.LocationId + ":" +
            request.TargetTileX + "," + request.TargetTileY +
            "].qualified_item_id=" + request.QualifiedItemId +
            ";player.inventory[" + request.InventorySlotIndex +
            "].stack_decreases=1";
        if (!request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "typed_target=missing",
                "place_machine_typed_target_fields_required");
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
                "place_machine",
                requested,
                "location_id=" +
                (location?.NameOrUniqueName ?? "unavailable"),
                "place_machine_location_mismatch");
        }

        var slotIndex = request.InventorySlotIndex.Value;
        if (slotIndex < 0 || slotIndex >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_slot=" + slotIndex,
                "place_machine_inventory_slot_out_of_range");
        }
        if (Game1.player.Items[slotIndex] is not StardewValley.Object machine ||
            !machine.bigCraftable.Value ||
            machine.GetMachineData() is null)
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_slot_item=not_machine",
                "place_machine_inventory_slot_not_machine");
        }
        if (!string.Equals(
                machine.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(request.ItemId) &&
             !string.Equals(
                 machine.ItemId,
                 request.ItemId,
                 StringComparison.Ordinal)))
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "inventory_item=" + machine.QualifiedItemId,
                "place_machine_inventory_identity_mismatch");
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
                "place_machine",
                requested,
                "player_tile=" + playerTile.X + "," + playerTile.Y,
                "place_machine_player_not_adjacent");
        }

        var targetVector = new Vector2(target.X, target.Y);
        var pixelX = target.X * Game1.tileSize;
        var pixelY = target.Y * Game1.tileSize;
        if (location.objects.ContainsKey(targetVector) ||
            !Utility.playerCanPlaceItemHere(
                location,
                machine,
                pixelX,
                pixelY,
                Game1.player))
        {
            return BlockedWithPrimitive(
                request,
                "place_machine",
                requested,
                "native_placement_recheck=false",
                "place_machine_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var selectedSlotBefore = Game1.player.CurrentToolIndex;
        var stackBefore = machine.Stack;
        Game1.player.CurrentToolIndex = slotIndex;
        var placed = Utility.tryToPlaceItem(
            location,
            machine,
            pixelX,
            pixelY);
        if (selectedSlotBefore >= 0 &&
            selectedSlotBefore < Game1.player.Items.Count)
        {
            Game1.player.CurrentToolIndex = selectedSlotBefore;
        }

        location.objects.TryGetValue(
            targetVector,
            out var placedObject);
        var afterSlot = slotIndex < Game1.player.Items.Count
            ? Game1.player.Items[slotIndex]
            : null;
        var stackAfter = afterSlot?.Stack ?? 0;
        var inventoryConsumed = stackAfter == stackBefore - 1;
        var placedIdentityMatches = placedObject is not null &&
            string.Equals(
                placedObject.QualifiedItemId,
                request.QualifiedItemId,
                StringComparison.OrdinalIgnoreCase);
        var verified = placed &&
            placedIdentityMatches &&
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
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_machine",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "Utility.playerCanPlaceItemHere_rechecked",
                    "Utility.tryToPlaceItem_applied_native_callbacks",
                    "placed_machine_identity_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    placed
                        ? "native_place_returned_true"
                        : "native_place_returned_false",
                    placedIdentityMatches
                        ? "placed_identity_matches"
                        : "placed_identity_missing_or_mismatched",
                    inventoryConsumed
                        ? "inventory_consumed_one"
                        : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" +
                location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_qualified_item_id=" +
                (placedObject?.QualifiedItemId ?? "null") +
                ";inventory_stack_before=" + stackBefore +
                ";inventory_stack_after=" + stackAfter,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "place_machine_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" +
                            location.NameOrUniqueName + ":" +
                            target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = request.QualifiedItemId
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slotIndex + "].stack",
                        Before = stackBefore.ToString(),
                        After = stackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
