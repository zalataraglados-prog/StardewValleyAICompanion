using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSignPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            !request.QualifiedItemId.StartsWith("(BC)", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "debug_setup_sign_placement_target",
                "player.sign_placement.ready=true", "qualified_item_id=missing", "fixture_sign_qid_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.activeClickableMenu = null;
        var slot = EnsureInventoryItem(request.QualifiedItemId, 1);
        if (slot < 0 || Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object sign || RuntimeSignPlacementKind(sign) is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_sign_placement_target",
                "player.sign_placement.ready=true", "inventory_sign=unavailable", "fixture_vanilla_sign_required");
        }

        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var target = Point.Zero;
        var stand = Point.Zero;
        var moveReason = string.Empty;
        var found = false;
        foreach (var candidate in Enumerable.Range(0, height)
                     .SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y)))
                     .OrderBy(tile => ManhattanDistance(tile, Game1.player.TilePoint)))
        {
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateReason))
            {
                moveReason = candidateReason;
                continue;
            }
            if (!CanPlaceInventoryObjectNative(farm, sign, slot, candidate))
            {
                continue;
            }
            target = candidate;
            stand = candidateStand;
            found = true;
            break;
        }
        var verified = found && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_sign_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "vanilla_inventory_sign_ready", "farm_native_legal_target_ready", "inventory_slot_index=" + slot, "placement_kind=" + RuntimeSignPlacementKind(sign) }
                : new[] { "fixture_no_native_legal_sign_target", moveReason },
            RequestedEffect = "player.sign_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y + ";inventory_slot_index=" + slot +
                ";qualified_item_id=" + sign.QualifiedItemId + ";placement_kind=" + RuntimeSignPlacementKind(sign),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "sign_placement_fixture_not_ready" }
        };
    }
}
