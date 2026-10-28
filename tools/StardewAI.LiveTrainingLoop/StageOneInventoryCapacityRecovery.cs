using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.LiveTrainingLoop;

public sealed record StageOneInventoryCapacityRecoveryDecision(
    PolicyEventCandidatePrediction Candidate,
    string ReleasedQualifiedItemId,
    int TargetStackQuantityBefore,
    string CaughtQualifiedItemId,
    string TriggerReason,
    string SelectionReason);

public static class StageOneInventoryCapacityRecovery
{
    public const string TriggerReason =
        "stage_one_fishing_inventory_full_after_catch";

    public const string PreAttemptTriggerReason =
        "stage_one_fishing_inventory_full_before_attempt";

    public const string CropHarvestTriggerReason =
        "crop_teacher_harvest_inventory_full";

    private const string NoExactFishingAttemptReason =
        "master_angler_no_exact_runtime_source_attempt";

    private const string FishingInventoryFullReason =
        "fishing_inventory_full_requires_storage_transfer";

    public static bool HasFullInventory(JsonObject snapshot) =>
        InventoryIsFull(snapshot);

    public static bool HasRecoveryContext(JsonObject snapshot) =>
        HasFullInventory(snapshot) &&
        !string.IsNullOrWhiteSpace(ReadCaughtQualifiedItemId(snapshot));

    public static StageOneInventoryCapacityRecoveryDecision? TrySelect(
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        JsonObject snapshot,
        bool allowObjectiveSafeRelease = false,
        bool requireFishingAttemptBlocked = true)
    {
        var fishingAttemptBlocked = ranking.Availability.Options
            .Where(option => string.Equals(
                option.OptionId,
                "fishing.catch_fish",
                StringComparison.Ordinal))
            .SelectMany(option => option.EventCandidates)
            .Any(candidate => candidate.BlockReasons.Any(reason =>
                string.Equals(
                    reason,
                    NoExactFishingAttemptReason,
                    StringComparison.Ordinal) ||
                string.Equals(
                    reason,
                    FishingInventoryFullReason,
                    StringComparison.Ordinal)));
        if ((requireFishingAttemptBlocked && !fishingAttemptBlocked) ||
            !HasFullInventory(snapshot))
            return null;

        var caughtQualifiedItemId = ReadCaughtQualifiedItemId(snapshot);
        var releaseCandidates = ranking.RankedEventCandidates
            .Where(candidate =>
                candidate.Available &&
                string.Equals(
                    candidate.OptionId,
                    "economy.ship_items",
                    StringComparison.Ordinal) &&
                candidate.Quantity == 1 &&
                candidate.AvailableStack is > 0 &&
                CapacityReleaseValue(candidate) <= int.MaxValue)
            .ToArray();
        var exactCatch = releaseCandidates
            .Where(candidate => string.Equals(
                candidate.QualifiedItemId,
                caughtQualifiedItemId,
                StringComparison.Ordinal))
            .OrderBy(CapacityReleaseValue)
            .ThenBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
        var selectedSource = exactCatch;
        var selectionReason = exactCatch?.AvailableStack == 1
            ? "exact_latest_catch_singleton"
            : "exact_latest_catch_stack_drain";
        if (selectedSource is null && allowObjectiveSafeRelease)
        {
            selectedSource = releaseCandidates
                .OrderByDescending(candidate =>
                    candidate.FullShipmentContributes == true)
                .ThenBy(CapacityReleaseValue)
                .ThenBy(candidate => candidate.EstimatedTicks)
                .ThenBy(
                    candidate => candidate.CandidateId,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            selectionReason = selectedSource?.AvailableStack == 1
                ? "available_unprotected_singleton"
                : "available_unprotected_stack_drain";
        }

        if (selectedSource is null)
            return null;

        var selected = Clone(selectedSource);
        var targetStackQuantityBefore = Math.Max(
            1,
            selected.AvailableStack ?? selected.Quantity);
        selected.Rank = 1;
        selected.Score = 0;
        selected.ModelScore = null;
        selected.PolicyModelSource =
            "stage_one_inventory_capacity_recovery";
        selected.ExpectedReward = 0;
        return new StageOneInventoryCapacityRecoveryDecision(
            selected,
            selected.QualifiedItemId,
            targetStackQuantityBefore,
            caughtQualifiedItemId,
            requireFishingAttemptBlocked
                ? string.IsNullOrWhiteSpace(caughtQualifiedItemId)
                    ? PreAttemptTriggerReason
                    : TriggerReason
                : CropHarvestTriggerReason,
            selectionReason);
    }

    private static long CapacityReleaseValue(
        PolicyEventCandidatePrediction candidate) =>
        (long)Math.Max(0, candidate.UnitPrice) *
        Math.Max(1, candidate.AvailableStack ?? candidate.Quantity);

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
