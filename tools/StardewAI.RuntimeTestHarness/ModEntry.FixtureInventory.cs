using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private static void ClearReadyMachineOutputsForFixture(GameLocation location, Vector2 preservedTile)
    {
        foreach (var pair in location.objects.Pairs.ToArray())
        {
            if (pair.Key == preservedTile || !pair.Value.bigCraftable.Value)
            {
                continue;
            }

            if (pair.Value.readyForHarvest.Value || pair.Value.heldObject.Value is not null)
            {
                pair.Value.readyForHarvest.Value = false;
                pair.Value.heldObject.Value = null;
                if (pair.Value.MinutesUntilReady < 0)
                {
                    pair.Value.MinutesUntilReady = 0;
                }
            }
        }
    }

    private static void RefreshTransparentMachineProbeCache()
    {
        try
        {
            var bridgeType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType("StardewAI.TransparentBridge.Adapters.FarmReadAdapter", throwOnError: false))
                .FirstOrDefault(type => type is not null);
            var method = bridgeType?.GetMethod("RefreshMachineProbeCache", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
        catch
        {
            // The executor must not fail because the read-side cache refresh failed.
        }
    }

    private static string MachineRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.machines[" + request.LocationId + ":" + request.TargetTileX + "," + request.TargetTileY + "].held_item=null;player.inventory.updated";
    }

    private static string MachineInputRequestedEffect(TrainingExecutionRequest request)
    {
        return "farm.machines[" + request.LocationId + ":" + request.TargetTileX + "," + request.TargetTileY + "].minutes_until_ready>0_or_ready=true;player.inventory[" + request.InputSlotIndex + "].stack_decreases";
    }

    private static string MachineObservedEffect(GameLocation location, Point target)
    {
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return "machine_present=false";
        }

        return "machine_present=true;qualified_item_id=" + machine.QualifiedItemId +
            ";ready_for_harvest=" + machine.readyForHarvest.Value.ToString().ToLowerInvariant() +
            ";minutes_until_ready=" + machine.MinutesUntilReady +
            ";held_item=" + (machine.heldObject.Value?.QualifiedItemId ?? "null");
    }

    private static int EnsureInventoryItem(string qualifiedItemId, int stack)
    {
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var existing = Game1.player.Items[index];
            if (existing is not null &&
                string.Equals(existing.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                if (existing.Stack < stack)
                {
                    existing.Stack = stack;
                }
                return index;
            }
        }

        var item = ItemRegistry.Create(qualifiedItemId, stack);
        if (!Game1.player.addItemToInventoryBool(item))
        {
            return -1;
        }

        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            var existing = Game1.player.Items[index];
            if (existing is not null &&
                string.Equals(existing.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string InventoryStackSignature()
    {
        return string.Join("|", Game1.player.Items
            .Select((item, index) => item is null ? index + ":null" : index + ":" + item.QualifiedItemId + ":" + item.Stack)
            .Where(value => !value.EndsWith(":null", StringComparison.Ordinal)));
    }

    private TrainingExecutionResult ExecuteSetupShippingTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (Game1.currentLocation is not Farm farm ||
            !string.Equals(Game1.currentLocation.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none"),
                "fixture_requires_farm");
        }

        var qualifiedItemId = !string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? request.QualifiedItemId
            : "(O)388";
        var quantity = Math.Max(1, request.Quantity ?? 5);

        var slotIndex = EnsureInventoryItem(qualifiedItemId, quantity);
        if (slotIndex < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "inventory_full_or_item_invalid");
        }

        ShippingBin? bin;
        if (request.TargetTileX.HasValue && request.TargetTileY.HasValue)
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b =>
                    b.daysOfConstructionLeft.Value <= 0 &&
                    request.TargetTileX.Value >= b.tileX.Value &&
                    request.TargetTileX.Value <= b.tileX.Value + b.tilesWide.Value - 1 &&
                    request.TargetTileY.Value == b.tileY.Value);
        }
        else
        {
            bin = farm.buildings
                .OfType<ShippingBin>()
                .FirstOrDefault(b => b.daysOfConstructionLeft.Value <= 0);
        }

        if (bin is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "no_completed_shipping_bin");
        }

        var binCenterX = (float)(bin.tileX.Value + bin.tilesWide.Value * 0.5);
        var binCenterY = (float)bin.tileY.Value;
        Point? standTile = null;
        for (var dx = -2; dx <= 2; dx++)
        {
            for (var dy = -2; dy <= 2; dy++)
            {
                var tx = bin.tileX.Value + dx;
                var ty = bin.tileY.Value + dy;
                if (tx >= bin.tileX.Value && tx < bin.tileX.Value + bin.tilesWide.Value &&
                    ty == bin.tileY.Value) continue;
                if (tx < 0 || ty < 0 || tx >= farm.map.Layers[0].LayerWidth ||
                    ty >= farm.map.Layers[0].LayerHeight) continue;
                var dist = Math.Sqrt((tx - binCenterX) * (tx - binCenterX) +
                                     (ty - binCenterY) * (ty - binCenterY));
                if (dist > 2.0) continue;
                var tileLoc = new xTile.Dimensions.Location(tx, ty);
                if (farm.isTilePassable(tileLoc, Game1.viewport) &&
                    !farm.isCollidingPosition(
                        new XnaRectangle(tx * 64 + 1, ty * 64 + 1, 62, 62),
                        Game1.viewport, isFarmer: true, damagesFarmer: 0, glider: false,
                        Game1.player, pathfinding: true))
                {
                    standTile = new Point(tx, ty);
                    break;
                }
            }
            if (standTile.HasValue) break;
        }

        if (!standTile.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_shipping_target",
                "shipping_target_fixture=completed",
                "qualified_item_id=" + qualifiedItemId,
                "no_passable_stand_tile_near_bin");
        }

        var item = Game1.player.Items[slotIndex];
        var unqualifiedId = item?.ItemId ?? string.Empty;

        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.player.Position = new Vector2(
            standTile.Value.X * 64 + 32 - Game1.player.GetBoundingBox().Width / 2,
            standTile.Value.Y * 64 + 32 - Game1.player.GetBoundingBox().Height / 2 + 16);

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_shipping_target",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "fixture_item_ensured", "slot_index=" + slotIndex,
                "qualified_item_id=" + qualifiedItemId,
                "unqualified_item_id=" + unqualifiedId,
                "bin_tile=" + bin.tileX.Value + "," + bin.tileY.Value,
                "stand_tile=" + standTile.Value.X + "," + standTile.Value.Y
            },
            RequestedEffect = "player.inventory[" + slotIndex + "].stack>=" + quantity,
            ObservedEffect = "fixture_item_ensured;slot_index=" + slotIndex +
                ";qualified_item_id=" + qualifiedItemId,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.inventory.slot_index",
                    Before = "",
                    After = slotIndex.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "shipping_bin.tile",
                    Before = "",
                    After = bin.tileX.Value + "," + bin.tileY.Value
                },
                new SimulatedFactChange
                {
                    Path = "shipping_bin.stand_tile",
                    Before = "",
                    After = standTile.Value.X + "," + standTile.Value.Y
                }
            }
        };
    }
}
