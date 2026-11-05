using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class NativeExecutionSourceSupport
{
    internal static void ValidateRecordedBoundary(JsonElement result, SnapshotEnvelope before, SnapshotEnvelope after)
    {
        Require(result.TryGetProperty("after_state_hash", out var hash) && hash.ValueKind == JsonValueKind.String &&
            hash.GetString() == after.StateHash && MatchesTick("before_game_tick", before.GameTick) &&
            MatchesTick("after_game_tick", after.GameTick) && result.TryGetProperty("after_snapshot_fresh", out var fresh) &&
            fresh.ValueKind == JsonValueKind.True, "recorded_execution_boundary_mismatch_or_missing");

        bool MatchesTick(string name, long observed) => result.TryGetProperty(name, out var tick) &&
            tick.ValueKind == JsonValueKind.Number && tick.TryGetInt64(out var recorded) && recorded == observed;
    }

    internal static void ValidateSnapshot(SnapshotEnvelope snapshot) =>
        Require(snapshot.GameVersion == "1.6.15" && !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
            snapshot.State is not null && SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash),
            "snapshot_identity_or_hash_invalid");

    internal static int TotalDay(SnapshotEnvelope snapshot)
    {
        var value = Value(snapshot, "time", "total_days");
        Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _), "canonical_total_day_missing_or_mistyped");
        return value.GetInt32();
    }

    internal static JsonElement Value(SnapshotEnvelope snapshot, string section, string name)
    {
        Require(snapshot.State.TryGetValue(section, out var parent) && parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Object &&
            field.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String &&
            status.GetString() is FieldStatus.Available or FieldStatus.Derived && field.TryGetProperty("value", out _),
            "snapshot_field_unavailable:" + section + "." + name);
        return snapshot.State[section].GetProperty(name).GetProperty("value");
    }

    internal static string Identity(FieldEnvelope<string?>? field)
    {
        Require(field is not null && FieldEnvelopeValidator.IsReadableStatus(field.Status) && !string.IsNullOrWhiteSpace(field.Value),
            "save_or_player_identity_unreadable");
        return field!.Value!;
    }

    internal static T ReadTyped<T>(string path, string schema)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(path, "Native execution source");
        Require(raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("schema_version", out var version) &&
            version.ValueKind == JsonValueKind.String && version.GetString() == schema, "source_schema_invalid:" + path);
        if (schema == "snapshot.v1")
            Require(raw.TryGetProperty("game_tick", out var tick) && tick.ValueKind == JsonValueKind.Number && tick.TryGetInt64(out _),
                "observed_game_tick_missing_or_mistyped");
        return raw.Deserialize<T>(JsonDefaults.Options) ?? throw new InvalidDataException("Native execution source is null.");
    }

    internal static string RequiredFile(string path)
    {
        Require(!string.IsNullOrWhiteSpace(path), "source_path_missing");
        var full = Path.GetFullPath(path);
        Require(File.Exists(full), "source_file_missing:" + full);
        return full;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException("Native execution source: " + reason);
    }
}
