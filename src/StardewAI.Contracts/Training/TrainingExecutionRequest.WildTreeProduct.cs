using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_wild_tree_product_profile")]
    public string FixtureWildTreeProductProfile { get; set; } = string.Empty;

    [JsonPropertyName("tree_product_tree_type")]
    public string TreeProductTreeType { get; set; } = string.Empty;

    [JsonPropertyName("expected_tree_has_seed_before")]
    public bool? ExpectedTreeHasSeedBefore { get; set; }

    [JsonPropertyName("expected_tree_has_seed_after")]
    public bool? ExpectedTreeHasSeedAfter { get; set; }

    [JsonPropertyName("expected_tree_was_shaken_today_before")]
    public bool? ExpectedTreeWasShakenTodayBefore { get; set; }

    [JsonPropertyName("expected_tree_was_shaken_today_after")]
    public bool? ExpectedTreeWasShakenTodayAfter { get; set; }

    [JsonPropertyName("tree_product_output_domain_json")]
    public string TreeProductOutputDomainJson { get; set; } = string.Empty;

    [JsonPropertyName("tree_product_output_domain_contract")]
    public string TreeProductOutputDomainContract { get; set; } = string.Empty;

    [JsonPropertyName("tree_product_projection_status")]
    public string TreeProductProjectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("tree_product_native_contract")]
    public string TreeProductNativeContract { get; set; } = string.Empty;
}
