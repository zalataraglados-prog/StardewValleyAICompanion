using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteLoadCrabPotBait(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "GameLocation.checkAction->CrabPot.performObjectDropInAction(Category=-21,probe:false,owner=current_player)->Farmer.reduceActiveItemByOne";
        var requested = "current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].crab_pot_bait=" + request.QualifiedItemId + ";current_location.objects[" + request.TargetTileX + "," + request.TargetTileY +
            "].owner=current_player;player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.InventorySlotIndex.HasValue ||
            !request.ExpectedStackBefore.HasValue || !request.BaitQuality.HasValue ||
            !request.ExpectedContainerOwnerPlayerIdBefore.HasValue || !request.ExpectedContainerOwnerPlayerIdAfter.HasValue)
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "typed_target=missing", "load_crab_pot_bait_typed_projection_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "native_contract=" + request.NativeContract, "load_crab_pot_bait_native_contract_mismatch");
        }
        if (!string.Equals(request.TargetRuntimeType, typeof(CrabPot).FullName, StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedContainerBaitQualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "target_or_bait_identity_mismatch", "load_crab_pot_bait_identity_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "load_crab_pot_bait_location_mismatch");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        if (!location.objects.TryGetValue(targetVector, out var targetObject) ||
            targetObject is not CrabPot pot || pot.GetType() != typeof(CrabPot) ||
            pot.Location != location || pot.TileLocation != targetVector)
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "target_runtime_type=" + (targetObject?.GetType().FullName ?? "missing"), "load_crab_pot_bait_exact_base_target_required");
        }
        if (Math.Abs(Game1.player.TilePoint.X - target.X) + Math.Abs(Game1.player.TilePoint.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y, "load_crab_pot_bait_player_not_adjacent");
        }
        if (pot.bait.Value is not null || pot.readyForHarvest.Value || pot.heldObject.Value is not null ||
            !pot.NeedsBait(Game1.player) || pot.owner.Value != request.ExpectedContainerOwnerPlayerIdBefore.Value ||
            request.ExpectedContainerOwnerPlayerIdAfter.Value != Game1.player.UniqueMultiplayerID)
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                CrabPotBaitObservedEffect(pot), "load_crab_pot_bait_target_state_drifted");
        }

        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewObject bait || bait.GetType() != typeof(StardewObject) ||
            bait.Category != StardewObject.baitCategory || bait.Stack != request.ExpectedStackBefore.Value ||
            !string.Equals(bait.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(bait.GetType().FullName, request.BaitRuntimeType, StringComparison.Ordinal) ||
            bait.Quality != request.BaitQuality.Value)
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "inventory_identity_mismatch", "load_crab_pot_bait_inventory_identity_drift");
        }
        var expectedBaitKey = ClearanceOutputItemKey.From(bait);
        if (!string.Equals(expectedBaitKey.UnitStateSha256, request.ExpectedContainerBaitUnitStateSha256, StringComparison.Ordinal) ||
            !pot.performObjectDropInAction(bait, probe: true, Game1.player))
        {
            return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                "unit_state_or_native_probe_mismatch", "load_crab_pot_bait_native_probe_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var ownerBefore = pot.owner.Value;
        var stackBefore = bait.Stack;
        var previousSlot = Game1.player.CurrentToolIndex;
        bool handled;
        try
        {
            Game1.player.CurrentToolIndex = slot;
            if (!ReferenceEquals(Game1.player.ActiveObject, bait))
            {
                return BlockedWithPrimitive(request, "load_crab_pot_bait", requested,
                    "active_object_identity_mismatch", "load_crab_pot_bait_active_slot_drift");
            }
            handled = location.checkAction(
                new TileLocation(target.X, target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
        }
        finally
        {
            Game1.player.CurrentToolIndex = previousSlot;
        }

        var stackAfter = Game1.player.Items.ElementAtOrDefault(slot)?.Stack ?? 0;
        var actualBait = pot.bait.Value;
        var actualKey = actualBait is null ? default(ClearanceOutputItemKey?) : ClearanceOutputItemKey.From(actualBait);
        var verified = handled && actualBait is not null && actualKey.HasValue &&
            string.Equals(actualKey.Value.RuntimeType, request.BaitRuntimeType, StringComparison.Ordinal) &&
            string.Equals(actualKey.Value.QualifiedItemId, request.ExpectedContainerBaitQualifiedItemId, StringComparison.Ordinal) &&
            actualKey.Value.Quality == request.BaitQuality.Value &&
            string.Equals(actualKey.Value.UnitStateSha256, request.ExpectedContainerBaitUnitStateSha256, StringComparison.Ordinal) &&
            pot.owner.Value == request.ExpectedContainerOwnerPlayerIdAfter.Value &&
            stackAfter == stackBefore - 1 && !pot.readyForHarvest.Value && pot.heldObject.Value is null;

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
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "load_crab_pot_bait",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_CrabPot_drop_in_probe_rechecked",
                    "native_GameLocation_checkAction_loaded_bait",
                    "native_reduceActiveItemByOne_consumed_exactly_one",
                    "bait_identity_unit_state_and_owner_verified"
                }
                : new[] { "load_crab_pot_bait_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = CrabPotBaitObservedEffect(pot) +
                ";inventory_stack_before=" + stackBefore + ";inventory_stack_after=" + stackAfter +
                ";handled=" + handled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "load_crab_pot_bait_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].crab_pot_bait",
                        Before = "",
                        After = actualBait!.QualifiedItemId + ":" + actualKey!.Value.UnitStateSha256
                    },
                    new SimulatedFactChange
                    {
                        Path = "current_location.objects[" + target.X + "," + target.Y + "].owner",
                        Before = ownerBefore.ToString(),
                        After = pot.owner.Value.ToString()
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "].stack",
                        Before = stackBefore.ToString(),
                        After = stackAfter.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static string CrabPotBaitObservedEffect(CrabPot pot) =>
        "runtime_type=" + pot.GetType().FullName +
        ";owner=" + pot.owner.Value +
        ";bait=" + (pot.bait.Value?.QualifiedItemId ?? "null") +
        ";bait_runtime_type=" + (pot.bait.Value?.GetType().FullName ?? "null") +
        ";bait_quality=" + (pot.bait.Value?.Quality.ToString() ?? "null") +
        ";ready_for_harvest=" + pot.readyForHarvest.Value.ToString().ToLowerInvariant() +
        ";held_output=" + (pot.heldObject.Value?.QualifiedItemId ?? "null");
}
