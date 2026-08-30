using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class AdventureGuildRewardProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "adventure_guild_reward.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("invocation_policy")]
    public string InvocationPolicy { get; set; } = "autonomous_positive_reward";

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = "AdventureGuild";

    [JsonPropertyName("current_location_matches")]
    public bool CurrentLocationMatches { get; set; }

    [JsonPropertyName("action_tile_x")]
    public int? ActionTileX { get; set; }

    [JsonPropertyName("action_tile_y")]
    public int? ActionTileY { get; set; }

    [JsonPropertyName("action_tile_index")]
    public int? ActionTileIndex { get; set; }

    [JsonPropertyName("stand_tile_x")]
    public int? StandTileX { get; set; }

    [JsonPropertyName("stand_tile_y")]
    public int? StandTileY { get; set; }

    [JsonPropertyName("menu_clear")]
    public bool MenuClear { get; set; }

    [JsonPropertyName("batch_fingerprint")]
    public string BatchFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("pending_goal_count")]
    public int PendingGoalCount { get; set; }

    [JsonPropertyName("reward_item_count")]
    public int RewardItemCount { get; set; }

    [JsonPropertyName("reward_dialogue_count")]
    public int RewardDialogueCount { get; set; }

    [JsonPropertyName("inventory_max_items")]
    public int InventoryMaxItems { get; set; }

    [JsonPropertyName("inventory_occupied_slots")]
    public int InventoryOccupiedSlots { get; set; }

    [JsonPropertyName("inventory_capacity_sufficient")]
    public bool InventoryCapacitySufficient { get; set; }

    [JsonPropertyName("goals")]
    public AdventureGuildRewardGoalRef[] Goals { get; set; } = System.Array.Empty<AdventureGuildRewardGoalRef>();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();
}

public sealed class AdventureGuildRewardGoalRef
{
    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("targets")]
    public string[] Targets { get; set; } = System.Array.Empty<string>();

    [JsonPropertyName("required_kills")]
    public int RequiredKills { get; set; }

    [JsonPropertyName("current_kills")]
    public int CurrentKills { get; set; }

    [JsonPropertyName("complete")]
    public bool Complete { get; set; }

    [JsonPropertyName("collected")]
    public bool Collected { get; set; }

    [JsonPropertyName("gil_mail_flag")]
    public string GilMailFlag { get; set; } = string.Empty;

    [JsonPropertyName("reward_item_id")]
    public string RewardItemId { get; set; } = string.Empty;

    [JsonPropertyName("reward_item_runtime_type")]
    public string RewardItemRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("reward_item_stack")]
    public int RewardItemStack { get; set; }

    [JsonPropertyName("reward_item_quality")]
    public int RewardItemQuality { get; set; }

    [JsonPropertyName("reward_item_special_variable")]
    public int RewardItemSpecialVariable { get; set; }

    [JsonPropertyName("reward_item_special_item")]
    public bool RewardItemSpecialItem { get; set; }

    [JsonPropertyName("reward_dialogue")]
    public string RewardDialogue { get; set; } = string.Empty;

    [JsonPropertyName("reward_dialogue_flag")]
    public string RewardDialogueFlag { get; set; } = string.Empty;

    [JsonPropertyName("reward_dialogue_should_show")]
    public bool RewardDialogueShouldShow { get; set; }

    [JsonPropertyName("reward_mail")]
    public string RewardMail { get; set; } = string.Empty;

    [JsonPropertyName("reward_mail_all")]
    public string RewardMailAll { get; set; } = string.Empty;

    [JsonPropertyName("reward_flag")]
    public string RewardFlag { get; set; } = string.Empty;

    [JsonPropertyName("reward_flag_all")]
    public string RewardFlagAll { get; set; } = string.Empty;
}
