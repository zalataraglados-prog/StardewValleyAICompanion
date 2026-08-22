using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceCookoutKit(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)926)->Torch((BC)278,destroyOvernight:true)";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].qualified_item_id=(BC)278;current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].destroy_over_night=true;player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue)
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "typed_target=missing", "place_cookout_kit_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "native_contract=" + request.NativeContract, "place_cookout_kit_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"),
                "place_cookout_kit_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object kit ||
            !string.Equals(kit.QualifiedItemId, "(O)926", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, "(O)926", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "inventory_identity_mismatch", "place_cookout_kit_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_cookout_kit_player_not_adjacent");
        }

        var targetVector = new Vector2(target.X, target.Y);
        if (location.objects.ContainsKey(targetVector) ||
            !CanPlaceInventoryObjectNative(location, kit, slot, target))
        {
            return BlockedWithPrimitive(request, "place_cookout_kit", requested,
                "native_placement_recheck=false", "place_cookout_kit_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, kit, slot, target);
        var placedKit = attempt.PlacedObject as Torch;
        var identityVerified = placedKit is not null &&
            string.Equals(placedKit.QualifiedItemId, "(BC)278", StringComparison.Ordinal) &&
            placedKit.Fragility == 1 && placedKit.destroyOvernight;
        var consumed = attempt.StackAfter == attempt.StackBefore - 1;
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
            PrimitiveKind = "place_cookout_kit",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_Object_placementAction",
                    "placed_exact_Torch_278_fragility_1_destroyOvernight_true",
                    "inventory_stack_decreased_exactly_one",
                    "placed_Torch_is_native_cooking_endpoint"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_cookout_identity_verified" : "placed_cookout_identity_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (attempt.PlacedObject?.GetType().FullName ?? "null") +
                ";placed_qualified_item_id=" + (attempt.PlacedObject?.QualifiedItemId ?? "null") +
                ";fragility=" + (attempt.PlacedObject?.Fragility.ToString() ?? "null") +
                ";destroy_over_night=" + (attempt.PlacedObject?.destroyOvernight.ToString().ToLowerInvariant() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_cookout_kit_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = "(BC)278:Torch:destroyOvernight=true"
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
