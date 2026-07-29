using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("process_input_qualified_item_id")]
    public string ProcessInputQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("process_input_quantity")]
    public int? ProcessInputQuantity { get; set; }

    [JsonPropertyName("process_additional_items_json")]
    public string ProcessAdditionalItemsJson { get; set; } = string.Empty;

    [JsonPropertyName("machine_prediction_contract_fingerprint")]
    public string MachinePredictionContractFingerprint { get; set; } =
        string.Empty;

    [JsonPropertyName("machine_prediction_training_kind")]
    public string MachinePredictionTrainingKind { get; set; } =
        string.Empty;

    [JsonPropertyName("machine_output_distribution_outcome_kind")]
    public string MachineOutputDistributionOutcomeKind { get; set; } =
        string.Empty;

    [JsonPropertyName("fixture_machine_harvest_use_native_config")]
    public bool FixtureMachineHarvestUseNativeConfig { get; set; }

    [JsonPropertyName("fixture_machine_harvest_experience_override")]
    public bool FixtureMachineHarvestExperienceOverride { get; set; }

    [JsonPropertyName("fixture_machine_harvest_experience_raw")]
    public string FixtureMachineHarvestExperienceRaw { get; set; } =
        string.Empty;

    [JsonPropertyName("fixture_machine_harvest_skill_profile")]
    public string FixtureMachineHarvestSkillProfile { get; set; } =
        string.Empty;

    [JsonPropertyName("anvil_reforge_utility_metric")]
    public string AnvilReforgeUtilityMetric { get; set; } =
        string.Empty;

    [JsonPropertyName("anvil_reforge_current_utility")]
    public double? AnvilReforgeCurrentUtility { get; set; }

    [JsonPropertyName("anvil_reforge_expected_utility")]
    public double? AnvilReforgeExpectedUtility { get; set; }

    [JsonPropertyName("anvil_reforge_expected_utility_delta")]
    public double? AnvilReforgeExpectedUtilityDelta { get; set; }

    [JsonPropertyName("anvil_reforge_improvement_probability")]
    public double? AnvilReforgeImprovementProbability { get; set; }

    [JsonPropertyName("relocation_intent_id")]
    public string RelocationIntentId { get; set; } = string.Empty;

    [JsonPropertyName("machine_removal_projection_fingerprint")]
    public string MachineRemovalProjectionFingerprint { get; set; } =
        string.Empty;

    [JsonPropertyName("tool_qualified_item_id")]
    public string ToolQualifiedItemId { get; set; } = string.Empty;
}

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("machine_output_distribution_outcome_kind")]
    public string MachineOutputDistributionOutcomeKind { get; set; } =
        string.Empty;

    [JsonPropertyName("anvil_reforge_utility_metric")]
    public string AnvilReforgeUtilityMetric { get; set; } =
        string.Empty;

    [JsonPropertyName("anvil_reforge_current_utility")]
    public double? AnvilReforgeCurrentUtility { get; set; }

    [JsonPropertyName("anvil_reforge_expected_utility")]
    public double? AnvilReforgeExpectedUtility { get; set; }

    [JsonPropertyName("anvil_reforge_realized_utility")]
    public double? AnvilReforgeRealizedUtility { get; set; }

    [JsonPropertyName("anvil_reforge_realized_utility_delta")]
    public double? AnvilReforgeRealizedUtilityDelta { get; set; }

    [JsonPropertyName("anvil_reforge_realized_improved")]
    public bool? AnvilReforgeRealizedImproved { get; set; }

    [JsonPropertyName("anvil_reforge_realized_outcome_json")]
    public string AnvilReforgeRealizedOutcomeJson { get; set; } =
        string.Empty;
}
