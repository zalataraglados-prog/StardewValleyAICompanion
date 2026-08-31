using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class PrizeTicketRewardProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "prize_ticket_reward.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("invocation_policy")]
    public string InvocationPolicy { get; set; } = "autonomous_positive_reward";

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("stage")]
    public string Stage { get; set; } = "none";

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("current_location_matches")]
    public bool CurrentLocationMatches { get; set; }

    [JsonPropertyName("menu_clear")]
    public bool MenuClear { get; set; }

    [JsonPropertyName("inventory_ticket_count")]
    public int InventoryTicketCount { get; set; }

    [JsonPropertyName("pending_special_order_ticket_count")]
    public int PendingSpecialOrderTicketCount { get; set; }

    [JsonPropertyName("available_ticket_count")]
    public int AvailableTicketCount { get; set; }

    [JsonPropertyName("ticket_prizes_claimed")]
    public int TicketPrizesClaimed { get; set; }

    [JsonPropertyName("current_prize_level")]
    public int CurrentPrizeLevel { get; set; }

    [JsonPropertyName("current_reward_fingerprint")]
    public string CurrentRewardFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("current_reward")]
    public PrizeTicketRewardItemRef? CurrentReward { get; set; }

    [JsonPropertyName("preview_track")]
    public PrizeTicketRewardItemRef[] PreviewTrack { get; set; } = Array.Empty<PrizeTicketRewardItemRef>();

    [JsonPropertyName("prize_machine_action_tiles")]
    public PrizeTicketActionTileRef[] PrizeMachineActionTiles { get; set; } = Array.Empty<PrizeTicketActionTileRef>();

    [JsonPropertyName("special_order_ticket_action_tiles")]
    public PrizeTicketActionTileRef[] SpecialOrderTicketActionTiles { get; set; } = Array.Empty<PrizeTicketActionTileRef>();

    [JsonPropertyName("inventory_max_items")]
    public int InventoryMaxItems { get; set; }

    [JsonPropertyName("inventory_occupied_slots")]
    public int InventoryOccupiedSlots { get; set; }

    [JsonPropertyName("pending_ticket_capacity_sufficient")]
    public bool PendingTicketCapacitySufficient { get; set; }

    [JsonPropertyName("reward_delivery_policy")]
    public string RewardDeliveryPolicy { get; set; } = "inventory_else_debris";

    [JsonPropertyName("game_id")]
    public ulong GameId { get; set; }

    [JsonPropertyName("player_id")]
    public long PlayerId { get; set; }

    [JsonPropertyName("house_upgrade_level")]
    public int HouseUpgradeLevel { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day_of_month")]
    public int DayOfMonth { get; set; }

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("service_status")]
    public string ServiceStatus { get; set; } = "unavailable";

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = Array.Empty<string>();
}

public sealed class PrizeTicketRewardItemRef
{
    [JsonPropertyName("prize_level")]
    public int PrizeLevel { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("stack")]
    public int Stack { get; set; }

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("runtime_type")]
    public string RuntimeType { get; set; } = string.Empty;
}

public sealed class PrizeTicketActionTileRef
{
    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("action_raw")]
    public string ActionRaw { get; set; } = string.Empty;
}
