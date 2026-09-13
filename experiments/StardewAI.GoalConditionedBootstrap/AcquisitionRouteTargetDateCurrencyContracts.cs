using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateCurrencyReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_currency_budget.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_resource_sha256")]
    public string TargetDateResourceSha256 { get; set; } = string.Empty;

    [JsonPropertyName("static_calendar_resolution_sha256")]
    public string StaticCalendarResolutionSha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("currency_axis_resolved_count")]
    public int CurrencyAxisResolvedCount { get; set; }

    [JsonPropertyName("currency_budget_match_count")]
    public int CurrencyBudgetMatchCount { get; set; }

    [JsonPropertyName("currency_budget_miss_count")]
    public int CurrencyBudgetMissCount { get; set; }

    [JsonPropertyName("currency_not_required_count")]
    public int CurrencyNotRequiredCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_currency_evidence_count")]
    public int BlockedCurrencyEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("currency_axis_resolution_complete")]
    public bool CurrencyAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateCurrency[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateCurrency>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The currency_budget axis runs only after exact resource-input satisfaction. Shop purchases bind the exact current native ShopBuilder quote by shop, synchronized stock key and qualified item; purchase count is the route amount divided upward by the exact output stack, and price, finite stock, barter quantity and output quality must all satisfy that count. Missing, unavailable, sold-out, non-buyable or insufficient-quality quotes cannot be guessed from static price. Currency IDs are exactly ShopMenu 0 money, 1 star tokens, 2 club coins and 4 Qi gems. Native Vault money payments use the carried route amount. This axis proves one route occurrence only; cross-route reservations, future income, processing, time/energy, opportunity cost and terminal purchase/payment receipts remain downstream. Training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateCurrency(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateResource UpstreamRoute,
    [property: JsonPropertyName("currency_axis_status")]
    string CurrencyAxisStatus,
    [property: JsonPropertyName("currency_axis_resolved")]
    bool CurrencyAxisResolved,
    [property: JsonPropertyName("currency_budget_matches_target_date")]
    bool? CurrencyBudgetMatchesTargetDate,
    [property: JsonPropertyName("currency_requirement_kind")]
    string CurrencyRequirementKind,
    [property: JsonPropertyName("currency_evaluation")]
    AcquisitionCurrencyEvaluation? CurrencyEvaluation,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionCurrencyEvaluation(
    [property: JsonPropertyName("currency_id")]
    int CurrencyId,
    [property: JsonPropertyName("currency_key")]
    string CurrencyKey,
    [property: JsonPropertyName("required_amount")]
    int? RequiredAmount,
    [property: JsonPropertyName("available_amount")]
    int? AvailableAmount,
    [property: JsonPropertyName("output_stack_per_purchase")]
    int? OutputStackPerPurchase,
    [property: JsonPropertyName("output_quality")]
    int? OutputQuality,
    [property: JsonPropertyName("required_purchase_count")]
    int? RequiredPurchaseCount,
    [property: JsonPropertyName("purchase_quote_status")]
    string PurchaseQuoteStatus,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths);
