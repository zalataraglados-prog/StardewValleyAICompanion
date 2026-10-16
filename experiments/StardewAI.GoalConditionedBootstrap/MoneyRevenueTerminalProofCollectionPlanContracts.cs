using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTerminalProofCollectionModes
{
    public const string ReuseVerifiedShopSaleEpisode =
        "reuse_verified_shop_sale_episode";
    public const string CollectNativeShopSale = "collect_native_shop_sale";
    public const string CollectNativeShippingSettlement =
        "collect_native_shipping_settlement";
}

public sealed class MoneyRevenueTerminalProofCollectionPlanRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_collection_plan_request.v1";

    [JsonPropertyName("comparison_collection_plan_path")]
    public string ComparisonCollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("counterfactual_request_manifest_path")]
    public string CounterfactualRequestManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_path")]
    public string CounterfactualRequestCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueTerminalProofCollectionPlan
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_terminal_proof_collection_plan.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("comparison_collection_plan_path")]
    public string ComparisonCollectionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("comparison_collection_plan_sha256")]
    public string ComparisonCollectionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("counterfactual_request_manifest_path")]
    public string CounterfactualRequestManifestPath { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_manifest_sha256")]
    public string CounterfactualRequestManifestSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_path")]
    public string CounterfactualRequestCatalogPath { get; set; } =
        string.Empty;

    [JsonPropertyName("counterfactual_request_catalog_sha256")]
    public string CounterfactualRequestCatalogSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("dataset_partition")]
    public string DatasetPartition { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }

    [JsonPropertyName("request_ids")]
    public string[] RequestIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("offline_reuse_ready_count")]
    public int OfflineReuseReadyCount { get; set; }

    [JsonPropertyName("native_collection_required_count")]
    public int NativeCollectionRequiredCount { get; set; }

    [JsonPropertyName("work_items")]
    public MoneyRevenueTerminalProofCollectionWorkItem[] WorkItems
        { get; set; } =
        Array.Empty<MoneyRevenueTerminalProofCollectionWorkItem>();

    [JsonPropertyName("terminal_proof_index_ready")]
    public bool TerminalProofIndexReady { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("plan_policy")]
    public string PlanPolicy { get; set; } =
        "Every authoritative counterfactual request appears exactly once. Only the exact " +
        "already verified native shop-sale episode may enter the offline reuse path. All " +
        "other shop-sale and shipping alternatives remain native collection work. A plan " +
        "schedules evidence collection but is not a terminal proof index, Teacher label, " +
        "production evidence or formal training authorization.";
}

public sealed record MoneyRevenueTerminalProofCollectionWorkItem(
    [property: JsonPropertyName("ordinal")] int Ordinal,
    [property: JsonPropertyName("request_id")] string RequestId,
    [property: JsonPropertyName("request_sha256")] string RequestSha256,
    [property: JsonPropertyName("concrete_alternative_id")]
        string ConcreteAlternativeId,
    [property: JsonPropertyName("concrete_alternative_sha256")]
        string ConcreteAlternativeSha256,
    [property: JsonPropertyName("terminal_state_hash")]
        string TerminalStateHash,
    [property: JsonPropertyName("terminal_snapshot_path")]
        string TerminalSnapshotPath,
    [property: JsonPropertyName("terminal_snapshot_sha256")]
        string TerminalSnapshotSha256,
    [property: JsonPropertyName("terminal_option_id")]
        string TerminalOptionId,
    [property: JsonPropertyName("terminal_catalog_candidate_id")]
        string TerminalCatalogCandidateId,
    [property: JsonPropertyName("terminal_candidate_sha256")]
        string TerminalCandidateSha256,
    [property: JsonPropertyName("terminal_strategy_ledger_path")]
        string TerminalStrategyLedgerPath,
    [property: JsonPropertyName("terminal_strategy_ledger_sha256")]
        string TerminalStrategyLedgerSha256,
    [property: JsonPropertyName("terminal_strategy_ledger_id")]
        string TerminalStrategyLedgerId,
    [property: JsonPropertyName("terminal_strategy_ledger_revision")]
        int TerminalStrategyLedgerRevision,
    [property: JsonPropertyName("acquisition_rollout_proof_required")]
        bool AcquisitionRolloutProofRequired,
    [property: JsonPropertyName("required_revenue_transition_proof_kind")]
        string RequiredRevenueTransitionProofKind,
    [property: JsonPropertyName("collection_mode")]
        string CollectionMode,
    [property: JsonPropertyName("reusable_native_episode_path")]
        string ReusableNativeEpisodePath,
    [property: JsonPropertyName("reusable_native_episode_sha256")]
        string ReusableNativeEpisodeSha256,
    [property: JsonPropertyName("required_phases")]
        string[] RequiredPhases,
    [property: JsonPropertyName("status")] string Status);

internal sealed record MoneyRevenueReusableShopSaleEpisode(
    string Path,
    string Sha256,
    string BeforeStateHash,
    string ShopId,
    string QualifiedItemId,
    int SlotIndex,
    int Quantity,
    int UnitPrice);
