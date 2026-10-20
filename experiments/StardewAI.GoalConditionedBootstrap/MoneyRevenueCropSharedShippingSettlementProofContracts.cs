using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class MoneyRevenueCropSharedShippingSettlementProofRequest
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_shared_shipping_settlement_proof_request.v2";

    [JsonPropertyName("crop_production_plan_path")]
    public string CropProductionPlanPath { get; set; } = string.Empty;

    [JsonPropertyName("settlement_capacity_proof_path")]
    public string SettlementCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_start_snapshot_path")]
    public string TerminalStartSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("route_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[] RouteTransitions
        { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();

    [JsonPropertyName("waves")]
    public MoneyRevenueCropSharedShippingWaveRequest[] Waves { get; set; } =
        Array.Empty<MoneyRevenueCropSharedShippingWaveRequest>();

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

public sealed class MoneyRevenueCropSharedShippingWaveRequest
{
    [JsonPropertyName("output_retention_proof_path")]
    public string OutputRetentionProofPath { get; set; } = string.Empty;

    [JsonPropertyName("shipping_deposit_transitions")]
    public MoneyRevenueTerminalQueueTransitionProof[]
        ShippingDepositTransitions { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();
}

public sealed class MoneyRevenueCropSharedShippingSettlementProof
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "money_revenue_crop_shared_shipping_settlement_proof.v2";

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

    [JsonPropertyName("settlement_capacity_proof_path")]
    public string SettlementCapacityProofPath { get; set; } = string.Empty;

    [JsonPropertyName("settlement_capacity_proof_sha256")]
    public string SettlementCapacityProofSha256 { get; set; } = string.Empty;

    [JsonPropertyName("harvest_total_day")]
    public int HarvestTotalDay { get; set; }

    [JsonPropertyName("terminal_start_snapshot_path")]
    public string TerminalStartSnapshotPath { get; set; } = string.Empty;

    [JsonPropertyName("terminal_start_snapshot_sha256")]
    public string TerminalStartSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("terminal_start_state_hash")]
    public string TerminalStartStateHash { get; set; } = string.Empty;

    [JsonPropertyName("covered_terminal_request_ids")]
    public string[] CoveredTerminalRequestIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("route_transitions")]
    public MoneyRevenueTerminalQueueTransitionEvidence[] RouteTransitions
        { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>();

    [JsonPropertyName("waves")]
    public MoneyRevenueCropSharedShippingWaveEvidence[] Waves { get; set; } =
        Array.Empty<MoneyRevenueCropSharedShippingWaveEvidence>();

    [JsonPropertyName("settlement_route_transitions")]
    public MoneyRevenueTerminalQueueTransitionEvidence[]
        SettlementRouteTransitions { get; set; } =
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>();

    [JsonPropertyName("settled_quantity")]
    public int SettledQuantity { get; set; }

    [JsonPropertyName("settled_revenue")]
    public long SettledRevenue { get; set; }

    [JsonPropertyName("terminal_action_after_state_hash")]
    public string TerminalActionAfterStateHash { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_manifest_path")]
    public string RevenueRecurrenceManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_manifest_sha256")]
    public string RevenueRecurrenceManifestSha256 { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_path")]
    public string RevenueRecurrenceReceiptPath { get; set; } = string.Empty;

    [JsonPropertyName("revenue_recurrence_receipt_sha256")]
    public string RevenueRecurrenceReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("shared_native_shipping_settlement_verified")]
    public bool SharedNativeShippingSettlementVerified { get; set; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; set; }

    [JsonPropertyName("proof_policy")]
    public string ProofPolicy { get; set; } =
        "This proof closes every output-retention-terminal request on one exact " +
        "multi-wave harvest day only after independently rebuilding the crop " +
        "plan, aggregate settlement capacity and every retained-output proof. " +
        "The exact native terminal snapshot must contain the aggregate " +
        "conservative inventory denominator. One shared route, a contiguous " +
        "single-item native deposit chain, an explicit resource-neutral route " +
        "home and one native overnight Revenue " +
        "recurrence carry the pending value from zero through all ordered waves. " +
        "No support artifact alone closes a request, and formal product training " +
        "remains a separate gate.";
}

public sealed record MoneyRevenueCropSharedShippingWaveEvidence(
    [property: JsonPropertyName("evidence_request_id")]
        string EvidenceRequestId,
    [property: JsonPropertyName("commitment_id")] string CommitmentId,
    [property: JsonPropertyName("wave_index")] int WaveIndex,
    [property: JsonPropertyName("output_retention_proof_path")]
        string OutputRetentionProofPath,
    [property: JsonPropertyName("output_retention_proof_sha256")]
        string OutputRetentionProofSha256,
    [property: JsonPropertyName("harvest_item_qualified_id")]
        string HarvestItemQualifiedId,
    [property: JsonPropertyName("minimum_units")] int MinimumUnits,
    [property: JsonPropertyName("minimum_gross_revenue")]
        long MinimumGrossRevenue,
    [property: JsonPropertyName("slot_index")] int SlotIndex,
    [property: JsonPropertyName("unit_price")] int UnitPrice,
    [property: JsonPropertyName("pending_value_before")]
        long PendingValueBefore,
    [property: JsonPropertyName("pending_value_after")]
        long PendingValueAfter,
    [property: JsonPropertyName("shipping_deposits")]
        MoneyRevenueCropShippingDepositEvidence[] ShippingDeposits);
