using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge;

public sealed partial class ModEntry
{
    private int nativeOutputSnapshotGameThreadId;

    private void InitializeNativeOutputSnapshotApi() =>
        Volatile.Write(ref nativeOutputSnapshotGameThreadId, Environment.CurrentManagedThreadId);

    // Exporting the API never captures a snapshot. Only an explicit isolated read may do so.
    public override object GetApi() => new NativeOutputSnapshotReadApi(ReadNativeOutputSnapshotAnchor);

    private string ReadNativeOutputSnapshotAnchor()
    {
        if (Volatile.Read(ref nativeOutputSnapshotGameThreadId) != Environment.CurrentManagedThreadId)
            return NativeOutputSnapshotUnavailable("snapshot_anchor_requires_entry_game_thread");

        try
        {
            if (!Context.IsWorldReady)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_world_not_ready");
            if (Context.IsMultiplayer || Game1.IsMultiplayer)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_requires_singleplayer");
            if (stateCollector is null)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_collector_unavailable");

            var trainingMode = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE");
            var runId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID");
            var isolationPath = Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH");
            if (trainingMode != "1" || string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(isolationPath))
                return NativeOutputSnapshotUnavailable("snapshot_anchor_requires_isolated_training_run");

            var beforeTick = unchecked((long)Game1.ticks);
            var snapshot = RefreshSnapshotCache("full", publishSnapshotEvent: false);
            var afterTick = unchecked((long)Game1.ticks);
            if (beforeTick < 0 || beforeTick != afterTick || snapshot.GameTick != beforeTick)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_capture_crossed_game_tick");
            if (!Context.IsWorldReady || Context.IsMultiplayer || Game1.IsMultiplayer)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_world_context_changed");

            var snapshotMode = NativeOutputSnapshotEnvironment(snapshot, "training_mode");
            var snapshotRunId = NativeOutputSnapshotEnvironment(snapshot, "training_run_id");
            var snapshotIsolationPath = NativeOutputSnapshotEnvironment(snapshot, "save_isolation_path");
            if (snapshotMode != "1" || snapshotRunId != runId || snapshotIsolationPath != isolationPath)
                return NativeOutputSnapshotUnavailable("snapshot_anchor_training_identity_changed");
            if (snapshot.StateHash.Length != 64 || !snapshot.StateHash.All(Uri.IsHexDigit) ||
                string.IsNullOrWhiteSpace(snapshot.GameVersion) ||
                snapshot.SaveId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.SaveId.Value) ||
                snapshot.PlayerId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.PlayerId.Value))
                return NativeOutputSnapshotUnavailable("snapshot_anchor_snapshot_identity_unavailable");

            // Metadata only. The existing full-profile cache retains this exact snapshot for
            // the existing expected_state_hash/expected_game_tick consumer; no second scan.
            return JsonSerializer.Serialize(new
            {
                schema_version = "native_output_snapshot_anchor.v1",
                status = "available",
                snapshot_profile = "full",
                state_hash = snapshot.StateHash,
                game_tick = snapshot.GameTick,
                game_version = snapshot.GameVersion,
                save_id = snapshot.SaveId.Value,
                player_id = snapshot.PlayerId.Value,
                run_id = snapshotRunId,
                save_isolation_path = snapshotIsolationPath,
                diagnostics = Array.Empty<string>(),
                native_outcome_verified = false,
                formal_product_training_authorized = false
            }, jsonOptions);
        }
        catch (Exception error)
        {
            // Report failure, never an available anchor with a fallback hash/tick or stale cache.
            return NativeOutputSnapshotUnavailable("snapshot_anchor_capture_failed:" + error.GetType().Name);
        }
    }

    private static string? NativeOutputSnapshotEnvironment(SnapshotEnvelope snapshot, string name)
    {
        if (snapshot.State.TryGetValue("environment", out var environment) &&
            environment.ValueKind == JsonValueKind.Object && environment.TryGetProperty(name, out var field) &&
            field.ValueKind == JsonValueKind.Object && field.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String && status.GetString() == FieldStatus.Available &&
            field.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();
        return null;
    }

    private string NativeOutputSnapshotUnavailable(string diagnostic) => JsonSerializer.Serialize(new
    {
        schema_version = "native_output_snapshot_anchor.v1",
        status = "unavailable",
        diagnostics = new[] { diagnostic },
        native_outcome_verified = false,
        formal_product_training_authorized = false
    }, jsonOptions);
}
