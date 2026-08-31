using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class QueueReplanFilter
{
    private static readonly HashSet<string> NonSemanticParameterNames = new(StringComparer.Ordinal)
    {
        "precondition",
        "safety_constraint",
        "failure_policy",
        "estimated_minutes"
    };

    private static readonly string[] PlayerCustomizationContinuationNames =
    {
        "customization_name", "customization_favorite_thing", "customization_gender",
        "customization_skin_index", "customization_hair_style_id", "customization_accessory_index",
        "customization_eye_hue", "customization_eye_saturation", "customization_eye_value",
        "customization_hair_hue", "customization_hair_saturation", "customization_hair_value"
    };

    public static JsonObject[] FilterUnattempted(JsonObject[] queueItems, ISet<string> attemptedSemanticKeys)
    {
        return queueItems
            .Where(item => !attemptedSemanticKeys.Contains(SemanticQueueItemKey(item)))
            .ToArray();
    }

    public static JsonObject? ReadSocialContinuation(JsonObject? queueItem)
    {
        var continuation = ReadObjectiveContinuation(queueItem);
        return string.Equals(ReadString(continuation, "kind"), "social", StringComparison.Ordinal)
            ? continuation
            : null;
    }

    public static JsonObject? ReadObjectiveContinuation(JsonObject? queueItem)
    {
        var optionId = ReadParameter(queueItem, "continuation.option_id");
        var prizeLevel = ReadParameter(queueItem, "continuation.expected_prize_level");
        var prizeRewardFingerprint = ReadParameter(queueItem, "continuation.expected_reward_fingerprint");
        if (string.Equals(optionId, "rewards.claim_prize_ticket", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(prizeLevel) && !string.IsNullOrWhiteSpace(prizeRewardFingerprint))
        {
            return new JsonObject
            {
                ["kind"] = "prize_ticket_reward",
                ["option_id"] = optionId,
                ["expected_prize_level"] = prizeLevel,
                ["expected_reward_fingerprint"] = prizeRewardFingerprint
            };
        }
        var fieldOfficeSlot = ReadParameter(queueItem, "continuation.inventory_slot_index");
        var fieldOfficeItem = ReadParameter(queueItem, "continuation.qualified_item_id");
        var fieldOfficePiece = ReadParameter(queueItem, "continuation.target_piece_index");
        if (string.Equals(optionId, "island.field_office_donate", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(fieldOfficeSlot) && !string.IsNullOrWhiteSpace(fieldOfficeItem) &&
            !string.IsNullOrWhiteSpace(fieldOfficePiece) &&
            string.Equals(ReadParameter(queueItem, "continuation.confirm_donation"), "true", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["kind"] = "field_office_donation",
                ["option_id"] = optionId,
                ["inventory_slot_index"] = fieldOfficeSlot,
                ["qualified_item_id"] = fieldOfficeItem,
                ["target_piece_index"] = fieldOfficePiece,
                ["confirm_donation"] = "true"
            };
        }
        var fieldOfficeSurveyKind = ReadParameter(queueItem, "continuation.survey_kind");
        var fieldOfficeSurveyAnswer = ReadParameter(queueItem, "continuation.answer");
        if (string.Equals(optionId, "island.field_office_survey", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(fieldOfficeSurveyKind) && !string.IsNullOrWhiteSpace(fieldOfficeSurveyAnswer))
        {
            return new JsonObject
            {
                ["kind"] = "field_office_survey",
                ["option_id"] = optionId,
                ["survey_kind"] = fieldOfficeSurveyKind,
                ["survey_answer"] = fieldOfficeSurveyAnswer
            };
        }
        var calicoTargetCoins = ReadParameter(queueItem, "continuation.calico_target_club_coins");
        var calicoTargetItem = ReadParameter(queueItem, "continuation.calico_target_item_id");
        if (string.Equals(optionId, "minigame.play_calico_jack", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(calicoTargetCoins) && !string.IsNullOrWhiteSpace(calicoTargetItem))
        {
            return new JsonObject
            {
                ["kind"] = "calico_jack",
                ["option_id"] = optionId,
                ["calico_target_club_coins"] = calicoTargetCoins,
                ["calico_target_item_id"] = calicoTargetItem
            };
        }
        var slotsTargetCoins = ReadParameter(queueItem, "continuation.slots_target_club_coins");
        var slotsTargetItem = ReadParameter(queueItem, "continuation.slots_target_item_id");
        if (string.Equals(optionId, "minigame.play_slots", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(slotsTargetCoins) && !string.IsNullOrWhiteSpace(slotsTargetItem))
        {
            return new JsonObject
            {
                ["kind"] = "slots",
                ["option_id"] = optionId,
                ["slots_target_club_coins"] = slotsTargetCoins,
                ["slots_target_item_id"] = slotsTargetItem
            };
        }
        var craneSelectionPolicy = ReadParameter(queueItem, "continuation.crane_selection_policy");
        var craneFeeGold = ReadParameter(queueItem, "continuation.crane_fee_gold");
        if (string.Equals(optionId, "minigame.play_crane_game", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(craneSelectionPolicy) && !string.IsNullOrWhiteSpace(craneFeeGold))
        {
            return new JsonObject
            {
                ["kind"] = "crane_game",
                ["option_id"] = optionId,
                ["crane_selection_policy"] = craneSelectionPolicy,
                ["crane_fee_gold"] = craneFeeGold
            };
        }
        var dartsDroppedBefore = ReadParameter(queueItem, "continuation.darts_limited_nut_dropped_before");
        var dartsStartingCount = ReadParameter(queueItem, "continuation.darts_starting_dart_count");
        if (string.Equals(optionId, "minigame.play_darts", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(dartsDroppedBefore) && !string.IsNullOrWhiteSpace(dartsStartingCount))
        {
            return new JsonObject
            {
                ["kind"] = "darts_game",
                ["option_id"] = optionId,
                ["darts_limited_nut_dropped_before"] = dartsDroppedBefore,
                ["darts_starting_dart_count"] = dartsStartingCount
            };
        }
        var renovationId = ReadParameter(queueItem, "continuation.renovation_id");
        var renovationSelectedIndex = ReadParameter(queueItem, "continuation.selected_index");
        var renovationReason = ReadParameter(queueItem, "continuation.renovation_reason");
        var renovationConfirmed = ReadParameter(queueItem, "continuation.confirm_renovation");
        if (string.Equals(optionId, "housing.renovate", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(renovationId) &&
            !string.IsNullOrWhiteSpace(renovationSelectedIndex) &&
            !string.IsNullOrWhiteSpace(renovationReason) &&
            string.Equals(renovationConfirmed, "true", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["kind"] = "home_renovation",
                ["option_id"] = optionId,
                ["renovation_id"] = renovationId,
                ["selected_index"] = renovationSelectedIndex,
                ["renovation_reason"] = renovationReason,
                ["confirm_renovation"] = renovationConfirmed,
                ["confirm_destructive"] = ReadParameter(queueItem, "continuation.confirm_destructive")
            };
        }
        var walletOperation = ReadParameter(queueItem, "continuation.wallet_operation");
        var walletReason = ReadParameter(queueItem, "continuation.wallet_reason");
        var walletConfirmed = ReadParameter(queueItem, "continuation.confirm_wallet_operation");
        if (string.Equals(optionId, "multiplayer.manage_wallet", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(walletOperation) && !string.IsNullOrWhiteSpace(walletReason) &&
            string.Equals(walletConfirmed, "true", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["kind"] = "multiplayer_wallet",
                ["option_id"] = optionId,
                ["wallet_operation"] = walletOperation,
                ["wallet_reason"] = walletReason,
                ["confirm_wallet_operation"] = walletConfirmed,
                ["confirm_wallet_transfer"] = ReadParameter(queueItem, "continuation.confirm_wallet_transfer"),
                ["wallet_recipient_player_id"] = ReadParameter(queueItem, "continuation.wallet_recipient_player_id"),
                ["wallet_transfer_amount"] = ReadParameter(queueItem, "continuation.wallet_transfer_amount")
            };
        }
        var bobberStyleId = ReadParameter(queueItem, "continuation.bobber_style_id");
        var bobberReason = ReadParameter(queueItem, "continuation.bobber_reason");
        var bobberConfirmed = ReadParameter(queueItem, "continuation.confirm_bobber_style");
        if (string.Equals(optionId, "player.choose_bobber", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(bobberStyleId) && !string.IsNullOrWhiteSpace(bobberReason) &&
            string.Equals(bobberConfirmed, "true", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["kind"] = "bobber_selection",
                ["option_id"] = optionId,
                ["bobber_style_id"] = bobberStyleId,
                ["bobber_reason"] = bobberReason,
                ["confirm_bobber_style"] = bobberConfirmed
            };
        }
        var jukeboxTrackId = ReadParameter(queueItem, "continuation.jukebox_track_id");
        var jukeboxReason = ReadParameter(queueItem, "continuation.jukebox_reason");
        var jukeboxConfirmed = ReadParameter(queueItem, "continuation.confirm_jukebox_track");
        if (string.Equals(optionId, "player.choose_jukebox_track", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(jukeboxTrackId) && !string.IsNullOrWhiteSpace(jukeboxReason) &&
            string.Equals(jukeboxConfirmed, "true", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["kind"] = "jukebox_selection",
                ["option_id"] = optionId,
                ["jukebox_track_id"] = jukeboxTrackId,
                ["jukebox_reason"] = jukeboxReason,
                ["confirm_jukebox_track"] = jukeboxConfirmed
            };
        }
        var customizationMode = ReadParameter(queueItem, "continuation.customization_mode");
        var customizationReason = ReadParameter(queueItem, "continuation.customization_reason");
        var customizationConfirmed = ReadParameter(queueItem, "continuation.confirm_customization");
        if (string.Equals(optionId, "player.customize", StringComparison.Ordinal) &&
            customizationMode is "wizard_shrine" or "desert_makeover" &&
            !string.IsNullOrWhiteSpace(customizationReason) && string.Equals(customizationConfirmed, "true", StringComparison.Ordinal))
        {
            var result = new JsonObject
            {
                ["kind"] = "player_customization",
                ["option_id"] = optionId,
                ["customization_mode"] = customizationMode,
                ["customization_reason"] = customizationReason,
                ["confirm_customization"] = customizationConfirmed
            };
            foreach (var name in PlayerCustomizationContinuationNames)
            {
                var value = ReadParameter(queueItem, "continuation." + name);
                if (!string.IsNullOrEmpty(value))
                    result[name] = value;
            }
            return result;
        }
        var geodeQid = ReadParameter(queueItem, "continuation.geode_qualified_item_id");
        var geodePurpose = ReadParameter(queueItem, "continuation.geode_purpose");
        if (string.Equals(optionId, "processing.crack_geode", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(geodeQid) && !string.IsNullOrWhiteSpace(geodePurpose))
        {
            return new JsonObject
            {
                ["kind"] = "geode_processing",
                ["option_id"] = optionId,
                ["geode_qualified_item_id"] = geodeQid,
                ["geode_purpose"] = geodePurpose
            };
        }

        var questCandidateId = ReadParameter(queueItem, "continuation.quest_candidate_id");
        if (string.Equals(optionId, "quest.advance", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(questCandidateId))
        {
            return new JsonObject
            {
                ["kind"] = "quest",
                ["option_id"] = optionId,
                ["quest_candidate_id"] = questCandidateId,
                ["npc_name"] = ReadParameter(queueItem, "continuation.npc_name"),
                ["target_location"] = ReadParameter(queueItem, "continuation.target_location"),
                ["slot_index"] = ReadParameter(queueItem, "continuation.slot_index"),
                ["qualified_item_id"] = ReadParameter(queueItem, "continuation.qualified_item_id"),
                ["partnership_action_kind"] = ReadParameter(queueItem, "continuation.partnership_action_kind")
            };
        }

        var shopId = ReadParameter(
            queueItem,
            "continuation.shop_id");
        var qualifiedItemId = ReadParameter(
            queueItem,
            "continuation.qualified_item_id");
        if (string.Equals(
                optionId,
                "economy.buy_supplies",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(shopId) &&
            !string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return new JsonObject
            {
                ["kind"] = "economy_purchase",
                ["option_id"] = optionId,
                ["shop_id"] = shopId,
                ["target_location"] = ReadParameter(
                    queueItem,
                    "continuation.target_location"),
                ["item_id"] = ReadParameter(
                    queueItem,
                    "continuation.item_id"),
                ["qualified_item_id"] = qualifiedItemId,
                ["max_unit_price"] = ReadParameter(
                    queueItem,
                    "continuation.max_unit_price"),
                ["quantity"] = ReadParameter(
                    queueItem,
                    "continuation.quantity")
            };
        }

        if (string.Equals(
                optionId,
                "economy.sell_items",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(shopId) &&
            !string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return new JsonObject
            {
                ["kind"] = "economy_sale",
                ["option_id"] = optionId,
                ["shop_id"] = shopId,
                ["target_location"] = ReadParameter(
                    queueItem,
                    "continuation.target_location"),
                ["item_id"] = ReadParameter(
                    queueItem,
                    "continuation.item_id"),
                ["qualified_item_id"] = qualifiedItemId,
                ["slot_index"] = ReadParameter(
                    queueItem,
                    "continuation.slot_index"),
                ["quantity"] = ReadParameter(
                    queueItem,
                    "continuation.quantity"),
                ["expected_unit_price"] = ReadParameter(
                    queueItem,
                    "continuation.expected_unit_price")
            };
        }

        if (string.Equals(
                optionId,
                "economy.ship_items",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return new JsonObject
            {
                ["kind"] = "economy_shipping",
                ["option_id"] = optionId,
                ["target_location"] = ReadParameter(
                    queueItem,
                    "continuation.target_location"),
                ["item_id"] = ReadParameter(
                    queueItem,
                    "continuation.item_id"),
                ["qualified_item_id"] = qualifiedItemId,
                ["slot_index"] = ReadParameter(
                    queueItem,
                    "continuation.slot_index"),
                ["quantity"] = ReadParameter(
                    queueItem,
                    "continuation.quantity"),
                ["expected_unit_price"] = ReadParameter(
                    queueItem,
                    "continuation.expected_unit_price"),
                ["bin_location"] = ReadParameter(
                    queueItem,
                    "continuation.bin_location"),
                ["bin_tile_x"] = ReadParameter(
                    queueItem,
                    "continuation.bin_tile_x"),
                ["bin_tile_y"] = ReadParameter(
                    queueItem,
                    "continuation.bin_tile_y"),
                ["stand_tile_x"] = ReadParameter(
                    queueItem,
                    "continuation.stand_tile_x"),
                ["stand_tile_y"] = ReadParameter(
                    queueItem,
                    "continuation.stand_tile_y")
            };
        }

        var npcName = ReadParameter(queueItem, "continuation.npc_name");
        if (!string.IsNullOrWhiteSpace(optionId) && !string.IsNullOrWhiteSpace(npcName))
        {
            return new JsonObject
            {
                ["kind"] = "social",
                ["option_id"] = optionId,
                ["npc_name"] = npcName,
                ["target_location"] = ReadParameter(queueItem, "continuation.target_location"),
                ["slot_index"] = ReadParameter(queueItem, "continuation.slot_index"),
                ["qualified_item_id"] = ReadParameter(queueItem, "continuation.qualified_item_id"),
                ["partnership_action_kind"] = ReadParameter(queueItem, "continuation.partnership_action_kind")
            };
        }

        var machineLocation = ReadParameter(queueItem, "continuation.machine_location_id");
        var machinePlacementSlot = ReadParameter(
            queueItem,
            "continuation.machine_inventory_slot_index");
        var machinePlacementQualifiedItemId = ReadParameter(
            queueItem,
            "continuation.machine_qualified_item_id");
        if (string.Equals(
                optionId,
                "executor.place_machine",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(machineLocation) &&
            !string.IsNullOrWhiteSpace(machinePlacementSlot) &&
            !string.IsNullOrWhiteSpace(
                machinePlacementQualifiedItemId))
        {
            return new JsonObject
            {
                ["kind"] = "machine_placement",
                ["option_id"] = "farm.process_machines",
                ["execution_option_id"] = optionId,
                ["machine_location_id"] = machineLocation,
                ["machine_inventory_slot_index"] =
                    machinePlacementSlot,
                ["machine_qualified_item_id"] =
                    machinePlacementQualifiedItemId,
                ["machine_item_id"] = ReadParameter(
                    queueItem,
                    "continuation.machine_item_id"),
                ["relocation_intent_id"] = ReadParameter(
                    queueItem,
                    "continuation.relocation_intent_id")
            };
        }

        var machineTileX = ReadParameter(queueItem, "continuation.machine_tile_x");
        var machineTileY = ReadParameter(queueItem, "continuation.machine_tile_y");
        if (string.IsNullOrWhiteSpace(optionId) || string.IsNullOrWhiteSpace(machineLocation) ||
            string.IsNullOrWhiteSpace(machineTileX) || string.IsNullOrWhiteSpace(machineTileY))
        {
            return null;
        }

        return new JsonObject
        {
            ["kind"] = "machine",
            ["option_id"] = "farm.process_machines",
            ["execution_option_id"] = optionId,
            ["machine_location_id"] = machineLocation,
            ["machine_tile_x"] = machineTileX,
            ["machine_tile_y"] = machineTileY
        };
    }

    public static JsonArray FilterRankedCandidates(JsonArray rankedCandidates, JsonObject? continuation)
    {
        if (continuation is null)
        {
            return JsonNode.Parse(rankedCandidates.ToJsonString())?.AsArray() ?? new JsonArray();
        }

        var filtered = rankedCandidates
            .Select(node => node?.AsObject())
            .Where(candidate => candidate is not null && MatchesContinuation(candidate, continuation))
            .Select(candidate => JsonNode.Parse(candidate!.ToJsonString()))
            .ToArray();
        return new JsonArray(filtered);
    }

    public static JsonArray FilterCandidateKind(
        JsonArray rankedCandidates,
        string requiredKind)
    {
        if (string.IsNullOrWhiteSpace(requiredKind))
        {
            return JsonNode.Parse(rankedCandidates.ToJsonString())?
                .AsArray() ?? new JsonArray();
        }

        var filtered = rankedCandidates
            .Select(node => node?.AsObject())
            .Where(candidate =>
                candidate is not null &&
                string.Equals(
                    ReadString(candidate, "kind"),
                    requiredKind,
                    StringComparison.Ordinal))
            .Select(candidate =>
                JsonNode.Parse(candidate!.ToJsonString()))
            .ToArray();
        return new JsonArray(filtered);
    }

    public static string EffectiveCandidateKindFilter(
        string requestedKind,
        JsonObject? objectiveContinuation)
    {
        return objectiveContinuation is null
            ? requestedKind
            : string.Empty;
    }

    public static JsonArray FilterCandidateId(
        JsonArray rankedCandidates,
        string requiredCandidateId)
    {
        if (string.IsNullOrWhiteSpace(requiredCandidateId))
        {
            return JsonNode.Parse(rankedCandidates.ToJsonString())?
                .AsArray() ?? new JsonArray();
        }

        var filtered = rankedCandidates
            .Select(node => node?.AsObject())
            .Where(candidate =>
                candidate is not null &&
                string.Equals(
                    ReadString(candidate, "candidate_id"),
                    requiredCandidateId,
                    StringComparison.Ordinal))
            .Select(candidate =>
                JsonNode.Parse(candidate!.ToJsonString()))
            .ToArray();
        return new JsonArray(filtered);
    }

    public static string EffectiveCandidateIdFilter(
        string requestedCandidateId,
        JsonObject? objectiveContinuation)
    {
        return objectiveContinuation is null
            ? requestedCandidateId
            : string.Empty;
    }

    public static bool CompletesSocialContinuation(JsonObject? queueItem, JsonObject? continuation, string executionStatus)
    {
        return string.Equals(ReadString(continuation, "kind"), "social", StringComparison.Ordinal) &&
            CompletesObjectiveContinuation(queueItem, continuation, executionStatus);
    }

    public static bool CompletesObjectiveContinuation(JsonObject? queueItem, JsonObject? continuation, string executionStatus)
    {
        if (!string.Equals(executionStatus, "applied", StringComparison.Ordinal) || queueItem is null)
        {
            return false;
        }

        var optionId = ReadString(queueItem, "option_id");
        if (continuation is null)
        {
            return string.Equals(optionId, "executor.social_interact", StringComparison.Ordinal);
        }

        var continuationKind = ReadString(continuation, "kind");
        if (string.Equals(continuationKind, "prize_ticket_reward", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.claim_prize_ticket", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_stage"), "redeem_prize", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_prize_level"), ReadString(continuation, "expected_prize_level"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_current_reward_fingerprint"), ReadString(continuation, "expected_reward_fingerprint"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "field_office_donation", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.donate_field_office_piece", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "inventory_slot_index"), ReadString(continuation, "inventory_slot_index"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "qualified_item_id"), ReadString(continuation, "qualified_item_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "target_piece_index"), ReadString(continuation, "target_piece_index"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "field_office_survey", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.answer_field_office_survey", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "survey_kind"), ReadString(continuation, "survey_kind"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "survey_answer"), ReadString(continuation, "survey_answer"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "calico_jack", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.play_calico_jack", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "calico_target_club_coins"), ReadString(continuation, "calico_target_club_coins"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "calico_target_item_id"), ReadString(continuation, "calico_target_item_id"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "slots", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.play_slots", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "slots_target_club_coins"), ReadString(continuation, "slots_target_club_coins"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "slots_target_item_id"), ReadString(continuation, "slots_target_item_id"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "crane_game", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.play_crane_game", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "crane_selection_policy"), ReadString(continuation, "crane_selection_policy"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "crane_fee_gold"), ReadString(continuation, "crane_fee_gold"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "darts_game", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.play_darts", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "darts_limited_nut_dropped_before"), ReadString(continuation, "darts_limited_nut_dropped_before"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "darts_starting_dart_count"), ReadString(continuation, "darts_starting_dart_count"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "home_renovation", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.renovate_home", StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "renovation_id"),
                    ReadString(continuation, "renovation_id"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "selected_index"),
                    ReadString(continuation, "selected_index"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "multiplayer_wallet", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.manage_multiplayer_wallet", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "wallet_operation"), ReadString(continuation, "wallet_operation"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "wallet_reason"), ReadString(continuation, "wallet_reason"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "wallet_recipient_player_id"), ReadString(continuation, "wallet_recipient_player_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "wallet_transfer_amount"), ReadString(continuation, "wallet_transfer_amount"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "bobber_selection", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.choose_bobber_style", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bobber_style_id"), ReadString(continuation, "bobber_style_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bobber_reason"), ReadString(continuation, "bobber_reason"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "jukebox_selection", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.choose_jukebox_track", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "jukebox_track_id"), ReadString(continuation, "jukebox_track_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "jukebox_reason"), ReadString(continuation, "jukebox_reason"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "player_customization", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.customize_player", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "customization_mode"), ReadString(continuation, "customization_mode"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "customization_reason"), ReadString(continuation, "customization_reason"), StringComparison.Ordinal) &&
                PlayerCustomizationContinuationNames.All(name => string.IsNullOrEmpty(ReadString(continuation, name)) ||
                    string.Equals(ReadParameter(queueItem, name), ReadString(continuation, name), StringComparison.Ordinal));
        }
        if (string.Equals(continuationKind, "geode_processing", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.crack_geode", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "geode_qualified_item_id"), ReadString(continuation, "geode_qualified_item_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "geode_purpose"), ReadString(continuation, "geode_purpose"), StringComparison.Ordinal);
        }

        if (string.Equals(
                continuationKind,
                "economy_purchase",
                StringComparison.Ordinal))
        {
            return string.Equals(
                    optionId,
                    "executor.buy_shop_item",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "expected_shop_id"),
                    ReadString(continuation, "shop_id"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    ReadParameter(queueItem, "qualified_item_id"),
                    ReadString(continuation, "qualified_item_id"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "quantity"),
                    ReadString(continuation, "quantity"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(
                continuationKind,
                "economy_sale",
                StringComparison.Ordinal))
        {
            return string.Equals(
                    optionId,
                    "executor.sell_shop_item",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "expected_shop_id"),
                    ReadString(continuation, "shop_id"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    ReadParameter(queueItem, "qualified_item_id"),
                    ReadString(continuation, "qualified_item_id"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "slot_index"),
                    ReadString(continuation, "slot_index"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "quantity"),
                    ReadString(continuation, "quantity"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "expected_unit_price"),
                    ReadString(continuation, "expected_unit_price"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(
                continuationKind,
                "economy_shipping",
                StringComparison.Ordinal))
        {
            return string.Equals(
                    optionId,
                    "executor.ship_inventory_item_to_bin",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "qualified_item_id"),
                    ReadString(continuation, "qualified_item_id"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "slot_index"),
                    ReadString(continuation, "slot_index"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "quantity"),
                    ReadString(continuation, "quantity"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "expected_unit_price"),
                    ReadString(continuation, "expected_unit_price"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "target_tile_x"),
                    ReadString(continuation, "bin_tile_x"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "target_tile_y"),
                    ReadString(continuation, "bin_tile_y"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "stand_tile_x"),
                    ReadString(continuation, "stand_tile_x"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "stand_tile_y"),
                    ReadString(continuation, "stand_tile_y"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(
                continuationKind,
                "machine_placement",
                StringComparison.Ordinal))
        {
            return string.Equals(
                    optionId,
                    "executor.place_machine",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "location_id"),
                    ReadString(
                        continuation,
                        "machine_location_id"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    ReadParameter(
                        queueItem,
                        "inventory_slot_index"),
                    ReadString(
                        continuation,
                        "machine_inventory_slot_index"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(
                        queueItem,
                        "qualified_item_id"),
                    ReadString(
                        continuation,
                        "machine_qualified_item_id"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "machine", StringComparison.Ordinal))
        {
            return string.Equals(optionId, ReadString(continuation, "execution_option_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "machine_location_id"), ReadString(continuation, "machine_location_id"), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ReadParameter(queueItem, "target_tile_x"), ReadString(continuation, "machine_tile_x"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "target_tile_y"), ReadString(continuation, "machine_tile_y"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "quest", StringComparison.Ordinal))
        {
            return (string.Equals(optionId, "executor.quest_npc_interact", StringComparison.Ordinal) ||
                    string.Equals(optionId, "executor.quest_drop_box_donate", StringComparison.Ordinal)) &&
                string.Equals(
                    ReadParameter(queueItem, "quest_candidate_id"),
                    ReadString(continuation, "quest_candidate_id"),
                    StringComparison.Ordinal);
        }

        if (!string.Equals(optionId, "executor.social_interact", StringComparison.Ordinal))
        {
            return false;
        }

        var npcName = ReadParameter(queueItem, "npc_name");
        var actionKind = ReadParameter(queueItem, "social_action_kind");
        var continuationOption = ReadString(continuation, "option_id");
        var expectedActionKind = string.Equals(continuationOption, "social.gift_npc", StringComparison.Ordinal)
            ? "gift"
            : string.Equals(continuationOption, "social.advance_partnership", StringComparison.Ordinal)
                ? ReadString(continuation, "partnership_action_kind")
                : "talk";
        return string.Equals(npcName, ReadString(continuation, "npc_name"), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionKind, expectedActionKind, StringComparison.Ordinal);
    }

    public static QueueReplanDecision DecideAfterExecution(
        string executionStatus,
        bool continueAfterBlocked,
        bool useDailyPlan,
        bool hasExecutorOverride,
        bool afterSnapshotFresh,
        bool canAttemptMoreItems)
    {
        var continuable = IsContinuableExecutionStatus(executionStatus);
        if (continuable)
        {
            return new QueueReplanDecision(false, false, false, "continuable_execution");
        }

        if (!continueAfterBlocked)
        {
            return new QueueReplanDecision(false, true, false, "continue_after_blocked_disabled");
        }

        if (!useDailyPlan || hasExecutorOverride)
        {
            return new QueueReplanDecision(false, false, false, "non_daily_plan_continue_after_blocked");
        }

        if (!afterSnapshotFresh)
        {
            return new QueueReplanDecision(false, true, false, "stale_after_snapshot");
        }

        if (!canAttemptMoreItems)
        {
            return new QueueReplanDecision(false, true, false, "max_queue_item_attempts_reached");
        }

        return new QueueReplanDecision(true, false, true, "blocked_continue_after_fresh_after_snapshot");
    }

    public static string SemanticQueueItemKey(JsonObject item)
    {
        var optionId = ReadString(item, "option_id");
        var command = item["normalized_command"]?.AsObject();
        var commandType = ReadString(command, "command_type");
        var parameters = command?["parameters"]?.AsArray()
            .Select(node => node?.AsObject())
            .Where(parameter => parameter is not null)
            .Cast<JsonObject>()
            .Select(parameter => new
            {
                Name = ReadString(parameter, "name"),
                Value = ReadString(parameter, "value")
            })
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .Where(parameter => !NonSemanticParameterNames.Contains(parameter.Name))
            .Where(parameter => !parameter.Name.StartsWith("compiler_context.", StringComparison.Ordinal))
            .Where(parameter => !parameter.Name.StartsWith("budget.", StringComparison.Ordinal))
            .OrderBy(parameter => parameter.Name, StringComparer.Ordinal)
            .ThenBy(parameter => parameter.Value, StringComparer.Ordinal)
            .Select(parameter => parameter.Name + "=" + parameter.Value)
            .ToArray() ?? Array.Empty<string>();
        var steps = command?["steps"]?.AsArray()
            .Select(node => node?.AsObject())
            .Where(step => step is not null)
            .Cast<JsonObject>()
            .Select(step => ReadString(step, "step_type") + ":" + ReadString(step, "target"))
            .Where(value => value != ":")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();

        return optionId + "|" + commandType + "|params:" + string.Join(";", parameters) + "|steps:" + string.Join(";", steps);
    }

    private static string ReadString(JsonObject? obj, string propertyName)
    {
        return obj is not null && obj.TryGetPropertyValue(propertyName, out var value) && value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
    }

    private static bool MatchesContinuation(JsonObject candidate, JsonObject continuation)
    {
        var optionId = ReadString(candidate, "option_id");
        if (!string.Equals(optionId, ReadString(continuation, "option_id"), StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(ReadString(continuation, "kind"), "home_renovation", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "renovation_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "selected_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "renovation_reason") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_renovation") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_destructive");
        }
        if (string.Equals(ReadString(continuation, "kind"), "prize_ticket_reward", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "expected_prize_level") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "expected_reward_fingerprint");
        }
        if (string.Equals(ReadString(continuation, "kind"), "multiplayer_wallet", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "wallet_operation") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "wallet_reason") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_wallet_operation") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_wallet_transfer") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "wallet_recipient_player_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "wallet_transfer_amount");
        }
        if (string.Equals(ReadString(continuation, "kind"), "bobber_selection", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "bobber_style_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "bobber_reason") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_bobber_style");
        }
        if (string.Equals(ReadString(continuation, "kind"), "jukebox_selection", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "jukebox_track_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "jukebox_reason") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_jukebox_track");
        }
        if (string.Equals(ReadString(continuation, "kind"), "geode_processing", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "geode_qualified_item_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "geode_purpose");
        }

        if (string.Equals(ReadString(continuation, "kind"), "field_office_donation", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "inventory_slot_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "qualified_item_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "target_piece_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_donation");
        }

        if (string.Equals(ReadString(continuation, "kind"), "field_office_survey", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "survey_kind") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "survey_answer");
        }
        if (string.Equals(ReadString(continuation, "kind"), "calico_jack", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "calico_target_club_coins") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "calico_target_item_id");
        }
        if (string.Equals(ReadString(continuation, "kind"), "slots", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "slots_target_club_coins") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "slots_target_item_id");
        }
        if (string.Equals(ReadString(continuation, "kind"), "crane_game", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "crane_selection_policy") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "crane_fee_gold");
        }

        if (string.Equals(
                ReadString(continuation, "kind"),
                "economy_purchase",
                StringComparison.Ordinal))
        {
            var candidateShopId = ReadString(candidate, "shop_id");
            if (string.IsNullOrWhiteSpace(candidateShopId))
            {
                candidateShopId = ReadCandidateParameter(
                    candidate,
                    "continuation.shop_id");
            }
            var candidateQualifiedItemId = ReadString(
                candidate,
                "qualified_item_id");
            if (string.IsNullOrWhiteSpace(candidateQualifiedItemId))
            {
                candidateQualifiedItemId = ReadCandidateParameter(
                    candidate,
                    "continuation.qualified_item_id");
            }
            return string.Equals(
                    candidateShopId,
                    ReadString(continuation, "shop_id"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidateQualifiedItemId,
                    ReadString(continuation, "qualified_item_id"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(
                ReadString(continuation, "kind"),
                "economy_sale",
                StringComparison.Ordinal))
        {
            var candidateShopId = ReadString(candidate, "shop_id");
            if (string.IsNullOrWhiteSpace(candidateShopId))
            {
                candidateShopId = ReadCandidateParameter(
                    candidate,
                    "continuation.shop_id");
            }
            var candidateQualifiedItemId = ReadString(
                candidate,
                "qualified_item_id");
            if (string.IsNullOrWhiteSpace(candidateQualifiedItemId))
            {
                candidateQualifiedItemId = ReadCandidateParameter(
                    candidate,
                    "continuation.qualified_item_id");
            }
            var candidateSlotIndex = ReadString(candidate, "slot_index");
            if (string.IsNullOrWhiteSpace(candidateSlotIndex))
            {
                candidateSlotIndex = ReadCandidateParameter(
                    candidate,
                    "continuation.slot_index");
            }
            return string.Equals(
                    candidateShopId,
                    ReadString(continuation, "shop_id"),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidateQualifiedItemId,
                    ReadString(continuation, "qualified_item_id"),
                    StringComparison.Ordinal) &&
                string.Equals(
                    candidateSlotIndex,
                    ReadString(continuation, "slot_index"),
                    StringComparison.Ordinal);
        }
        if (string.Equals(
                ReadString(continuation, "kind"),
                "economy_shipping",
                StringComparison.Ordinal))
        {
            return OptionalIdentityMatches(
                    candidate,
                    continuation,
                    "qualified_item_id",
                    "continuation.qualified_item_id") &&
                OptionalIdentityMatches(
                    candidate,
                    continuation,
                    "slot_index",
                    "continuation.slot_index") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "quantity") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "expected_unit_price") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "bin_location") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "bin_tile_x") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "bin_tile_y") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "stand_tile_x") &&
                CandidateParameterMatchesContinuation(
                    candidate,
                    continuation,
                    "stand_tile_y");
        }

        if (string.Equals(ReadString(continuation, "kind"), "machine", StringComparison.Ordinal))
        {
            return MatchesMachineContinuation(candidate, continuation);
        }
        if (string.Equals(
                ReadString(continuation, "kind"),
                "machine_placement",
                StringComparison.Ordinal))
        {
            return MatchesMachinePlacementContinuation(
                candidate,
                continuation);
        }
        if (string.Equals(ReadString(continuation, "kind"), "quest", StringComparison.Ordinal))
        {
            return string.Equals(
                ReadCandidateParameter(candidate, "quest_candidate_id"),
                ReadString(continuation, "quest_candidate_id"),
                StringComparison.Ordinal);
        }

        var npcName = ReadCandidateParameter(candidate, "continuation.npc_name");
        if (string.IsNullOrWhiteSpace(npcName))
        {
            npcName = ReadCandidateParameter(candidate, "npc_name");
        }
        if (!string.Equals(npcName, ReadString(continuation, "npc_name"), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return OptionalIdentityMatches(candidate, continuation, "slot_index", "continuation.slot_index") &&
            OptionalIdentityMatches(candidate, continuation, "qualified_item_id", "continuation.qualified_item_id") &&
            OptionalIdentityMatches(candidate, continuation, "partnership_action_kind", "continuation.partnership_action_kind");
    }

    private static bool MatchesMachineContinuation(JsonObject candidate, JsonObject continuation)
    {
        var expectedExecutionOption = ReadString(continuation, "execution_option_id");
        var candidateExecutionOption = ReadCandidateParameter(candidate, "continuation.option_id");
        var expectedLocation = ReadString(continuation, "machine_location_id");
        var candidateLocation = ReadCandidateParameter(candidate, "continuation.machine_location_id");
        var expectedX = ReadString(continuation, "machine_tile_x");
        var expectedY = ReadString(continuation, "machine_tile_y");
        var candidateX = ReadCandidateParameter(candidate, "continuation.machine_tile_x");
        var candidateY = ReadCandidateParameter(candidate, "continuation.machine_tile_y");

        if (string.IsNullOrWhiteSpace(candidateExecutionOption))
        {
            var kind = ReadString(candidate, "kind");
            candidateExecutionOption = string.Equals(kind, "collect_machine_output_tile", StringComparison.Ordinal)
                ? "executor.collect_machine_output"
                : string.Equals(kind, "load_machine_input_tile", StringComparison.Ordinal)
                    ? "executor.load_machine_input"
                    : string.Equals(kind, "craft_machine_item", StringComparison.Ordinal)
                        ? "executor.craft_machine_item"
                    : string.Equals(kind, "craft_storage_item", StringComparison.Ordinal)
                        ? "executor.craft_storage_item"
                    : string.Empty;
            candidateLocation = ReadString(candidate, "location_id");
            candidateX = candidate["tile_x"]?.ToString() ?? string.Empty;
            candidateY = candidate["tile_y"]?.ToString() ?? string.Empty;
        }

        return string.Equals(candidateExecutionOption, expectedExecutionOption, StringComparison.Ordinal) &&
            string.Equals(candidateLocation, expectedLocation, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidateX, expectedX, StringComparison.Ordinal) &&
            string.Equals(candidateY, expectedY, StringComparison.Ordinal);
    }

    private static bool MatchesMachinePlacementContinuation(
        JsonObject candidate,
        JsonObject continuation)
    {
        var kind = ReadString(candidate, "kind");
        var location = ReadString(candidate, "location_id");
        var slot = candidate["slot_index"]?.ToString() ??
            string.Empty;
        var qualifiedItemId = ReadString(
            candidate,
            "qualified_item_id");
        var relocationIntentId = ReadCandidateParameter(
            candidate,
            "relocation_intent_id");
        if (string.Equals(
                kind,
                "route_connector_tile",
                StringComparison.Ordinal))
        {
            location = ReadCandidateParameter(
                candidate,
                "continuation.machine_location_id");
            slot = ReadCandidateParameter(
                candidate,
                "continuation.machine_inventory_slot_index");
            qualifiedItemId = ReadCandidateParameter(
                candidate,
                "continuation.machine_qualified_item_id");
            relocationIntentId = ReadCandidateParameter(
                candidate,
                "continuation.relocation_intent_id");
        }
        else if (!string.Equals(
                     kind,
                     "place_machine_item",
                     StringComparison.Ordinal))
        {
            return false;
        }

        return string.Equals(
                location,
                ReadString(
                    continuation,
                    "machine_location_id"),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                slot,
                ReadString(
                    continuation,
                    "machine_inventory_slot_index"),
                StringComparison.Ordinal) &&
            string.Equals(
                qualifiedItemId,
                ReadString(
                    continuation,
                    "machine_qualified_item_id"),
                StringComparison.Ordinal) &&
            OptionalValueMatches(
                relocationIntentId,
                ReadString(
                    continuation,
                    "relocation_intent_id"));
    }

    private static bool OptionalValueMatches(
        string actual,
        string expected)
    {
        return string.IsNullOrWhiteSpace(expected) ||
            string.Equals(
                actual,
                expected,
                StringComparison.Ordinal);
    }

    private static bool OptionalIdentityMatches(JsonObject candidate, JsonObject continuation, string directName, string continuationName)
    {
        var expected = ReadString(continuation, directName);
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var actual = ReadCandidateParameter(candidate, continuationName);
        if (string.IsNullOrWhiteSpace(actual))
        {
            actual = ReadCandidateParameter(candidate, directName);
        }
        if (string.IsNullOrWhiteSpace(actual) && candidate.TryGetPropertyValue(directName, out var directValue))
        {
            actual = directValue?.ToString() ?? string.Empty;
        }
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static bool CandidateParameterMatchesContinuation(
        JsonObject candidate,
        JsonObject continuation,
        string name)
    {
        var expected = ReadString(continuation, name);
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        var actual = ReadCandidateParameter(candidate, "continuation." + name);
        if (string.IsNullOrWhiteSpace(actual))
        {
            actual = ReadCandidateParameter(candidate, name);
        }
        if (string.IsNullOrWhiteSpace(actual) &&
            candidate.TryGetPropertyValue(name, out var directValue))
        {
            actual = directValue?.ToString() ?? string.Empty;
        }
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadCandidateParameter(JsonObject candidate, string name)
    {
        var parameters = candidate["parameters"]?.AsArray();
        if (parameters is null)
        {
            return string.Empty;
        }

        var parameter = parameters
            .Select(node => node?.AsObject())
            .FirstOrDefault(value => value is not null && string.Equals(ReadString(value, "name"), name, StringComparison.Ordinal));
        return ReadString(parameter, "value");
    }

    private static string ReadParameter(JsonObject? queueItem, string name)
    {
        var parameters = queueItem?["normalized_command"]?["parameters"]?.AsArray();
        if (parameters is null)
        {
            return string.Empty;
        }

        var parameter = parameters
            .Select(node => node?.AsObject())
            .FirstOrDefault(value => value is not null && string.Equals(ReadString(value, "name"), name, StringComparison.Ordinal));
        return ReadString(parameter, "value");
    }

    private static bool IsContinuableExecutionStatus(string status)
    {
        return string.Equals(status, "applied", StringComparison.Ordinal) ||
            string.Equals(status, "no_op", StringComparison.Ordinal);
    }
}

public readonly struct QueueReplanDecision
{
    public QueueReplanDecision(bool shouldReplan, bool shouldStop, bool shouldFilterRegeneratedQueue, string reason)
    {
        ShouldReplan = shouldReplan;
        ShouldStop = shouldStop;
        ShouldFilterRegeneratedQueue = shouldFilterRegeneratedQueue;
        Reason = reason;
    }

    public bool ShouldReplan { get; }
    public bool ShouldStop { get; }
    public bool ShouldFilterRegeneratedQueue { get; }
    public string Reason { get; }
}
