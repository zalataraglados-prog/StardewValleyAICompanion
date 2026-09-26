using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static partial class QueueReplanFilter
{
    public static QueueReplanDecision DecideAfterExecution(
        string executionStatus,
        bool continueAfterBlocked,
        bool useDailyPlan,
        bool hasExecutorOverride,
        bool afterSnapshotFresh,
        bool canAttemptMoreItems,
        bool requiresFreshSnapshotReplan,
        bool objectiveContinuationCompleted = false)
    {
        var continuable = IsContinuableExecutionStatus(executionStatus);
        if (continuable)
        {
            if (objectiveContinuationCompleted)
            {
                return new QueueReplanDecision(
                    false,
                    false,
                    false,
                    "selected_queue_candidate_completed_continue_in_order");
            }
            if (!requiresFreshSnapshotReplan)
            {
                return new QueueReplanDecision(false, false, false, "continuable_execution");
            }
            if (!useDailyPlan || hasExecutorOverride)
            {
                return new QueueReplanDecision(
                    false,
                    false,
                    false,
                    "non_daily_plan_fresh_snapshot_replan_not_applicable");
            }
            if (!afterSnapshotFresh)
            {
                return new QueueReplanDecision(false, true, false, "stale_after_snapshot");
            }
            if (!canAttemptMoreItems)
            {
                return new QueueReplanDecision(
                    false,
                    true,
                    false,
                    "max_queue_item_attempts_reached");
            }

            return new QueueReplanDecision(
                true,
                false,
                true,
                "continuable_execution_requires_fresh_snapshot_replan");
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

    public static bool RequiresFreshSnapshotReplan(JsonObject? queueItem)
    {
        if (queueItem?["normalized_command"]?["parameters"] is not JsonArray parameters)
        {
            return false;
        }

        foreach (var node in parameters)
        {
            if (node is not JsonObject parameter)
            {
                continue;
            }

            var name = ReadString(parameter, "name");
            var value = ReadString(parameter, "value");
            if ((string.Equals(
                     name,
                     "fresh_snapshot_replan_required",
                     StringComparison.Ordinal) ||
                 string.Equals(
                     name,
                     "compiler_context.fresh_snapshot_replan_required",
                     StringComparison.Ordinal)) &&
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(name, "expected_effect", StringComparison.Ordinal) &&
                value.Contains(
                    "fresh_snapshot_replan_required=true",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    private static string ReadSnapshotField(JsonObject? section, string fieldName)
    {
        if (section?[fieldName] is not JsonObject field || field["value"] is not JsonValue value)
        {
            return string.Empty;
        }

        return value.TryGetValue<string>(out var text)
            ? text
            : value.TryGetValue<int>(out var number)
                ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
    }

    private static bool MatchesContinuation(JsonObject candidate, JsonObject continuation)
    {
        var optionId = ReadString(candidate, "option_id");
        if (!string.Equals(optionId, ReadString(continuation, "option_id"), StringComparison.Ordinal))
        {
            return false;
        }

        if (string.Equals(
                ReadString(continuation, "kind"),
                "master_angler",
                StringComparison.Ordinal))
        {
            var kind = ReadString(candidate, "kind");
            var expectedOption = ReadString(continuation, "option_id");
            var allowedKind = string.Equals(
                    expectedOption,
                    "fishing.collect_crab_pots",
                    StringComparison.Ordinal)
                ? kind == "collect_crab_pot"
                : kind is "route_connector_tile" or
                    "clear_obstacle_tile" or
                    "catch_fish";
            return allowedKind &&
                MasterAnglerStableIdentityMatches(candidate, continuation);
        }

        if (string.Equals(ReadString(continuation, "kind"), "mail", StringComparison.Ordinal))
        {
            if (string.Equals(
                    ReadString(candidate, "kind"),
                    "process_open_letter",
                    StringComparison.Ordinal))
            {
                var expectedMenuIdentity = ReadString(
                    continuation,
                    "mail_menu_identity_sha256");
                return string.IsNullOrWhiteSpace(expectedMenuIdentity) ||
                    CandidateParameterMatchesContinuation(
                        candidate,
                        continuation,
                        "mail_menu_identity_sha256");
            }

            var expectedMailId = ReadString(continuation, "mail_id");
            if (string.IsNullOrWhiteSpace(expectedMailId))
            {
                return false;
            }
            var candidateMailId = ReadCandidateParameter(
                candidate,
                "continuation.mail_id");
            if (string.IsNullOrWhiteSpace(candidateMailId))
            {
                candidateMailId = ReadCandidateParameter(
                    candidate,
                    "target_runtime_identity");
            }
            return string.Equals(
                candidateMailId,
                expectedMailId,
                StringComparison.Ordinal);
        }

        if (string.Equals(ReadString(continuation, "kind"), "home_renovation", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "renovation_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "selected_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "renovation_reason") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_renovation") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "confirm_destructive");
        }
        if (string.Equals(ReadString(continuation, "kind"), "movie_theater", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "movie_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "movie_guest_name") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "movie_concession_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "movie_objective_key");
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

        if (string.Equals(ReadString(continuation, "kind"), "museum_donation", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "inventory_slot_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "qualified_item_id");
        }

        if (string.Equals(ReadString(continuation, "kind"), "community_center_donation", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "bundle_data_key") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "bundle_ingredient_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "inventory_slot_index") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "qualified_item_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "expected_item_quality") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "required_stack");
        }

        if (string.Equals(ReadString(continuation, "kind"), "community_center_reward", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "bundle_data_key") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "bundle_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "qualified_item_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "reward_claim_mode");
        }

        if (string.Equals(ReadString(continuation, "kind"), "community_center_money_payment", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(candidate, continuation, "bundle_data_key") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "bundle_id") &&
                CandidateParameterMatchesContinuation(candidate, continuation, "price");
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
        if (string.Equals(ReadString(continuation, "kind"), "prairie_king", StringComparison.Ordinal))
        {
            return CandidateParameterMatchesContinuation(
                candidate,
                continuation,
                "prairie_king_completion_goal");
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
                    "bin_tile_y");
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

    private static bool MasterAnglerStableIdentityMatches(
        JsonObject candidate,
        JsonObject continuation)
    {
        return MasterAnglerContinuationNames
            .Where(name => !string.Equals(
                name,
                "master_angler_effective_start_time",
                StringComparison.Ordinal))
            .All(name => CandidateParameterMatchesContinuation(
                candidate,
                continuation,
                name));
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

    public static bool IsContinuableExecutionStatus(string status)
    {
        return string.Equals(status, "applied", StringComparison.Ordinal) ||
            string.Equals(status, "no_op", StringComparison.Ordinal);
    }

    public static bool CompletesSelectedQueueCandidate(
        string executionStatus,
        bool objectiveContinuationCompleted,
        JsonObject? activeObjectiveContinuation,
        string selectedCandidateId,
        string nextCandidateId)
    {
        return IsContinuableExecutionStatus(executionStatus) &&
            (objectiveContinuationCompleted ||
             activeObjectiveContinuation is null &&
             (string.IsNullOrWhiteSpace(nextCandidateId) ||
              !string.Equals(
                  nextCandidateId,
                  selectedCandidateId,
                  StringComparison.Ordinal)));
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
