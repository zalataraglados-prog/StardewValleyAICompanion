using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceCrabPot(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)710)->CrabPot.placementAction(owner=current_player)";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].runtime_type=StardewValley.Objects.CrabPot;current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].owner=current_player;current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].ready_for_harvest=false;player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "typed_target=missing", "place_crab_pot_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "native_contract=" + request.NativeContract, "place_crab_pot_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"),
                "place_crab_pot_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object inventoryPot ||
            !string.Equals(inventoryPot.QualifiedItemId, "(O)710", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, "(O)710", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "inventory_identity_mismatch", "place_crab_pot_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_crab_pot_player_not_adjacent");
        }
        if (!CrabPot.IsValidCrabPotLocationTile(location, target.X, target.Y) ||
            !CanPlaceInventoryObjectNative(location, inventoryPot, slot, target))
        {
            return BlockedWithPrimitive(request, "place_crab_pot", requested,
                "native_placement_recheck=false", "place_crab_pot_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventoryPot, slot, target);
        var placedPot = attempt.PlacedObject as CrabPot;
        var expectedNeedsBait = !Game1.player.professions.Contains(11);
        var identityVerified = placedPot?.GetType() == typeof(CrabPot) &&
            string.Equals(placedPot.QualifiedItemId, "(O)710", StringComparison.Ordinal) &&
            placedPot.owner.Value == Game1.player.UniqueMultiplayerID &&
            placedPot.Location == location && placedPot.TileLocation == new Vector2(target.X, target.Y);
        var initialStateVerified = placedPot is not null && placedPot.bait.Value is null &&
            placedPot.heldObject.Value is null && !placedPot.readyForHarvest.Value &&
            placedPot.NeedsBait(Game1.player) == expectedNeedsBait;
        var consumed = attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && identityVerified && initialStateVerified && consumed;

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
            PrimitiveKind = "place_crab_pot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_Object_placementAction_710",
                    "placed_exact_base_CrabPot_owned_by_current_player",
                    "initial_bait_output_and_ready_state_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_crab_pot_identity_verified" : "placed_crab_pot_identity_mismatch",
                    initialStateVerified ? "initial_state_verified" : "initial_state_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (attempt.PlacedObject?.GetType().FullName ?? "null") +
                ";placed_qualified_item_id=" + (attempt.PlacedObject?.QualifiedItemId ?? "null") +
                ";owner_player_id=" + (placedPot?.owner.Value.ToString() ?? "null") +
                ";bait=" + (placedPot?.bait.Value?.QualifiedItemId ?? "null") +
                ";held_output=" + (placedPot?.heldObject.Value?.QualifiedItemId ?? "null") +
                ";ready_for_harvest=" + (placedPot?.readyForHarvest.Value.ToString().ToLowerInvariant() ?? "null") +
                ";needs_bait=" + (placedPot?.NeedsBait(Game1.player).ToString().ToLowerInvariant() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_crab_pot_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = "(O)710:StardewValley.Objects.CrabPot:owner=" + Game1.player.UniqueMultiplayerID
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
}
