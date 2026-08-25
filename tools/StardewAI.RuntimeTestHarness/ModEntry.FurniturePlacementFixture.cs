using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFurniturePlacementTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId) || !request.QualifiedItemId.StartsWith("(F)", StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(request, "debug_setup_furniture_placement_target",
                "player.furniture_placement.ready=true", "qualified_item_id=missing", "fixture_furniture_qid_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var home = Utility.getHomeOfFarmer(Game1.player) as FarmHouse;
        if (home is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_furniture_placement_target",
                "player.furniture_placement.ready=true", "home=unavailable", "fixture_farmhouse_required");
        }
        Game1.currentLocation = home;
        Game1.player.currentLocation = home;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.activeClickableMenu = null;

        var slot = EnsureInventoryItem(request.QualifiedItemId, 1);
        if (slot < 0 || Game1.player.Items[slot] is not Furniture inventory || !IsSupportedVanillaFurnitureType(inventory.GetType()))
        {
            return BlockedWithPrimitive(request, "debug_setup_furniture_placement_target",
                "player.furniture_placement.ready=true", "inventory_furniture=unavailable", "fixture_vanilla_furniture_required");
        }
        for (var attempts = 0; inventory.currentRotation.Value < 0 && attempts < 4; attempts++)
        {
            inventory.rotate();
        }
        if (inventory.currentRotation.Value < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_furniture_placement_target",
                "player.furniture_placement.ready=true", "current_rotation=" + inventory.currentRotation.Value,
                "fixture_native_rotation_normalization_failed");
        }
        var rotationSteps = Math.Clamp(request.FurnitureRotationSteps ?? 0, 0, 3);

        var endpoint = string.IsNullOrWhiteSpace(request.FurniturePlacementEndpoint)
            ? "location_furniture"
            : request.FurniturePlacementEndpoint;
        Furniture? table = null;
        if (endpoint == "table_held_object")
        {
            table = EnsureFixtureFurnitureTable(home);
            if (table is null)
            {
                return BlockedWithPrimitive(request, "debug_setup_furniture_placement_target",
                    "player.furniture_placement.ready=true", "table=unavailable", "fixture_empty_table_required");
            }
        }

        var layers = home.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var candidates = Enumerable.Range(0, height)
            .SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y)));
        if (table is not null)
        {
            var tableTile = new Point((int)table.TileLocation.X, (int)table.TileLocation.Y);
            candidates = candidates.OrderBy(tile => ManhattanDistance(tile, tableTile));
        }
        else
        {
            candidates = candidates.OrderBy(tile => ManhattanDistance(tile, Game1.player.TilePoint));
        }

        var target = Point.Zero;
        var found = false;
        foreach (var candidate in candidates)
        {
            var candidateProbe = Furniture.GetFurnitureInstance(inventory.ItemId);
            for (var attempts = 0; candidateProbe.currentRotation.Value != inventory.currentRotation.Value && attempts < 4; attempts++)
            {
                candidateProbe.rotate();
            }
            for (var i = 0; i < rotationSteps; i++)
            {
                candidateProbe.rotate();
            }
            candidateProbe.InitializeAtTile(new Vector2(candidate.X, candidate.Y));
            if (!Utility.playerCanPlaceItemHere(home, candidateProbe, candidate.X * Game1.tileSize, candidate.Y * Game1.tileSize, Game1.player))
            {
                continue;
            }
            var resolvedEndpoint = FindFurnitureEndpoint(home, candidateProbe);
            if (!string.Equals(resolvedEndpoint.Kind, endpoint, StringComparison.Ordinal))
            {
                continue;
            }
            target = candidate;
            found = true;
            break;
        }

        var verified = found && home.CanFreePlaceFurniture();
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
            PrimitiveKind = "debug_setup_furniture_placement_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "vanilla_inventory_furniture_ready", "farmhouse_native_free_placement_ready", "endpoint=" + endpoint, "inventory_slot_index=" + slot, "rotation_steps=" + rotationSteps }
                : new[] { "fixture_no_native_legal_furniture_target" },
            RequestedEffect = "player.furniture_placement.ready=true",
            ObservedEffect = "location_id=" + home.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";inventory_slot_index=" + slot + ";qualified_item_id=" + inventory.QualifiedItemId +
                ";rotation_steps=" + rotationSteps + ";endpoint=" + endpoint,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "furniture_placement_fixture_not_ready" }
        };
    }

    private static Furniture? EnsureFixtureFurnitureTable(GameLocation location)
    {
        var existing = location.furniture.FirstOrDefault(item => item.furniture_type.Value == 11 && item.heldObject.Value is null);
        if (existing is not null)
        {
            return existing;
        }
        var data = Game1.content.Load<Dictionary<string, string>>("Data\\Furniture");
        var tableId = data.Keys.FirstOrDefault(id =>
        {
            try { return Furniture.GetFurnitureInstance(id).furniture_type.Value == 11; }
            catch { return false; }
        });
        if (tableId is null)
        {
            return null;
        }
        var table = Furniture.GetFurnitureInstance(tableId);
        var layers = location.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!table.canBePlacedHere(location, new Vector2(x, y), ~(CollisionMask.Characters | CollisionMask.Farmers)))
                {
                    continue;
                }
                table.Location = location;
                table.TileLocation = new Vector2(x, y);
                location.furniture.Add(table);
                return table;
            }
        }
        return null;
    }
}
