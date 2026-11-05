using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record MasterAnglerTerminalSaveBoundaryRequest
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; init; } = "master_angler_terminal_save_boundary_request.v1";
    [JsonPropertyName("source_save_file")] public string SourceSaveFile { get; init; } = string.Empty;
    [JsonPropertyName("isolated_save_file")] public string IsolatedSaveFile { get; init; } = string.Empty;
    [JsonPropertyName("isolation_root")] public string IsolationRoot { get; init; } = string.Empty;
    [JsonPropertyName("requirement_inventory_path")] public string RequirementInventoryPath { get; init; } = string.Empty;
    [JsonPropertyName("acquisition_lowering_path")] public string AcquisitionLoweringPath { get; init; } = string.Empty;
    [JsonPropertyName("missing_qualified_item_id")] public string MissingQualifiedItemId { get; init; } = string.Empty;
    [JsonPropertyName("decision_spring_day_year_two")] public int DecisionSpringDayYearTwo { get; init; }
    [JsonPropertyName("declared_local_prerequisite_only")] public bool DeclaredLocalPrerequisiteOnly { get; init; }
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized { get; init; }
}

public sealed record MasterAnglerTerminalSaveBoundaryReport(
    [property: JsonPropertyName("request_path")] string RequestPath,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("source_save_file")] string SourceSaveFile,
    [property: JsonPropertyName("isolated_save_file")] string IsolatedSaveFile,
    [property: JsonPropertyName("source_save_sha256")] string SourceSaveSha256,
    [property: JsonPropertyName("prepared_save_sha256")] string PreparedSaveSha256,
    [property: JsonPropertyName("requirement_inventory_sha256")] string RequirementInventorySha256,
    [property: JsonPropertyName("acquisition_lowering_sha256")] string AcquisitionLoweringSha256,
    [property: JsonPropertyName("missing_qualified_item_id")] string MissingQualifiedItemId,
    [property: JsonPropertyName("decision_spring_day_year_two")] int DecisionSpringDayYearTwo)
{
    [JsonPropertyName("schema_version")] public string SchemaVersion => "master_angler_terminal_save_boundary.v1";
    [JsonPropertyName("status")] public string Status => "prepared_local_prerequisites_not_native_outcome";
    [JsonPropertyName("eligible_species_count")] public int EligibleSpeciesCount => 72;
    [JsonPropertyName("fixture_prior_caught_species_count")] public int FixturePriorCaughtSpeciesCount => 71;
    [JsonPropertyName("source_save_unchanged")] public bool SourceSaveUnchanged => true;
    [JsonPropertyName("prior_species_acquisition_verified")] public bool PriorSpeciesAcquisitionVerified => false;
    [JsonPropertyName("native_criterion_outcome_verified")] public bool NativeCriterionOutcomeVerified => false;
    [JsonPropertyName("formal_product_training_authorized")] public bool FormalProductTrainingAuthorized => false;
}
