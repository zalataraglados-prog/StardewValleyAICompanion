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
                ["qualified_item_id"] = ReadParameter(queueItem, "continuation.qualified_item_id")
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
                ["qualified_item_id"] = ReadParameter(queueItem, "continuation.qualified_item_id")
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
                    "continuation.machine_item_id")
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
            OptionalIdentityMatches(candidate, continuation, "qualified_item_id", "continuation.qualified_item_id");
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
