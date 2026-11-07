using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void Verify(SnapshotEnvelope before, SnapshotEnvelope after, PreparedMaterialTransfer prepared, string resolvedSeedId)
    {
        var hasBefore = MaterialTransferIntentBinder.TryReadGraph(before, out var first);
        var hasAfter = MaterialTransferIntentBinder.TryReadGraph(after, out var next);
        Require(hasBefore && hasAfter, "Owned seed transfer native graphs are unavailable.");
        var physical = new MaterialTransferProjector().Project(prepared.BeforeInventoryGraph, prepared.Intent);
        var reasons = MaterialTransferProjector.VerifyNativeBeforeSlots(prepared.BeforeInventoryGraph, first!, physical)
            .Concat(MaterialTransferProjector.VerifyNativeSlots(prepared.BeforeInventoryGraph, next!, physical)).ToArray();
        Require(reasons.Length == 0, "Owned seed transfer native slots differ: " + string.Join(",", reasons));
        var (graph, seed, sourceAccesses) = ProjectGraph(before, after, first!, prepared.Intent, physical);
        var player = next!.InventoryNodes.Single(row => row.NodeId == prepared.Intent.DestinationNodeId);
        var priorPlayer = first!.InventoryNodes.Single(row => row.NodeId == player.NodeId);
        CropTeacherOwnedSeedSupply.VerifyPlayerSlots(after, player);
        VerifyInventory(before, after, player, seed, physical);
        VerifyCapacity(before, after, priorPlayer, player);
        VerifySeedInventory(before, after, priorPlayer, player, prepared.Intent.QualifiedItemId, resolvedSeedId);
        foreach (var name in new[] { "money", "shop_currency_balances", "multiplayer_wallet" })
            SameField(before, after, "player", name, required: name != "multiplayer_wallet");
        VerifyStorageEcho(before, after, graph, sourceAccesses);
    }

    private static JsonObject? Field(SnapshotEnvelope snapshot, string domain, string name, bool required = true)
    {
        if (!snapshot.State.TryGetValue(domain, out var fields) || fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty(name, out var value))
        {
            Require(!required, "Owned seed transfer field is missing: " + domain + "." + name);
            return null;
        }
        return JsonNode.Parse(value.GetRawText()) as JsonObject ?? throw new InvalidDataException(
            "Owned seed transfer field envelope is invalid: " + domain + "." + name);
    }

    private static JsonNode Value(JsonObject field)
    {
        Require(Readable(field),
            "Owned seed transfer field is not readable.");
        return field["value"]!;
    }

    private static bool Readable(JsonObject? field) => field?["status"] is JsonValue status &&
        status.TryGetValue<string>(out var text) && FieldEnvelopeValidator.IsReadableStatus(text) && field["value"] is not null;

    private static void EqualField(JsonObject? expected, JsonObject? actual, string label)
    {
        RuntimeSnapshotBoundaryNormalization.RemoveNonSemanticMetadata(expected);
        RuntimeSnapshotBoundaryNormalization.RemoveNonSemanticMetadata(actual);
        Require(JsonNode.DeepEquals(expected, actual), "Owned seed transfer changed unexpected " + label + ".");
    }

    private static void SameField(SnapshotEnvelope before, SnapshotEnvelope after, string domain, string name, bool required)
        => EqualField(Field(before, domain, name, required), Field(after, domain, name, required), domain + "." + name);

    private static JsonArray Array(JsonNode? node, string label) => node as JsonArray ?? throw new InvalidDataException(
        "Owned seed transfer array is invalid: " + label);

    private static int Int(JsonNode node, string name) => node[name] is JsonValue value && value.TryGetValue<int>(out var number)
        ? number : throw new InvalidDataException("Owned seed transfer integer is missing: " + name);

    private static JsonObject Unique(JsonArray rows, string field, string value) => rows.OfType<JsonObject>()
        .Where(row => row[field]?.GetValue<string>() == value).ToArray() is [var only]
            ? only : throw new InvalidDataException("Owned seed transfer identity is not unique: " + field + "=" + value);

    private static JsonObject Slot(JsonArray rows, int index) => rows.OfType<JsonObject>()
        .Where(row => Int(row, "slot_index") == index).ToArray() is [var only]
            ? only : throw new InvalidDataException("Owned seed transfer slot is not unique: " + index);
}
