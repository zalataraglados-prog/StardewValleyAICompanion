using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropProductionTeacherComparisonVersions
{
    public const string Receipt =
        "money_revenue_crop_production_teacher_comparison_receipt.v2";
}

public sealed record MoneyRevenueCropProductionTeacherComparisonCandidate(
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("seed_id")] string SeedId,
    [property: JsonPropertyName("seed_qualified_item_id")]
        string SeedQualifiedItemId,
    [property: JsonPropertyName("shop_id")] string ShopId,
    [property: JsonPropertyName("tile_count")] int TileCount,
    [property: JsonPropertyName("harvest_wave_count")] int HarvestWaveCount,
    [property: JsonPropertyName("minimum_gross_revenue")]
        long MinimumGrossRevenue,
    [property: JsonPropertyName("seed_acquisition_cost")]
        long SeedAcquisitionCost,
    [property: JsonPropertyName("minimum_net_revenue")]
        long MinimumNetRevenue,
    [property: JsonPropertyName("last_harvest_total_day")]
        int LastHarvestTotalDay,
    [property: JsonPropertyName("required_watering_action_count")]
        int RequiredWateringActionCount,
    [property: JsonPropertyName("available_watering_actions_without_refill")]
        int AvailableWateringActionsWithoutRefill,
    [property: JsonPropertyName("watering_refill_route_verified")]
        bool WateringRefillRouteVerified,
    [property: JsonPropertyName("watering_capacity_limited")]
        bool WateringCapacityLimited,
    [property: JsonPropertyName("admitted")] bool Admitted,
    [property: JsonPropertyName("block_reasons")] string[] BlockReasons,
    [property: JsonPropertyName("selected")] bool Selected,
    [property: JsonPropertyName("native_outcome_observed")]
        bool NativeOutcomeObserved,
    [property: JsonPropertyName("observed_gross_revenue")]
        long? ObservedGrossRevenue,
    [property: JsonPropertyName("observed_seed_acquisition_cost")]
        long? ObservedSeedAcquisitionCost,
    [property: JsonPropertyName("observed_cash_net_revenue")]
        long? ObservedCashNetRevenue);

public sealed record MoneyRevenueCropProductionTeacherPairwisePreference(
    [property: JsonPropertyName("preferred_candidate_id")]
        string PreferredCandidateId,
    [property: JsonPropertyName("candidate_id")] string CandidateId,
    [property: JsonPropertyName("criterion")] string Criterion);

public sealed class MoneyRevenueCropProductionTeacherComparisonReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        MoneyRevenueCropProductionTeacherComparisonVersions.Receipt;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("segment_receipt_path")]
    public string SegmentReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("segment_receipt_sha256")]
    public string SegmentReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("teacher_decision_path")]
    public string TeacherDecisionPath { get; set; } = string.Empty;

    [JsonPropertyName("teacher_decision_sha256")]
    public string TeacherDecisionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("decision_id")]
    public string DecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("teacher_selection_policy")]
    public string TeacherSelectionPolicy { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_count")]
    public int CandidateDenominatorCount { get; set; }

    [JsonPropertyName("admitted_candidate_count")]
    public int AdmittedCandidateCount { get; set; }

    [JsonPropertyName("blocked_candidate_count")]
    public int BlockedCandidateCount { get; set; }

    [JsonPropertyName("selected_candidate_id")]
    public string SelectedCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("selected_seed_qualified_item_id")]
    public string SelectedSeedQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("actual_seed_acquisition_cost")]
    public long ActualSeedAcquisitionCost { get; set; }

    [JsonPropertyName("actual_gross_revenue")]
    public long ActualGrossRevenue { get; set; }

    [JsonPropertyName("actual_cash_net_revenue")]
    public long ActualCashNetRevenue { get; set; }

    [JsonPropertyName("crossed_criterion_ids")]
    public string[] CrossedCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("candidates")]
    public MoneyRevenueCropProductionTeacherComparisonCandidate[] Candidates
        { get; set; } =
            Array.Empty<MoneyRevenueCropProductionTeacherComparisonCandidate>();

    [JsonPropertyName("pairwise_preferences")]
    public MoneyRevenueCropProductionTeacherPairwisePreference[]
        PairwisePreferences { get; set; } =
            Array.Empty<MoneyRevenueCropProductionTeacherPairwisePreference>();

    [JsonPropertyName("candidate_denominator_authoritative")]
    public bool CandidateDenominatorAuthoritative { get; set; }

    [JsonPropertyName("selected_native_outcome_verified")]
    public bool SelectedNativeOutcomeVerified { get; set; }

    [JsonPropertyName("outcome_backed_teacher_comparison_verified")]
    public bool OutcomeBackedTeacherComparisonVerified { get; set; }

    [JsonPropertyName("criterion_local_join_ready")]
    public bool CriterionLocalJoinReady { get; set; }

    [JsonPropertyName("receipt_sha256")]
    public string ReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("comparison_policy")]
    public string ComparisonPolicy { get; set; } =
        "The receipt replays the complete authoritative transparent crop " +
        "candidate denominator and preserves every blocked candidate. Pairwise " +
        "Teacher preferences cover admitted counterfactual candidates only; " +
        "the selected candidate alone is bound to a verified native production " +
        "outcome and must meet its predicted gross, seed-cost and net bounds. " +
        "Unexecuted candidates are never presented as native outcomes. This " +
        "segment-level comparison is not a Money criterion-partition join and " +
        "cannot authorize formal training.";
}
