using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropTerminalSettlementProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_terminal_settlement_proof_request.v3";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("output_retention_proof_path")]
    public string OutputRetentionProofPath { get; set; } = string.Empty;

    [JsonPropertyName("route_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[] RouteTransitions
        { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();

    [JsonPropertyName("shop_sale_episode_path")]
    public string ShopSaleEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("shipping_deposit_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[]
        ShippingDepositTransitions { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();

    [JsonPropertyName("settlement_route_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[]
        SettlementRouteTransitions { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();

    [JsonPropertyName("revenue_recurrence_manifest_path")]
    public string RevenueRecurrenceManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_path")]
    public string RevenueRecurrenceReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }
}

public sealed class MoneyRevenueCropTerminalSettlementProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_terminal_settlement_proof.v3";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("request_path")]
    public string RequestPath { get; set; } = string.Empty;

    [JsonPropertyName("request_sha256")]
    public string RequestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("crop_production_plan_sha256")]
    public string CropProductionPlanSha256 { get; set; } = string.Empty;

    [JsonPropertyName("output_retention_proof_path")]
    public string OutputRetentionProofPath { get; set; } = string.Empty;

    [JsonPropertyName("output_retention_proof_sha256")]
    public string OutputRetentionProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("evidence_request_id")]
    public string EvidenceRequestId { get; set; } = string.Empty;

    [JsonPropertyName("commitment_id")]
    public string CommitmentId { get; set; } = string.Empty;

    [JsonPropertyName("wave_index")]
    public int WaveIndex { get; set; }

    [JsonPropertyName("harvest_item_qualified_id")]
    public string HarvestItemQualifiedId { get; set; } = string.Empty;

    [JsonPropertyName("minimum_units")]
    public int MinimumUnits { get; set; }

    [JsonPropertyName("minimum_gross_revenue")]
    public long MinimumGrossRevenue { get; set; }

    [JsonPropertyName("terminal_kind")]
    public string TerminalKind { get; set; } = "native_shop_sale";

    [JsonPropertyName("terminal_start_state_hash")]
    public string TerminalStartStateHash { get; set; } = string.Empty;

    [JsonPropertyName("terminal_action_before_state_hash")]
    public string TerminalActionBeforeStateHash { get; set; } = string.Empty;

    [JsonPropertyName("terminal_action_after_state_hash")]
    public string TerminalActionAfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("route_transitions")]
    public MoneyRevenueTerminalQueueTransitionEvidence[] RouteTransitions
        { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>();

    [JsonPropertyName("shop_sale_episode_path")]
    public string ShopSaleEpisodePath { get; set; } = string.Empty;

    [JsonPropertyName("shop_sale_episode_sha256")]
    public string ShopSaleEpisodeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("shop_id")]
    public string ShopId { get; set; } = string.Empty;

    [JsonPropertyName("shipping_deposits")]
    public MoneyRevenueCropShippingDepositEvidence[] ShippingDeposits
        { get; set; } =
            Array.Empty<MoneyRevenueCropShippingDepositEvidence>();

    [JsonPropertyName("settlement_route_transitions")]
    public MoneyRevenueTerminalQueueTransitionEvidence[]
        SettlementRouteTransitions { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>();

    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("settled_quantity")]
    public int SettledQuantity { get; set; }

    [JsonPropertyName("unit_price")]
    public int UnitPrice { get; set; }

    [JsonPropertyName("settled_revenue")]
    public int SettledRevenue { get; set; }

    [JsonPropertyName("revenue_recurrence_manifest_path")]
    public string RevenueRecurrenceManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_manifest_sha256")]
    public string RevenueRecurrenceManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_path")]
    public string RevenueRecurrenceReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_sha256")]
    public string RevenueRecurrenceReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("native_terminal_settlement_verified")]
    public bool NativeTerminalSettlementVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "This proof closes one output-retention-terminal request only after " +
        "independently rebuilding its crop plan and retained-output support, " +
        "then verifying exactly one native settlement mode: either a resource-" +
        "neutral route plus an exact ShopMenu sale, or one exact native shipping " +
        "deposit per conservative unit, an explicit resource-neutral route home, " +
        "and one native overnight settlement. The single-transition Revenue " +
        "recurrence must rebuild the " +
        "same terminal outcome. Retained output alone is supporting evidence and " +
        "cannot close the request. Aggregate settlement capacity, production " +
        "collection and formal product training remain separate gates.";
}

public sealed record MoneyRevenueCropShippingDepositEvidence(
    [property: JsonPropertyName("transition_index")] int TransitionIndex,
    [property: JsonPropertyName("transition_id")] string TransitionId,
    [property: JsonPropertyName("queue_sha256")] string QueueSha256,
    [property: JsonPropertyName("before_snapshot_sha256")]
        string BeforeSnapshotSha256,
    [property: JsonPropertyName("execution_receipt_sha256")]
        string ExecutionReceiptSha256,
    [property: JsonPropertyName("after_snapshot_sha256")]
        string AfterSnapshotSha256,
    [property: JsonPropertyName("source_state_hash")] string SourceStateHash,
    [property: JsonPropertyName("after_state_hash")] string AfterStateHash,
    [property: JsonPropertyName("queue_item_id")] string QueueItemId,
    [property: JsonPropertyName("slot_index")] int SlotIndex,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unit_price")] int UnitPrice,
    [property: JsonPropertyName("pending_value_before")] long PendingValueBefore,
    [property: JsonPropertyName("pending_value_after")] long PendingValueAfter,
    [property: JsonPropertyName("option_ids")] string[] OptionIds);
