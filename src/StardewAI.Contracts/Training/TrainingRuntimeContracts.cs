using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public static class TrainingSessionMode
    {
        public const string OfflineSmoke = "offline_smoke";
        public const string SimulatedTransition = "simulated_transition";
        public const string StardewWindowed = "stardew_windowed";
        public const string FormalProductTraining = "formal_product_training";
    }

    public sealed class TrainingLaunchRequest
    {
        [JsonPropertyName("run_id")]
        public string? RunId { get; set; }

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

        [JsonPropertyName("policy_trajectory_path")]
        public string? PolicyTrajectoryPath { get; set; }

        [JsonPropertyName("policy_dataset_manifest_path")]
        public string? PolicyDatasetManifestPath { get; set; }

        [JsonPropertyName("product_receipt_root")]
        public string? ProductReceiptRoot { get; set; }

        [JsonPropertyName("product_executor_url")]
        public string ProductExecutorUrl { get; set; } = "http://127.0.0.1:8768";

        [JsonPropertyName("native_executor_url")]
        public string NativeExecutorUrl { get; set; } = "http://127.0.0.1:8767";

        [JsonPropertyName("product_executor_executable_path")]
        public string? ProductExecutorExecutablePath { get; set; }

        [JsonPropertyName("live_training_loop_executable_path")]
        public string? LiveTrainingLoopExecutablePath { get; set; }

        [JsonPropertyName("max_attempts")]
        public int MaxAttempts { get; set; } = 1000000;

        [JsonPropertyName("max_persisted_iterations")]
        public int MaxPersistedIterations { get; set; } = 4;

        [JsonPropertyName("required_verified_actions")]
        public int RequiredVerifiedActions { get; set; }

        [JsonPropertyName("require_native_save_boundary")]
        public bool RequireNativeSaveBoundary { get; set; } = true;

        [JsonPropertyName("save_boundary_max_attempts")]
        public int SaveBoundaryMaxAttempts { get; set; } = 16;

        [JsonPropertyName("min_free_space_mb")]
        public int MinFreeSpaceMb { get; set; } = 8192;

        [JsonPropertyName("target_execution_mode")]
        public string TargetExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("manifest_path")]
        public string? ManifestPath { get; set; }

        [JsonPropertyName("game_executable_path")]
        public string? GameExecutablePath { get; set; }

        [JsonPropertyName("game_working_directory")]
        public string? GameWorkingDirectory { get; set; }

        [JsonPropertyName("save_isolation_path")]
        public string? SaveIsolationPath { get; set; }

        [JsonPropertyName("save_slot")]
        public string? SaveSlot { get; set; }

        [JsonPropertyName("bridge_url")]
        public string BridgeUrl { get; set; } = "http://127.0.0.1:8765";

        [JsonPropertyName("backend_url")]
        public string BackendUrl { get; set; } = "http://127.0.0.1:5000";

        [JsonPropertyName("allow_game_launch")]
        public bool AllowGameLaunch { get; set; }

        [JsonPropertyName("attach_existing_game")]
        public bool AttachExistingGame { get; set; }

        [JsonPropertyName("existing_game_process_id")]
        public int? ExistingGameProcessId { get; set; }

        [JsonPropertyName("sound_enabled")]
        public bool SoundEnabled { get; set; }

        [JsonPropertyName("window_style")]
        public string WindowStyle { get; set; } = "minimized";
    }

    public sealed class TrainingRunManifest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_run_manifest.v2";

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

        [JsonPropertyName("checkpoint_sha256")]
        public string CheckpointSha256 { get; set; } = string.Empty;

        [JsonPropertyName("policy_trajectory_path")]
        public string PolicyTrajectoryPath { get; set; } = string.Empty;

        [JsonPropertyName("policy_dataset_manifest_path")]
        public string PolicyDatasetManifestPath { get; set; } = string.Empty;

        [JsonPropertyName("policy_dataset_manifest_sha256")]
        public string PolicyDatasetManifestSha256 { get; set; } = string.Empty;

        [JsonPropertyName("product_receipt_root")]
        public string ProductReceiptRoot { get; set; } = string.Empty;

        [JsonPropertyName("product_executor_url")]
        public string ProductExecutorUrl { get; set; } = string.Empty;

        [JsonPropertyName("native_executor_url")]
        public string NativeExecutorUrl { get; set; } = string.Empty;

        [JsonPropertyName("product_executor_executable_path")]
        public string ProductExecutorExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("live_training_loop_executable_path")]
        public string LiveTrainingLoopExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("max_attempts")]
        public int MaxAttempts { get; set; }

        [JsonPropertyName("max_persisted_iterations")]
        public int MaxPersistedIterations { get; set; } = 4;

        [JsonPropertyName("required_verified_actions")]
        public int RequiredVerifiedActions { get; set; }

        [JsonPropertyName("require_native_save_boundary")]
        public bool RequireNativeSaveBoundary { get; set; }

        [JsonPropertyName("save_boundary_max_attempts")]
        public int SaveBoundaryMaxAttempts { get; set; } = 16;

        [JsonPropertyName("min_free_space_mb")]
        public int MinFreeSpaceMb { get; set; } = 8192;

        [JsonPropertyName("target_execution_mode")]
        public string TargetExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("compiler_version")]
        public string CompilerVersion { get; set; } = string.Empty;

        [JsonPropertyName("executor_version")]
        public string ExecutorVersion { get; set; } = string.Empty;

        [JsonPropertyName("structured_policy_required")]
        public bool StructuredPolicyRequired { get; set; }

        [JsonPropertyName("manifest_path")]
        public string ManifestPath { get; set; } = string.Empty;

        [JsonPropertyName("game_executable_path")]
        public string GameExecutablePath { get; set; } = string.Empty;

        [JsonPropertyName("game_working_directory")]
        public string GameWorkingDirectory { get; set; } = string.Empty;

        [JsonPropertyName("save_isolation_path")]
        public string SaveIsolationPath { get; set; } = string.Empty;

        [JsonPropertyName("save_slot")]
        public string SaveSlot { get; set; } = string.Empty;

        [JsonPropertyName("bridge_url")]
        public string BridgeUrl { get; set; } = string.Empty;

        [JsonPropertyName("backend_url")]
        public string BackendUrl { get; set; } = string.Empty;

        [JsonPropertyName("game_launch")]
        public string GameLaunch { get; set; } = "disabled";

        [JsonPropertyName("game_process_ownership")]
        public string GameProcessOwnership { get; set; } = "launcher";

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

        [JsonPropertyName("product_executor_process_id")]
        public int? ProductExecutorProcessId { get; set; }

        [JsonPropertyName("live_training_loop_process_id")]
        public int? LiveTrainingLoopProcessId { get; set; }

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
        public string SchemaVersion { get; set; } = "training_ready_probe.v2";

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

        [JsonPropertyName("formal_boundary_required")]
        public bool FormalBoundaryRequired { get; set; }

        [JsonPropertyName("dataset_manifest_verified")]
        public bool DatasetManifestVerified { get; set; }

        [JsonPropertyName("checkpoint_verified")]
        public bool CheckpointVerified { get; set; }

        [JsonPropertyName("product_executor_reachable")]
        public bool ProductExecutorReachable { get; set; }

        [JsonPropertyName("receipt_journal_ready")]
        public bool ReceiptJournalReady { get; set; }

        [JsonPropertyName("game_process_alive")]
        public bool GameProcessAlive { get; set; }

        [JsonPropertyName("product_executor_process_alive")]
        public bool ProductExecutorProcessAlive { get; set; }

        [JsonPropertyName("live_training_loop_process_alive")]
        public bool LiveTrainingLoopProcessAlive { get; set; }

        [JsonPropertyName("unresolved_product_receipts")]
        public int UnresolvedProductReceipts { get; set; }

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
