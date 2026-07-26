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

    [JsonPropertyName("route_connector_count")]
    public int RouteConnectorCount { get; set; }

    [JsonPropertyName("route_connector_kind")]
    public string RouteConnectorKind { get; set; } = string.Empty;

    [JsonPropertyName("route_estimated_ticks")]
    public int RouteEstimatedTicks { get; set; }

    [JsonPropertyName("route_segments")]
    public MachineRelocationRouteSegment[] RouteSegments { get; set; } =
        [];

    [JsonPropertyName("target_arrival_tile_x")]
    public int TargetArrivalTileX { get; set; }

    [JsonPropertyName("target_arrival_tile_y")]
    public int TargetArrivalTileY { get; set; }

    [JsonPropertyName("target_stand_tile_x")]
    public int TargetStandTileX { get; set; }

    [JsonPropertyName("target_stand_tile_y")]
    public int TargetStandTileY { get; set; }

    [JsonPropertyName("target_route_distance_tiles")]
    public int TargetRouteDistanceTiles { get; set; }

    [JsonPropertyName("layout_relocation_cost_ticks")]
    public int LayoutRelocationCostTicks { get; set; }

    [JsonPropertyName("layout_benefit_policy")]
    public string LayoutBenefitPolicy { get; set; } = string.Empty;

    [JsonPropertyName("target_selection_policy")]
    public string TargetSelectionPolicy { get; set; } = string.Empty;

    [JsonPropertyName("time_estimate_policy")]
    public string TimeEstimatePolicy { get; set; } = string.Empty;

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

    [JsonPropertyName("route_connector_count")]
    public int RouteConnectorCount { get; set; }

    [JsonPropertyName("route_connector_kind")]
    public string RouteConnectorKind { get; set; } = string.Empty;

    [JsonPropertyName("route_estimated_ticks")]
    public int RouteEstimatedTicks { get; set; }

    [JsonPropertyName("route_segments")]
    public MachineRelocationRouteSegment[] RouteSegments { get; set; } =
        [];

    [JsonPropertyName("target_arrival_tile_x")]
    public int TargetArrivalTileX { get; set; }

    [JsonPropertyName("target_arrival_tile_y")]
    public int TargetArrivalTileY { get; set; }

    [JsonPropertyName("target_stand_tile_x")]
    public int TargetStandTileX { get; set; }

    [JsonPropertyName("target_stand_tile_y")]
    public int TargetStandTileY { get; set; }

    [JsonPropertyName("target_route_distance_tiles")]
    public int TargetRouteDistanceTiles { get; set; }

    [JsonPropertyName("layout_relocation_cost_ticks")]
    public int LayoutRelocationCostTicks { get; set; }

    [JsonPropertyName("layout_benefit_policy")]
    public string LayoutBenefitPolicy { get; set; } = string.Empty;

    [JsonPropertyName("target_selection_policy")]
    public string TargetSelectionPolicy { get; set; } = string.Empty;

    [JsonPropertyName("time_estimate_policy")]
    public string TimeEstimatePolicy { get; set; } = string.Empty;
}

public sealed class MachineRelocationRouteSegment
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("from_location_id")]
    public string FromLocationId { get; set; } = string.Empty;

    [JsonPropertyName("from_tile_x")]
    public int FromTileX { get; set; }

    [JsonPropertyName("from_tile_y")]
    public int FromTileY { get; set; }

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("arrival_tile_x")]
    public int ArrivalTileX { get; set; }

    [JsonPropertyName("arrival_tile_y")]
    public int ArrivalTileY { get; set; }

    [JsonPropertyName("approach_distance_tiles")]
    public int ApproachDistanceTiles { get; set; }

    [JsonPropertyName("estimated_ticks")]
    public int EstimatedTicks { get; set; }
}
