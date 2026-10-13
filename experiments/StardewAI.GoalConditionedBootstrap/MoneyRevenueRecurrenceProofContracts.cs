using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueRecurrenceProofKinds
{
    public const string NativeShopSale = "native_shop_sale";
    public const string NativeShippingSettlement =
        "native_shipping_settlement";
}

public sealed class MoneyRevenueRecurrenceProofManifest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_recurrence_proof_manifest.v1";

    [JsonPropertyName("transitions")]
    public MoneyRevenueRecurrenceTransitionProof[] Transitions { get; set; } =
        Array.Empty<MoneyRevenueRecurrenceTransitionProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueRecurrenceTransitionProof
{
    [JsonPropertyName("transition_id")]
    public string TransitionId { get; set; } = string.Empty;

    [JsonPropertyName("proof_kind")]
    public string ProofKind { get; set; } = string.Empty;

    [JsonPropertyName("shop_sale_episode_path")]
    public string ShopSaleEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("queue_path")]
    public string QueuePath { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("executor_version")]
    public string ExecutorVersion { get; set; } = string.Empty;

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;
}

public sealed record MoneyRevenueArtifactBinding(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record MoneyRevenueRecurrenceTransitionProofEvidence(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("proof_kind")] string ProofKind,
    [property: JsonPropertyName("progression")]
        MoneyRevenueTransitionEvidence Progression,
    [property: JsonPropertyName("artifacts")]
        MoneyRevenueArtifactBinding[] Artifacts);

public sealed class MoneyRevenueRecurrenceProofReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_recurrence_proof_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("manifest_sha256")]
    public string ManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public MoneyRevenueRecurrencePrefixEvidence? Prefix { get; set; }

    [JsonPropertyName("transitions")]
    public MoneyRevenueRecurrenceTransitionProofEvidence[] Transitions
        { get; set; } =
        Array.Empty<MoneyRevenueRecurrenceTransitionProofEvidence>();

    [JsonPropertyName("proof_verified")]
    public bool ProofVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
