using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueTerminalExecutionManifestAssemblyRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_execution_manifest_assembly_request.v1";

    [JsonPropertyName("counterfactual_request_manifest_path")]
    public string CounterfactualRequestManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_path")]
    public string CounterfactualRequestCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("alternatives")]
    public MoneyRevenueTerminalAlternativeProof[] Alternatives { get; set; } =
        Array.Empty<MoneyRevenueTerminalAlternativeProof>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}
