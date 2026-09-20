using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRouteTargetDateOpportunityCostReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_target_date_opportunity_cost.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("game_version")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("target_date_daily_time_energy_sha256")]
    public string TargetDateDailyTimeEnergySha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; set; }

    [JsonPropertyName("opportunity_cost_axis_resolved_count")]
    public int OpportunityCostAxisResolvedCount { get; set; }

    [JsonPropertyName("pareto_frontier_count")]
    public int ParetoFrontierCount { get; set; }

    [JsonPropertyName("pareto_dominated_count")]
    public int ParetoDominatedCount { get; set; }

    [JsonPropertyName("not_applicable_upstream_count")]
    public int NotApplicableUpstreamCount { get; set; }

    [JsonPropertyName("blocked_upstream_count")]
    public int BlockedUpstreamCount { get; set; }

    [JsonPropertyName("blocked_cost_evidence_count")]
    public int BlockedCostEvidenceCount { get; set; }

    [JsonPropertyName("route_occurrence_inventory_complete")]
    public bool RouteOccurrenceInventoryComplete { get; set; }

    [JsonPropertyName("opportunity_cost_axis_resolution_complete")]
    public bool OpportunityCostAxisResolutionComplete { get; set; }

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("routes")]
    public AcquisitionRouteTargetDateOpportunityCost[] Routes { get; set; } =
        Array.Empty<AcquisitionRouteTargetDateOpportunityCost>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "The opportunity_cost axis runs only after an exact daily time/energy match. It preserves a non-scalar cost vector: guaranteed elapsed game minutes, native energy, exact material quantities separated by qualified item, quality and live sale price, and each native currency domain separately. Material sale value is an audit summary, not a substitute for item identity. Comparison is restricted to routes for the same requirement alternative and uses strict Pareto dominance only; trade-offs and equal vectors remain on the frontier. Learner scores, future income, speculative utility and cross-currency conversion are forbidden. This axis does not commit reservations, choose a route portfolio or prove a fresh terminal receipt, and training authorization stays false.";
}

public sealed record AcquisitionRouteTargetDateOpportunityCost(
    [property: JsonPropertyName("route_occurrence_id")]
    string RouteOccurrenceId,
    [property: JsonPropertyName("upstream_route")]
    AcquisitionRouteTargetDateDailyTimeEnergy UpstreamRoute,
    [property: JsonPropertyName("comparison_group_key")]
    string ComparisonGroupKey,
    [property: JsonPropertyName("opportunity_cost_axis_status")]
    string OpportunityCostAxisStatus,
    [property: JsonPropertyName("opportunity_cost_axis_resolved")]
    bool OpportunityCostAxisResolved,
    [property: JsonPropertyName("opportunity_cost_matches_target_date")]
    bool? OpportunityCostMatchesTargetDate,
    [property: JsonPropertyName("cost_vector")]
    AcquisitionOpportunityCostVector? CostVector,
    [property: JsonPropertyName("dominated_by_route_occurrence_ids")]
    string[] DominatedByRouteOccurrenceIds,
    [property: JsonPropertyName("non_matching_reasons")]
    string[] NonMatchingReasons,
    [property: JsonPropertyName("blocking_reasons")]
    string[] BlockingReasons);

public sealed record AcquisitionOpportunityCostVector(
    [property: JsonPropertyName("guaranteed_elapsed_game_minutes")]
    int GuaranteedElapsedGameMinutes,
    [property: JsonPropertyName("required_energy")]
    double RequiredEnergy,
    [property: JsonPropertyName("material_total_sale_value")]
    int MaterialTotalSaleValue,
    [property: JsonPropertyName("material_costs")]
    AcquisitionOpportunityMaterialCost[] MaterialCosts,
    [property: JsonPropertyName("currency_costs")]
    AcquisitionOpportunityCurrencyCost[] CurrencyCosts,
    [property: JsonPropertyName("evidence_paths")]
    string[] EvidencePaths);

public sealed record AcquisitionOpportunityMaterialCost(
    [property: JsonPropertyName("qualified_item_id")]
    string QualifiedItemId,
    [property: JsonPropertyName("quality")]
    int Quality,
    [property: JsonPropertyName("unit_sale_price")]
    int UnitSalePrice,
    [property: JsonPropertyName("quantity")]
    int Quantity,
    [property: JsonPropertyName("total_sale_value")]
    int TotalSaleValue);

public sealed record AcquisitionOpportunityCurrencyCost(
    [property: JsonPropertyName("currency_id")]
    int CurrencyId,
    [property: JsonPropertyName("currency_key")]
    string CurrencyKey,
    [property: JsonPropertyName("amount")]
    int Amount);
