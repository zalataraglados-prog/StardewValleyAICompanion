using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("forge_candidate_id")]
    public string ForgeCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("forge_operation")]
    public string ForgeOperation { get; set; } = string.Empty;

    [JsonPropertyName("forge_reason")]
    public string ForgeReason { get; set; } = string.Empty;

    [JsonPropertyName("forge_source_id")]
    public string ForgeSourceId { get; set; } = string.Empty;

    [JsonPropertyName("forge_source_kind")]
    public string ForgeSourceKind { get; set; } = string.Empty;

    [JsonPropertyName("left_source_id")]
    public string LeftSourceId { get; set; } = string.Empty;

    [JsonPropertyName("left_state_json")]
    public string LeftStateJson { get; set; } = string.Empty;

    [JsonPropertyName("right_source_id")]
    public string RightSourceId { get; set; } = string.Empty;

    [JsonPropertyName("right_state_json")]
    public string RightStateJson { get; set; } = string.Empty;

    [JsonPropertyName("forge_shard_cost")]
    public int? ForgeShardCost { get; set; }

    [JsonPropertyName("forge_shard_refund")]
    public int? ForgeShardRefund { get; set; }

    [JsonPropertyName("forge_shard_count_before")]
    public int? ForgeShardCountBefore { get; set; }

    [JsonPropertyName("times_enchanted_before")]
    public long? TimesEnchantedBefore { get; set; }

    [JsonPropertyName("times_enchanted_after")]
    public long? TimesEnchantedAfter { get; set; }

    [JsonPropertyName("forge_output_contract_kind")]
    public string ForgeOutputContractKind { get; set; } = string.Empty;

    [JsonPropertyName("expected_output_state_json")]
    public string ExpectedOutputStateJson { get; set; } = string.Empty;

    [JsonPropertyName("random_outcome_contract_json")]
    public string RandomOutcomeContractJson { get; set; } = string.Empty;
}
