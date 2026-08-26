using Microsoft.Xna.Framework;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupTentPlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.Direction.HasValue || request.Direction.Value is < 0 or > 3)
        {
            return BlockedWithPrimitive(request, "debug_setup_tent_placement_target",
                "player.tent_placement.ready=true", "target_or_direction=missing", "target_tile_and_direction_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        foreach (var oldTent in farm.largeTerrainFeatures.Where(feature => feature.GetType() == typeof(Tent)).ToArray())
        {
            farm.largeTerrainFeatures.Remove(oldTent);
        }

        var slot = EnsureInventoryItem("(O)TentKit", 1);
        var kit = slot >= 0 && slot < Game1.player.Items.Count
            ? Game1.player.Items[slot] as StardewValley.Object
            : null;
        var requestedStand = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        TentPlacementGeometry? selected = null;
        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var stands = Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y)))
            .OrderBy(tile => Math.Abs(tile.X - requestedStand.X) + Math.Abs(tile.Y - requestedStand.Y))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X);
        foreach (var stand in stands)
        {
            if (!IsTileWalkable(farm, stand))
            {
                continue;
            }
            var geometry = TentPlacementGeometryResolver.ResolveFromStand(stand.X, stand.Y, request.Direction.Value);
            var rectangle = new Rectangle(geometry.RectangleX, geometry.RectangleY, geometry.RectangleWidth, geometry.RectangleHeight);
            if (!farm.isAreaClear(rectangle))
            {
                continue;
            }

            Game1.player.Position = stand.ToVector2() * Game1.tileSize;
            Game1.player.faceDirection(geometry.Direction);
            if (kit is not null)
            {
                kit.Location = farm;
                kit.TileLocation = Vector2.Zero;
            }
            if (kit is not null && CanPlaceInventoryObjectNative(
                    farm,
                    kit,
                    slot,
                    new Point(geometry.TargetTileX, geometry.TargetTileY)))
            {
                selected = geometry;
                break;
            }
        }

        var verified = selected is not null && kit?.GetType() == typeof(StardewValley.Object) &&
            string.Equals(kit.QualifiedItemId, "(O)TentKit", StringComparison.Ordinal);
        var geometryResult = selected ?? TentPlacementGeometryResolver.ResolveFromStand(requestedStand.X, requestedStand.Y, request.Direction.Value);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            TargetTileX = geometryResult.TargetTileX,
            TargetTileY = geometryResult.TargetTileY,
            TentDirection = geometryResult.Direction,
            TentStandTileX = geometryResult.StandTileX,
            TentStandTileY = geometryResult.StandTileY,
            TentRectangleX = geometryResult.RectangleX,
            TentRectangleY = geometryResult.RectangleY,
            TentRectangleWidth = geometryResult.RectangleWidth,
            TentRectangleHeight = geometryResult.RectangleHeight,
            TentAnchorTileX = geometryResult.AnchorTileX,
            TentAnchorTileY = geometryResult.AnchorTileY,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_tent_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "exact_base_TentKit_ready", "native_directional_3x2_area_clear", "exact_stand_and_facing_ready", "inventory_slot_index=" + slot }
                : new[] { slot >= 0 ? "inventory_ready_but_no_native_directional_stand" : "inventory_unavailable" },
            RequestedEffect = "player.tent_placement.ready=true",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName +
                ";stand_tile=" + geometryResult.StandTileX + "," + geometryResult.StandTileY +
                ";target_probe_tile=" + geometryResult.TargetTileX + "," + geometryResult.TargetTileY +
                ";direction=" + geometryResult.Direction +
                ";rectangle=" + geometryResult.RectangleX + "," + geometryResult.RectangleY + "," + geometryResult.RectangleWidth + "x" + geometryResult.RectangleHeight +
                ";anchor_tile=" + geometryResult.AnchorTileX + "," + geometryResult.AnchorTileY +
                ";inventory_slot_index=" + slot +
                ";inventory_runtime_type=" + (kit?.GetType().FullName ?? "null") +
                ";inventory_qualified_item_id=" + (kit?.QualifiedItemId ?? "null"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "tent_placement_fixture_not_ready" }
        };
    }
}
