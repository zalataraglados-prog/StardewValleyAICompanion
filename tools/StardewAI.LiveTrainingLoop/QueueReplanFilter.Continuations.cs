using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static JsonObject? ReadObjectiveContinuation(JsonObject? queueItem)
    {
        var optionId = ReadParameter(queueItem, "continuation.option_id");
        var masterAnglerTarget = ReadParameter(
            queueItem,
            "continuation.master_angler_target_qualified_item_id");
        if ((string.Equals(optionId, "fishing.catch_fish", StringComparison.Ordinal) ||
             string.Equals(optionId, "fishing.collect_crab_pots", StringComparison.Ordinal)) &&
            !string.IsNullOrWhiteSpace(masterAnglerTarget))
        {
            var result = new JsonObject
            {
                ["kind"] = "master_angler",
                ["option_id"] = optionId
            };
            foreach (var name in MasterAnglerContinuationNames)
            {
                var value = ReadParameter(
                    queueItem,
                    "continuation." + name);
                if (string.IsNullOrWhiteSpace(value))
                {
                    return null;
                }
                result[name] = value;
            }
            return result;
        }
        if (string.Equals(optionId, "mail.process_letter", StringComparison.Ordinal))
        {
            var mailId = ReadParameter(queueItem, "continuation.mail_id");
            if (string.IsNullOrWhiteSpace(mailId))
            {
                mailId = ReadParameter(queueItem, "target_runtime_identity");
            }
            var mailMenuIdentity = ReadParameter(
                queueItem,
                "continuation.mail_menu_identity_sha256");
            if (string.IsNullOrWhiteSpace(mailMenuIdentity))
            {
                mailMenuIdentity = ReadParameter(
                    queueItem,
                    "mail_menu_identity_sha256");
            }

            if (!string.IsNullOrWhiteSpace(mailId) ||
                !string.IsNullOrWhiteSpace(mailMenuIdentity))
            {
                return new JsonObject
                {
                    ["kind"] = "mail",
                    ["option_id"] = optionId,
                    ["mail_id"] = mailId,
                    ["mail_data_sha256"] = ReadParameter(
                        queueItem,
                        "continuation.mail_data_sha256"),
                    ["mail_menu_identity_sha256"] = mailMenuIdentity,
                    ["target_location"] = ReadParameter(
                        queueItem,
                        "continuation.target_location")
                };
            }
        }
        var movieId = ReadParameter(queueItem, "continuation.movie_id");
        var movieGuest = ReadParameter(queueItem, "continuation.movie_guest_name");
        var movieObjectiveKey = ReadParameter(queueItem, "continuation.movie_objective_key");
        if (string.Equals(optionId, "social.watch_movie", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(movieId) && !string.IsNullOrWhiteSpace(movieGuest) &&
            !string.IsNullOrWhiteSpace(movieObjectiveKey))
        {
            return new JsonObject
            {
                ["kind"] = "movie_theater",
                ["option_id"] = optionId,
                ["movie_id"] = movieId,
                ["movie_guest_name"] = movieGuest,
                ["movie_concession_id"] = ReadParameter(queueItem, "continuation.movie_concession_id"),
                ["movie_objective_key"] = movieObjectiveKey,
                ["movie_friendship_effective"] = ReadParameter(queueItem, "continuation.movie_friendship_effective"),
                ["movie_concession_friendship_effective"] = ReadParameter(queueItem, "continuation.movie_concession_friendship_effective")
            };
        }
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
        var masterySkillId = ReadParameter(queueItem, "continuation.mastery_skill_id");
        var masteryOptionFingerprint = ReadParameter(queueItem, "continuation.mastery_option_fingerprint");
        if (string.Equals(optionId, "skills.claim_mastery", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(masterySkillId) && !string.IsNullOrWhiteSpace(masteryOptionFingerprint))
        {
            return new JsonObject
            {
                ["kind"] = "mastery_claim",
                ["option_id"] = optionId,
                ["mastery_skill_id"] = masterySkillId,
                ["mastery_option_fingerprint"] = masteryOptionFingerprint
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
        var museumSlot = ReadParameter(queueItem, "continuation.inventory_slot_index");
        var museumItem = ReadParameter(queueItem, "continuation.qualified_item_id");
        if (string.Equals(optionId, "museum.donate_items", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(museumSlot) &&
            !string.IsNullOrWhiteSpace(museumItem))
        {
            return new JsonObject
            {
                ["kind"] = "museum_donation",
                ["option_id"] = optionId,
                ["inventory_slot_index"] = museumSlot,
                ["qualified_item_id"] = museumItem
            };
        }
        var communityBundleKey = ReadParameter(
            queueItem,
            "continuation.bundle_data_key");
        var communityIngredient = ReadParameter(
            queueItem,
            "continuation.bundle_ingredient_index");
        var communitySlot = ReadParameter(
            queueItem,
            "continuation.inventory_slot_index");
        var communityItem = ReadParameter(
            queueItem,
            "continuation.qualified_item_id");
        var communityQuality = ReadParameter(
            queueItem,
            "continuation.expected_item_quality");
        var communityRequiredStack = ReadParameter(
            queueItem,
            "continuation.required_stack");
        if (string.Equals(
                optionId,
                "community_center.donate_bundle_items",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(communityBundleKey) &&
            !string.IsNullOrWhiteSpace(communityIngredient) &&
            !string.IsNullOrWhiteSpace(communitySlot) &&
            !string.IsNullOrWhiteSpace(communityItem) &&
            !string.IsNullOrWhiteSpace(communityQuality) &&
            !string.IsNullOrWhiteSpace(communityRequiredStack))
        {
            return new JsonObject
            {
                ["kind"] = "community_center_donation",
                ["option_id"] = optionId,
                ["bundle_data_key"] = communityBundleKey,
                ["bundle_ingredient_index"] = communityIngredient,
                ["inventory_slot_index"] = communitySlot,
                ["qualified_item_id"] = communityItem,
                ["expected_item_quality"] = communityQuality,
                ["required_stack"] = communityRequiredStack
            };
        }
        var communityRewardBundleId = ReadParameter(
            queueItem,
            "continuation.bundle_id");
        var communityPaymentAmount = ReadParameter(
            queueItem,
            "continuation.required_money");
        var communityPaymentRouteKind = ReadParameter(
            queueItem,
            "continuation.route_kind");
        var communityPaymentSourceId = ReadParameter(
            queueItem,
            "continuation.source_id");
        if (string.Equals(
                optionId,
                "community_center.donate_bundle_items",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(communityBundleKey) &&
            !string.IsNullOrWhiteSpace(communityRewardBundleId) &&
            !string.IsNullOrWhiteSpace(communityPaymentAmount) &&
            communityPaymentRouteKind == "native_money_payment" &&
            communityPaymentSourceId == "money")
        {
            return new JsonObject
            {
                ["kind"] = "community_center_money_payment",
                ["option_id"] = optionId,
                ["bundle_data_key"] = communityBundleKey,
                ["bundle_id"] = communityRewardBundleId,
                ["price"] = communityPaymentAmount,
                ["route_kind"] = communityPaymentRouteKind,
                ["source_id"] = communityPaymentSourceId
            };
        }
        var communityRewardMode = ReadParameter(
            queueItem,
            "continuation.reward_claim_mode");
        if (string.Equals(
                optionId,
                "community_center.donate_bundle_items",
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(communityBundleKey) &&
            !string.IsNullOrWhiteSpace(communityRewardBundleId) &&
            !string.IsNullOrWhiteSpace(communityItem) &&
            !string.IsNullOrWhiteSpace(communityRewardMode))
        {
            return new JsonObject
            {
                ["kind"] = "community_center_reward",
                ["option_id"] = optionId,
                ["bundle_data_key"] = communityBundleKey,
                ["bundle_id"] = communityRewardBundleId,
                ["qualified_item_id"] = communityItem,
                ["reward_claim_mode"] = communityRewardMode
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
        var prairieKingCompletionGoal = ReadParameter(
            queueItem,
            "continuation.prairie_king_completion_goal");
        if (string.Equals(optionId, "minigame.play_prairie_king", StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(prairieKingCompletionGoal))
        {
            return new JsonObject
            {
                ["kind"] = "prairie_king",
                ["option_id"] = optionId,
                ["prairie_king_completion_goal"] = prairieKingCompletionGoal
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
                ["partnership_action_kind"] = ReadParameter(queueItem, "continuation.partnership_action_kind"),
                ["retry_count"] = ReadParameter(queueItem, "continuation.retry_count"),
                ["retry_game_time"] = ReadParameter(queueItem, "continuation.retry_game_time")
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

}
