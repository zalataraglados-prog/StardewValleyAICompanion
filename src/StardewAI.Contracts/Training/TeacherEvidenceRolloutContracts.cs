using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public static class TeacherEvidenceRolloutLimits
{
    public const int MaxQueueItems = 8;
    public const int MaxEpisodes = 16;
}

public sealed class QueueExecutionReceiptEnvelope
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "queue_execution_receipt.v1";

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("before_game_tick")]
    public long BeforeGameTick { get; set; }

    [JsonPropertyName("after_game_tick")]
    public long AfterGameTick { get; set; }

    [JsonPropertyName("after_snapshot_fresh")]
    public bool AfterSnapshotFresh { get; set; }

    [JsonPropertyName("queue_execution_mode")]
    public string QueueExecutionMode { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("planned_item_count")]
    public int PlannedItemCount { get; set; }

    [JsonPropertyName("executed_item_count")]
    public int ExecutedItemCount { get; set; }

    [JsonPropertyName("final_pending_item_count")]
    public int FinalPendingItemCount { get; set; }

    [JsonPropertyName("max_queue_item_attempts")]
    public int MaxQueueItemAttempts { get; set; }

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_completed")]
    public bool SelectedCandidateCompleted { get; set; }

    [JsonPropertyName("step_results")]
    public QueueExecutionStepReceipt[] StepResults { get; set; } =
        Array.Empty<QueueExecutionStepReceipt>();

    [JsonPropertyName("block_reasons")]
    public string[] BlockReasons { get; set; } = Array.Empty<string>();
}

public sealed class QueueExecutionStepReceipt
{
    [JsonPropertyName("queue_item_index")]
    public int QueueItemIndex { get; set; }

    [JsonPropertyName("queue_item_count")]
    public int QueueItemCount { get; set; }

    [JsonPropertyName("queue_original_planned_item_count")]
    public int OriginalPlannedItemCount { get; set; }

    [JsonPropertyName("effective_queue_id")]
    public string QueueId { get; set; } = string.Empty;

    [JsonPropertyName("queue_item_id")]
    public string QueueItemId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("effective_before_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("compiled_command_state_hash")]
    public string CompiledCommandStateHash { get; set; } = string.Empty;

    [JsonPropertyName("teacher_preference_state_rebound")]
    public bool TeacherPreferenceStateRebound { get; set; }

    [JsonPropertyName("selected_queue_candidate_completed")]
    public bool SelectedQueueCandidateCompleted { get; set; }

    [JsonPropertyName("after_state_hash")]
    public string AfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("state_hash_changed")]
    public bool StateHashChanged { get; set; }

    [JsonPropertyName("before_game_tick")]
    public long BeforeGameTick { get; set; }

    [JsonPropertyName("after_game_tick")]
    public long AfterGameTick { get; set; }

    [JsonPropertyName("after_snapshot_fresh")]
    public bool AfterSnapshotFresh { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("primitive_kind")]
    public string PrimitiveKind { get; set; } = string.Empty;

    [JsonPropertyName("primitive_verification_status")]
    public string PrimitiveVerificationStatus { get; set; } = string.Empty;

    [JsonPropertyName("primitive_verification_reasons")]
    public string[] PrimitiveVerificationReasons { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("failure_attribution")]
    public string FailureAttribution { get; set; } = string.Empty;

    [JsonPropertyName("block_reasons")]
    public string[] BlockReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("effective_queue_item")]
    public JsonElement? EffectiveQueueItem { get; set; }

    [JsonPropertyName("changed_facts")]
    public JsonElement ChangedFacts { get; set; }
}
