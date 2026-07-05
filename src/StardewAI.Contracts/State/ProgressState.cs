using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class QuestProgressRef
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("current_objective")]
        public string? CurrentObjective { get; set; }

        [JsonPropertyName("quest_type")]
        public int QuestType { get; set; }

        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("daily_quest")]
        public bool DailyQuest { get; set; }

        [JsonPropertyName("days_left")]
        public int DaysLeft { get; set; }

        [JsonPropertyName("money_reward")]
        public int MoneyReward { get; set; }
    }

    public sealed class CompletedQuestProgressRef
    {
        [JsonPropertyName("total_count")]
        public uint TotalCount { get; set; }

        [JsonPropertyName("retained_completed_quests")]
        public QuestProgressRef[] RetainedCompletedQuests { get; set; } = new QuestProgressRef[0];

        [JsonPropertyName("history_identity_available")]
        public bool HistoryIdentityAvailable { get; set; }

        [JsonPropertyName("history_identity_source")]
        public string HistoryIdentitySource { get; set; } = string.Empty;
    }

    public sealed class SpecialOrderProgressRef
    {
        [JsonPropertyName("quest_key")]
        public string? QuestKey { get; set; }

        [JsonPropertyName("quest_name")]
        public string? QuestName { get; set; }

        [JsonPropertyName("quest_description")]
        public string? QuestDescription { get; set; }

        [JsonPropertyName("requester")]
        public string? Requester { get; set; }

        [JsonPropertyName("order_type")]
        public string? OrderType { get; set; }

        [JsonPropertyName("quest_state")]
        public string? QuestState { get; set; }

        [JsonPropertyName("due_date")]
        public int DueDate { get; set; }

        [JsonPropertyName("duration")]
        public string? Duration { get; set; }

        [JsonPropertyName("objectives")]
        public SpecialOrderObjectiveProgressRef[] Objectives { get; set; } = new SpecialOrderObjectiveProgressRef[0];
    }

    public sealed class SpecialOrderObjectiveProgressRef
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("current_count")]
        public int CurrentCount { get; set; }

        [JsonPropertyName("max_count")]
        public int MaxCount { get; set; }
    }

    public sealed class CommunityCenterProgressRef
    {
        [JsonPropertyName("bundles")]
        public Dictionary<int, bool[]> Bundles { get; set; } = new Dictionary<int, bool[]>();

        [JsonPropertyName("bundle_rewards")]
        public Dictionary<int, bool> BundleRewards { get; set; } = new Dictionary<int, bool>();

        [JsonPropertyName("completed_area_mail_flags")]
        public string[] CompletedAreaMailFlags { get; set; } = new string[0];
    }

    public sealed class MuseumProgressRef
    {
        [JsonPropertyName("pieces")]
        public MuseumPieceProgressRef[] Pieces { get; set; } = new MuseumPieceProgressRef[0];

        [JsonPropertyName("donated_count")]
        public int DonatedCount { get; set; }
    }

    public sealed class MuseumPieceProgressRef
    {
        [JsonPropertyName("tile_x")]
        public int TileX { get; set; }

        [JsonPropertyName("tile_y")]
        public int TileY { get; set; }

        [JsonPropertyName("item_id")]
        public string? ItemId { get; set; }
    }

    public sealed class CollectionsProgressRef
    {
        [JsonPropertyName("basic_shipped")]
        public Dictionary<string, int> BasicShipped { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("fish_caught")]
        public Dictionary<string, int[]> FishCaught { get; set; } = new Dictionary<string, int[]>();

        [JsonPropertyName("artifacts_found")]
        public Dictionary<string, int[]> ArtifactsFound { get; set; } = new Dictionary<string, int[]>();

        [JsonPropertyName("minerals_found")]
        public Dictionary<string, int> MineralsFound { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("cooking_recipes")]
        public Dictionary<string, int> CookingRecipes { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("crafting_recipes")]
        public Dictionary<string, int> CraftingRecipes { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("achievements")]
        public int[] Achievements { get; set; } = new int[0];
    }

    public sealed class PerfectionProgressRef
    {
        [JsonPropertyName("percent_complete")]
        public double PercentComplete { get; set; }

        [JsonPropertyName("percent_floor")]
        public double PercentFloor { get; set; }

        [JsonPropertyName("perfection_waivers")]
        public int PerfectionWaivers { get; set; }

        [JsonPropertyName("effective_percent_with_waivers")]
        public double EffectivePercentWithWaivers { get; set; }

        [JsonPropertyName("is_complete_with_waivers")]
        public bool IsCompleteWithWaivers { get; set; }
    }

    public sealed class GoldenWalnutProgressRef
    {
        [JsonPropertyName("current")]
        public int Current { get; set; }

        [JsonPropertyName("found")]
        public int Found { get; set; }

        [JsonPropertyName("found_capped_for_perfection")]
        public int FoundCappedForPerfection { get; set; }

        [JsonPropertyName("perfection_target")]
        public int PerfectionTarget { get; set; }

        [JsonPropertyName("qi_room_actual_found")]
        public int QiRoomActualFound { get; set; }

        [JsonPropertyName("qi_room_unlock_target")]
        public int QiRoomUnlockTarget { get; set; }

        [JsonPropertyName("qi_room_unlocked")]
        public bool QiRoomUnlocked { get; set; }
    }
}
