using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Strategy;

public static class MachineSupportIntentStages
{
    public const string CraftSelected = "craft_selected";
    public const string PlacementBound = "placement_bound";
}

public sealed class MachineSupportIntent
{
    [JsonPropertyName("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [JsonPropertyName("revision")]
    public int Revision { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } =
        StrategyCommitmentStatuses.Active;

    [JsonPropertyName("stage")]
    public string Stage { get; set; } =
        MachineSupportIntentStages.CraftSelected;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("source_state_hash")]
    public string SourceStateHash { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("demand_class")]
    public string DemandClass { get; set; } = string.Empty;

    [JsonPropertyName("support_kind")]
    public string SupportKind { get; set; } = string.Empty;

    [JsonPropertyName("evidence_status")]
    public string EvidenceStatus { get; set; } = string.Empty;

    [JsonPropertyName("gross_benefit")]
    public int GrossBenefit { get; set; }

    [JsonPropertyName("opportunity_cost")]
    public int OpportunityCost { get; set; }

    [JsonPropertyName("net_benefit")]
    public int NetBenefit { get; set; }

    [JsonPropertyName("support_score")]
    public double SupportScore { get; set; }

    [JsonPropertyName("required_additional_machine_count")]
    public int RequiredAdditionalMachineCount { get; set; }

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }

    [JsonPropertyName("completion_reason")]
    public string CompletionReason { get; set; } = string.Empty;
}

public sealed class MachineSupportIntentUpsertRequest
{
    [JsonPropertyName("state_hash")]
    public string StateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [JsonPropertyName("stage")]
    public string Stage { get; set; } = string.Empty;

    [JsonPropertyName("source_decision_id")]
    public string SourceDecisionId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("demand_class")]
    public string DemandClass { get; set; } = string.Empty;

    [JsonPropertyName("support_kind")]
    public string SupportKind { get; set; } = string.Empty;

    [JsonPropertyName("evidence_status")]
    public string EvidenceStatus { get; set; } = string.Empty;

    [JsonPropertyName("gross_benefit")]
    public int GrossBenefit { get; set; }

    [JsonPropertyName("opportunity_cost")]
    public int OpportunityCost { get; set; }

    [JsonPropertyName("net_benefit")]
    public int NetBenefit { get; set; }

    [JsonPropertyName("support_score")]
    public double SupportScore { get; set; }

    [JsonPropertyName("required_additional_machine_count")]
    public int RequiredAdditionalMachineCount { get; set; }

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("target_tile_x")]
    public int? TargetTileX { get; set; }

    [JsonPropertyName("target_tile_y")]
    public int? TargetTileY { get; set; }
}
