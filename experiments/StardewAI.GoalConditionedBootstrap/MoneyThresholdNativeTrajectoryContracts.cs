using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyThresholdNativeTrajectoryVersions
{
    public const string Manifest =
        "money_threshold_native_trajectory_manifest.v1";
    public const string Receipt =
        "money_threshold_native_trajectory_receipt.v1";
}

public static class MoneyThresholdNativeTrajectoryKinds
{
    public const string DirectShopSale = "direct_shop_sale";
    public const string DirectShipping = "direct_shipping";
    public const string AcquisitionThenShopSale =
        "acquisition_then_shop_sale";
    public const string AcquisitionThenShipping =
        "acquisition_then_shipping";
}

public sealed class MoneyThresholdNativeTrajectoryManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdNativeTrajectoryVersions.Manifest;

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_execution_proof_manifest_path")]
    public string TerminalExecutionProofManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("terminal_execution_proof_receipt_path")]
    public string TerminalExecutionProofReceiptPath { get; set; } =
        string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyThresholdNativeTrajectoryReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyThresholdNativeTrajectoryVersions.Receipt;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("concrete_alternative_id")]
    public string ConcreteAlternativeId { get; set; } = string.Empty;

    [JsonPropertyName("trajectory_kind")]
    public string TrajectoryKind { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_path")]
    public string SourceSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("source_snapshot_sha256")]
    public string SourceSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("source_checkpoint")]
    public MoneyRevenueCheckpoint SourceCheckpoint { get; set; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        string.Empty,
        0,
        string.Empty);

    [JsonPropertyName("final_checkpoint")]
    public MoneyRevenueCheckpoint FinalCheckpoint { get; set; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        0,
        0,
        0,
        string.Empty,
        0,
        string.Empty);

    [JsonPropertyName("revenue_prefix")]
    public MoneyRevenueRecurrencePrefixEvidence RevenuePrefix { get; set; } =
        new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            Array.Empty<MoneyRevenueTransitionEvidence>());

    [JsonPropertyName("acquisition_rollout_required")]
    public bool AcquisitionRolloutRequired { get; set; }

    [JsonPropertyName("terminal_execution_evidence_sha256")]
    public string TerminalExecutionEvidenceSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("artifacts")]
    public MoneyRevenueArtifactBinding[] Artifacts { get; set; } =
        Array.Empty<MoneyRevenueArtifactBinding>();

    [JsonPropertyName("native_outcome_verified")]
    public bool NativeOutcomeVerified { get; set; }

    [JsonPropertyName("teacher_preference_label_verified")]
    public bool TeacherPreferenceLabelVerified { get; set; }

    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("trajectory_policy")]
    public string TrajectoryPolicy { get; set; } =
        "The receipt selects one request from an authoritative Revenue request denominator and replays its existing terminal proof, optional acquisition rollout and Revenue recurrence without consulting a Teacher comparison. It proves a native money outcome from the exact source state through the final revenue checkpoint, but cannot label a preference or authorize formal training.";
}
