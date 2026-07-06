using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public static class TrainingSessionMode
    {
        public const string OfflineSmoke = "offline_smoke";
        public const string SimulatedTransition = "simulated_transition";
        public const string StardewWindowed = "stardew_windowed";
    }

    public sealed class TrainingLaunchRequest
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = TrainingSessionMode.OfflineSmoke;

        [JsonPropertyName("root_path")]
        public string RootPath { get; set; } = @"E:\StardewAITraining";

        [JsonPropertyName("dataset_path")]
        public string? DatasetPath { get; set; }

        [JsonPropertyName("report_path")]
        public string? ReportPath { get; set; }

        [JsonPropertyName("checkpoint_path")]
        public string? CheckpointPath { get; set; }

        [JsonPropertyName("manifest_path")]
        public string? ManifestPath { get; set; }

        [JsonPropertyName("game_executable_path")]
        public string? GameExecutablePath { get; set; }

        [JsonPropertyName("game_working_directory")]
        public string? GameWorkingDirectory { get; set; }

        [JsonPropertyName("save_isolation_path")]
        public string? SaveIsolationPath { get; set; }

        [JsonPropertyName("bridge_url")]
        public string BridgeUrl { get; set; } = "http://127.0.0.1:8766";

        [JsonPropertyName("backend_url")]
        public string BackendUrl { get; set; } = "http://127.0.0.1:5000";

        [JsonPropertyName("allow_game_launch")]
        public bool AllowGameLaunch { get; set; }

        [JsonPropertyName("sound_enabled")]
        public bool SoundEnabled { get; set; }

        [JsonPropertyName("window_style")]
        public string WindowStyle { get; set; } = "minimized";
    }

    public sealed class TrainingRunManifest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_run_manifest.v1";

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = TrainingSessionMode.OfflineSmoke;

        [JsonPropertyName("root_path")]
        public string RootPath { get; set; } = string.Empty;

        [JsonPropertyName("dataset_path")]
        public string DatasetPath { get; set; } = string.Empty;

        [JsonPropertyName("report_path")]
        public string ReportPath { get; set; } = string.Empty;

        [JsonPropertyName("checkpoint_path")]
        public string CheckpointPath { get; set; } = string.Empty;

        [JsonPropertyName("manifest_path")]
        public string ManifestPath { get; set; } = string.Empty;

        [JsonPropertyName("game_executable_path")]
        public string GameExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("game_working_directory")]
        public string GameWorkingDirectory { get; set; } = string.Empty;

        [JsonPropertyName("save_isolation_path")]
        public string SaveIsolationPath { get; set; } = string.Empty;

        [JsonPropertyName("bridge_url")]
        public string BridgeUrl { get; set; } = string.Empty;

        [JsonPropertyName("backend_url")]
        public string BackendUrl { get; set; } = string.Empty;

        [JsonPropertyName("game_launch")]
        public string GameLaunch { get; set; } = "disabled";

        [JsonPropertyName("sound")]
        public string Sound { get; set; } = "disabled";

        [JsonPropertyName("window_style")]
        public string WindowStyle { get; set; } = "minimized";

        [JsonPropertyName("executable_kind")]
        public string ExecutableKind { get; set; } = string.Empty;

        [JsonPropertyName("environment_overrides")]
        public TrainingEnvironmentOverride[] EnvironmentOverrides { get; set; } = Array.Empty<TrainingEnvironmentOverride>();

        [JsonPropertyName("process_id")]
        public int? ProcessId { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "prepared";

        [JsonPropertyName("audit")]
        public TrainingRuntimeAudit Audit { get; set; } = new TrainingRuntimeAudit();
    }

    public sealed class TrainingEnvironmentOverride
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;
    }

    public sealed class TrainingRuntimeAudit
    {
        [JsonPropertyName("controller")]
        public string Controller { get; set; } = "StardewAI";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "No game launch unless allow_game_launch is true; sound must remain disabled for training.";

        [JsonPropertyName("notes")]
        public string[] Notes { get; set; } = Array.Empty<string>();
    }

    public sealed class TrainingLaunchResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_launch_result.v1";

        [JsonPropertyName("started")]
        public bool Started { get; set; }

        [JsonPropertyName("launch_attempted")]
        public bool LaunchAttempted { get; set; }

        [JsonPropertyName("blocked")]
        public bool Blocked { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("manifest")]
        public TrainingRunManifest Manifest { get; set; } = new TrainingRunManifest();
    }

    public sealed class TrainingReadyProbeResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_ready_probe.v1";

        [JsonPropertyName("ready")]
        public bool Ready { get; set; }

        [JsonPropertyName("backend_reachable")]
        public bool BackendReachable { get; set; }

        [JsonPropertyName("bridge_reachable")]
        public bool BridgeReachable { get; set; }

        [JsonPropertyName("latest_snapshot_available")]
        public bool LatestSnapshotAvailable { get; set; }

        [JsonPropertyName("latest_state_hash")]
        public string LatestStateHash { get; set; } = string.Empty;

        [JsonPropertyName("manifest_loaded")]
        public bool ManifestLoaded { get; set; }

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("snapshot_run_id")]
        public string SnapshotRunId { get; set; } = string.Empty;

        [JsonPropertyName("snapshot_game_tick")]
        public long? SnapshotGameTick { get; set; }

        [JsonPropertyName("checked_at")]
        public string CheckedAt { get; set; } = string.Empty;

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();
    }
}
