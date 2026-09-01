using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.WorldModel;

namespace StardewAI.LiveTrainingLoop;

public sealed record SelectedQueueBoundaryDecision(
    bool Allowed,
    string[] Reasons,
    int PreviousQueueIndex,
    int NextQueueIndex,
    int RemainingRequiredMinutes,
    int RemainingOptionalMinutes,
    int RemainingEnergyCost,
    int AvailableEnergy);

public static class SelectedQueueBoundaryValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static SelectedQueueBoundaryDecision Validate(
        JsonObject snapshot,
        JsonObject[] remainingItems,
        int previousQueueIndex,
        int nextQueueIndex,
        string goalId,
        string executionMode)
    {
        var reasons = new List<string>();
        if (previousQueueIndex < 0 || nextQueueIndex < 0)
        {
            reasons.Add("selected_queue_index_missing");
        }
        else if (nextQueueIndex != previousQueueIndex + 1)
        {
            reasons.Add("selected_queue_precedence_discontinuity");
        }

        var orderedIndexes = remainingItems
            .Select(QueueReplanFilter.ReadAcceptedCandidateIndex)
            .ToArray();
        if (orderedIndexes.Any(index => index < 0))
        {
            reasons.Add("selected_queue_remaining_index_missing");
        }
        else if (orderedIndexes.Zip(orderedIndexes.Skip(1), (left, right) => right < left).Any(regressed => regressed))
        {
            reasons.Add("selected_queue_remaining_order_regressed");
        }

        var snapshotEnvelope = JsonSerializer.Deserialize<SnapshotEnvelope>(
            snapshot.ToJsonString(JsonOptions),
            JsonOptions) ?? throw new InvalidOperationException(
                "selected queue boundary snapshot is empty");
        var typedItems = remainingItems
            .Select(item => JsonSerializer.Deserialize<ActionQueueItem>(
                item.ToJsonString(JsonOptions),
                JsonOptions) ?? throw new InvalidOperationException(
                    "selected queue boundary item is empty"))
            .ToArray();
        var budget = new TimeBudgetValidator().Validate(
            new WorldModelProjector().Project(
                snapshotEnvelope,
                goalId,
                executionMode),
            new ActionQueueEnvelope
            {
                StateHash = snapshotEnvelope.StateHash,
                GoalId = goalId,
                ExecutionMode = executionMode,
                Items = typedItems
            });
        if (!budget.FitsRequiredPlusOptional)
        {
            reasons.AddRange(budget.BlockReasons);
            reasons.Add("selected_queue_remaining_time_budget_exceeded");
        }

        var remainingEnergyCost = remainingItems
            .GroupBy(QueueReplanFilter.ReadAcceptedCandidateIndex)
            .Select(group => ReadParameterInt(
                group.First(),
                "budget.candidate_energy_cost"))
            .Where(value => value > 0)
            .Sum();
        var availableEnergy = ReadSnapshotInt(snapshot, "player", "energy");
        if (availableEnergy >= 0 && remainingEnergyCost > availableEnergy)
        {
            reasons.Add("selected_queue_remaining_energy_budget_exceeded");
        }

        return new SelectedQueueBoundaryDecision(
            reasons.Count == 0,
            reasons.Distinct(StringComparer.Ordinal).ToArray(),
            previousQueueIndex,
            nextQueueIndex,
            budget.RequiredMinutes,
            budget.OptionalMinutes,
            remainingEnergyCost,
            availableEnergy);
    }

    private static int ReadParameterInt(JsonObject item, string name)
    {
        if (item["normalized_command"]?["parameters"] is not JsonArray parameters)
        {
            return 0;
        }

        var raw = parameters
            .OfType<JsonObject>()
            .FirstOrDefault(parameter => string.Equals(
                ReadString(parameter, "name"),
                name,
                StringComparison.Ordinal))?["value"]?.GetValue<string>();
        return int.TryParse(raw, out var value) ? value : 0;
    }

    private static int ReadSnapshotInt(
        JsonObject snapshot,
        string section,
        string field)
    {
        var node = snapshot["state"]?[section]?[field]?["value"] ??
            snapshot[section]?[field]?["value"];
        if (node is not JsonValue value)
        {
            return -1;
        }

        if (value.TryGetValue<int>(out var integer))
        {
            return integer;
        }

        return value.TryGetValue<double>(out var number)
            ? (int)number
            : -1;
    }

    private static string ReadString(JsonObject value, string property) =>
        value[property] is JsonValue jsonValue &&
        jsonValue.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
}
