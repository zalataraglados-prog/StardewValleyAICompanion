using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueComparisonCollectionPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_comparison_collection_plan_request.v1";

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("native_run_summary_path")]
    public string NativeRunSummaryPath { get; set; } = string.Empty;

    [JsonPropertyName("alternative_denominator_catalog_path")]
    public string AlternativeDenominatorCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueComparisonCollectionPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_comparison_collection_plan.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_run_summary_path")]
    public string NativeRunSummaryPath { get; set; } = string.Empty;

    [JsonPropertyName("native_run_summary_sha256")]
    public string NativeRunSummarySha256 { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("crossed_criterion_ids")]
    public string[] CrossedCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("candidate_denominator_sha256")]
    public string CandidateDenominatorSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_terminal_episode_path")]
    public string NativeTerminalEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("native_terminal_episode_sha256")]
    public string NativeTerminalEpisodeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_catalog_candidate_id")]
    public string SelectedCatalogCandidateId { get; set; } = string.Empty;

    [JsonPropertyName("alternative_denominator_catalog_path")]
    public string AlternativeDenominatorCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("alternative_denominator_file_sha256")]
    public string AlternativeDenominatorFileSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("alternative_denominator_catalog_sha256")]
    public string AlternativeDenominatorCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("alternative_count")]
    public int AlternativeCount { get; set; }

    [JsonPropertyName("direct_alternative_count")]
    public int DirectAlternativeCount { get; set; }

    [JsonPropertyName("fresh_rebind_alternative_count")]
    public int FreshRebindAlternativeCount { get; set; }

    [JsonPropertyName("existing_native_episode_reuse_count")]
    public int ExistingNativeEpisodeReuseCount { get; set; }

    [JsonPropertyName("work_items")]
    public MoneyRevenueComparisonCollectionWorkItem[] WorkItems { get; set; } =
        Array.Empty<MoneyRevenueComparisonCollectionWorkItem>();

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("plan_policy")]
    public string PlanPolicy { get; set; } =
        "The plan is projected from one replay-verified native Money run and its rebuilt authoritative Revenue alternative skeleton denominator. It preserves every alternative exactly once. An already verified native sale episode may satisfy only the matching terminal episode input; it never substitutes for acquisition rollout, fresh terminal rebind, strategy-ledger boundaries, opportunity cost, reservation safety, recurrence, exact comparison admission or criterion-local join.";
}

public sealed record MoneyRevenueComparisonCollectionWorkItem(
    [property: JsonPropertyName("alternative_id")] string AlternativeId,
    [property: JsonPropertyName("source_catalog_candidate_id")]
        string SourceCatalogCandidateId,
    [property: JsonPropertyName("source_candidate_id")]
        string SourceCandidateId,
    [property: JsonPropertyName("source_option_id")] string SourceOptionId,
    [property: JsonPropertyName("terminal_option_id")]
        string TerminalOptionId,
    [property: JsonPropertyName("proof_shape")] string ProofShape,
    [property: JsonPropertyName("requires_acquisition_rollout")]
        bool RequiresAcquisitionRollout,
    [property: JsonPropertyName("requires_fresh_terminal_rebind")]
        bool RequiresFreshTerminalRebind,
    [property: JsonPropertyName("requires_native_shop_sale_proof")]
        bool RequiresNativeShopSaleProof,
    [property: JsonPropertyName("requires_native_shipping_deposit_proof")]
        bool RequiresNativeShippingDepositProof,
    [property: JsonPropertyName("requires_native_shipping_settlement_proof")]
        bool RequiresNativeShippingSettlementProof,
    [property: JsonPropertyName("existing_native_episode_reusable")]
        bool ExistingNativeEpisodeReusable,
    [property: JsonPropertyName("next_action")] string NextAction);
