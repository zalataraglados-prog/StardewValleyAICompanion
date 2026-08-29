using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class MailboxProcessingRef
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("queue_count")]
        public int QueueCount { get; set; }

        [JsonPropertyName("queue_mail_ids_native_order")]
        public string[] QueueMailIdsNativeOrder { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("pending_mail_id")]
        public string PendingMailId { get; set; } = string.Empty;

        [JsonPropertyName("mail_data_found")]
        public bool MailDataFound { get; set; }

        [JsonPropertyName("mail_data_sha256")]
        public string MailDataSha256 { get; set; } = string.Empty;

        [JsonPropertyName("dynamic_native_resolution")]
        public string DynamicNativeResolution { get; set; } = string.Empty;

        [JsonPropertyName("directives")]
        public MailDirectiveRef[] Directives { get; set; } = System.Array.Empty<MailDirectiveRef>();

        [JsonPropertyName("constructor_effect_classes")]
        public string[] ConstructorEffectClasses { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("attachment_slot_upper_bound")]
        public int AttachmentSlotUpperBound { get; set; }

        [JsonPropertyName("inventory_empty_slots")]
        public int InventoryEmptySlots { get; set; }

        [JsonPropertyName("attachment_capacity_sufficient")]
        public bool AttachmentCapacitySufficient { get; set; }

        [JsonPropertyName("mail_received_on_open")]
        public bool MailReceivedOnOpen { get; set; }

        [JsonPropertyName("mailbox_location_id")]
        public string MailboxLocationId { get; set; } = string.Empty;

        [JsonPropertyName("mailbox_action_tile_x")]
        public int? MailboxActionTileX { get; set; }

        [JsonPropertyName("mailbox_action_tile_y")]
        public int? MailboxActionTileY { get; set; }

        [JsonPropertyName("mailbox_action_raw")]
        public string MailboxActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("stand_tile_x")]
        public int? StandTileX { get; set; }

        [JsonPropertyName("stand_tile_y")]
        public int? StandTileY { get; set; }

        [JsonPropertyName("menu_clear")]
        public bool MenuClear { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("blocked_diagnostics")]
        public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();
    }

    public sealed class MailDirectiveRef
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("execution_phase")]
        public string ExecutionPhase { get; set; } = string.Empty;

        [JsonPropertyName("source_offset")]
        public int SourceOffset { get; set; }

        [JsonPropertyName("raw")]
        public string Raw { get; set; } = string.Empty;

        [JsonPropertyName("command")]
        public string Command { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string[] Arguments { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("errors")]
        public string[] Errors { get; set; } = System.Array.Empty<string>();
    }

    public sealed class DailyQuestOfferRef
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("can_accept")]
        public bool CanAccept { get; set; }

        [JsonPropertyName("accepted_daily_quest")]
        public bool AcceptedDailyQuest { get; set; }

        [JsonPropertyName("offer_fingerprint")]
        public string OfferFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("quest")]
        public QuestProgressRef? Quest { get; set; }

        [JsonPropertyName("board_location_id")]
        public string BoardLocationId { get; set; } = string.Empty;

        [JsonPropertyName("board_action_tile_x")]
        public int? BoardActionTileX { get; set; }

        [JsonPropertyName("board_action_tile_y")]
        public int? BoardActionTileY { get; set; }

        [JsonPropertyName("board_action_raw")]
        public string BoardActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("stand_tile_x")]
        public int? StandTileX { get; set; }

        [JsonPropertyName("stand_tile_y")]
        public int? StandTileY { get; set; }

        [JsonPropertyName("menu_clear")]
        public bool MenuClear { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("blocked_diagnostics")]
        public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();
    }

    public sealed class QuestProgressRef
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("title_available")]
        public bool TitleAvailable { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("description_available")]
        public bool DescriptionAvailable { get; set; }

        [JsonPropertyName("current_objective")]
        public string? CurrentObjective { get; set; }

        [JsonPropertyName("current_objective_available")]
        public bool CurrentObjectiveAvailable { get; set; }

        [JsonPropertyName("quest_type")]
        public int QuestType { get; set; }

        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("hidden")]
        public bool Hidden { get; set; }

        [JsonPropertyName("daily_quest")]
        public bool DailyQuest { get; set; }

        [JsonPropertyName("days_left")]
        public int DaysLeft { get; set; }

        [JsonPropertyName("money_reward")]
        public int MoneyReward { get; set; }

        [JsonPropertyName("reward_description")]
        public string RewardDescription { get; set; } = string.Empty;

        [JsonPropertyName("show_new")]
        public bool ShowNew { get; set; }

        [JsonPropertyName("destroy")]
        public bool Destroy { get; set; }

        [JsonPropertyName("next_quests")]
        public string[] NextQuests { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("can_be_cancelled")]
        public bool CanBeCancelled { get; set; }

        [JsonPropertyName("day_quest_accepted")]
        public int DayQuestAccepted { get; set; }

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("mod_data")]
        public Dictionary<string, string> ModData { get; set; } = new();

        [JsonPropertyName("obsolete_completion_string")]
        public string ObsoleteCompletionString { get; set; } = string.Empty;

        [JsonPropertyName("per_type_fields")]
        public PerTypeQuestFields PerTypeFields { get; set; } = new();
    }

    public sealed class QuestRewardClaimRef
    {
        [JsonPropertyName("reward_fingerprint")]
        public string RewardFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("quest")]
        public QuestProgressRef Quest { get; set; } = new();

        [JsonPropertyName("claimable")]
        public bool Claimable { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("blocked_diagnostics")]
        public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();
    }

    public sealed class PerTypeQuestFields
    {
        [JsonPropertyName("is_base_quest")]
        public bool IsBaseQuest { get; set; }

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("unavailable_reason")]
        public string UnavailableReason { get; set; } = string.Empty;

        [JsonPropertyName("unsupported_subtype")]
        public string UnsupportedSubtype { get; set; } = string.Empty;

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("target_npc")]
        public string TargetNpc { get; set; } = string.Empty;

        [JsonPropertyName("target_location")]
        public string TargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("target_count")]
        public int TargetCount { get; set; }

        [JsonPropertyName("current_count")]
        public int CurrentCount { get; set; }

        [JsonPropertyName("monster_name")]
        public string MonsterName { get; set; } = string.Empty;

        [JsonPropertyName("ignore_farm_monsters")]
        public bool IgnoreFarmMonsters { get; set; }

        [JsonPropertyName("who_to_greet")]
        public string[] WhoToGreet { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("total_to_greet")]
        public int TotalToGreet { get; set; }

        [JsonPropertyName("building_type")]
        public string BuildingType { get; set; } = string.Empty;

        [JsonPropertyName("tile_x")]
        public int TileX { get; set; }

        [JsonPropertyName("tile_y")]
        public int TileY { get; set; }

        [JsonPropertyName("item_found")]
        public bool ItemFound { get; set; }

        [JsonPropertyName("friendship_reward")]
        public int FriendshipReward { get; set; }

        [JsonPropertyName("exclusive_quest_id")]
        public string ExclusiveQuestId { get; set; } = string.Empty;

        [JsonPropertyName("npc_name")]
        public string NpcName { get; set; } = string.Empty;

        [JsonPropertyName("location_of_item")]
        public string LocationOfItem { get; set; } = string.Empty;

        [JsonPropertyName("number_to_kill")]
        public int NumberToKill { get; set; }

        [JsonPropertyName("number_killed")]
        public int NumberKilled { get; set; }

        [JsonPropertyName("number_to_fish")]
        public int NumberToFish { get; set; }

        [JsonPropertyName("number_fished")]
        public int NumberFished { get; set; }

        [JsonPropertyName("number_collected")]
        public int NumberCollected { get; set; }

        [JsonPropertyName("number_required")]
        public int NumberRequired { get; set; }

        [JsonPropertyName("reward")]
        public int Reward { get; set; }

        [JsonPropertyName("target_message")]
        public string TargetMessage { get; set; } = string.Empty;
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

        [JsonPropertyName("special_rule")]
        public string SpecialRule { get; set; } = string.Empty;

        [JsonPropertyName("is_island_order")]
        public int IsIslandOrder { get; set; }

        [JsonPropertyName("applied_special_rules")]
        public bool AppliedSpecialRules { get; set; }

        [JsonPropertyName("participants")]
        public Dictionary<long, bool> Participants { get; set; } = new();

        [JsonPropertyName("seen_participants")]
        public Dictionary<long, bool> SeenParticipants { get; set; } = new();

        [JsonPropertyName("unclaimed_rewards")]
        public Dictionary<long, bool> UnclaimedRewards { get; set; } = new();

        [JsonPropertyName("donated_items")]
        public SpecialOrderDonatedItemRef[] DonatedItems { get; set; } = new SpecialOrderDonatedItemRef[0];

        [JsonPropertyName("pre_selected_items")]
        public Dictionary<string, string> PreSelectedItems { get; set; } = new();

        [JsonPropertyName("selected_random_elements")]
        public Dictionary<string, int> SelectedRandomElements { get; set; } = new();

        [JsonPropertyName("generation_seed")]
        public int GenerationSeed { get; set; }

        [JsonPropertyName("ready_for_removal")]
        public bool ReadyForRemoval { get; set; }

        [JsonPropertyName("item_to_remove_on_end")]
        public string ItemToRemoveOnEnd { get; set; } = string.Empty;

        [JsonPropertyName("mail_to_remove_on_end")]
        public string MailToRemoveOnEnd { get; set; } = string.Empty;

        [JsonPropertyName("objectives")]
        public SpecialOrderObjectiveProgressRef[] Objectives { get; set; } = new SpecialOrderObjectiveProgressRef[0];

        [JsonPropertyName("rewards")]
        public SpecialOrderRewardProgressRef[] Rewards { get; set; } = new SpecialOrderRewardProgressRef[0];
    }

    public sealed class SpecialOrderOfferRef
    {
        [JsonPropertyName("selection_index")]
        public int SelectionIndex { get; set; }

        [JsonPropertyName("selection_side")]
        public string SelectionSide { get; set; } = string.Empty;

        [JsonPropertyName("offer_fingerprint")]
        public string OfferFingerprint { get; set; } = string.Empty;

        [JsonPropertyName("order")]
        public SpecialOrderProgressRef Order { get; set; } = new();
    }

    public sealed class SpecialOrderBoardRef
    {
        [JsonPropertyName("board_type")]
        public string BoardType { get; set; } = string.Empty;

        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("action_token")]
        public string ActionToken { get; set; } = string.Empty;

        [JsonPropertyName("action_raw")]
        public string ActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("action_tile_x")]
        public int? ActionTileX { get; set; }

        [JsonPropertyName("action_tile_y")]
        public int? ActionTileY { get; set; }

        [JsonPropertyName("stand_tile_x")]
        public int? StandTileX { get; set; }

        [JsonPropertyName("stand_tile_y")]
        public int? StandTileY { get; set; }

        [JsonPropertyName("unlocked")]
        public bool Unlocked { get; set; }

        [JsonPropertyName("accepted_this_cycle")]
        public bool AcceptedThisCycle { get; set; }

        [JsonPropertyName("menu_open")]
        public bool MenuOpen { get; set; }

        [JsonPropertyName("dialogue_ready_for_board")]
        public bool DialogueReadyForBoard { get; set; }

        [JsonPropertyName("offers")]
        public SpecialOrderOfferRef[] Offers { get; set; } = System.Array.Empty<SpecialOrderOfferRef>();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("blocked_diagnostics")]
        public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();
    }

    public sealed class SpecialOrderDonatedItemRef
    {
        [JsonPropertyName("is_null_entry")]
        public bool IsNullEntry { get; set; }

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("stack")]
        public int Stack { get; set; }

        [JsonPropertyName("quality")]
        public int Quality { get; set; }

        [JsonPropertyName("mod_data")]
        public Dictionary<string, string> ModData { get; set; } = new();
    }

    public sealed class SpecialOrderObjectiveProgressRef
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("current_count")]
        public int CurrentCount { get; set; }

        [JsonPropertyName("max_count")]
        public int MaxCount { get; set; }

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("fail_on_completion")]
        public bool FailOnCompletion { get; set; }

        [JsonPropertyName("complete")]
        public bool Complete { get; set; }

        [JsonPropertyName("per_type_fields")]
        public PerTypeObjectiveFields PerTypeFields { get; set; } = new();
    }

    public sealed class PerTypeObjectiveFields
    {
        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("unavailable_reason")]
        public string UnavailableReason { get; set; } = string.Empty;

        [JsonPropertyName("acceptable_context_tag_sets")]
        public string[] AcceptableContextTagSets { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("target_name")]
        public string TargetName { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("drop_box")]
        public string DropBox { get; set; } = string.Empty;

        [JsonPropertyName("drop_box_game_location")]
        public string DropBoxGameLocation { get; set; } = string.Empty;

        [JsonPropertyName("resolved_drop_box_game_location")]
        public string ResolvedDropBoxGameLocation { get; set; } = string.Empty;

        [JsonPropertyName("drop_box_tile_x")]
        public float DropBoxTileX { get; set; }

        [JsonPropertyName("drop_box_tile_y")]
        public float DropBoxTileY { get; set; }

        [JsonPropertyName("minimum_capacity")]
        public int MinimumCapacity { get; set; }

        [JsonPropertyName("confirmed")]
        public bool Confirmed { get; set; }

        [JsonPropertyName("minimum_like_level")]
        public string MinimumLikeLevel { get; set; } = string.Empty;

        [JsonPropertyName("skull_cave")]
        public bool SkullCave { get; set; }

        [JsonPropertyName("use_shipment_value")]
        public bool UseShipmentValue { get; set; }

        [JsonPropertyName("target_names")]
        public string[] TargetNames { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("ignore_farm_monsters")]
        public bool IgnoreFarmMonsters { get; set; }
    }

    public sealed class SpecialOrderRewardProgressRef
    {
        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("unavailable_reason")]
        public string UnavailableReason { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("multiplier")]
        public float Multiplier { get; set; }

        [JsonPropertyName("target_name")]
        public string TargetName { get; set; } = string.Empty;

        [JsonPropertyName("no_letter")]
        public bool NoLetter { get; set; }

        [JsonPropertyName("granted_mails")]
        public string[] GrantedMails { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("host")]
        public bool Host { get; set; }

        [JsonPropertyName("item_key")]
        public string ItemKey { get; set; } = string.Empty;

        [JsonPropertyName("reset_events")]
        public string[] ResetEvents { get; set; } = System.Array.Empty<string>();
    }

    public sealed class QuestCandidateRef
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("family")]
        public string Family { get; set; } = string.Empty;

        [JsonPropertyName("quest_id")]
        public string QuestId { get; set; } = string.Empty;

        [JsonPropertyName("quest_key")]
        public string QuestKey { get; set; } = string.Empty;

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("available")]
        public bool Available { get; set; }

        [JsonPropertyName("blocked_diagnostics")]
        public string[] BlockedDiagnostics { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("next_action_category")]
        public string NextActionCategory { get; set; } = string.Empty;

        [JsonPropertyName("required_target_location")]
        public string RequiredTargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("required_target_npc")]
        public string RequiredTargetNpc { get; set; } = string.Empty;

        [JsonPropertyName("required_item_id")]
        public string RequiredItemId { get; set; } = string.Empty;

        [JsonPropertyName("required_building_type")]
        public string RequiredBuildingType { get; set; } = string.Empty;

        [JsonPropertyName("required_target_count")]
        public int RequiredTargetCount { get; set; }

        [JsonPropertyName("required_target_tile_x")]
        public int? RequiredTargetTileX { get; set; }

        [JsonPropertyName("required_target_tile_y")]
        public int? RequiredTargetTileY { get; set; }

        [JsonPropertyName("current_progress_count")]
        public int CurrentProgressCount { get; set; }

        [JsonPropertyName("is_complete")]
        public bool IsComplete { get; set; }

        [JsonPropertyName("days_remaining")]
        public int DaysRemaining { get; set; }

        [JsonPropertyName("due_date")]
        public int DueDate { get; set; }

        [JsonPropertyName("time_cost_unknown")]
        public bool TimeCostUnknown { get; set; }

        [JsonPropertyName("energy_cost_unknown")]
        public bool EnergyCostUnknown { get; set; }

        [JsonPropertyName("selected_objective_index")]
        public int SelectedObjectiveIndex { get; set; } = -1;

        [JsonPropertyName("provenance")]
        public string Provenance { get; set; } = string.Empty;

        [JsonPropertyName("planning_eligible")]
        public bool PlanningEligible { get; set; }
    }

    public sealed class QuestCompilerEnvelope
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "quest_compiler.v1";

        [JsonPropertyName("selected_candidate_id")]
        public string SelectedCandidateId { get; set; } = string.Empty;

        [JsonPropertyName("selected_quest_id")]
        public string SelectedQuestId { get; set; } = string.Empty;

        [JsonPropertyName("selected_quest_key")]
        public string SelectedQuestKey { get; set; } = string.Empty;

        [JsonPropertyName("selected_runtime_type")]
        public string SelectedRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("family")]
        public string Family { get; set; } = string.Empty;

        [JsonPropertyName("next_action_category")]
        public string NextActionCategory { get; set; } = string.Empty;

        [JsonPropertyName("required_target_npc")]
        public string RequiredTargetNpc { get; set; } = string.Empty;

        [JsonPropertyName("required_target_location")]
        public string RequiredTargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("required_item_id")]
        public string RequiredItemId { get; set; } = string.Empty;

        [JsonPropertyName("required_target_count")]
        public int RequiredTargetCount { get; set; }

        [JsonPropertyName("current_progress_count")]
        public int CurrentProgressCount { get; set; }

        [JsonPropertyName("selected_objective_index")]
        public int SelectedObjectiveIndex { get; set; } = -1;

        [JsonPropertyName("time_estimate")]
        public string TimeEstimate { get; set; } = "unknown";

        [JsonPropertyName("energy_cost")]
        public string EnergyCost { get; set; } = "unknown";

        [JsonPropertyName("executor_block_reason")]
        public string ExecutorBlockReason { get; set; } = "quest_requires_typed_daily_candidate_binding";

        [JsonPropertyName("live_evidence")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public QuestCompilerEvidence? LiveEvidence { get; set; }
    }

    public sealed class QuestCompilerEvidence
    {
        [JsonPropertyName("candidate")]
        public QuestCandidateRef Candidate { get; set; } = new();

        [JsonPropertyName("raw_active_quests")]
        public QuestProgressRef[] RawActiveQuests { get; set; } = System.Array.Empty<QuestProgressRef>();

        [JsonPropertyName("raw_special_orders")]
        public SpecialOrderProgressRef[] RawSpecialOrders { get; set; } = System.Array.Empty<SpecialOrderProgressRef>();
    }

    public sealed class QuestProgressSnapshot
    {
        [JsonPropertyName("active_quests")]
        public QuestProgressRef[] ActiveQuests { get; set; } = System.Array.Empty<QuestProgressRef>();

        [JsonPropertyName("special_orders")]
        public SpecialOrderProgressRef[] SpecialOrders { get; set; } = System.Array.Empty<SpecialOrderProgressRef>();

        [JsonPropertyName("quest_candidates")]
        public QuestCandidateRef[] QuestCandidates { get; set; } = System.Array.Empty<QuestCandidateRef>();

        [JsonPropertyName("special_order_candidates")]
        public QuestCandidateRef[] SpecialOrderCandidates { get; set; } = System.Array.Empty<QuestCandidateRef>();

        [JsonPropertyName("compiler_envelope")]
        public QuestCompilerEnvelope CompilerEnvelope { get; set; } = new();
    }

    public sealed class CommunityCenterProgressRef
    {
        [JsonPropertyName("location_accessible")]
        public bool LocationAccessible { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("bundles")]
        public Dictionary<int, bool[]> Bundles { get; set; } = new Dictionary<int, bool[]>();

        [JsonPropertyName("bundle_rewards")]
        public Dictionary<int, bool> BundleRewards { get; set; } = new Dictionary<int, bool>();

        [JsonPropertyName("areas_complete")]
        public bool[] AreasComplete { get; set; } = System.Array.Empty<bool>();

        [JsonPropertyName("complete_bundle_count")]
        public int CompleteBundleCount { get; set; }

        [JsonPropertyName("completed_area_mail_flags")]
        public string[] CompletedAreaMailFlags { get; set; } = new string[0];

        [JsonPropertyName("pending_area_mail_flags")]
        public string[] PendingAreaMailFlags { get; set; } = new string[0];

        [JsonPropertyName("route_state")]
        public string RouteState { get; set; } = string.Empty;

        [JsonPropertyName("route_state_reason")]
        public string RouteStateReason { get; set; } = string.Empty;

        [JsonPropertyName("max_grandpa_score_route")]
        public string MaxGrandpaScoreRoute { get; set; } = string.Empty;

        [JsonPropertyName("joja_membership_received")]
        public bool JojaMembershipReceived { get; set; }

        [JsonPropertyName("joja_membership_pending")]
        public bool JojaMembershipPending { get; set; }

        [JsonPropertyName("community_center_complete_flag_received_or_pending")]
        public bool CommunityCenterCompleteFlagReceivedOrPending { get; set; }

        [JsonPropertyName("community_center_complete_native")]
        public bool CommunityCenterCompleteNative { get; set; }

        [JsonPropertyName("community_center_is_current_location")]
        public bool CommunityCenterIsCurrentLocation { get; set; }

        [JsonPropertyName("can_read_junimo_text")]
        public bool CanReadJunimoText { get; set; }

        [JsonPropertyName("bundle_data_row_count")]
        public int BundleDataRowCount { get; set; }

        [JsonPropertyName("projected_bundle_row_count")]
        public int ProjectedBundleRowCount { get; set; }

        [JsonPropertyName("unavailable_bundle_row_count")]
        public int UnavailableBundleRowCount { get; set; }

        [JsonPropertyName("bundle_rows")]
        public CommunityCenterBundleProgressRef[] BundleRows { get; set; } = System.Array.Empty<CommunityCenterBundleProgressRef>();
    }

    public sealed class CommunityCenterBundleProgressRef
    {
        [JsonPropertyName("projection_status")]
        public string ProjectionStatus { get; set; } = string.Empty;

        [JsonPropertyName("projection_failure")]
        public string ProjectionFailure { get; set; } = string.Empty;

        [JsonPropertyName("bundle_data_key")]
        public string BundleDataKey { get; set; } = string.Empty;

        [JsonPropertyName("bundle_id")]
        public int BundleId { get; set; }

        [JsonPropertyName("area_id")]
        public int AreaId { get; set; }

        [JsonPropertyName("area_name")]
        public string AreaName { get; set; } = string.Empty;

        [JsonPropertyName("internal_name")]
        public string InternalName { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("reward_description")]
        public string RewardDescription { get; set; } = string.Empty;

        [JsonPropertyName("required_slot_count")]
        public int RequiredSlotCount { get; set; }

        [JsonPropertyName("completed_ingredient_count")]
        public int CompletedIngredientCount { get; set; }

        [JsonPropertyName("complete")]
        public bool Complete { get; set; }

        [JsonPropertyName("note_appears")]
        public bool NoteAppears { get; set; }

        [JsonPropertyName("note_tile_x")]
        public int? NoteTileX { get; set; }

        [JsonPropertyName("note_tile_y")]
        public int? NoteTileY { get; set; }

        [JsonPropertyName("interaction_tile_x")]
        public int? InteractionTileX { get; set; }

        [JsonPropertyName("interaction_tile_y")]
        public int? InteractionTileY { get; set; }

        [JsonPropertyName("area_mutex_locked")]
        public bool? AreaMutexLocked { get; set; }

        [JsonPropertyName("reward_available")]
        public bool RewardAvailable { get; set; }

        [JsonPropertyName("area_complete")]
        public bool AreaComplete { get; set; }

        [JsonPropertyName("area_completion_mail_id")]
        public string AreaCompletionMailId { get; set; } = string.Empty;

        [JsonPropertyName("area_completion_mail_pending")]
        public bool AreaCompletionMailPending { get; set; }

        [JsonPropertyName("bulletin_thank_you_pending")]
        public bool BulletinThankYouPending { get; set; }

        [JsonPropertyName("ingredients")]
        public CommunityCenterIngredientProgressRef[] Ingredients { get; set; } = System.Array.Empty<CommunityCenterIngredientProgressRef>();

        [JsonPropertyName("donation_candidates")]
        public CommunityCenterDonationCandidateRef[] DonationCandidates { get; set; } = System.Array.Empty<CommunityCenterDonationCandidateRef>();
    }

    public sealed class CommunityCenterIngredientProgressRef
    {
        [JsonPropertyName("ingredient_index")]
        public int IngredientIndex { get; set; }

        [JsonPropertyName("item_id_or_category")]
        public string ItemIdOrCategory { get; set; } = string.Empty;

        [JsonPropertyName("required_stack")]
        public int RequiredStack { get; set; }

        [JsonPropertyName("minimum_quality")]
        public int MinimumQuality { get; set; }

        [JsonPropertyName("completed")]
        public bool Completed { get; set; }
    }

    public sealed class CommunityCenterDonationCandidateRef
    {
        [JsonPropertyName("inventory_slot_index")]
        public int InventorySlotIndex { get; set; }

        [JsonPropertyName("ingredient_index")]
        public int IngredientIndex { get; set; }

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("quality")]
        public int Quality { get; set; }

        [JsonPropertyName("stack_before")]
        public int StackBefore { get; set; }

        [JsonPropertyName("stack_after")]
        public int StackAfter { get; set; }

        [JsonPropertyName("required_stack")]
        public int RequiredStack { get; set; }

        [JsonPropertyName("inventory_item_total_before")]
        public int InventoryItemTotalBefore { get; set; }

        [JsonPropertyName("inventory_item_total_after")]
        public int InventoryItemTotalAfter { get; set; }

        [JsonPropertyName("completed_ingredient_count_before")]
        public int CompletedIngredientCountBefore { get; set; }

        [JsonPropertyName("completed_ingredient_count_after")]
        public int CompletedIngredientCountAfter { get; set; }

        [JsonPropertyName("completes_bundle")]
        public bool CompletesBundle { get; set; }

        [JsonPropertyName("expected_bundle_reward_available_after")]
        public bool ExpectedBundleRewardAvailableAfter { get; set; }

        [JsonPropertyName("expected_complete_bundle_count_after")]
        public int ExpectedCompleteBundleCountAfter { get; set; }

        [JsonPropertyName("completes_area")]
        public bool CompletesArea { get; set; }

        [JsonPropertyName("expected_area_complete_after")]
        public bool ExpectedAreaCompleteAfter { get; set; }

        [JsonPropertyName("expected_area_completion_mail_pending_after")]
        public bool ExpectedAreaCompletionMailPendingAfter { get; set; }

        [JsonPropertyName("expected_bulletin_thank_you_pending_after")]
        public bool ExpectedBulletinThankYouPendingAfter { get; set; }

        [JsonPropertyName("expected_all_areas_complete_after")]
        public bool ExpectedAllAreasCompleteAfter { get; set; }

        [JsonPropertyName("newly_appearing_note_area_ids")]
        public int[] NewlyAppearingNoteAreaIds { get; set; } = System.Array.Empty<int>();

        [JsonPropertyName("action_status")]
        public string ActionStatus { get; set; } = string.Empty;
    }

    public sealed class JojaDevelopmentProgressRef
    {
        [JsonPropertyName("location_accessible")]
        public bool LocationAccessible { get; set; }

        [JsonPropertyName("is_current_location")]
        public bool IsCurrentLocation { get; set; }

        [JsonPropertyName("join_action_tile_x")]
        public int? JoinActionTileX { get; set; }

        [JsonPropertyName("join_action_tile_y")]
        public int? JoinActionTileY { get; set; }

        [JsonPropertyName("join_action_raw")]
        public string JoinActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("host_route_state")]
        public string HostRouteState { get; set; } = string.Empty;

        [JsonPropertyName("actor_membership_received")]
        public bool ActorMembershipReceived { get; set; }

        [JsonPropertyName("actor_membership_pending")]
        public bool ActorMembershipPending { get; set; }

        [JsonPropertyName("actor_greeting_received")]
        public bool ActorGreetingReceived { get; set; }

        [JsonPropertyName("actor_membership_event_seen")]
        public bool ActorMembershipEventSeen { get; set; }

        [JsonPropertyName("completion_ceremony_event_seen")]
        public bool CompletionCeremonyEventSeen { get; set; }

        [JsonPropertyName("membership_price")]
        public int MembershipPrice { get; set; }

        [JsonPropertyName("money")]
        public int Money { get; set; }

        [JsonPropertyName("membership_action_status")]
        public string MembershipActionStatus { get; set; } = string.Empty;

        [JsonPropertyName("project_order_pending")]
        public bool ProjectOrderPending { get; set; }

        [JsonPropertyName("pending_project_mail_ids")]
        public string[] PendingProjectMailIds { get; set; } = System.Array.Empty<string>();

        [JsonPropertyName("all_projects_complete_or_pending")]
        public bool AllProjectsCompleteOrPending { get; set; }

        [JsonPropertyName("projects")]
        public JojaDevelopmentProjectRef[] Projects { get; set; } = System.Array.Empty<JojaDevelopmentProjectRef>();
    }

    public sealed class JojaDevelopmentProjectRef
    {
        [JsonPropertyName("button_number")]
        public int ButtonNumber { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; } = string.Empty;

        [JsonPropertyName("cc_mail_id")]
        public string CcMailId { get; set; } = string.Empty;

        [JsonPropertyName("joja_mail_id")]
        public string JojaMailId { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("complete_or_pending")]
        public bool CompleteOrPending { get; set; }

        [JsonPropertyName("cc_mail_received_or_pending")]
        public bool CcMailReceivedOrPending { get; set; }

        [JsonPropertyName("any_farmer_cc_mail_received_or_pending")]
        public bool AnyFarmerCcMailReceivedOrPending { get; set; }

        [JsonPropertyName("joja_mail_received_or_pending")]
        public bool JojaMailReceivedOrPending { get; set; }

        [JsonPropertyName("action_status")]
        public string ActionStatus { get; set; } = string.Empty;
    }

    public sealed class MuseumProgressRef
    {
        [JsonPropertyName("pieces")]
        public MuseumPieceProgressRef[] Pieces { get; set; } = new MuseumPieceProgressRef[0];

        [JsonPropertyName("donated_count")]
        public int DonatedCount { get; set; }

        [JsonPropertyName("total_donatable_items")]
        public int TotalDonatableItems { get; set; }

        [JsonPropertyName("collection_complete")]
        public bool CollectionComplete { get; set; }

        [JsonPropertyName("complete_collection_achievement_received")]
        public bool CompleteCollectionAchievementReceived { get; set; }

        [JsonPropertyName("field_guide_quest_present")]
        public bool FieldGuideQuestPresent { get; set; }

        [JsonPropertyName("field_guide_quest_completed")]
        public bool FieldGuideQuestCompleted { get; set; }

        [JsonPropertyName("rusty_key_donation_threshold")]
        public int RustyKeyDonationThreshold { get; set; }

        [JsonPropertyName("rusty_key_reward_id")]
        public string RustyKeyRewardId { get; set; } = string.Empty;

        [JsonPropertyName("rusty_key_reward_action")]
        public string RustyKeyRewardAction { get; set; } = string.Empty;

        [JsonPropertyName("rusty_key_reward_claimed")]
        public bool RustyKeyRewardClaimed { get; set; }

        [JsonPropertyName("rusty_key_prerequisite_event_seen")]
        public bool RustyKeyPrerequisiteEventSeen { get; set; }

        [JsonPropertyName("rusty_key_event_seen")]
        public bool RustyKeyEventSeen { get; set; }

        [JsonPropertyName("has_rusty_key")]
        public bool HasRustyKey { get; set; }

        [JsonPropertyName("museum_location_id")]
        public string MuseumLocationId { get; set; } = string.Empty;

        [JsonPropertyName("museum_is_current_location")]
        public bool MuseumIsCurrentLocation { get; set; }

        [JsonPropertyName("museum_mutex_locked")]
        public bool? MuseumMutexLocked { get; set; }

        [JsonPropertyName("gunther_action_tile_x")]
        public int? GuntherActionTileX { get; set; }

        [JsonPropertyName("gunther_action_tile_y")]
        public int? GuntherActionTileY { get; set; }

        [JsonPropertyName("gunther_action_raw")]
        public string GuntherActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("free_donation_tile_x")]
        public int? FreeDonationTileX { get; set; }

        [JsonPropertyName("free_donation_tile_y")]
        public int? FreeDonationTileY { get; set; }

        [JsonPropertyName("free_donation_tile_count")]
        public int FreeDonationTileCount { get; set; }

        [JsonPropertyName("pending_reward_ids")]
        public string[] PendingRewardIds { get; set; } = new string[0];

        [JsonPropertyName("donation_candidates")]
        public MuseumDonationCandidateRef[] DonationCandidates { get; set; } = new MuseumDonationCandidateRef[0];
    }

    public sealed class MuseumDonationCandidateRef
    {
        [JsonPropertyName("slot_index")]
        public int SlotIndex { get; set; }

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("stack_before")]
        public int StackBefore { get; set; }

        [JsonPropertyName("stack_after")]
        public int StackAfter { get; set; }

        [JsonPropertyName("donated_count_before")]
        public int DonatedCountBefore { get; set; }

        [JsonPropertyName("donated_count_after")]
        public int DonatedCountAfter { get; set; }

        [JsonPropertyName("completes_collection")]
        public bool CompletesCollection { get; set; }

        [JsonPropertyName("reaches_rusty_key_threshold")]
        public bool ReachesRustyKeyThreshold { get; set; }

        [JsonPropertyName("expected_complete_collection_achievement_after")]
        public bool ExpectedCompleteCollectionAchievementAfter { get; set; }

        [JsonPropertyName("field_guide_quest_present_before")]
        public bool FieldGuideQuestPresentBefore { get; set; }

        [JsonPropertyName("field_guide_quest_completed_before")]
        public bool FieldGuideQuestCompletedBefore { get; set; }

        [JsonPropertyName("expected_field_guide_quest_completed_after")]
        public bool ExpectedFieldGuideQuestCompletedAfter { get; set; }

        [JsonPropertyName("pending_reward_ids_before")]
        public string[] PendingRewardIdsBefore { get; set; } = new string[0];

        [JsonPropertyName("pending_reward_ids_after")]
        public string[] PendingRewardIdsAfter { get; set; } = new string[0];

        [JsonPropertyName("newly_pending_reward_ids")]
        public string[] NewlyPendingRewardIds { get; set; } = new string[0];

        [JsonPropertyName("auto_applied_reward_ids")]
        public string[] AutoAppliedRewardIds { get; set; } = new string[0];

        [JsonPropertyName("auto_applied_reward_actions")]
        public string[] AutoAppliedRewardActions { get; set; } = new string[0];

        [JsonPropertyName("reward_projection_status")]
        public string RewardProjectionStatus { get; set; } = string.Empty;

        [JsonPropertyName("action_status")]
        public string ActionStatus { get; set; } = string.Empty;
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

    public sealed class IslandFieldOfficeProgressRef
    {
        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("is_current_location")]
        public bool IsCurrentLocation { get; set; }

        [JsonPropertyName("north_cave_opened")]
        public bool NorthCaveOpened { get; set; }

        [JsonPropertyName("professor_available")]
        public bool ProfessorAvailable { get; set; }

        [JsonPropertyName("intro_received_or_pending")]
        public bool IntroReceivedOrPending { get; set; }

        [JsonPropertyName("mutex_locked")]
        public bool MutexLocked { get; set; }

        [JsonPropertyName("menu_clear")]
        public bool MenuClear { get; set; }

        [JsonPropertyName("desk_action_tiles")]
        public IslandFieldOfficeActionTileRef[] DeskActionTiles { get; set; } = System.Array.Empty<IslandFieldOfficeActionTileRef>();

        [JsonPropertyName("survey_action_tiles")]
        public IslandFieldOfficeActionTileRef[] SurveyActionTiles { get; set; } = System.Array.Empty<IslandFieldOfficeActionTileRef>();

        [JsonPropertyName("pieces")]
        public IslandFieldOfficePieceRef[] Pieces { get; set; } = System.Array.Empty<IslandFieldOfficePieceRef>();

        [JsonPropertyName("donated_piece_count")]
        public int DonatedPieceCount { get; set; }

        [JsonPropertyName("center_skeleton_restored")]
        public bool CenterSkeletonRestored { get; set; }

        [JsonPropertyName("snake_restored")]
        public bool SnakeRestored { get; set; }

        [JsonPropertyName("bat_restored")]
        public bool BatRestored { get; set; }

        [JsonPropertyName("frog_restored")]
        public bool FrogRestored { get; set; }

        [JsonPropertyName("plants_restored_left")]
        public bool PlantsRestoredLeft { get; set; }

        [JsonPropertyName("plants_restored_right")]
        public bool PlantsRestoredRight { get; set; }

        [JsonPropertyName("has_failed_survey_today")]
        public bool HasFailedSurveyToday { get; set; }

        [JsonPropertyName("next_survey_kind")]
        public string NextSurveyKind { get; set; } = string.Empty;

        [JsonPropertyName("next_survey_answer")]
        public int? NextSurveyAnswer { get; set; }

        [JsonPropertyName("finale_ready")]
        public bool FinaleReady { get; set; }

        [JsonPropertyName("finale_received_or_pending")]
        public bool FinaleReceivedOrPending { get; set; }

        [JsonPropertyName("golden_walnuts_found")]
        public int GoldenWalnutsFound { get; set; }

        [JsonPropertyName("uncollected_rewards")]
        public IslandFieldOfficeRewardRef[] UncollectedRewards { get; set; } = System.Array.Empty<IslandFieldOfficeRewardRef>();

        [JsonPropertyName("donation_candidates")]
        public IslandFieldOfficeDonationCandidateRef[] DonationCandidates { get; set; } = System.Array.Empty<IslandFieldOfficeDonationCandidateRef>();

        [JsonPropertyName("projection_status")]
        public string ProjectionStatus { get; set; } = string.Empty;
    }

    public sealed class IslandFieldOfficeActionTileRef
    {
        [JsonPropertyName("tile_x")]
        public int TileX { get; set; }

        [JsonPropertyName("tile_y")]
        public int TileY { get; set; }

        [JsonPropertyName("action_raw")]
        public string ActionRaw { get; set; } = string.Empty;
    }

    public sealed class IslandFieldOfficePieceRef
    {
        [JsonPropertyName("piece_index")]
        public int PieceIndex { get; set; }

        [JsonPropertyName("piece_kind")]
        public string PieceKind { get; set; } = string.Empty;

        [JsonPropertyName("set_kind")]
        public string SetKind { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("donated")]
        public bool Donated { get; set; }
    }

    public sealed class IslandFieldOfficeRewardRef
    {
        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("stack")]
        public int Stack { get; set; }

        [JsonPropertyName("quality")]
        public int Quality { get; set; }
    }

    public sealed class IslandFieldOfficeDonationCandidateRef
    {
        [JsonPropertyName("slot_index")]
        public int SlotIndex { get; set; }

        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("runtime_type")]
        public string RuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("stack_before")]
        public int StackBefore { get; set; }

        [JsonPropertyName("stack_after")]
        public int StackAfter { get; set; }

        [JsonPropertyName("target_piece_index")]
        public int TargetPieceIndex { get; set; }

        [JsonPropertyName("target_piece_kind")]
        public string TargetPieceKind { get; set; } = string.Empty;

        [JsonPropertyName("target_set_kind")]
        public string TargetSetKind { get; set; } = string.Empty;

        [JsonPropertyName("donated_piece_count_before")]
        public int DonatedPieceCountBefore { get; set; }

        [JsonPropertyName("donated_piece_count_after")]
        public int DonatedPieceCountAfter { get; set; }

        [JsonPropertyName("completes_set")]
        public bool CompletesSet { get; set; }

        [JsonPropertyName("new_reward_items")]
        public IslandFieldOfficeRewardRef[] NewRewardItems { get; set; } = System.Array.Empty<IslandFieldOfficeRewardRef>();

        [JsonPropertyName("uncollected_rewards_before")]
        public IslandFieldOfficeRewardRef[] UncollectedRewardsBefore { get; set; } = System.Array.Empty<IslandFieldOfficeRewardRef>();

        [JsonPropertyName("uncollected_rewards_after")]
        public IslandFieldOfficeRewardRef[] UncollectedRewardsAfter { get; set; } = System.Array.Empty<IslandFieldOfficeRewardRef>();

        [JsonPropertyName("expected_collected_nut_key")]
        public string ExpectedCollectedNutKey { get; set; } = string.Empty;

        [JsonPropertyName("collected_nut_before")]
        public bool CollectedNutBefore { get; set; }

        [JsonPropertyName("expected_finale_ready_after")]
        public bool ExpectedFinaleReadyAfter { get; set; }

        [JsonPropertyName("action_status")]
        public string ActionStatus { get; set; } = string.Empty;
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

    public sealed class FullShipmentProgressRef
    {
        [JsonPropertyName("eligible_item_count")]
        public int EligibleItemCount { get; set; }

        [JsonPropertyName("shipped_eligible_item_count")]
        public int ShippedEligibleItemCount { get; set; }

        [JsonPropertyName("missing_item_count")]
        public int MissingItemCount { get; set; }

        [JsonPropertyName("completion_ratio")]
        public double CompletionRatio { get; set; }

        [JsonPropertyName("complete")]
        public bool Complete { get; set; }

        [JsonPropertyName("items")]
        public FullShipmentItemProgressRef[] Items { get; set; } = System.Array.Empty<FullShipmentItemProgressRef>();

        [JsonPropertyName("missing_item_ids")]
        public string[] MissingItemIds { get; set; } = System.Array.Empty<string>();
    }

    public sealed class FullShipmentItemProgressRef
    {
        [JsonPropertyName("item_id")]
        public string ItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public int Category { get; set; }

        [JsonPropertyName("object_type")]
        public string ObjectType { get; set; } = string.Empty;

        [JsonPropertyName("current_shipped_count")]
        public int CurrentShippedCount { get; set; }

        [JsonPropertyName("shipped")]
        public bool Shipped { get; set; }
    }
}
