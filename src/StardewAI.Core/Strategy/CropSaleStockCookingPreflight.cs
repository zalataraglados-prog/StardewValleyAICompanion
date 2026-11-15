using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

// Bind the original cooking source aliases, then reuse the original material-consumption guard.
public static class CropSaleStockCookingPreflight
{
    private const string InvalidSource = "crop_sale_stock_cooking_source_unavailable";

    internal static JsonElement? ReadRecipe(SnapshotEnvelope snapshot, string recipeName, string sourceId)
    {
        var context = ReadStateFieldValue(snapshot, "player", "cooking");
        if (context is not { ValueKind: JsonValueKind.Object } value ||
            ReadString(value, "projection_status") != "complete_learned_cooking_recipe_and_native_source_projection" ||
            !value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array) return null;
        var matches = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "recipe_name") == recipeName && ReadString(row, "cooking_source_id") == sourceId).ToArray();
        return matches.Length == 1 ? matches[0].Clone() : null;
    }

    internal static bool AppliesTo(StrategyCommitmentLedger? ledger) => ledger is not null &&
        ledger.MaterialReservations.Any(row => row.Status == StrategyCommitmentStatuses.Active &&
            CropSaleStockReservationContract.TryRead(row, out _));

    public static string[] Evaluate(SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, JsonElement? recipe)
    {
        if (!AppliesTo(ledger)) return Array.Empty<string>();
        var reasons = Resolve(snapshot, ledger!, recipe, out _, out var mapped);
        if (reasons.Length > 0 || mapped.Length == 0) return reasons;
        var normalized = JsonSerializer.SerializeToElement(new[] { new { native_consumption_plan = mapped } });
        var result = new MachineCraftingMaterialReservationGuard().Evaluate(snapshot, normalized, usesWorkbench: true, ledger: ledger);
        return result.Ready ? Array.Empty<string>() : new[] { "crop_sale_stock_cooking_material_reserved" }
            .Concat(result.BlockingReasons).Distinct(StringComparer.Ordinal).ToArray();
    }

    internal static string[] Resolve(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, JsonElement? recipe,
        out MaterialInventoryGraph? graph, out InventorySlotConsumption[] consumptions)
    {
        graph = null; consumptions = Array.Empty<InventorySlotConsumption>();
        if (snapshot.SaveId.Status != FieldStatus.Available || snapshot.PlayerId.Status != FieldStatus.Available ||
            snapshot.SaveId.Value != ledger!.SaveId || snapshot.PlayerId.Value != ledger.PlayerId || ledger.SourceStateHash != snapshot.StateHash ||
            !MaterialInventoryGraphReader.TryRead(snapshot, out graph) || graph!.PlayerId.ToString(CultureInfo.InvariantCulture) != ledger.PlayerId ||
            recipe is not { ValueKind: JsonValueKind.Object } row) return new[] { InvalidSource };
        return ResolvePlan(graph, row, out consumptions);
    }

    internal static string[] ResolvePlan(MaterialInventoryGraph graph, JsonElement row, out InventorySlotConsumption[] consumptions)
    {
        consumptions = Array.Empty<InventorySlotConsumption>();
        if (graph.InventoryNodes is null || graph.InventoryNodes.Any(node => node is null) || graph.AccessPoints is null ||
            graph.AccessPoints.Any(point => point is null) || row.ValueKind != JsonValueKind.Object ||
            !row.TryGetProperty("material_container_ids", out var containers) || containers.ValueKind != JsonValueKind.Array ||
            containers.EnumerateArray().Any(value => value.ValueKind != JsonValueKind.String)) return new[] { InvalidSource };
        var aliases = containers.EnumerateArray().Select(value => value.GetString()!).ToArray();
        if (aliases.Any(string.IsNullOrWhiteSpace) || aliases.Distinct(StringComparer.Ordinal).Count() != aliases.Length)
            return new[] { InvalidSource };
        var location = ReadString(row, "location_id");
        var mapped = new List<InventorySlotConsumption>();
        foreach (var field in new[] { "ingredient_rows", "seasoning_rows" })
        {
            if (!row.TryGetProperty(field, out var ingredients) || ingredients.ValueKind != JsonValueKind.Array)
                return new[] { InvalidSource };
            foreach (var ingredient in ingredients.EnumerateArray())
            {
                if (ingredient.ValueKind != JsonValueKind.Object || ReadBool(ingredient, "satisfied") != true ||
                    !ingredient.TryGetProperty("required_count", out var required) || required.ValueKind != JsonValueKind.Number ||
                    !required.TryGetInt32(out var count) || count <= 0 ||
                    !ingredient.TryGetProperty("native_consumption_plan", out var plan) || plan.ValueKind != JsonValueKind.Array)
                    return new[] { InvalidSource };
                long total = 0;
                foreach (var entry in plan.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object || !entry.TryGetProperty("slot_index", out var slot) ||
                        slot.ValueKind != JsonValueKind.Number || !slot.TryGetInt32(out var index) || index < 0 ||
                        !entry.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number ||
                        !amount.TryGetInt32(out var quantity) || quantity <= 0) return new[] { InvalidSource };
                    var node = ResolveNode(graph, aliases, location, ReadString(entry, "source_id"));
                    var item = ReadString(entry, "qualified_item_id");
                    if (node is null || string.IsNullOrWhiteSpace(item)) return new[] { InvalidSource };
                    mapped.Add(new(node, index, item, quantity));
                    total += quantity;
                }
                if (total != count) return new[] { InvalidSource };
            }
        }
        if (mapped.Count == 0) return Array.Empty<string>();
        if (mapped.GroupBy(value => (value.NodeId, value.SlotIndex, value.QualifiedItemId)).Any(group => group.Sum(value => (long)value.Quantity) > int.MaxValue))
            return new[] { InvalidSource };
        consumptions = mapped.GroupBy(value => (value.NodeId, value.SlotIndex, value.QualifiedItemId))
            .Select(group => new InventorySlotConsumption(group.Key.NodeId, group.Key.SlotIndex, group.Key.QualifiedItemId, group.Sum(value => value.Quantity)))
            .OrderBy(value => value.NodeId, StringComparer.Ordinal).ThenBy(value => value.SlotIndex).ToArray();
        return Array.Empty<string>();
    }

    private static string? ResolveNode(MaterialInventoryGraph graph, string[] containers, string location, string source)
    {
        var player = "player:" + graph.PlayerId.ToString(CultureInfo.InvariantCulture);
        if (source == player)
        {
            var nodes = graph.InventoryNodes.Where(node => node.NodeId == player && node.InventoryKind == "player_inventory" &&
                node.OwnerPlayerId == graph.PlayerId && node.ActorUseAuthorized && node.SupplyState == "available").ToArray();
            return nodes.Length == 1 ? nodes[0].NodeId : null;
        }
        if (string.IsNullOrWhiteSpace(location) || !containers.Contains(source, StringComparer.Ordinal)) return null;
        var access = graph.AccessPoints.Where(point => point.LocationId == location && point.ActorUseAuthorized && !point.LockedByOtherPlayer &&
            (point.AccessKind == "built_in_fridge" && source == "kitchen-fridge:" + location ||
             point.AccessKind == "placed_chest" && point.IsFridge && point.TileX.HasValue && point.TileY.HasValue &&
             source == "mini-fridge:" + location + ":" + point.TileX.Value.ToString(CultureInfo.InvariantCulture) + "," +
                 point.TileY.Value.ToString(CultureInfo.InvariantCulture))).ToArray();
        if (access.Length != 1) return null;
        var fridge = graph.InventoryNodes.Where(node => node.NodeId == access[0].NodeId && node.InventoryKind == "fridge" &&
            node.ActorUseAuthorized && node.SupplyState == "available").ToArray();
        return fridge.Length == 1 ? fridge[0].NodeId : null;
    }

}
