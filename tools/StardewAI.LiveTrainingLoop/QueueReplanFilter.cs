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
