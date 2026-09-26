using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FullShipmentTerminalSettlementReceipt
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "full_shipment_terminal_settlement_receipt.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("training_label_eligible")]
    public bool TrainingLabelEligible { get; set; }

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; set; } = string.Empty;

    [JsonPropertyName("queue_sha256")]
    public string QueueSha256 { get; set; } = string.Empty;

    [JsonPropertyName("before_snapshot_sha256")]
    public string BeforeSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("execution_receipt_sha256")]
    public string ExecutionReceiptSha256 { get; set; } = string.Empty;

    [JsonPropertyName("after_snapshot_sha256")]
    public string AfterSnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("transition_evidence")]
    public FullShipmentTerminalSettlementEvidence TransitionEvidence
    {
        get;
        set;
    } = new();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class FullShipmentTerminalSettlementEvidence
{
    [JsonPropertyName("required_item_count")]
    public int RequiredItemCount { get; set; }

    [JsonPropertyName("terminal_item_id")]
    public string TerminalItemId { get; set; } = string.Empty;

    [JsonPropertyName("terminal_qualified_item_id")]
    public string TerminalQualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("before_shipped_item_count")]
    public int? BeforeShippedItemCount { get; set; }

    [JsonPropertyName("after_shipped_item_count")]
    public int? AfterShippedItemCount { get; set; }

    [JsonPropertyName("before_terminal_shipped_count")]
    public int? BeforeTerminalShippedCount { get; set; }

    [JsonPropertyName("after_terminal_shipped_count")]
    public int? AfterTerminalShippedCount { get; set; }

    [JsonPropertyName("before_terminal_bin_count")]
    public int? BeforeTerminalBinCount { get; set; }

    [JsonPropertyName("after_terminal_bin_count")]
    public int? AfterTerminalBinCount { get; set; }

    [JsonPropertyName("before_total_day")]
    public int? BeforeTotalDay { get; set; }

    [JsonPropertyName("after_total_day")]
    public int? AfterTotalDay { get; set; }

    [JsonPropertyName("achievement_34_before")]
    public bool? Achievement34Before { get; set; }

    [JsonPropertyName("achievement_34_after")]
    public bool? Achievement34After { get; set; }

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}
