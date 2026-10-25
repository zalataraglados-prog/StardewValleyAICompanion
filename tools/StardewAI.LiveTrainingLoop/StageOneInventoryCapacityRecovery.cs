using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.LiveTrainingLoop;

public sealed record StageOneInventoryCapacityRecoveryDecision(
    PolicyEventCandidatePrediction Candidate,
    string CaughtQualifiedItemId,
    string TriggerReason);

public static class StageOneInventoryCapacityRecovery
{
    public const string TriggerReason =
        "stage_one_fishing_inventory_full_after_catch";

    private const string NoExactFishingAttemptReason =
        "master_angler_no_exact_runtime_source_attempt";

    public static StageOneInventoryCapacityRecoveryDecision? TrySelect(
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        JsonObject snapshot)
    {
        var fishingAttemptBlocked = ranking.Availability.Options
            .Where(option => string.Equals(
                option.OptionId,
                "fishing.catch_fish",
                StringComparison.Ordinal))
            .SelectMany(option => option.EventCandidates)
            .Any(candidate => candidate.BlockReasons.Contains(
                NoExactFishingAttemptReason,
                StringComparer.Ordinal));
        if (!fishingAttemptBlocked || !InventoryIsFull(snapshot))
            return null;

        var caughtQualifiedItemId = ReadCaughtQualifiedItemId(snapshot);
        if (string.IsNullOrWhiteSpace(caughtQualifiedItemId))
            return null;

        var matches = ranking.RankedEventCandidates
            .Where(candidate =>
                candidate.Available &&
                string.Equals(
                    candidate.OptionId,
                    "economy.ship_items",
                    StringComparison.Ordinal) &&
                string.Equals(
                    candidate.QualifiedItemId,
                    caughtQualifiedItemId,
                    StringComparison.Ordinal) &&
                candidate.Quantity == 1)
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
            return null;

        var selected = Clone(matches[0]);
        selected.Rank = 1;
        selected.Score = 0;
        selected.ModelScore = null;
        selected.PolicyModelSource =
            "stage_one_inventory_capacity_recovery";
        selected.ExpectedReward = 0;
        return new StageOneInventoryCapacityRecoveryDecision(
            selected,
            caughtQualifiedItemId,
            TriggerReason);
    }

    private static bool InventoryIsFull(JsonObject snapshot)
    {
        if (snapshot["state"]?["player"]?["inventory_capacity"]?
                ["value"] is not JsonObject capacity ||
            capacity["has_empty_slot"] is not JsonValue hasEmptySlot ||
            !hasEmptySlot.TryGetValue<bool>(out var hasEmpty) ||
            capacity["max_items"] is not JsonValue maxItemsNode ||
            !maxItemsNode.TryGetValue<int>(out var maxItems) ||
            capacity["occupied_item_stacks"] is not JsonValue occupiedNode ||
            !occupiedNode.TryGetValue<int>(out var occupied))
        {
            return false;
        }

        return !hasEmpty && maxItems > 0 && occupied >= maxItems;
    }

    private static string ReadCaughtQualifiedItemId(JsonObject snapshot)
    {
        var value = snapshot["state"]?["fishing"]?["active_cast_state"]?
            ["value"]?["caught_qualified_item_id"];
        return value is JsonValue jsonValue &&
               jsonValue.TryGetValue<string>(out var caught)
            ? caught
            : string.Empty;
    }

    private static PolicyEventCandidatePrediction Clone(
        PolicyEventCandidatePrediction candidate) =>
        JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(candidate)) ??
        throw new InvalidDataException(
            "Stage-one inventory-capacity recovery candidate could not be cloned.");
}
