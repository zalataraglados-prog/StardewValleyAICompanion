using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class MaterialOutputCustodyPositionTransition
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_output_custody_position_transition.v1";
    [JsonPropertyName("initial_position_sha256")] public string InitialPositionSha256 { get; set; } = string.Empty;
    [JsonPropertyName("previous_position_sha256")] public string PreviousPositionSha256 { get; set; } = string.Empty;
    [JsonPropertyName("before_node_id")] public string BeforeNodeId { get; set; } = string.Empty;
    [JsonPropertyName("before_slot_index")] public int BeforeSlotIndex { get; set; }
    [JsonPropertyName("before_state_hash")] public string BeforeStateHash { get; set; } = string.Empty;
    [JsonPropertyName("before_reservation_revision")] public int BeforeReservationRevision { get; set; }
    [JsonPropertyName("after_node_id")] public string AfterNodeId { get; set; } = string.Empty;
    [JsonPropertyName("after_slot_index")] public int AfterSlotIndex { get; set; }
    [JsonPropertyName("after_state_hash")] public string AfterStateHash { get; set; } = string.Empty;
    [JsonPropertyName("after_reservation_revision")] public int AfterReservationRevision { get; set; }
    [JsonPropertyName("prepared_portfolio_id")] public string PreparedPortfolioId { get; set; } = string.Empty;
    [JsonPropertyName("prepared_route_source_decision_id")] public string PreparedRouteSourceDecisionId { get; set; } = string.Empty;
    [JsonPropertyName("prepared_ledger_revision")] public int PreparedLedgerRevision { get; set; }
    [JsonPropertyName("prepared_source_state_hash")] public string PreparedSourceStateHash { get; set; } = string.Empty;
    [JsonPropertyName("prepared_transfer_sha256")] public string PreparedTransferSha256 { get; set; } = string.Empty;
    [JsonPropertyName("supporting_transition_receipt_sha256")] public string SupportingTransitionReceiptSha256 { get; set; } = string.Empty;
    [JsonPropertyName("binding_sha256")] public string BindingSha256 { get; set; } = string.Empty;
}
