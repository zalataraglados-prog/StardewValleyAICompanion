using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CommunityCenterLifecycleReceiptAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "community_center_lifecycle_receipt_admission.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("transition_kind")]
    public string TransitionKind { get; set; } = string.Empty;

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("queue_sha256")]
    public string QueueSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_evidence")]
    public CommunityCenterLifecycleTransitionEvidence TransitionEvidence { get; set; } = new();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class CommunityCenterLifecycleTransitionEvidence
{
    [JsonPropertyName("transition_kind")]
    public string TransitionKind { get; set; } = string.Empty;

    [JsonPropertyName("expected_event_id")]
    public string ExpectedEventId { get; set; } = string.Empty;

    [JsonPropertyName("before_stage")]
    public string BeforeStage { get; set; } = string.Empty;

    [JsonPropertyName("after_stage")]
    public string AfterStage { get; set; } = string.Empty;

    [JsonPropertyName("before_summary")]
    public string BeforeSummary { get; set; } = string.Empty;

    [JsonPropertyName("after_summary")]
    public string AfterSummary { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}
