using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training
{
    public sealed class TrainingExecutionRequest
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_execution_request.v1";

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("execution_mode")]
        public string ExecutionMode { get; set; } = "training_singleplayer";

        [JsonPropertyName("actor")]
        public string Actor { get; set; } = "training_farmer.main";

        [JsonPropertyName("save_isolation_path")]
        public string SaveIsolationPath { get; set; } = string.Empty;

        [JsonPropertyName("request_nonce")]
        public string RequestNonce { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("max_crops")]
        public int MaxCrops { get; set; } = 512;

        [JsonPropertyName("max_movement_tiles")]
        public int? MaxMovementTiles { get; set; }

        [JsonPropertyName("target_tile_x")]
        public int? TargetTileX { get; set; }

        [JsonPropertyName("target_tile_y")]
        public int? TargetTileY { get; set; }

        [JsonPropertyName("target_runtime_type")]
        public string TargetRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("target_runtime_identity")]
        public string TargetRuntimeIdentity { get; set; } = string.Empty;

        [JsonPropertyName("target_name")]
        public string TargetName { get; set; } = string.Empty;

        [JsonPropertyName("max_attacks")]
        public int MaxAttacks { get; set; } = 256;

        [JsonPropertyName("direction")]
        public int? Direction { get; set; }

        [JsonPropertyName("wait_ticks")]
        public int? WaitTicks { get; set; }

        [JsonPropertyName("target_time")]
        public int? TargetTime { get; set; }

        [JsonPropertyName("safe_slot_index")]
        public int? SafeSlotIndex { get; set; }

        [JsonPropertyName("interaction_kind")]
        public string InteractionKind { get; set; } = string.Empty;

        [JsonPropertyName("expected_action_type")]
        public string ExpectedActionType { get; set; } = string.Empty;

        [JsonPropertyName("connector_kind")]
        public string ConnectorKind { get; set; } = string.Empty;

        [JsonPropertyName("expected_target_location")]
        public string ExpectedTargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("expected_arrival_tile_x")]
        public int? ExpectedArrivalTileX { get; set; }

        [JsonPropertyName("expected_arrival_tile_y")]
        public int? ExpectedArrivalTileY { get; set; }

        [JsonPropertyName("shop_item_id")]
        public string ShopItemId { get; set; } = string.Empty;

        [JsonPropertyName("qualified_item_id")]
        public string QualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("mine_level")]
        public int? MineLevel { get; set; }

        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }

        [JsonPropertyName("max_unit_price")]
        public int? MaxUnitPrice { get; set; }

        [JsonPropertyName("expected_shop_id")]
        public string ExpectedShopId { get; set; } = string.Empty;

        [JsonPropertyName("expected_dialogue_key")]
        public string ExpectedDialogueKey { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_response_key")]
        public string DialogueResponseKey { get; set; } = string.Empty;

        [JsonPropertyName("seed_id")]
        public string SeedId { get; set; } = string.Empty;

        [JsonPropertyName("harvest_method")]
        public string HarvestMethod { get; set; } = string.Empty;

        [JsonPropertyName("giant_crop_id")]
        public string GiantCropId { get; set; } = string.Empty;

        [JsonPropertyName("debris_index")]
        public int? DebrisIndex { get; set; }

        [JsonPropertyName("input_slot_index")]
        public int? InputSlotIndex { get; set; }

        [JsonPropertyName("debug_fill_inventory")]
        public bool DebugFillInventory { get; set; }

        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("stand_tile_x")]
        public int? StandTileX { get; set; }

        [JsonPropertyName("stand_tile_y")]
        public int? StandTileY { get; set; }

        [JsonPropertyName("bobber_tile_x")]
        public int? BobberTileX { get; set; }

        [JsonPropertyName("bobber_tile_y")]
        public int? BobberTileY { get; set; }

        [JsonPropertyName("rod_slot_index")]
        public int? RodSlotIndex { get; set; }

        [JsonPropertyName("rule_key")]
        public string RuleKey { get; set; } = string.Empty;

        [JsonPropertyName("expected_qualified_item_id")]
        public string ExpectedQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("outcome_distribution_complete")]
        public bool OutcomeDistributionComplete { get; set; }

        [JsonPropertyName("outcome_distribution_json")]
        public string OutcomeDistributionJson { get; set; } = string.Empty;

        [JsonPropertyName("possible_qualified_item_ids_json")]
        public string PossibleQualifiedItemIdsJson { get; set; } = string.Empty;

        [JsonPropertyName("outcome_probability_status")]
        public string OutcomeProbabilityStatus { get; set; } = string.Empty;

        [JsonPropertyName("social_npc_name")]
        public string SocialNpcName { get; set; } = string.Empty;

        [JsonPropertyName("social_action_kind")]
        public string SocialActionKind { get; set; } = string.Empty;

        [JsonPropertyName("social_observed_npc_tile_x")]
        public int? SocialObservedNpcTileX { get; set; }

        [JsonPropertyName("social_observed_npc_tile_y")]
        public int? SocialObservedNpcTileY { get; set; }

        [JsonPropertyName("social_gift_slot_index")]
        public int? SocialGiftSlotIndex { get; set; }

        [JsonPropertyName("social_gift_qualified_item_id")]
        public string SocialGiftQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("social_expected_friendship_delta")]
        public string SocialExpectedFriendshipDelta { get; set; } = string.Empty;

        [JsonPropertyName("social_expected_talked_to_today_before")]
        public bool? SocialExpectedTalkedToTodayBefore { get; set; }

        [JsonPropertyName("slot_index")]
        public int? SlotIndex { get; set; }

        [JsonPropertyName("expected_mine_level_delta")]
        public int? ExpectedMineLevelDelta { get; set; }

        [JsonPropertyName("expected_mine_level_after")]
        public int? ExpectedMineLevelAfter { get; set; }

        [JsonPropertyName("expected_health_cost")]
        public int? ExpectedHealthCost { get; set; }

        [JsonPropertyName("expected_health_after")]
        public int? ExpectedHealthAfter { get; set; }
    }

    public sealed class TrainingExecutionResult
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "training_execution_result.v1";

        [JsonPropertyName("run_id")]
        public string RunId { get; set; } = string.Empty;

        [JsonPropertyName("queue_id")]
        public string QueueId { get; set; } = string.Empty;

        [JsonPropertyName("queue_item_id")]
        public string QueueItemId { get; set; } = string.Empty;

        [JsonPropertyName("before_state_hash")]
        public string BeforeStateHash { get; set; } = string.Empty;

        [JsonPropertyName("option_id")]
        public string OptionId { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = "blocked";

        [JsonPropertyName("feedback_available")]
        public bool FeedbackAvailable { get; set; }

        [JsonPropertyName("watered_count")]
        public int WateredCount { get; set; }

        [JsonPropertyName("energy_before")]
        public double EnergyBefore { get; set; }

        [JsonPropertyName("energy_after")]
        public double EnergyAfter { get; set; }

        [JsonPropertyName("target_location")]
        public string TargetLocation { get; set; } = string.Empty;

        [JsonPropertyName("target_tile_x")]
        public int? TargetTileX { get; set; }

        [JsonPropertyName("target_tile_y")]
        public int? TargetTileY { get; set; }

        [JsonPropertyName("tool_qualified_item_id")]
        public string ToolQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("tool_upgrade_level")]
        public int? ToolUpgradeLevel { get; set; }

        [JsonPropertyName("tool_power")]
        public int? ToolPower { get; set; }

        [JsonPropertyName("water_before")]
        public int? WaterBefore { get; set; }

        [JsonPropertyName("water_after")]
        public int? WaterAfter { get; set; }

        [JsonPropertyName("estimated_ticks")]
        public int? EstimatedTicks { get; set; }

        [JsonPropertyName("actual_ticks")]
        public int? ActualTicks { get; set; }

        [JsonPropertyName("failure_category")]
        public string FailureCategory { get; set; } = string.Empty;

        [JsonPropertyName("training_impact_scope")]
        public string TrainingImpactScope { get; set; } = string.Empty;

        [JsonPropertyName("started_at")]
        public string StartedAt { get; set; } = string.Empty;

        [JsonPropertyName("completed_at")]
        public string CompletedAt { get; set; } = string.Empty;

        [JsonPropertyName("changed_facts")]
        public SimulatedFactChange[] ChangedFacts { get; set; } = Array.Empty<SimulatedFactChange>();

        [JsonPropertyName("primitive_kind")]
        public string PrimitiveKind { get; set; } = string.Empty;

        [JsonPropertyName("primitive_verification_status")]
        public string PrimitiveVerificationStatus { get; set; } = "not_applicable";

        [JsonPropertyName("primitive_verification_reasons")]
        public string[] PrimitiveVerificationReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("requested_effect")]
        public string RequestedEffect { get; set; } = string.Empty;

        [JsonPropertyName("observed_effect")]
        public string ObservedEffect { get; set; } = string.Empty;

        [JsonPropertyName("combat_target_runtime_type")]
        public string CombatTargetRuntimeType { get; set; } = string.Empty;

        [JsonPropertyName("combat_target_runtime_identity")]
        public string CombatTargetRuntimeIdentity { get; set; } = string.Empty;

        [JsonPropertyName("combat_target_name")]
        public string CombatTargetName { get; set; } = string.Empty;

        [JsonPropertyName("combat_attack_count")]
        public int? CombatAttackCount { get; set; }

        [JsonPropertyName("combat_hit_count")]
        public int? CombatHitCount { get; set; }

        [JsonPropertyName("combat_target_health_sequence")]
        public int[] CombatTargetHealthSequence { get; set; } = Array.Empty<int>();

        [JsonPropertyName("combat_player_health_sequence")]
        public int[] CombatPlayerHealthSequence { get; set; } = Array.Empty<int>();

        [JsonPropertyName("combat_damage_taken")]
        public int? CombatDamageTaken { get; set; }

        [JsonPropertyName("combat_target_defeated")]
        public bool? CombatTargetDefeated { get; set; }

        [JsonPropertyName("recovery_food_slot_index")]
        public int? RecoveryFoodSlotIndex { get; set; }

        [JsonPropertyName("recovery_food_qualified_item_id")]
        public string RecoveryFoodQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("recovery_food_stack_before")]
        public int? RecoveryFoodStackBefore { get; set; }

        [JsonPropertyName("recovery_food_stack_after")]
        public int? RecoveryFoodStackAfter { get; set; }

        [JsonPropertyName("recovery_health_before")]
        public int? RecoveryHealthBefore { get; set; }

        [JsonPropertyName("recovery_health_after")]
        public int? RecoveryHealthAfter { get; set; }

        [JsonPropertyName("recovery_restore_slot_index")]
        public int? RecoveryRestoreSlotIndex { get; set; }

        [JsonPropertyName("recovery_safety_status")]
        public string RecoverySafetyStatus { get; set; } = string.Empty;

        [JsonPropertyName("shaft_mine_level_before")]
        public int? ShaftMineLevelBefore { get; set; }

        [JsonPropertyName("shaft_mine_level_after")]
        public int? ShaftMineLevelAfter { get; set; }

        [JsonPropertyName("shaft_level_delta")]
        public int? ShaftLevelDelta { get; set; }

        [JsonPropertyName("shaft_health_before")]
        public int? ShaftHealthBefore { get; set; }

        [JsonPropertyName("shaft_health_after")]
        public int? ShaftHealthAfter { get; set; }

        [JsonPropertyName("shaft_native_dialogue_handled")]
        public bool? ShaftNativeDialogueHandled { get; set; }

        [JsonPropertyName("block_reasons")]
        public string[] BlockReasons { get; set; } = Array.Empty<string>();

        [JsonPropertyName("social_npc_name")]
        public string SocialNpcName { get; set; } = string.Empty;

        [JsonPropertyName("social_npc_location_before")]
        public string SocialNpcLocationBefore { get; set; } = string.Empty;

        [JsonPropertyName("social_npc_location_after")]
        public string SocialNpcLocationAfter { get; set; } = string.Empty;

        [JsonPropertyName("social_npc_tile_x_before")]
        public int? SocialNpcTileXBefore { get; set; }

        [JsonPropertyName("social_npc_tile_y_before")]
        public int? SocialNpcTileYBefore { get; set; }

        [JsonPropertyName("social_npc_tile_x_after")]
        public int? SocialNpcTileXAfter { get; set; }

        [JsonPropertyName("social_npc_tile_y_after")]
        public int? SocialNpcTileYAfter { get; set; }

        [JsonPropertyName("social_npc_visible_before")]
        public bool? SocialNpcVisibleBefore { get; set; }

        [JsonPropertyName("social_npc_visible_after")]
        public bool? SocialNpcVisibleAfter { get; set; }

        [JsonPropertyName("social_npc_sleeping_before")]
        public bool? SocialNpcSleepingBefore { get; set; }

        [JsonPropertyName("social_npc_sleeping_after")]
        public bool? SocialNpcSleepingAfter { get; set; }

        [JsonPropertyName("social_npc_present_before")]
        public bool? SocialNpcPresentBefore { get; set; }

        [JsonPropertyName("social_npc_present_after")]
        public bool? SocialNpcPresentAfter { get; set; }

        [JsonPropertyName("social_npc_ordinary_before")]
        public bool? SocialNpcOrdinaryBefore { get; set; }

        [JsonPropertyName("social_npc_ordinary_after")]
        public bool? SocialNpcOrdinaryAfter { get; set; }

        [JsonPropertyName("social_player_tile_x_before")]
        public int? SocialPlayerTileXBefore { get; set; }

        [JsonPropertyName("social_player_tile_y_before")]
        public int? SocialPlayerTileYBefore { get; set; }

        [JsonPropertyName("social_player_tile_x_after")]
        public int? SocialPlayerTileXAfter { get; set; }

        [JsonPropertyName("social_player_tile_y_after")]
        public int? SocialPlayerTileYAfter { get; set; }

        [JsonPropertyName("social_player_facing_before")]
        public int? SocialPlayerFacingBefore { get; set; }

        [JsonPropertyName("social_player_facing_after")]
        public int? SocialPlayerFacingAfter { get; set; }

        [JsonPropertyName("social_player_selected_slot_before")]
        public int? SocialPlayerSelectedSlotBefore { get; set; }

        [JsonPropertyName("social_player_selected_slot_after")]
        public int? SocialPlayerSelectedSlotAfter { get; set; }

        [JsonPropertyName("social_action_kind")]
        public string SocialActionKind { get; set; } = string.Empty;

        [JsonPropertyName("social_native_handled")]
        public bool? SocialNativeHandled { get; set; }

        [JsonPropertyName("social_gift_item_id_before")]
        public string SocialGiftItemIdBefore { get; set; } = string.Empty;

        [JsonPropertyName("social_gift_item_id_after")]
        public string SocialGiftItemIdAfter { get; set; } = string.Empty;

        [JsonPropertyName("social_gift_stack_before")]
        public int? SocialGiftStackBefore { get; set; }

        [JsonPropertyName("social_gift_stack_after")]
        public int? SocialGiftStackAfter { get; set; }

        [JsonPropertyName("social_gift_quality_before")]
        public int? SocialGiftQualityBefore { get; set; }

        [JsonPropertyName("social_gift_quality_after")]
        public int? SocialGiftQualityAfter { get; set; }

        [JsonPropertyName("social_gift_slot_before")]
        public int? SocialGiftSlotBefore { get; set; }

        [JsonPropertyName("social_gift_slot_after")]
        public int? SocialGiftSlotAfter { get; set; }

        [JsonPropertyName("social_friendship_points_before")]
        public int? SocialFriendshipPointsBefore { get; set; }

        [JsonPropertyName("social_friendship_points_after")]
        public int? SocialFriendshipPointsAfter { get; set; }

        [JsonPropertyName("social_talked_to_today_before")]
        public bool? SocialTalkedToTodayBefore { get; set; }

        [JsonPropertyName("social_talked_to_today_after")]
        public bool? SocialTalkedToTodayAfter { get; set; }

        [JsonPropertyName("social_gifts_today_before")]
        public int? SocialGiftsTodayBefore { get; set; }

        [JsonPropertyName("social_gifts_today_after")]
        public int? SocialGiftsTodayAfter { get; set; }

        [JsonPropertyName("social_gifts_this_week_before")]
        public int? SocialGiftsThisWeekBefore { get; set; }

        [JsonPropertyName("social_gifts_this_week_after")]
        public int? SocialGiftsThisWeekAfter { get; set; }

        [JsonPropertyName("social_menu_open_before")]
        public bool? SocialMenuOpenBefore { get; set; }

        [JsonPropertyName("social_menu_open_after")]
        public bool? SocialMenuOpenAfter { get; set; }

        [JsonPropertyName("social_menu_type_before")]
        public string SocialMenuTypeBefore { get; set; } = string.Empty;

        [JsonPropertyName("social_menu_type_after")]
        public string SocialMenuTypeAfter { get; set; } = string.Empty;

        [JsonPropertyName("social_current_dialogue_count_before")]
        public int? SocialCurrentDialogueCountBefore { get; set; }

        [JsonPropertyName("social_current_dialogue_count_after")]
        public int? SocialCurrentDialogueCountAfter { get; set; }

        [JsonPropertyName("social_current_dialogue_key_before")]
        public string SocialCurrentDialogueKeyBefore { get; set; } = string.Empty;

        [JsonPropertyName("social_current_dialogue_key_after")]
        public string SocialCurrentDialogueKeyAfter { get; set; } = string.Empty;

        [JsonPropertyName("social_current_dialogue_speaker_name_before")]
        public string SocialCurrentDialogueSpeakerNameBefore { get; set; } = string.Empty;

        [JsonPropertyName("social_current_dialogue_speaker_name_after")]
        public string SocialCurrentDialogueSpeakerNameAfter { get; set; } = string.Empty;

        [JsonPropertyName("social_dialogue_open_before")]
        public bool? SocialDialogueOpenBefore { get; set; }

        [JsonPropertyName("social_dialogue_open_after")]
        public bool? SocialDialogueOpenAfter { get; set; }

        [JsonPropertyName("social_friendship_row_exists_before")]
        public bool? SocialFriendshipRowExistsBefore { get; set; }

        [JsonPropertyName("social_friendship_row_exists_after")]
        public bool? SocialFriendshipRowExistsAfter { get; set; }

        [JsonPropertyName("ship_slot_index")]
        public int? ShipSlotIndex { get; set; }

        [JsonPropertyName("ship_qualified_item_id")]
        public string ShipQualifiedItemId { get; set; } = string.Empty;

        [JsonPropertyName("ship_inventory_count_before")]
        public int? ShipInventoryCountBefore { get; set; }

        [JsonPropertyName("ship_inventory_count_after")]
        public int? ShipInventoryCountAfter { get; set; }

        [JsonPropertyName("ship_bin_count_before")]
        public int? ShipBinCountBefore { get; set; }

        [JsonPropertyName("ship_bin_count_after")]
        public int? ShipBinCountAfter { get; set; }

        [JsonPropertyName("ship_bin_total_count_before")]
        public int? ShipBinTotalCountBefore { get; set; }

        [JsonPropertyName("ship_bin_total_count_after")]
        public int? ShipBinTotalCountAfter { get; set; }

        [JsonPropertyName("ship_bin_distinct_count_before")]
        public int? ShipBinDistinctCountBefore { get; set; }

        [JsonPropertyName("ship_bin_distinct_count_after")]
        public int? ShipBinDistinctCountAfter { get; set; }

        [JsonPropertyName("ship_bin_signature_before")]
        public string ShipBinSignatureBefore { get; set; } = string.Empty;

        [JsonPropertyName("ship_bin_signature_after")]
        public string ShipBinSignatureAfter { get; set; } = string.Empty;

        [JsonPropertyName("ship_pending_receipt_path")]
        public string ShipPendingReceiptPath { get; set; } = string.Empty;

        [JsonPropertyName("ship_basic_shipped_count_before")]
        public int? ShipBasicShippedCountBefore { get; set; }

        [JsonPropertyName("ship_item_id")]
        public string ShipItemId { get; set; } = string.Empty;

        [JsonPropertyName("ship_before_slot_stack")]
        public int? ShipBeforeSlotStack { get; set; }

        [JsonPropertyName("ship_after_slot_stack")]
        public int? ShipAfterSlotStack { get; set; }

        [JsonPropertyName("ship_before_slot_qualified_id")]
        public string ShipBeforeSlotQualifiedId { get; set; } = string.Empty;

        [JsonPropertyName("ship_after_slot_qualified_id")]
        public string ShipAfterSlotQualifiedId { get; set; } = string.Empty;

        [JsonPropertyName("ship_source_date")]
        public string ShipSourceDate { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_native_handled")]
        public bool? DialogueNativeHandled { get; set; }

        [JsonPropertyName("dialogue_press_attempts")]
        public int? DialoguePressAttempts { get; set; }

        [JsonPropertyName("dialogue_advance_ticks")]
        public int? DialogueAdvanceTicks { get; set; }

        [JsonPropertyName("dialogue_menu_type_before")]
        public string DialogueMenuTypeBefore { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_menu_type_after")]
        public string DialogueMenuTypeAfter { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_is_question_before")]
        public bool? DialogueIsQuestionBefore { get; set; }

        [JsonPropertyName("dialogue_is_question_after")]
        public bool? DialogueIsQuestionAfter { get; set; }

        [JsonPropertyName("dialogue_response_count_before")]
        public int? DialogueResponseCountBefore { get; set; }

        [JsonPropertyName("dialogue_response_count_after")]
        public int? DialogueResponseCountAfter { get; set; }

        [JsonPropertyName("dialogue_speaker_name_before")]
        public string DialogueSpeakerNameBefore { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_speaker_name_after")]
        public string DialogueSpeakerNameAfter { get; set; } = string.Empty;

        [JsonPropertyName("dialogue_event_up_before")]
        public bool? DialogueEventUpBefore { get; set; }

        [JsonPropertyName("dialogue_event_up_after")]
        public bool? DialogueEventUpAfter { get; set; }
    }
}
