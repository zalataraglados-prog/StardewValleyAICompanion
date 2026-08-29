using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FireworkNativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)893|(O)894|(O)895)->broadcastSprites+netAudio(fuse)+DelayedAction.StopPlaying(fuse)";
    private const string FireworkRandomContract = "live_Game1.random_runtime_only_no_read_side_rng_advance";

    private TrainingExecutionResult ExecuteUseFirework(TrainingExecutionRequest request)
    {
        var requested = "current_location.temporary_firework[" + request.TargetTileX + "," + request.TargetTileY +
            "].type=" + request.ExpectedFireworkType + ";player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.ExpectedFireworkType.HasValue ||
            !request.ExpectedFireworkSourceRectX.HasValue || !request.ExpectedFireworkSourceRectY.HasValue ||
            !request.ExpectedFireworkFuseDurationMs.HasValue || !request.ExpectedFireworkRocketDelayMs.HasValue ||
            !request.ExpectedFireworkRocketIdMin.HasValue || !request.ExpectedFireworkRocketIdMax.HasValue)
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "typed_target=missing", "use_firework_typed_target_fields_required");
        }

        var expectedType = request.QualifiedItemId switch { "(O)893" => 0, "(O)894" => 1, "(O)895" => 2, _ => -1 };
        if (expectedType < 0 || request.ExpectedFireworkType != expectedType ||
            request.ExpectedFireworkSourceRectX != 256 + expectedType * 16 || request.ExpectedFireworkSourceRectY != 397 ||
            request.ExpectedFireworkFuseDurationMs != 2400 || request.ExpectedFireworkRocketDelayMs != 2400 ||
            request.ExpectedFireworkRocketIdMin != 20 || request.ExpectedFireworkRocketIdMax != 30 ||
            !string.Equals(request.FireworkAccelerationYMin, "-0.36", StringComparison.Ordinal) ||
            !string.Equals(request.FireworkAccelerationYMax, "-0.27", StringComparison.Ordinal) ||
            !string.Equals(request.FireworkAccelerationYStep, "0.01", StringComparison.Ordinal) ||
            !string.Equals(request.FireworkRandomContract, FireworkRandomContract, StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, FireworkNativeContract, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "native_effect_projection_mismatch", "use_firework_native_contract_or_variant_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "use_firework_location_mismatch");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object inventoryFirework ||
            inventoryFirework.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(inventoryFirework.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "inventory_identity_mismatch", "use_firework_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetPosition = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "use_firework_player_not_adjacent");
        }
        if (location.temporarySprites.Any(sprite => sprite.position.Equals(targetPosition)) ||
            !CanPlaceInventoryObjectNative(location, inventoryFirework, slot, target))
        {
            return BlockedWithPrimitive(request, "use_firework", requested,
                "native_placement_recheck=false", "use_firework_native_placement_recheck_failed");
        }

        var beforeSprites = location.temporarySprites.ToHashSet();
        var started = DateTimeOffset.UtcNow.ToString("O");
        var attempt = PlaceInventoryObjectNative(location, inventoryFirework, slot, target);
        var created = location.temporarySprites.Where(sprite => !beforeSprites.Contains(sprite)).ToArray();
        var exactTarget = created.Where(sprite => sprite.position.Equals(targetPosition)).ToArray();
        var rocket = created.SingleOrDefault(sprite => sprite.fireworkType == expectedType &&
            sprite.delayBeforeAnimationStart == 2400 && sprite.startSound == "firework");
        var sourceVerified = rocket is not null && rocket.sourceRect.X == request.ExpectedFireworkSourceRectX &&
            rocket.sourceRect.Y == request.ExpectedFireworkSourceRectY;
        var accelerationStep = rocket is null ? double.NaN : (rocket.acceleration.Y + 0.36f) * 100f;
        var randomDomainVerified = rocket is not null && rocket.id >= 20 && rocket.id <= 30 &&
            rocket.acceleration.Y >= -0.36001f && rocket.acceleration.Y <= -0.26999f &&
            Math.Abs(accelerationStep - Math.Round(accelerationStep)) < 0.001;
        var consumed = attempt.StackBefore == request.ExpectedStackBefore.Value && attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && attempt.PlacedObject is null && attempt.PlacedTerrainFeature is null &&
            created.Length == 5 && exactTarget.Length == 2 && sourceVerified && randomDomainVerified && consumed;

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
            PrimitiveKind = "use_firework",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "shared_Utility_playerCanPlaceItemHere_rechecked",
                    "shared_Utility_tryToPlaceItem_invoked_exact_native_firework_branch",
                    "five_native_temporary_sprites_and_exact_variant_rocket_verified",
                    "runtime_random_outcomes_within_decompiled_domains",
                    "inventory_stack_decreased_exactly_one"
                }
                : new[]
                {
                    "created_sprite_count=" + created.Length,
                    "exact_target_sprite_count=" + exactTarget.Length,
                    sourceVerified ? "source_rect_verified" : "source_rect_mismatch",
                    randomDomainVerified ? "random_domain_verified" : "random_domain_mismatch",
                    consumed ? "inventory_consumed_one" : "inventory_consumption_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";created_sprite_count=" + created.Length +
                ";exact_target_sprite_count=" + exactTarget.Length +
                ";firework_type=" + (rocket?.fireworkType.ToString() ?? "null") +
                ";rocket_id=" + (rocket?.id.ToString() ?? "null") +
                ";rocket_acceleration_y=" + (rocket?.acceleration.Y.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_firework_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "current_location.temporary_firework[" + target.X + "," + target.Y + "]", Before = "missing", After = "firework_type=" + expectedType + ":native_sprite_count=5" },
                    new SimulatedFactChange { Path = "player.inventory[" + slot + "].stack", Before = attempt.StackBefore.ToString(), After = attempt.StackAfter.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
