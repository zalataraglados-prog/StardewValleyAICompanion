using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public sealed class MachineRelocationIntent
{
    [JsonPropertyName("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = StrategyCommitmentStatuses.Active;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("source_location_id")]
    public string SourceLocationId { get; set; } = string.Empty;

    [JsonPropertyName("source_tile_x")]
    public int SourceTileX { get; set; }

    [JsonPropertyName("source_tile_y")]
    public int SourceTileY { get; set; }

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int TargetTileY { get; set; }

    [JsonPropertyName("machine_placement_projection_fingerprint")]
    public string MachinePlacementProjectionFingerprint { get; set; } =
        string.Empty;

    [JsonPropertyName("layout_net_benefit_ticks")]
    public int LayoutNetBenefitTicks { get; set; }

    [JsonPropertyName("completion_reason")]
    public string CompletionReason { get; set; } = string.Empty;
}

public sealed class MachineRelocationIntentUpsertRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("source_location_id")]
    public string SourceLocationId { get; set; } = string.Empty;

    [JsonPropertyName("source_tile_x")]
    public int SourceTileX { get; set; }

    [JsonPropertyName("source_tile_y")]
    public int SourceTileY { get; set; }

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int TargetTileY { get; set; }

    [JsonPropertyName("machine_placement_projection_fingerprint")]
    public string MachinePlacementProjectionFingerprint { get; set; } =
        string.Empty;

    [JsonPropertyName("layout_net_benefit_ticks")]
    public int LayoutNetBenefitTicks { get; set; }
}
