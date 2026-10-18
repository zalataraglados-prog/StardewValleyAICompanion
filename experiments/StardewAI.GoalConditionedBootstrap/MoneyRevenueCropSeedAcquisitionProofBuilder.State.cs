using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static void ValidateSnapshot(
        SnapshotEnvelope snapshot,
        string boundary)
    {
        Require(snapshot.SchemaVersion == "snapshot.v1" &&
                snapshot.GameVersion == "1.6.15" &&
                snapshot.SaveId.Status == FieldStatus.Available &&
                !string.IsNullOrWhiteSpace(snapshot.SaveId.Value) &&
                snapshot.PlayerId.Status == FieldStatus.Available &&
                !string.IsNullOrWhiteSpace(snapshot.PlayerId.Value) &&
                !string.IsNullOrWhiteSpace(snapshot.StateHash) &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(
                    snapshot.State),
            "Money Revenue crop seed acquisition " + boundary +
            " snapshot is invalid.");
    }

    private static bool SameSnapshotIdentity(
        SnapshotEnvelope expected,
        SnapshotEnvelope actual) =>
        expected.SaveId.Value == actual.SaveId.Value &&
        expected.PlayerId.Value == actual.PlayerId.Value &&
        expected.GameVersion == actual.GameVersion;

    private static int ReadSeedQuantity(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        Require(TryStateValue(
                    snapshot,
                    "player",
                    "inventory",
                    out var inventory) &&
                inventory.ValueKind == JsonValueKind.Array,
            "Money Revenue crop seed player inventory is unavailable.");
        var playerQuantity = SumInventoryRows(
            inventory,
            qualifiedItemId);

        if (!TryStateValue(
                snapshot,
                "farm",
                "material_inventory_graph",
                out var graph) ||
            graph.ValueKind != JsonValueKind.Object ||
            !graph.TryGetProperty("inventory_nodes", out var nodes) ||
            nodes.ValueKind != JsonValueKind.Array ||
            !long.TryParse(snapshot.PlayerId.Value, out var playerId))
        {
            throw new InvalidDataException(
                "Money Revenue crop seed material inventory graph is unavailable.");
        }
        var playerNodes = nodes.EnumerateArray().Where(node =>
                ReadString(node, "inventory_kind") == "player_inventory" &&
                ReadIntProperty(node, "owner_player_id") == playerId &&
                ReadString(node, "supply_state") == "available" &&
                ReadBool(node, "actor_use_authorized"))
            .ToArray();
        if (playerNodes.Length != 1 ||
            !playerNodes[0].TryGetProperty("slots", out var slots) ||
            slots.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                "Money Revenue crop seed material player inventory is ambiguous.");
        }
        var graphQuantity = SumInventoryRows(slots, qualifiedItemId);
        Require(playerQuantity == graphQuantity,
            "Money Revenue crop seed inventory projections disagree.");
        return playerQuantity;
    }

    private static int SumInventoryRows(
        JsonElement rows,
        string qualifiedItemId)
    {
        var quantity = 0;
        foreach (var row in rows.EnumerateArray().Where(row =>
                     ReadString(row, "qualified_item_id") ==
                        qualifiedItemId))
        {
            var stack = ReadIntProperty(row, "stack");
            Require(stack > 0,
                "Money Revenue crop seed inventory row is invalid.");
            quantity = checked(quantity + (int)stack);
        }
        return quantity;
    }

    private static int ReadInt(
        SnapshotEnvelope snapshot,
        string section,
        string field)
    {
        if (TryStateValue(snapshot, section, field, out var value) &&
            value.TryGetInt32(out var result))
        {
            return result;
        }
        throw new InvalidDataException(
            "Money Revenue crop seed acquisition field is unavailable: " +
            section + "." + field);
    }

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var parent) &&
            parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static long ReadIntProperty(
        JsonElement row,
        string name) =>
        row.TryGetProperty(name, out var value) &&
        value.TryGetInt64(out var result)
            ? result
            : long.MinValue;

    private static string ReadString(
        JsonElement row,
        string name) =>
        row.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool ReadBool(
        JsonElement row,
        string name) =>
        row.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();
}
