using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSavedResourceReadback
{
    private static SnapshotEnvelope NativeLoadComparisonView(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        if (TryEnvelope(before, "farm", "hay_storage", out var beforeHay) && TryEnvelope(after, "farm", "hay_storage", out var afterHay))
            ReplaceEnvelope(state, "farm", "hay_storage", ProjectNativeCellarEnvelope(beforeHay, afterHay));
        if (TryEnvelope(before, "player", "multiplayer_wallet", out var beforeWallet) && TryEnvelope(after, "player", "multiplayer_wallet", out var afterWallet))
            ReplaceEnvelope(state, "player", "multiplayer_wallet", CropTeacherNativeWalletLoadComparison.ProjectEnvelope(beforeWallet, afterWallet));
        // Only the already-validated original snapshots are evidence. This view is never ingested, hashed as a snapshot, or persisted.
        return new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
    }

    private static bool TryEnvelope(SnapshotEnvelope snapshot, string domain, string field, out JsonElement envelope)
    {
        envelope = default;
        return snapshot.State.TryGetValue(domain, out var section) && section.ValueKind == JsonValueKind.Object && section.TryGetProperty(field, out envelope);
    }

    private static void ReplaceEnvelope(Dictionary<string, JsonElement> state, string domain, string field, JsonElement envelope)
    {
        var values = state[domain].EnumerateObject().ToDictionary(row => row.Name, row => row.Value, StringComparer.Ordinal);
        values[field] = envelope;
        state[domain] = JsonSerializer.SerializeToElement(values);
    }

    private static JsonElement ProjectNativeCellarEnvelope(JsonElement before, JsonElement after)
    {
        if (before.ValueKind != JsonValueKind.Object || after.ValueKind != JsonValueKind.Object ||
            !before.TryGetProperty("value", out var source) || !after.TryGetProperty("value", out var loaded) || EqualJson(source, loaded)) return after;
        Require(ReadableHay(before, source) && ReadableHay(after, loaded), "native-load hay storage must retain its complete original readable scope.");
        var originalIds = ReadHayLocationIds(source);
        _ = ReadHayLocationIds(loaded);
        var envelope = JsonNode.Parse(after.GetRawText())!.AsObject();
        var locations = envelope["value"]!["locations"]!.AsArray();
        // Game1.AddLocations creates empty Cellar2..HighestPlayerLimit on native load. Existing rows are never removed or rewritten.
        for (var index = locations.Count - 1; index >= 0; index--)
        {
            var row = locations[index]!.AsObject();
            var id = row["location_id"]!.GetValue<string>();
            if (originalIds.Contains(id)) continue;
            Require(EmptyGeneratedCellar(row, id), "native-load hay storage added a location which is not an empty native generated cellar.");
            locations.RemoveAt(index);
        }
        return JsonSerializer.SerializeToElement(envelope);
    }

    private static bool ReadableHay(JsonElement envelope, JsonElement value) =>
        envelope.ValueKind == JsonValueKind.Object && envelope.TryGetProperty("status", out var status) &&
        status.ValueKind == JsonValueKind.String && FieldEnvelopeValidator.IsReadableStatus(status.GetString()) &&
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty("schema_version", out var schema) && schema.ValueKind == JsonValueKind.String && schema.GetString() == "hay_storage_inventory.v1" &&
        value.TryGetProperty("status", out var nativeStatus) && nativeStatus.ValueKind == JsonValueKind.String && nativeStatus.GetString() == "available" &&
        value.TryGetProperty("location_scope", out var scope) && scope.ValueKind == JsonValueKind.String && scope.GetString() == "noninterior_locations_and_farm" &&
        value.TryGetProperty("locations", out var locations) && locations.ValueKind == JsonValueKind.Array &&
        value.TryGetProperty("diagnostics", out var diagnostics) && diagnostics.ValueKind == JsonValueKind.Array && diagnostics.GetArrayLength() == 0;

    private static HashSet<string> ReadHayLocationIds(JsonElement storage)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in storage.GetProperty("locations").EnumerateArray())
            Require(row.ValueKind == JsonValueKind.Object && row.TryGetProperty("location_id", out var identity) && identity.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(identity.GetString()) && ids.Add(identity.GetString()!), "native-load hay storage has incomplete or duplicate location identities.");
        return ids;
    }

    private static bool EmptyGeneratedCellar(JsonObject row, string id)
    {
        string[] fields = ["location_id", "name", "runtime_type", "building_count", "stored_hay", "hay_capacity", "capacity_status"];
        return row.Count == fields.Length && fields.All(row.ContainsKey) && id.StartsWith("Cellar", StringComparison.Ordinal) &&
            int.TryParse(id.AsSpan("Cellar".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 2 &&
            id == "Cellar" + number.ToString(CultureInfo.InvariantCulture) &&
            row["name"] is JsonValue name && name.TryGetValue<string>(out var actualName) && actualName == id &&
            row["runtime_type"] is JsonValue type && type.TryGetValue<string>(out var runtimeType) && runtimeType == "StardewValley.Locations.Cellar" &&
            Zero(row["building_count"]) && Zero(row["stored_hay"]) && Zero(row["hay_capacity"]) &&
            row["capacity_status"] is JsonValue capacity && capacity.TryGetValue<string>(out var capacityStatus) && capacityStatus == "exact_native_getter";
    }

    private static bool Zero(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) && number == 0;
}
