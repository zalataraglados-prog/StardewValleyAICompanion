using Microsoft.Xna.Framework;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePlaceTent(TrainingExecutionRequest request)
    {
        const string nativeContract =
            "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction((O)TentKit)->largeTerrainFeatures.Add(Tent(rectangle.X+1,rectangle.Y+1))";
        var requested = "current_location.large_terrain_features[" + request.TentAnchorTileX + "," + request.TentAnchorTileY +
            "].runtime_type=StardewValley.TerrainFeatures.Tent;player.inventory[" + request.InventorySlotIndex + "].stack_decreases=1";
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.InventorySlotIndex.HasValue || !request.Direction.HasValue || !request.TentRectangleX.HasValue || !request.TentRectangleY.HasValue ||
            !request.TentRectangleWidth.HasValue || !request.TentRectangleHeight.HasValue || !request.TentAnchorTileX.HasValue || !request.TentAnchorTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "place_tent", requested, "typed_target=missing", "place_tent_typed_target_fields_required");
        }
        if (!string.Equals(request.NativeContract, nativeContract, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_tent", requested, "native_contract=" + request.NativeContract, "place_tent_native_contract_mismatch");
        }

        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "place_tent", requested,
                "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "place_tent_location_mismatch");
        }
        var slot = request.InventorySlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot]?.GetType() != typeof(StardewValley.Object) ||
            Game1.player.Items[slot] is not StardewValley.Object kit ||
            !string.Equals(kit.QualifiedItemId, "(O)TentKit", StringComparison.Ordinal) ||
            !string.Equals(request.QualifiedItemId, "(O)TentKit", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "place_tent", requested, "inventory_identity_mismatch", "place_tent_inventory_identity_drift");
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (!TentPlacementGeometryResolver.TryResolve(
                Game1.player.TilePoint.X,
                Game1.player.TilePoint.Y,
                target.X,
                target.Y,
                out var geometry) ||
            Game1.player.TilePoint != new Point(request.StandTileX.Value, request.StandTileY.Value) ||
            geometry.Direction != request.Direction.Value ||
            geometry.RectangleX != request.TentRectangleX.Value || geometry.RectangleY != request.TentRectangleY.Value ||
            geometry.RectangleWidth != request.TentRectangleWidth.Value || geometry.RectangleHeight != request.TentRectangleHeight.Value ||
            geometry.AnchorTileX != request.TentAnchorTileX.Value || geometry.AnchorTileY != request.TentAnchorTileY.Value)
        {
            return BlockedWithPrimitive(request, "place_tent", requested,
                "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
                "place_tent_directional_geometry_drifted");
        }

        var anchor = new Vector2(geometry.AnchorTileX, geometry.AnchorTileY);
        if (location.largeTerrainFeatures.Any(feature => feature.Tile == anchor) ||
            !location.IsOutdoors || !location.isAreaClear(new Rectangle(
                geometry.RectangleX,
                geometry.RectangleY,
                geometry.RectangleWidth,
                geometry.RectangleHeight)) ||
            !CanPlaceInventoryObjectNative(location, kit, slot, target))
        {
            return BlockedWithPrimitive(request, "place_tent", requested,
                "native_placement_recheck=false", "place_tent_native_placement_recheck_failed");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var beforeTentCount = location.largeTerrainFeatures.Count(feature => feature.GetType() == typeof(Tent));
        var attempt = PlaceInventoryObjectNative(location, kit, slot, target);
        var tent = location.largeTerrainFeatures
            .FirstOrDefault(feature => feature.GetType() == typeof(Tent) && feature.Tile == anchor) as Tent;
        var afterTentCount = location.largeTerrainFeatures.Count(feature => feature.GetType() == typeof(Tent));
        var bounds = tent?.getBoundingBox() ?? Rectangle.Empty;
        var consumed = attempt.StackAfter == attempt.StackBefore - 1;
        var verified = attempt.Placed && consumed && tent is not null && afterTentCount == beforeTentCount + 1 &&
            tent.health.Value == 5 && bounds.X == geometry.RectangleX * Game1.tileSize && bounds.Y == geometry.RectangleY * Game1.tileSize &&
            bounds.Width == geometry.RectangleWidth * Game1.tileSize && bounds.Height == geometry.RectangleHeight * Game1.tileSize &&
            tent.isPassable(Game1.player) && !tent.isPassable();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            TentDirection = geometry.Direction,
            TentStandTileX = geometry.StandTileX,
            TentStandTileY = geometry.StandTileY,
            TentRectangleX = geometry.RectangleX,
            TentRectangleY = geometry.RectangleY,
            TentRectangleWidth = geometry.RectangleWidth,
            TentRectangleHeight = geometry.RectangleHeight,
            TentAnchorTileX = geometry.AnchorTileX,
            TentAnchorTileY = geometry.AnchorTileY,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_tent",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_TentKit_branch_returned_true", "exact_base_Tent_created_at_directional_anchor", "native_3x2_bounds_and_initial_health_verified", "inventory_decremented_exactly_one", "separate_sleep_handoff_exposed" }
                : new[] { "native_or_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = "location_id=" + location.NameOrUniqueName +
                ";stand_tile=" + geometry.StandTileX + "," + geometry.StandTileY +
                ";target_probe_tile=" + target.X + "," + target.Y +
                ";direction=" + geometry.Direction +
                ";rectangle=" + geometry.RectangleX + "," + geometry.RectangleY + "," + geometry.RectangleWidth + "x" + geometry.RectangleHeight +
                ";anchor_tile=" + geometry.AnchorTileX + "," + geometry.AnchorTileY +
                ";placed_runtime_type=" + (tent?.GetType().FullName ?? "null") +
                ";health=" + (tent?.health.Value.ToString() ?? "null") +
                ";inventory_stack_before=" + attempt.StackBefore +
                ";inventory_stack_after=" + attempt.StackAfter,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "place_tent_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "current_location.large_terrain_features[" + geometry.AnchorTileX + "," + geometry.AnchorTileY + "]",
                        Before = "missing",
                        After = "StardewValley.TerrainFeatures.Tent:health=5"
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
