using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

public sealed class LiveTrainingLoopReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "live_training_loop_report.v1";

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("backend_url")]
    public string BackendUrl { get; set; } = string.Empty;

    [JsonPropertyName("bridge_snapshot_url")]
    public string BridgeSnapshotUrl { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_file")]
    public string SnapshotFile { get; set; } = string.Empty;

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("progress_log_path")]
    public string ProgressLogPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_dir")]
    public string SnapshotDir { get; set; } = string.Empty;

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    [JsonPropertyName("max_attempts")]
    public int MaxAttempts { get; set; }

    [JsonPropertyName("attempts_started")]
    public int AttemptsStarted { get; set; }

    [JsonPropertyName("rows_appended")]
    public int RowsAppended { get; set; }

    [JsonPropertyName("verified_actions")]
    public int VerifiedActions { get; set; }

    [JsonPropertyName("required_verified_actions")]
    public int RequiredVerifiedActions { get; set; }

    [JsonPropertyName("primary_attempts_started")]
    public int PrimaryAttemptsStarted { get; set; }

    [JsonPropertyName("native_save_boundary_required")]
    public bool NativeSaveBoundaryRequired { get; set; }

    [JsonPropertyName("native_save_boundary_attempts_started")]
    public int NativeSaveBoundaryAttemptsStarted { get; set; }

    [JsonPropertyName("native_save_boundary_verified")]
    public bool NativeSaveBoundaryVerified { get; set; }

    [JsonPropertyName("training_data_transaction_status")]
    public string TrainingDataTransactionStatus { get; set; } = "not_required";

    [JsonPropertyName("training_data_transaction_path")]
    public string TrainingDataTransactionPath { get; set; } = string.Empty;

    [JsonPropertyName("canonical_training_artifacts_updated")]
    public bool CanonicalTrainingArtifactsUpdated { get; set; }

    [JsonPropertyName("native_save_boundary_initial_day_key")]
    public string NativeSaveBoundaryInitialDayKey { get; set; } = string.Empty;

    [JsonPropertyName("native_save_boundary_current_day_key")]
    public string NativeSaveBoundaryCurrentDayKey { get; set; } = string.Empty;

    [JsonPropertyName("native_save_boundary_initial_save_sha256")]
    public string NativeSaveBoundaryInitialSaveSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_save_boundary_current_save_sha256")]
    public string NativeSaveBoundaryCurrentSaveSha256 { get; set; } = string.Empty;

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; set; } = string.Empty;

    [JsonPropertyName("objective_completed")]
    public bool ObjectiveCompleted { get; set; }

    [JsonPropertyName("active_objective_continuation")]
    public JsonObject? ActiveObjectiveContinuation { get; set; }

    [JsonPropertyName("social_objective_completed")]
    public bool SocialObjectiveCompleted { get; set; }

    [JsonPropertyName("active_social_continuation")]
    public JsonObject? ActiveSocialContinuation { get; set; }

    [JsonPropertyName("last_state_hash")]
    public string LastStateHash { get; set; } = string.Empty;

    [JsonPropertyName("last_queue_id")]
    public string LastQueueId { get; set; } = string.Empty;

    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; }

    [JsonPropertyName("execution")]
    public string Execution { get; set; } = "disabled";

    [JsonPropertyName("executor_feedback_required")]
    public bool ExecutorFeedbackRequired { get; set; }

    [JsonPropertyName("training_report")]
    public JsonObject? TrainingReport { get; set; }

    [JsonPropertyName("prediction")]
    public JsonObject? Prediction { get; set; }
}
