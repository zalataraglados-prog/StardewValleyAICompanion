using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MoneyRevenueAlternativeSelectionDisposition
{
    InvalidDenominator = 0,
    IncomparableFrontier = 1,
    UniqueAuthoritativePreference = 2
}

public sealed record MoneyRevenueAlternativeCandidate(
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("revenue_prefix")]
        MoneyRevenueRecurrencePrefixEvidence RevenuePrefix,
    [property: JsonPropertyName("opportunity_cost")]
        AcquisitionOpportunityCostVector OpportunityCost,
    [property: JsonPropertyName("reservation_not_required")]
        bool ReservationNotRequired,
    [property: JsonPropertyName("reservation_claim_set")]
        AcquisitionRouteReservationClaimSet? ReservationClaimSet);

public sealed record MoneyRevenueAlternativeEvaluation(
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("target_satisfied")] bool TargetSatisfied,
    [property: JsonPropertyName("progress_toward_target")]
        uint ProgressTowardTarget,
    [property: JsonPropertyName("final_total_day")] int FinalTotalDay,
    [property: JsonPropertyName("total_revenue")] int TotalRevenue,
    [property: JsonPropertyName("reservation_not_required")]
        bool ReservationNotRequired,
    [property: JsonPropertyName("reservation_ids")] string[] ReservationIds,
    [property: JsonPropertyName("opportunity_cost")]
        AcquisitionOpportunityCostVector OpportunityCost,
    [property: JsonPropertyName("dominated_by_alternative_ids")]
        string[] DominatedByAlternativeIds,
    [property: JsonPropertyName("on_pareto_frontier")]
        bool OnParetoFrontier);

public sealed record MoneyRevenueAlternativePairwisePreference(
    [property: JsonPropertyName("preferred_alternative_id")]
        string PreferredAlternativeId,
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("criterion")]
        string Criterion);

public sealed record MoneyRevenueAlternativeComparison(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("target_total_money_earned")]
        uint TargetTotalMoneyEarned,
    [property: JsonPropertyName("source_state_hash")]
        string SourceStateHash,
    [property: JsonPropertyName("expected_ledger_revision")]
        int ExpectedLedgerRevision,
    [property: JsonPropertyName("candidate_denominator_count")]
        int CandidateDenominatorCount,
    [property: JsonPropertyName("candidate_denominator_complete")]
        bool CandidateDenominatorComplete,
    [property: JsonPropertyName("selection_disposition")]
        MoneyRevenueAlternativeSelectionDisposition SelectionDisposition,
    [property: JsonPropertyName("selected_alternative_id")]
        string SelectedAlternativeId,
    [property: JsonPropertyName("teacher_preference_label_eligible")]
        bool TeacherPreferenceLabelEligible,
    [property: JsonPropertyName("evaluations")]
        MoneyRevenueAlternativeEvaluation[] Evaluations,
    [property: JsonPropertyName("pairwise_preferences")]
        MoneyRevenueAlternativePairwisePreference[] PairwisePreferences)
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion =>
        "money_revenue_reserve_aware_alternative_comparison.v1";

    [JsonPropertyName("selection_policy")]
    public string SelectionPolicy =>
        "authoritative_threshold_then_reserve_aware_strict_pareto.v1";

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized => false;

    [JsonPropertyName("uses_learner_score")]
    public bool UsesLearnerScore => false;
}
