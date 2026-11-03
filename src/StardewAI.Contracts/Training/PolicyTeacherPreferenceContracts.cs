using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public static class PolicyTrainingObjectives
{
    public const string IndependentTeacherPairwise = "independent_teacher_pairwise.v1";
}

public sealed class PolicyTeacherEvidenceSources
{
    [JsonPropertyName("strategy_ledger_path")]
    public string StrategyLedgerPath { get; set; } = string.Empty;
    [JsonPropertyName("requirement_inventory_path")]
    public string RequirementInventoryPath { get; set; } = string.Empty;
    [JsonPropertyName("acquisition_lowering_path")]
    public string AcquisitionLoweringPath { get; set; } = string.Empty;
    [JsonPropertyName("ranking_path")]
    public string RankingPath { get; set; } = string.Empty;
    [JsonPropertyName("before_snapshot_path")]
    public string BeforeSnapshotPath { get; set; } = string.Empty;
    [JsonPropertyName("target_date_intents_path")]
    public string TargetDateIntentsPath { get; set; } = string.Empty;
    [JsonPropertyName("preference_path")]
    public string PreferencePath { get; set; } = string.Empty;
    [JsonPropertyName("execution_receipt_path")]
    public string ExecutionReceiptPath { get; set; } = string.Empty;
    [JsonPropertyName("after_snapshot_path")]
    public string AfterSnapshotPath { get; set; } = string.Empty;
}

public sealed class PolicyTeacherPreferencePair
{
    [JsonPropertyName("preferred_candidate_id")]
    public string PreferredCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("alternative_candidate_id")]
    public string AlternativeCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("first_differing_authoritative_criterion")]
    public string FirstDifferingAuthoritativeCriterion { get; set; } = string.Empty;

    [JsonPropertyName("label_semantics")]
    public string LabelSemantics { get; set; } = string.Empty;
}
