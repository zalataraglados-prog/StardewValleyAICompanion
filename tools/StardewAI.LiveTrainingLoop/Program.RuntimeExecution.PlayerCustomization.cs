using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyPlayerCustomizationRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId is not ("executor.customize_player" or "player.customize" or "debug.setup_player_customization"))
            return;
        request.CustomizationMode = ReadQueueParameterString(item, "customization_mode");
        request.CustomizationReason = ReadQueueParameterString(item, "customization_reason");
        request.ConfirmCustomization = ReadQueueParameterBool(item, "confirm_customization");
        request.CustomizationProjectionFingerprint = ReadQueueParameterString(item, "customization_projection_fingerprint");
        request.CustomizationName = ReadQueueParameterString(item, "customization_name");
        request.CustomizationFavoriteThing = ReadQueueParameterString(item, "customization_favorite_thing");
        request.CustomizationGender = ReadQueueParameterString(item, "customization_gender");
        request.CustomizationSkinIndex = ReadQueueParameterInt(item, "customization_skin_index");
        request.CustomizationHairStyleId = ReadQueueParameterInt(item, "customization_hair_style_id");
        request.CustomizationAccessoryIndex = ReadQueueParameterInt(item, "customization_accessory_index");
        request.CustomizationEyeHue = ReadQueueParameterInt(item, "customization_eye_hue");
        request.CustomizationEyeSaturation = ReadQueueParameterInt(item, "customization_eye_saturation");
        request.CustomizationEyeValue = ReadQueueParameterInt(item, "customization_eye_value");
        request.CustomizationHairHue = ReadQueueParameterInt(item, "customization_hair_hue");
        request.CustomizationHairSaturation = ReadQueueParameterInt(item, "customization_hair_saturation");
        request.CustomizationHairValue = ReadQueueParameterInt(item, "customization_hair_value");
        request.CustomizationPriceGold = ReadQueueParameterInt(item, "customization_price_gold");
        request.CustomizationMoneyBefore = ReadQueueParameterInt(item, "customization_money_before");
        request.CustomizationStylistName = ReadQueueParameterString(item, "customization_stylist_name");
        request.CustomizationPassiveFestivalDay = ReadQueueParameterInt(item, "customization_passive_festival_day");
        request.CustomizationFreeInventorySlots = ReadQueueParameterInt(item, "customization_free_inventory_slots");
        request.CustomizationEquippedItemCount = ReadQueueParameterInt(item, "customization_equipped_item_count");
        request.CustomizationExpectedOutfitIndex = ReadQueueParameterInt(item, "customization_expected_outfit_index");
        request.CustomizationUsesPlayerSeed = ReadQueueParameterBool(item, "customization_uses_player_seed");
        request.CustomizationSpecialLaurelOutfit = ReadQueueParameterBool(item, "customization_special_laurel_outfit");
        request.CustomizationExpectedHatQid = ReadQueueParameterString(item, "customization_expected_hat_qid");
        request.CustomizationExpectedHatColor = ReadQueueParameterString(item, "customization_expected_hat_color");
        request.CustomizationExpectedShirtQid = ReadQueueParameterString(item, "customization_expected_shirt_qid");
        request.CustomizationExpectedShirtColor = ReadQueueParameterString(item, "customization_expected_shirt_color");
        request.CustomizationExpectedPantsQid = ReadQueueParameterString(item, "customization_expected_pants_qid");
        request.CustomizationExpectedPantsColor = ReadQueueParameterString(item, "customization_expected_pants_color");
        request.CustomizationActionRaw = ReadQueueParameterString(item, "customization_action_raw");
        request.CustomizationActionToken = ReadQueueParameterString(item, "customization_action_token");
    }
}
