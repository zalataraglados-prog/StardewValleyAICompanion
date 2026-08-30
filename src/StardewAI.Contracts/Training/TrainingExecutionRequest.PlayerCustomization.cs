using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("customization_mode")] public string CustomizationMode { get; set; } = string.Empty;
    [JsonPropertyName("customization_reason")] public string CustomizationReason { get; set; } = string.Empty;
    [JsonPropertyName("confirm_customization")] public bool? ConfirmCustomization { get; set; }
    [JsonPropertyName("customization_projection_fingerprint")] public string CustomizationProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("customization_name")] public string CustomizationName { get; set; } = string.Empty;
    [JsonPropertyName("customization_favorite_thing")] public string CustomizationFavoriteThing { get; set; } = string.Empty;
    [JsonPropertyName("customization_gender")] public string CustomizationGender { get; set; } = string.Empty;
    [JsonPropertyName("customization_skin_index")] public int? CustomizationSkinIndex { get; set; }
    [JsonPropertyName("customization_hair_style_id")] public int? CustomizationHairStyleId { get; set; }
    [JsonPropertyName("customization_accessory_index")] public int? CustomizationAccessoryIndex { get; set; }
    [JsonPropertyName("customization_eye_hue")] public int? CustomizationEyeHue { get; set; }
    [JsonPropertyName("customization_eye_saturation")] public int? CustomizationEyeSaturation { get; set; }
    [JsonPropertyName("customization_eye_value")] public int? CustomizationEyeValue { get; set; }
    [JsonPropertyName("customization_hair_hue")] public int? CustomizationHairHue { get; set; }
    [JsonPropertyName("customization_hair_saturation")] public int? CustomizationHairSaturation { get; set; }
    [JsonPropertyName("customization_hair_value")] public int? CustomizationHairValue { get; set; }
    [JsonPropertyName("customization_price_gold")] public int? CustomizationPriceGold { get; set; }
    [JsonPropertyName("customization_money_before")] public int? CustomizationMoneyBefore { get; set; }
    [JsonPropertyName("customization_stylist_name")] public string CustomizationStylistName { get; set; } = string.Empty;
    [JsonPropertyName("customization_passive_festival_day")] public int? CustomizationPassiveFestivalDay { get; set; }
    [JsonPropertyName("customization_free_inventory_slots")] public int? CustomizationFreeInventorySlots { get; set; }
    [JsonPropertyName("customization_equipped_item_count")] public int? CustomizationEquippedItemCount { get; set; }
    [JsonPropertyName("customization_expected_outfit_index")] public int? CustomizationExpectedOutfitIndex { get; set; }
    [JsonPropertyName("customization_uses_player_seed")] public bool? CustomizationUsesPlayerSeed { get; set; }
    [JsonPropertyName("customization_special_laurel_outfit")] public bool? CustomizationSpecialLaurelOutfit { get; set; }
    [JsonPropertyName("customization_expected_hat_qid")] public string CustomizationExpectedHatQid { get; set; } = string.Empty;
    [JsonPropertyName("customization_expected_hat_color")] public string CustomizationExpectedHatColor { get; set; } = string.Empty;
    [JsonPropertyName("customization_expected_shirt_qid")] public string CustomizationExpectedShirtQid { get; set; } = string.Empty;
    [JsonPropertyName("customization_expected_shirt_color")] public string CustomizationExpectedShirtColor { get; set; } = string.Empty;
    [JsonPropertyName("customization_expected_pants_qid")] public string CustomizationExpectedPantsQid { get; set; } = string.Empty;
    [JsonPropertyName("customization_expected_pants_color")] public string CustomizationExpectedPantsColor { get; set; } = string.Empty;
    [JsonPropertyName("customization_action_raw")] public string CustomizationActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("customization_action_token")] public string CustomizationActionToken { get; set; } = string.Empty;
}
