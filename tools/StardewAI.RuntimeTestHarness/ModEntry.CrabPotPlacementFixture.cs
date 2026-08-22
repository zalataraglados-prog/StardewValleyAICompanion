using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCrabPotPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        var slot = EnsureInventoryItem("(O)710", 1);
        var inventoryPot = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;

        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var requested = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : new Point(-1, -1);
        var candidates = new[] { requested }
            .Concat(Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y))))
            .Where(tile => tile.X >= 0 && tile.Y >= 0)
            .Distinct()
            .OrderBy(tile => requested.X < 0 ? tile.Y * Math.Max(1, width) + tile.X : ManhattanDistance(tile, requested));

        var target = Point.Zero;
        var stand = Point.Zero;
        var moveReason = "fixture_no_native_legal_reachable_water_tile";
        var nativeLegal = false;
        var nativeLegalWaterCountScanned = 0;
        if (inventoryPot is not null)
        {
            foreach (var candidate in candidates)
            {
                if (!CrabPot.IsValidCrabPotLocationTile(farm, candidate.X, candidate.Y))
                {
                    continue;
                }
                nativeLegalWaterCountScanned++;
                if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var candidateMoveReason))
                {
                    moveReason = candidateMoveReason;
                    continue;
                }
                if (!CanPlaceInventoryObjectNative(farm, inventoryPot, slot, candidate))
                {
                    continue;
                }
                target = candidate;
                stand = candidateStand;
                nativeLegal = true;
                break;
            }
        }

        var verified = slot >= 0 && inventoryPot is not null && nativeLegal &&
            Game1.player.TilePoint == stand && ManhattanDistance(stand, target) == 1;
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
            PrimitiveKind = "debug_setup_crab_pot_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_inventory_O710_ready",
                    "native_legal_reachable_water_tile_found",
                    "shared_adjacent_path_fixture_ready",
                    "Utility.playerCanPlaceItemHere=true",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y
                }
                : new[] { slot >= 0 ? "inventory_ready" : "inventory_unavailable", nativeLegal ? "native_placement_legal" : moveReason },
            RequestedEffect = "player.crab_pot_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName +
                ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y +
                ";inventory_slot_index=" + slot +
                ";inventory_runtime_type=" + (inventoryPot?.GetType().FullName ?? "null") +
                ";inventory_qualified_item_id=" + (inventoryPot?.QualifiedItemId ?? "null") +
                ";map_dimensions=" + width + "x" + height +
                ";native_legal_water_count_scanned=" + nativeLegalWaterCountScanned +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "crab_pot_placement_fixture_not_ready" }
        };
    }
}
