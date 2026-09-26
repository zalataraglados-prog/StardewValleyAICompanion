using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
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

    public static JsonArray FilterSuppressedContinuations(
        JsonArray rankedCandidates,
        IReadOnlyCollection<JsonObject> suppressedContinuations)
    {
        if (suppressedContinuations.Count == 0)
        {
            return JsonNode.Parse(rankedCandidates.ToJsonString())?.AsArray() ?? new JsonArray();
        }

        var filtered = rankedCandidates
            .Select(node => node?.AsObject())
            .Where(candidate => candidate is not null &&
                !suppressedContinuations.Any(continuation =>
                    MatchesContinuation(candidate, continuation)))
            .Select(candidate => JsonNode.Parse(candidate!.ToJsonString()))
            .ToArray();
        return new JsonArray(filtered);
    }

    public static bool AddSuppressedContinuation(
        ICollection<JsonObject> suppressedContinuations,
        JsonObject? continuation)
    {
        var identity = ContinuationIdentity(continuation);
        if (string.IsNullOrWhiteSpace(identity) ||
            suppressedContinuations.Any(existing =>
                string.Equals(
                    ContinuationIdentity(existing),
                    identity,
                    StringComparison.Ordinal)))
        {
            return false;
        }

        suppressedContinuations.Add(
            JsonNode.Parse(continuation!.ToJsonString())!.AsObject());
        return true;
    }

    private static string ContinuationIdentity(JsonObject? continuation)
    {
        if (continuation is null ||
            string.IsNullOrWhiteSpace(ReadString(continuation, "kind")) ||
            string.IsNullOrWhiteSpace(ReadString(continuation, "option_id")))
        {
            return string.Empty;
        }

        return string.Join(
            "\n",
            continuation
                .Where(property => property.Value is JsonValue)
                .OrderBy(property => property.Key, StringComparer.Ordinal)
                .Select(property =>
                    property.Key + "=" + property.Value!.ToJsonString()));
    }

    public static string SnapshotDayKey(JsonObject snapshot)
    {
        var time = snapshot["state"]?["time"]?.AsObject();
        var year = ReadSnapshotField(time, "year");
        var season = ReadSnapshotField(time, "season");
        var day = ReadSnapshotField(time, "day");
        return string.IsNullOrWhiteSpace(year) ||
            string.IsNullOrWhiteSpace(season) ||
            string.IsNullOrWhiteSpace(day)
                ? string.Empty
                : string.Join(":", year, season, day);
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

    public static bool CompletesObjectiveContinuation(
        JsonObject? queueItem,
        JsonObject? continuation,
        string executionStatus,
        JsonObject? afterSnapshot,
        bool afterSnapshotFresh)
    {
        if (!string.Equals(
                ReadString(continuation, "kind"),
                "master_angler",
                StringComparison.Ordinal))
        {
            return CompletesObjectiveContinuation(
                queueItem,
                continuation,
                executionStatus);
        }

        return afterSnapshotFresh &&
            string.Equals(executionStatus, "applied", StringComparison.Ordinal) &&
            (string.Equals(
                 ReadString(queueItem, "option_id"),
                 "executor.catch_fish",
                 StringComparison.Ordinal) ||
             string.Equals(
                 ReadString(queueItem, "option_id"),
                 "executor.collect_crab_pot",
                 StringComparison.Ordinal)) &&
            HasExactCaughtMasterAnglerTarget(
                afterSnapshot,
                ReadString(
                    continuation,
                    "master_angler_target_qualified_item_id"));
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
        if (string.Equals(continuationKind, "mail", StringComparison.Ordinal))
        {
            if (!string.Equals(
                    optionId,
                    "executor.close_menu",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(queueItem, "target_runtime_type"),
                    "LetterViewerMenu",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(
                    ReadParameter(queueItem, "mail_menu_identity_sha256")))
            {
                return false;
            }

            var expectedMenuIdentity = ReadString(
                continuation,
                "mail_menu_identity_sha256");
            return string.IsNullOrWhiteSpace(expectedMenuIdentity) ||
                string.Equals(
                    ReadParameter(queueItem, "mail_menu_identity_sha256"),
                    expectedMenuIdentity,
                    StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "movie_theater", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.watch_movie", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "movie_stage"), "watch_movie_screening", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "continuation.movie_objective_key"), ReadString(continuation, "movie_objective_key"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "prize_ticket_reward", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.claim_prize_ticket", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_stage"), "redeem_prize", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_prize_level"), ReadString(continuation, "expected_prize_level"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "prize_ticket_current_reward_fingerprint"), ReadString(continuation, "expected_reward_fingerprint"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "mastery_claim", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.claim_mastery", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "mastery_skill_id"), ReadString(continuation, "mastery_skill_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "mastery_option_fingerprint"), ReadString(continuation, "mastery_option_fingerprint"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "field_office_donation", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.donate_field_office_piece", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "inventory_slot_index"), ReadString(continuation, "inventory_slot_index"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "qualified_item_id"), ReadString(continuation, "qualified_item_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "target_piece_index"), ReadString(continuation, "target_piece_index"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "museum_donation", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.donate_museum_item", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "inventory_slot_index"), ReadString(continuation, "inventory_slot_index"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "qualified_item_id"), ReadString(continuation, "qualified_item_id"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "community_center_donation", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.donate_community_center_item", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_data_key"), ReadString(continuation, "bundle_data_key"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_ingredient_index"), ReadString(continuation, "bundle_ingredient_index"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "inventory_slot_index"), ReadString(continuation, "inventory_slot_index"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "qualified_item_id"), ReadString(continuation, "qualified_item_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "expected_item_quality"), ReadString(continuation, "expected_item_quality"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "required_stack"), ReadString(continuation, "required_stack"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "community_center_reward", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.claim_community_center_bundle_reward", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_data_key"), ReadString(continuation, "bundle_data_key"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_id"), ReadString(continuation, "bundle_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "qualified_item_id"), ReadString(continuation, "qualified_item_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "reward_claim_mode"), ReadString(continuation, "reward_claim_mode"), StringComparison.Ordinal);
        }
        if (string.Equals(continuationKind, "community_center_money_payment", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.pay_community_center_vault_bundle", StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_data_key"), ReadString(continuation, "bundle_data_key"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "bundle_id"), ReadString(continuation, "bundle_id"), StringComparison.Ordinal) &&
                string.Equals(ReadParameter(queueItem, "price"), ReadString(continuation, "price"), StringComparison.Ordinal);
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
        if (string.Equals(continuationKind, "prairie_king", StringComparison.Ordinal))
        {
            return string.Equals(optionId, "executor.play_prairie_king", StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(queueItem, "prairie_king_completion_goal"),
                    ReadString(continuation, "prairie_king_completion_goal"),
                    StringComparison.Ordinal);
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

    public static JsonObject RefreshAppliedObjectiveContinuation(
        JsonObject? queueItem,
        JsonObject continuation)
    {
        if (string.Equals(
                ReadString(continuation, "kind"),
                "master_angler",
                StringComparison.Ordinal))
        {
            var refreshedMasterAngler = ReadObjectiveContinuation(queueItem);
            return refreshedMasterAngler is not null &&
                string.Equals(
                    ReadString(refreshedMasterAngler, "kind"),
                    "master_angler",
                    StringComparison.Ordinal) &&
                MasterAnglerContinuationsHaveSameStableIdentity(
                    refreshedMasterAngler,
                    continuation)
                    ? refreshedMasterAngler
                    : continuation;
        }

        if (!string.Equals(
                ReadString(continuation, "kind"),
                "social",
                StringComparison.Ordinal))
        {
            return continuation;
        }

        var refreshed = ReadSocialContinuation(queueItem);
        if (refreshed is null ||
            !string.Equals(
                ReadString(refreshed, "option_id"),
                ReadString(continuation, "option_id"),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(refreshed, "npc_name"),
                ReadString(continuation, "npc_name"),
                StringComparison.OrdinalIgnoreCase) ||
            !OptionalContinuationIdentityMatches(
                refreshed,
                continuation,
                "slot_index") ||
            !OptionalContinuationIdentityMatches(
                refreshed,
                continuation,
                "qualified_item_id") ||
            !OptionalContinuationIdentityMatches(
                refreshed,
                continuation,
                "partnership_action_kind"))
        {
            return continuation;
        }

        return refreshed;
    }

    private static bool MasterAnglerContinuationsHaveSameStableIdentity(
        JsonObject first,
        JsonObject second)
    {
        return string.Equals(
                ReadString(first, "option_id"),
                ReadString(second, "option_id"),
                StringComparison.Ordinal) &&
            MasterAnglerContinuationNames
                .Where(name => !string.Equals(
                    name,
                    "master_angler_effective_start_time",
                    StringComparison.Ordinal))
                .All(name => string.Equals(
                    ReadString(first, name),
                    ReadString(second, name),
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExactCaughtMasterAnglerTarget(
        JsonObject? snapshot,
        string targetQualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(targetQualifiedItemId) ||
            snapshot?["state"] is not JsonObject state ||
            state["world_progress"] is not JsonObject worldProgress ||
            worldProgress["fish_collection_progress"] is not JsonObject field ||
            field["value"] is not JsonObject progress ||
            ReadIntNode(progress, "eligible_species_count") != 72 ||
            progress["items"] is not JsonArray items ||
            items.Count != 72 ||
            progress["missing_item_ids"] is not JsonArray missingItemIds)
        {
            return false;
        }

        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        var seenQualifiedItemIds = new HashSet<string>(StringComparer.Ordinal);
        var expectedMissingItemIds = new HashSet<string>(StringComparer.Ordinal);
        var targetCaught = false;
        var caughtCount = 0;
        foreach (var node in items)
        {
            if (node is not JsonObject item ||
                !TryReadBooleanNode(item, "caught", out var caught))
            {
                return false;
            }
            var itemId = ReadString(item, "item_id");
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            if (string.IsNullOrWhiteSpace(itemId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !seenItemIds.Add(itemId) ||
                !seenQualifiedItemIds.Add(qualifiedItemId))
            {
                return false;
            }
            if (caught)
            {
                caughtCount++;
                if (string.Equals(
                        qualifiedItemId,
                        targetQualifiedItemId,
                        StringComparison.Ordinal))
                {
                    targetCaught = true;
                }
            }
            else
            {
                expectedMissingItemIds.Add(itemId);
            }
        }

        var actualMissingItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in missingItemIds)
        {
            if (node is not JsonValue value ||
                !value.TryGetValue<string>(out var itemId) ||
                string.IsNullOrWhiteSpace(itemId) ||
                !actualMissingItemIds.Add(itemId))
            {
                return false;
            }
        }

        return targetCaught &&
            caughtCount == ReadIntNode(
                progress,
                "caught_eligible_species_count") &&
            expectedMissingItemIds.Count == ReadIntNode(
                progress,
                "missing_species_count") &&
            expectedMissingItemIds.SetEquals(actualMissingItemIds);
    }

    private static int ReadIntNode(JsonObject source, string name)
    {
        return source[name] is JsonValue value &&
            value.TryGetValue<int>(out var result)
                ? result
                : -1;
    }

    private static bool TryReadBooleanNode(
        JsonObject source,
        string name,
        out bool result)
    {
        result = false;
        return source[name] is JsonValue value &&
            value.TryGetValue<bool>(out result);
    }

    private static bool OptionalContinuationIdentityMatches(
        JsonObject refreshed,
        JsonObject existing,
        string propertyName)
    {
        var expected = ReadString(existing, propertyName);
        return string.IsNullOrWhiteSpace(expected) ||
            string.Equals(
                ReadString(refreshed, propertyName),
                expected,
                StringComparison.Ordinal);
    }

}
