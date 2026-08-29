using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFireworkTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        var fireworkType = request.QualifiedItemId switch { "(O)893" => 0, "(O)894" => 1, "(O)895" => 2, _ => -1 };
        if (fireworkType < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_firework_target",
                "player.firework_placement.ready=true", "qualified_item_id=" + request.QualifiedItemId,
                "fixture_supported_firework_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        var slot = EnsureInventoryItem(request.QualifiedItemId, 1);
        var firework = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        if (firework?.GetType() != typeof(StardewValley.Object))
        {
            return BlockedWithPrimitive(request, "debug_setup_firework_target",
                "player.firework_placement.ready=true", "inventory_firework_unavailable",
                "fixture_exact_base_firework_required");
        }

        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var requested = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : new Point(width / 2, height / 2);
        var candidates = Enumerable.Range(2, Math.Max(0, height - 4))
            .SelectMany(y => Enumerable.Range(2, Math.Max(0, width - 4)).Select(x => new Point(x, y)))
            .OrderBy(tile => ManhattanDistance(tile, requested));

        var target = Point.Zero;
        var stand = Point.Zero;
        var nativeLegal = false;
        var moveReason = "fixture_no_native_legal_firework_target";
        foreach (var candidate in candidates)
        {
            var position = new Vector2(candidate.X * Game1.tileSize, candidate.Y * Game1.tileSize);
            if (farm.temporarySprites.Any(sprite => sprite.position.Equals(position)))
            {
                moveReason = "fixture_target_has_exact_temporary_sprite";
                continue;
            }
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateMoveReason))
            {
                moveReason = candidateMoveReason ?? "fixture_adjacent_move_failed";
                continue;
            }
            firework.Location = farm;
            firework.TileLocation = Vector2.Zero;
            if (!CanPlaceInventoryObjectNative(farm, firework, slot, candidate))
                continue;
            target = candidate;
            stand = candidateStand;
            nativeLegal = true;
            break;
        }

        var verified = nativeLegal && Game1.player.TilePoint == stand && ManhattanDistance(stand, target) == 1;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            TargetTileX = target.X, TargetTileY = target.Y,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_firework_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "exact_base_inventory_firework_ready", "native_legal_transient_free_target_ready", "shared_adjacent_path_fixture_ready", "inventory_slot_index=" + slot, "stand_tile=" + stand.X + "," + stand.Y, "firework_type=" + fireworkType }
                : new[] { moveReason ?? "fixture_no_native_legal_firework_target" },
            RequestedEffect = "player.firework_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y + ";inventory_slot_index=" + slot +
                ";qualified_item_id=" + firework.QualifiedItemId + ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "firework_fixture_not_ready" }
        };
    }
}
