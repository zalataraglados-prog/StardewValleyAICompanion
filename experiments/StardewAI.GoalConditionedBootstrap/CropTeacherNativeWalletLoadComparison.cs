using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

// A comparison view only: native SaveGame.getLoadEnumerator assigns each farmer's Money
// to individualMoney on load. Raw observations and the persistent-resource projector stay unchanged.
internal static class CropTeacherNativeWalletLoadComparison
{
    internal static JsonElement ProjectEnvelope(JsonElement beforeEnvelope, JsonElement afterEnvelope)
    {
        var beforeValue = Value(beforeEnvelope);
        var afterValue = Value(afterEnvelope);
        // Old/absent unchanged values need no new schema; the original projector still owns admission.
        if (JsonNode.DeepEquals(beforeValue, afterValue)) return afterEnvelope.Clone();

        Require(Readable(beforeEnvelope) && Readable(afterEnvelope), "Native wallet load requires readable full envelopes.");
        var original = Object(beforeValue);
        var observed = Object(afterValue);
        Require(Text(original["schema_version"]) == "multiplayer_wallet.v1" &&
                Text(observed["schema_version"]) == "multiplayer_wallet.v1" &&
                Text(original["projection_status"]) == "complete_locked_base_1.6.15" &&
                Text(observed["projection_status"]) == "complete_locked_base_1.6.15",
            "Native wallet load requires the complete pinned wallet projection.");
        var expected = (JsonObject)original.DeepClone();
        var mode = Text(original["wallet_mode"]);
        Require(mode is "shared" or "separate" && original["use_separate_wallets"] is JsonValue separate &&
                separate.TryGetValue<bool>(out var isSeparate) && isSeparate == (mode == "separate"),
            "Native wallet load wallet mode is malformed.");
        var localId = Text(original["local_player_id"]);
        Require(!string.IsNullOrWhiteSpace(localId), "Native wallet load lacks local identity.");
        Require(expected["participants"] is JsonArray { Count: > 0 }, "Native wallet load lacks participants.");
        var participants = (JsonArray)expected["participants"]!;
        var identities = new Dictionary<string, bool>(StringComparer.Ordinal);
        var initialized = false;
        foreach (var node in participants)
        {
            var participant = Object(node);
            var id = Text(participant["player_id"]);
            Require(!string.IsNullOrWhiteSpace(id) && !identities.ContainsKey(id), "Native wallet load participant identity is missing or repeated.");
            Require(participant["effective_balance"] is JsonValue balance && balance.TryGetValue<int>(out _),
                "Native wallet load participant effective balance is not an integer.");
            Require(participant["individual_entry_exists"] is JsonValue entry && entry.TryGetValue<bool>(out _),
                "Native wallet load participant entry flag is not boolean.");
            var exists = participant["individual_entry_exists"]!.GetValue<bool>();
            Require(participant["is_local_player"] is JsonValue local && local.TryGetValue<bool>(out _) &&
                    participant["is_local_player"]!.GetValue<bool>() == (id == localId),
                "Native wallet load participant local identity differs.");
            Require(participant.ContainsKey("individual_balance") && participant.ContainsKey("native_default_if_missing") &&
                    (exists ? Integer(participant["individual_balance"]) : participant["individual_balance"] is null) &&
                    (participant["native_default_if_missing"] is null || Integer(participant["native_default_if_missing"])),
                "Native wallet load participant initialization fields are malformed.");
            identities.Add(id!, exists);
            var prior = participant.DeepClone();
            participant["individual_entry_exists"] = true;
            participant["individual_balance"] = participant["effective_balance"]!.DeepClone();
            participant["native_default_if_missing"] = null;
            initialized |= !JsonNode.DeepEquals(prior, participant);
        }
        Require(identities.ContainsKey(localId!), "Native wallet load local participant is missing.");
        Require(expected["recipients"] is JsonArray, "Native wallet load lacks recipients.");
        var recipientIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in (JsonArray)expected["recipients"]!)
        {
            var recipient = Object(node);
            var id = Text(recipient["player_id"]);
            Require(!string.IsNullOrWhiteSpace(id) && id != localId && identities.ContainsKey(id) && recipientIds.Add(id),
                "Native wallet load recipient identity is unknown, local or repeated.");
            Require(recipient["individual_entry_exists"] is JsonValue entry && entry.TryGetValue<bool>(out _) &&
                    recipient["individual_entry_exists"]!.GetValue<bool>() == identities[id!] && Integer(recipient["balance"]),
                "Native wallet load recipient entry flag differs from its participant.");
            recipient["individual_entry_exists"] = true;
        }
        // This checksum is derived bridge metadata, not currency. Substitute it only after
        // establishing a real expected initialization, then compare EVERY other property.
        Require(initialized && Fingerprint(original["projection_fingerprint"]) && Fingerprint(observed["projection_fingerprint"]),
            "Native wallet load fingerprint changed without valid initialization.");
        expected["projection_fingerprint"] = observed["projection_fingerprint"]!.DeepClone();
        Require(JsonNode.DeepEquals(expected, observed), "Native wallet load differs beyond expected individual initialization.");
        var result = Object(JsonNode.Parse(afterEnvelope.GetRawText()));
        result["value"] = original.DeepClone();
        return JsonSerializer.SerializeToElement(result);
    }

    private static JsonNode? Value(JsonElement envelope) => envelope.ValueKind == JsonValueKind.Object &&
        envelope.TryGetProperty("value", out var value) ? JsonNode.Parse(value.GetRawText()) : null;

    private static bool Readable(JsonElement envelope) => envelope.ValueKind == JsonValueKind.Object &&
        envelope.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
        FieldEnvelopeValidator.IsReadableStatus(status.GetString()) &&
        envelope.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Object;

    private static JsonObject Object(JsonNode? node)
    {
        Require(node is JsonObject, "Native wallet load expected an object.");
        return (JsonObject)node!;
    }

    private static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    private static bool Integer(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out _);
    private static bool Fingerprint(JsonNode? node) => Text(node) is { Length: 64 } text && text.All(Uri.IsHexDigit);
}
