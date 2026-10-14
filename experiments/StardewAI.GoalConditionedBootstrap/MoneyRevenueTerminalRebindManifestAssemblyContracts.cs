using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueTerminalRebindManifestAssemblyRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_rebind_manifest_assembly_request.v1";

    [JsonPropertyName("comparison_collection_plan_path")]
    public string ComparisonCollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("rollout_proofs")]
    public MoneyRevenueAcquisitionRolloutProofSource[] RolloutProofs
    { get; set; } = Array.Empty<MoneyRevenueAcquisitionRolloutProofSource>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed record MoneyRevenueAcquisitionRolloutProofSource(
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("rollout_proof_manifest_path")]
        string RolloutProofManifestPath,
    [property: JsonPropertyName("rollout_proof_receipt_path")]
        string RolloutProofReceiptPath);
