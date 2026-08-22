using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCookoutKitPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_cookout_kit_placement_target",
                "player.cookout_kit_placement.ready=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var targetVector = new Vector2(target.X, target.Y);
        farm.objects.Remove(targetVector);
        farm.terrainFeatures.Remove(targetVector);
        var slot = EnsureInventoryItem("(O)926", 1);
        var moved = MoveFixtureFarmerToFarmAdjacent(target, out var stand, out var moveReason);
        var kit = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;
        if (kit is not null)
        {
            kit.Location = farm;
            kit.TileLocation = Vector2.Zero;
        }
        var nativeLegal = kit is not null &&
            CanPlaceInventoryObjectNative(farm, kit, slot, target);
        var scannedWidth = 0;
        var scannedHeight = 0;
        var nativeLocationLegalCount = 0;
        var itemLegalCount = 0;
        if (kit is not null && !nativeLegal)
        {
            var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
            var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
            var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
            scannedWidth = width;
            scannedHeight = height;
            var candidates = Enumerable.Range(0, height)
                .SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y)))
                .OrderBy(tile => Math.Abs(tile.X - target.X) + Math.Abs(tile.Y - target.Y));
            foreach (var candidate in candidates)
            {
                var tile = new Vector2(candidate.X, candidate.Y);
                if (farm.CanItemBePlacedHere(tile, kit.isPassable(), CollisionMask.All))
                {
                    nativeLocationLegalCount++;
                }
                if (kit.canBePlacedHere(farm, tile, CollisionMask.All))
                {
                    itemLegalCount++;
                }
                if (farm.objects.ContainsKey(tile) || farm.terrainFeatures.ContainsKey(tile) ||
                    !kit.canBePlacedHere(farm, tile, CollisionMask.All))
                {
                    continue;
                }
                if (!MoveFixtureFarmerToFarmAdjacent(candidate, out var candidateStand, out var candidateMoveReason))
                {
                    moveReason = candidateMoveReason;
                    continue;
                }
                if (!CanPlaceInventoryObjectNative(farm, kit, slot, candidate))
                {
                    continue;
                }
                target = candidate;
                stand = candidateStand;
                moved = true;
                nativeLegal = true;
                break;
            }
        }
        var verified = moved && nativeLegal && slot >= 0 && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_cookout_kit_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_inventory_O926_ready",
                    "target_tile_cleared",
                    "shared_adjacent_path_fixture_ready",
                    "Utility.playerCanPlaceItemHere=true",
                    "inventory_slot_index=" + slot,
                    "stand_tile=" + stand.X + "," + stand.Y
                }
                : new[] { slot >= 0 ? "inventory_ready" : "inventory_unavailable", moved ? "player_moved_adjacent" : moveReason },
            RequestedEffect = "player.cookout_kit_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y + ";inventory_slot_index=" + slot +
                ";inventory_runtime_type=" + (kit?.GetType().FullName ?? "null") +
                ";inventory_qualified_item_id=" + (kit?.QualifiedItemId ?? "null") +
                ";inventory_type=" + (kit?.Type ?? "null") +
                ";item_is_placeable=" + (kit?.isPlaceable().ToString().ToLowerInvariant() ?? "null") +
                ";item_can_be_placed_at_target=" + (kit?.canBePlacedHere(farm, new Vector2(target.X, target.Y), CollisionMask.All).ToString().ToLowerInvariant() ?? "null") +
                ";map_dimensions=" + scannedWidth + "x" + scannedHeight +
                ";native_location_legal_count=" + nativeLocationLegalCount +
                ";item_legal_count=" + itemLegalCount +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";within_one_tile=" + Utility.withinRadiusOfPlayer(
                    target.X * Game1.tileSize,
                    target.Y * Game1.tileSize,
                    1,
                    Game1.player).ToString().ToLowerInvariant() +
                ";event_up=" + Game1.eventUp.ToString().ToLowerInvariant() +
                ";bathing_clothes=" + Game1.player.bathingClothes.Value.ToString().ToLowerInvariant() +
                ";on_bridge=" + Game1.player.onBridge.Value.ToString().ToLowerInvariant() +
                ";native_placement_legal=" + nativeLegal.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "cookout_kit_placement_fixture_not_ready" }
        };
    }
}
