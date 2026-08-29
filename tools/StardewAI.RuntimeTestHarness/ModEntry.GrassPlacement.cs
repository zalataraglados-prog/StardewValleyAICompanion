using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlantGrass(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)297|(O)BlueGrassStarter)->terrainFeatures.Add(Grass(type,4))";
        var requested = "current_location.terrain_features[" + request.TargetTileX + "," + request.TargetTileY +
            "].runtime_type=StardewValley.TerrainFeatures.Grass;grass_type=" + request.ExpectedGrassType +
            ";number_of_weeds=" + request.ExpectedInitialNumberOfWeeds +
            ";player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.ExpectedGrassType.HasValue ||
            !request.ExpectedInitialNumberOfWeeds.HasValue)
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "typed_target=missing", "plant_grass_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal) ||
            !string.Equals(request.TargetRuntimeType, typeof(Grass).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.GrassPlacementSound, "dirtyHit", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "native_contract_or_target_runtime_mismatch", "plant_grass_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "plant_grass_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        var expectedVariantType = string.Equals(request.QualifiedItemId, "(O)297", StringComparison.Ordinal) ? 1 :
            string.Equals(request.QualifiedItemId, "(O)BlueGrassStarter", StringComparison.Ordinal) ? 7 : -1;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object inventoryGrass ||
            inventoryGrass.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(inventoryGrass.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            expectedVariantType != request.ExpectedGrassType.Value || request.ExpectedInitialNumberOfWeeds.Value != 4)
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "inventory_or_grass_variant_identity_mismatch", "plant_grass_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "plant_grass_player_not_adjacent");
        }
        if (location.objects.ContainsKey(targetVector) || location.terrainFeatures.ContainsKey(targetVector) ||
            !CanPlaceInventoryObjectNative(location, inventoryGrass, slot, target))
        {
            return BlockedWithPrimitive(request, "plant_grass", requested,
                "native_placement_recheck=false", "plant_grass_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventoryGrass, slot, target);
        var placedGrass = attempt.PlacedTerrainFeature as Grass;
        var identityVerified = placedGrass?.GetType() == typeof(Grass) &&
            placedGrass.grassType.Value == request.ExpectedGrassType.Value &&
            placedGrass.numberOfWeeds.Value == request.ExpectedInitialNumberOfWeeds.Value &&
            placedGrass.Location == location && placedGrass.Tile == targetVector;
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value &&
            attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && attempt.PlacedObject is null && identityVerified && consumed;

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
            PrimitiveKind = "plant_grass",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_exact_native_grass_starter_branch",
                    "placed_exact_base_Grass_type_and_four_initial_weeds_verified",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    attempt.Placed ? "native_place_returned_true" : "native_place_returned_false",
                    identityVerified ? "placed_grass_identity_verified" : "placed_grass_identity_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";placed_runtime_type=" + (attempt.PlacedTerrainFeature?.GetType().FullName ?? "null") +
                ";grass_type=" + (placedGrass?.grassType.Value.ToString() ?? "null") +
                ";number_of_weeds=" + (placedGrass?.numberOfWeeds.Value.ToString() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "plant_grass_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.terrain_features[" + target.X + "," + target.Y + "]",
                        Before = "missing",
                        After = "grass_type=" + request.ExpectedGrassType + ":number_of_weeds=" + request.ExpectedInitialNumberOfWeeds
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
