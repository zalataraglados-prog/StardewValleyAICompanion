using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("geode_purpose")] public string GeodePurpose { get; set; } = string.Empty;
    [JsonPropertyName("geode_qualified_item_id")] public string GeodeQualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("geode_slot_index")] public int? GeodeSlotIndex { get; set; }
    [JsonPropertyName("geode_input_quality")] public int? GeodeInputQuality { get; set; }
    [JsonPropertyName("geode_stack_before")] public int? GeodeStackBefore { get; set; }
    [JsonPropertyName("geode_free_slots_before")] public int? GeodeFreeSlotsBefore { get; set; }
    [JsonPropertyName("geode_money_before")] public int? GeodeMoneyBefore { get; set; }
    [JsonPropertyName("geode_price_gold")] public int? GeodePriceGold { get; set; }
    [JsonPropertyName("geodes_cracked_before")] public int? GeodesCrackedBefore { get; set; }
    [JsonPropertyName("mystery_boxes_opened_before")] public int? MysteryBoxesOpenedBefore { get; set; }
    [JsonPropertyName("golden_coconut_cracked_before")] public bool? GoldenCoconutCrackedBefore { get; set; }
    [JsonPropertyName("golden_walnuts_before")] public int? GoldenWalnutsBefore { get; set; }
    [JsonPropertyName("golden_walnuts_found_before")] public int? GoldenWalnutsFoundBefore { get; set; }
    [JsonPropertyName("geode_archaeology_found_count")] public int? GeodeArchaeologyFoundCount { get; set; }
    [JsonPropertyName("geode_save_id_half")] public long? GeodeSaveIdHalf { get; set; }
    [JsonPropertyName("geode_player_id_half")] public long? GeodePlayerIdHalf { get; set; }
    [JsonPropertyName("geode_season")] public string GeodeSeason { get; set; } = string.Empty;
    [JsonPropertyName("geode_deepest_mine_level")] public int? GeodeDeepestMineLevel { get; set; }
    [JsonPropertyName("geode_skill_1_level")] public int? GeodeSkill1Level { get; set; }
    [JsonPropertyName("geode_farming_mastery_unlocked")] public bool? GeodeFarmingMasteryUnlocked { get; set; }
    [JsonPropertyName("geode_qi_beans_rule_active")] public bool? GeodeQiBeansRuleActive { get; set; }
    [JsonPropertyName("geode_got_mystery_book_mail_before")] public bool? GeodeGotMysteryBookMailBefore { get; set; }
    [JsonPropertyName("geode_artifact_found_mail_before")] public bool? GeodeArtifactFoundMailBefore { get; set; }
    [JsonPropertyName("geode_prediction_kind")] public string GeodePredictionKind { get; set; } = string.Empty;
    [JsonPropertyName("geode_expected_output_qid")] public string GeodeExpectedOutputQid { get; set; } = string.Empty;
    [JsonPropertyName("geode_expected_output_stack")] public int? GeodeExpectedOutputStack { get; set; }
    [JsonPropertyName("geode_expected_output_quality")] public int? GeodeExpectedOutputQuality { get; set; }
    [JsonPropertyName("geode_accepted_outputs_json")] public string GeodeAcceptedOutputsJson { get; set; } = string.Empty;
    [JsonPropertyName("geode_expected_mail_additions_json")] public string GeodeExpectedMailAdditionsJson { get; set; } = string.Empty;
    [JsonPropertyName("geode_projection_fingerprint")] public string GeodeProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("geode_action_raw")] public string GeodeActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("geode_action_token")] public string GeodeActionToken { get; set; } = string.Empty;
}
