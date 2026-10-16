using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueTerminalProofFragmentIndex
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_fragment_index.v1";

    [JsonPropertyName("collection_plan_path")]
    public string CollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public MoneyRevenueTerminalAlternativeProof[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueTerminalAlternativeProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueTerminalProofCollectionCheckpointRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_collection_checkpoint_request.v1";

    [JsonPropertyName("collection_plan_path")]
    public string CollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_path")]
    public string FragmentIndexPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueTerminalProofCollectionCheckpoint
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_collection_checkpoint.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("collection_plan_path")]
    public string CollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("collection_plan_sha256")]
    public string CollectionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_path")]
    public string FragmentIndexPath { get; set; } = string.Empty;

    [JsonPropertyName("fragment_index_sha256")]
    public string FragmentIndexSha256 { get; set; } = string.Empty;

    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }

    [JsonPropertyName("verified_fragment_count")]
    public int VerifiedFragmentCount { get; set; }

    [JsonPropertyName("pending_request_count")]
    public int PendingRequestCount { get; set; }

    [JsonPropertyName("verified_request_ids")]
    public string[] VerifiedRequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("pending_request_ids")]
    public string[] PendingRequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("verified_evidence")]
    public MoneyRevenueTerminalExecutionEvidence[] VerifiedEvidence
        { get; set; } =
        Array.Empty<MoneyRevenueTerminalExecutionEvidence>();

    [JsonPropertyName("terminal_proof_index_ready")]
    public bool TerminalProofIndexReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("checkpoint_policy")]
    public string CheckpointPolicy { get; set; } =
        "A fragment is counted only after the existing terminal execution verifier " +
        "replays it against the exact authoritative request. Partial checkpoints are " +
        "resumable progress only. A final terminal proof index can be emitted only when " +
        "the verified request ID set equals the complete collection-plan denominator.";
}

public sealed class MoneyRevenueTerminalProofIndex
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_index.v1";

    [JsonPropertyName("collection_checkpoint_path")]
    public string CollectionCheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("collection_checkpoint_sha256")]
    public string CollectionCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("alternatives")]
    public MoneyRevenueTerminalAlternativeProof[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueTerminalAlternativeProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
