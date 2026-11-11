using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private sealed record PhysicalStockDelta(int SlotIndex, string QualifiedItemId, int Quality, int StackBefore, int StackAfter);

    private static (MaterialInventoryGraph First, MaterialInventoryGraph Next, MaterialInventoryNode PriorPlayer, MaterialInventoryNode Player)
        ReadStockGraphs(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var hasBefore = MaterialTransferIntentBinder.TryReadGraph(before, out var first);
        var hasAfter = MaterialTransferIntentBinder.TryReadGraph(after, out var next);
        Require(hasBefore && hasAfter, "Pickup native material graphs are unavailable.");
        Require(long.TryParse(before.PlayerId.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actor) &&
                first!.PlayerId == actor && next!.PlayerId == actor, "Pickup graph actor differs from the original snapshot.");
        MaterialInventoryNode Player(MaterialInventoryGraph graph)
        {
            Require(graph.InventoryNodes.All(node => node is not null) && graph.InventoryNodes.Select(node => node.NodeId)
                    .Distinct(StringComparer.Ordinal).Count() == graph.InventoryNodes.Length,
                "Pickup graph node identities are invalid.");
            var players = graph.InventoryNodes.Where(node => node.InventoryKind == "player_inventory").ToArray();
            Require(players is [var player] && player.OwnerPlayerId == actor && player.ActorUseAuthorized &&
                    player.OwnershipClass == "actor_owned" && player.SupplyState == "available" &&
                    player.GlobalInventoryId.Length == 0, "Pickup player inventory authority is unavailable.");
            return players[0];
        }
        var priorPlayer = Player(first!); var player = Player(next!);
        Require(priorPlayer.NodeId == player.NodeId, "Pickup changed its player inventory identity.");
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(before, priorPlayer);
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(after, player);
        return (first!, next!, priorPlayer, player);
    }

    private static void ProjectStockQuantities(JsonObject graph, MaterialInventoryGraph first,
        MaterialInventoryGraph next, IEnumerable<PhysicalStockDelta> changes)
    {
        var quantities = Array(graph["quantity_rows"], "pickup quantity rows");
        void VerifyAll(MaterialInventoryGraph source, JsonArray rows)
        {
            var keys = source.InventoryNodes.SelectMany(node => node.Slots).Where(slot => slot.Stack > 0)
                .Select(slot => (slot.QualifiedItemId, slot.Quality)).Distinct().ToHashSet();
            Require(rows.All(row => row is JsonObject) && rows.Count == keys.Count,
                "Pickup material aggregates omit or duplicate an inventory identity.");
            foreach (var row in rows.OfType<JsonObject>())
            {
                var id = row["qualified_item_id"]!.GetValue<string>(); var quality = Int(row, "quality");
                Require(keys.Remove((id, quality)), "Pickup material aggregate identity is invalid.");
                VerifySourceQuantity(row, source, id, quality);
            }
        }
        VerifyAll(first, quantities);
        foreach (var group in changes.GroupBy(slot => (slot.QualifiedItemId, slot.Quality)))
        {
            var row = quantities.OfType<JsonObject>().SingleOrDefault(row => row["qualified_item_id"]?.GetValue<string>() ==
                group.Key.QualifiedItemId && Int(row, "quality") == group.Key.Quality);
            if (row is null)
            {
                row = JsonSerializer.SerializeToNode(new MaterialQuantityRow { QualifiedItemId = group.Key.QualifiedItemId,
                    Quality = group.Key.Quality })!.AsObject();
                quantities.Add(row);
            }
            row["available_quantity"] = checked(Int(row, "available_quantity") + group.Sum(slot => slot.StackAfter - slot.StackBefore));
            row["source_slot_count"] = checked(Int(row, "source_slot_count") + group.Count(slot => slot.StackBefore == 0));
        }
        graph["quantity_rows"] = new JsonArray(quantities.OfType<JsonObject>()
            .OrderBy(row => row["qualified_item_id"]!.GetValue<string>(), StringComparer.Ordinal)
            .ThenBy(row => Int(row, "quality")).Select(row => row.DeepClone()).ToArray());
        VerifyAll(next, graph["quantity_rows"]!.AsArray());
    }
}
