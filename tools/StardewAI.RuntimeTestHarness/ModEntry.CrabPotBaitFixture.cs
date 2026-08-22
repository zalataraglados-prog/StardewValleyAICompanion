using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCrabPotBaitTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(request, "debug_setup_crab_pot_bait_target",
                "crab_pot_bait_fixture=ready", "typed_target=missing", "fixture_target_and_bait_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        if (location is null || !location.objects.TryGetValue(targetVector, out var targetObject) ||
            targetObject is not CrabPot pot || pot.GetType() != typeof(CrabPot))
        {
            return BlockedWithPrimitive(request, "debug_setup_crab_pot_bait_target",
                "crab_pot_bait_fixture=ready", "target=" + target.X + "," + target.Y,
                "fixture_exact_base_crab_pot_required");
        }

        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.professions.Remove(11);
        pot.owner.Value = Game1.player.UniqueMultiplayerID;
        pot.bait.Value = null;
        pot.heldObject.Value = null;
        pot.readyForHarvest.Value = false;
        pot.tileIndexToShow = 710;

        var quantity = Math.Max(2, request.Quantity ?? 2);
        var slot = EnsureInventoryItem(request.QualifiedItemId, quantity);
        var bait = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewObject
            : null;
        if (bait is not null)
        {
            bait.Stack = quantity;
        }
        var moved = MoveFixtureFarmerToLocationAdjacent(location, target, out var stand, out var moveReason);
        var accepted = bait is not null && bait.GetType() == typeof(StardewObject) &&
            bait.Category == StardewObject.baitCategory && pot.performObjectDropInAction(bait, probe: true, Game1.player);
        var verified = moved && accepted && pot.NeedsBait(Game1.player) && pot.bait.Value is null &&
            pot.heldObject.Value is null && !pot.readyForHarvest.Value && Game1.player.TilePoint == stand;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_crab_pot_bait_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_base_CrabPot_reset_empty",
                    "owner_luremaster_removed_for_bait_required_fixture",
                    "exact_base_bait_Category_minus_21_ready",
                    "native_drop_in_probe_accepted",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y
                }
                : new[] { moved ? "adjacent_fixture_ready" : moveReason, accepted ? "native_probe_accepted" : "native_probe_rejected" },
            RequestedEffect = "crab_pot_bait_fixture=ready;qualified_item_id=" + request.QualifiedItemId,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slot +
                ";inventory_runtime_type=" + (bait?.GetType().FullName ?? "null") +
                ";inventory_qualified_item_id=" + (bait?.QualifiedItemId ?? "null") +
                ";inventory_category=" + (bait?.Category.ToString() ?? "null") +
                ";inventory_stack=" + (bait?.Stack.ToString() ?? "null") +
                ";native_probe_accepts=" + accepted.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "crab_pot_bait_fixture_not_ready" }
        };
    }
}
