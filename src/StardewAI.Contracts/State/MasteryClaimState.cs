using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State;

public sealed class MasteryClaimProjectionRef
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "mastery_claim.v1";

    [JsonPropertyName("projection_status")]
    public string ProjectionStatus { get; set; } = "unavailable";

    [JsonPropertyName("invocation_policy")]
    public string InvocationPolicy { get; set; } = "autonomous_strategic_choice";

    [JsonPropertyName("native_contract")]
    public string NativeContract { get; set; } = string.Empty;

    [JsonPropertyName("target_location_id")]
    public string TargetLocationId { get; set; } = "MasteryCave";

    [JsonPropertyName("current_location_matches")]
    public bool CurrentLocationMatches { get; set; }

    [JsonPropertyName("menu_clear")]
    public bool MenuClear { get; set; }

    [JsonPropertyName("all_base_skills_level_ten")]
    public bool AllBaseSkillsLevelTen { get; set; }

    [JsonPropertyName("mastery_experience")]
    public int MasteryExperience { get; set; }

    [JsonPropertyName("current_mastery_level")]
    public int CurrentMasteryLevel { get; set; }

    [JsonPropertyName("mastery_levels_spent")]
    public int MasteryLevelsSpent { get; set; }

    [JsonPropertyName("unspent_mastery_levels")]
    public int UnspentMasteryLevels { get; set; }

    [JsonPropertyName("all_plaques_completed")]
    public bool AllPlaquesCompleted { get; set; }

    [JsonPropertyName("trinket_slots")]
    public int TrinketSlots { get; set; }

    [JsonPropertyName("skills")]
    public MasteryClaimOptionRef[] Skills { get; set; } = Array.Empty<MasteryClaimOptionRef>();

    [JsonPropertyName("claimable_options")]
    public MasteryClaimOptionRef[] ClaimableOptions { get; set; } = Array.Empty<MasteryClaimOptionRef>();

    [JsonPropertyName("game_id")]
    public ulong GameId { get; set; }

    [JsonPropertyName("player_id")]
    public long PlayerId { get; set; }

    [JsonPropertyName("projection_fingerprint")]
    public string ProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("service_status")]
    public string ServiceStatus { get; set; } = "unavailable";

    [JsonPropertyName("blocked_diagnostics")]
    public string[] BlockedDiagnostics { get; set; } = Array.Empty<string>();
}

public sealed class MasteryClaimOptionRef
{
    [JsonPropertyName("skill_id")]
    public int SkillId { get; set; }

    [JsonPropertyName("skill_key")]
    public string SkillKey { get; set; } = string.Empty;

    [JsonPropertyName("skill_level")]
    public int SkillLevel { get; set; }

    [JsonPropertyName("mastery_stat_key")]
    public string MasteryStatKey { get; set; } = string.Empty;

    [JsonPropertyName("mastery_stat_value")]
    public int MasteryStatValue { get; set; }

    [JsonPropertyName("claimed")]
    public bool Claimed { get; set; }

    [JsonPropertyName("claimable")]
    public bool Claimable { get; set; }

    [JsonPropertyName("action_tile")]
    public MasteryClaimActionTileRef? ActionTile { get; set; }

    [JsonPropertyName("direct_rewards")]
    public MasteryClaimDirectRewardRef[] DirectRewards { get; set; } = Array.Empty<MasteryClaimDirectRewardRef>();

    [JsonPropertyName("recipe_rewards")]
    public MasteryClaimRecipeRewardRef[] RecipeRewards { get; set; } = Array.Empty<MasteryClaimRecipeRewardRef>();

    [JsonPropertyName("grants_trinket_slot")]
    public bool GrantsTrinketSlot { get; set; }

    [JsonPropertyName("option_fingerprint")]
    public string OptionFingerprint { get; set; } = string.Empty;
}

public sealed class MasteryClaimActionTileRef
{
    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = "MasteryCave";

    [JsonPropertyName("tile_x")]
    public int TileX { get; set; }

    [JsonPropertyName("tile_y")]
    public int TileY { get; set; }

    [JsonPropertyName("action_raw")]
    public string ActionRaw { get; set; } = string.Empty;
}

public sealed class MasteryClaimDirectRewardRef
{
    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("stack")]
    public int Stack { get; set; }

    [JsonPropertyName("runtime_type")]
    public string RuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("delivery_policy")]
    public string DeliveryPolicy { get; set; } = "inventory_else_debris";

    [JsonPropertyName("inventory_count_before")]
    public int InventoryCountBefore { get; set; }

    [JsonPropertyName("mastery_cave_debris_count_before")]
    public int MasteryCaveDebrisCountBefore { get; set; }
}

public sealed class MasteryClaimRecipeRewardRef
{
    [JsonPropertyName("recipe_name")]
    public string RecipeName { get; set; } = string.Empty;

    [JsonPropertyName("known_before")]
    public bool KnownBefore { get; set; }
}
