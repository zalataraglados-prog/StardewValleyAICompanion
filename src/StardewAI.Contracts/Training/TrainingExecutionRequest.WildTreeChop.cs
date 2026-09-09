using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_wild_tree_chop_profile")]
    public string FixtureWildTreeChopProfile { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_tree_type")]
    public string TreeChopTreeType { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_data_contract_status")]
    public string TreeChopDataContractStatus { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_protection_status")]
    public string TreeChopProtectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_projection_status")]
    public string TreeChopProjectionStatus { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_output_domain_contract")]
    public string TreeChopOutputDomainContract { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_guaranteed_minimum_outputs_json")]
    public string TreeChopGuaranteedMinimumOutputsJson { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_output_domain_json")]
    public string TreeChopOutputDomainJson { get; set; } = string.Empty;

    [JsonPropertyName("tree_chop_native_contract")]
    public string TreeChopNativeContract { get; set; } = string.Empty;

    [JsonPropertyName("expected_tree_present_after")]
    public bool? ExpectedTreePresentAfter { get; set; }

    [JsonPropertyName("expected_trees_chopped_before")]
    public long? ExpectedTreesChoppedBefore { get; set; }

    [JsonPropertyName("expected_trees_chopped_delta")]
    public long? ExpectedTreesChoppedDelta { get; set; }

    [JsonPropertyName("expected_trees_chopped_after")]
    public long? ExpectedTreesChoppedAfter { get; set; }
}
