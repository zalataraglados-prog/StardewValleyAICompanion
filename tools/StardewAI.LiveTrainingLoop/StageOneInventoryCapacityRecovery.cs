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

    private const string FestivalTransitClosedReason =
        "connector_active_festival_transit_closed";

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
            .Where(IsCapacityReleaseCandidate)
            .ToArray();
        var immediateCandidates = releaseCandidates
            .Where(candidate => !IsDeferredTransitRecovery(candidate))
            .ToArray();
        var deferredCandidates = releaseCandidates
            .Where(IsDeferredTransitRecovery)
            .ToArray();
        var selectedSource = SelectExactCatch(
            immediateCandidates,
            caughtQualifiedItemId);
        var selectedExactCatch = selectedSource is not null;
        if (selectedSource is null && allowObjectiveSafeRelease)
        {
            selectedSource = SelectObjectiveSafe(immediateCandidates);
        }

        if (selectedSource is null)
        {
            selectedSource = SelectExactCatch(
                deferredCandidates,
                caughtQualifiedItemId);
            selectedExactCatch = selectedSource is not null;
        }

        if (selectedSource is null && allowObjectiveSafeRelease)
        {
            selectedSource = SelectObjectiveSafe(deferredCandidates);
        }

        if (selectedSource is null)
            return null;

        var selectionReason = SelectionReason(
            selectedSource,
            selectedExactCatch);
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

    private static PolicyEventCandidatePrediction? SelectExactCatch(
        IEnumerable<PolicyEventCandidatePrediction> candidates,
        string caughtQualifiedItemId) =>
        candidates
            .Where(candidate => string.Equals(
                candidate.QualifiedItemId,
                caughtQualifiedItemId,
                StringComparison.Ordinal))
            .OrderBy(CapacityReleaseValue)
            .ThenBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();

    private static PolicyEventCandidatePrediction? SelectObjectiveSafe(
        IEnumerable<PolicyEventCandidatePrediction> candidates) =>
        candidates
            .OrderBy(candidate => Math.Max(
                1,
                candidate.AvailableStack ?? candidate.Quantity))
            .ThenByDescending(candidate =>
                candidate.FullShipmentContributes == true)
            .ThenBy(CapacityReleaseValue)
            .ThenBy(candidate => candidate.EstimatedTicks)
            .ThenBy(
                candidate => candidate.CandidateId,
                StringComparer.Ordinal)
            .FirstOrDefault();

    private static bool IsCapacityReleaseCandidate(
        PolicyEventCandidatePrediction candidate) =>
        string.Equals(
            candidate.OptionId,
            "economy.ship_items",
            StringComparison.Ordinal) &&
        candidate.Quantity == 1 &&
        candidate.AvailableStack is > 0 &&
        CapacityReleaseValue(candidate) <= int.MaxValue &&
        (candidate.Available || IsDeferredTransitRecovery(candidate));

    private static bool IsDeferredTransitRecovery(
        PolicyEventCandidatePrediction candidate) =>
        !candidate.Available &&
        string.Equals(
            candidate.Kind,
            "route_connector_tile",
            StringComparison.Ordinal) &&
        candidate.AllowedNow == false &&
        candidate.AllowedToday == true &&
        string.Equals(
            candidate.TimelineStatus,
            "deferred",
            StringComparison.Ordinal) &&
        candidate.ScheduledStartTime.HasValue &&
        candidate.ScheduledWaitCost is > 0 &&
        candidate.BlockReasons is { Length: > 0 } blockReasons &&
        blockReasons.All(reason => string.Equals(
            reason,
            FestivalTransitClosedReason,
            StringComparison.Ordinal));

    private static string SelectionReason(
        PolicyEventCandidatePrediction? candidate,
        bool exactCatch)
    {
        if (candidate is null)
            return string.Empty;

        var timing = IsDeferredTransitRecovery(candidate)
            ? "deferred_festival_transit_"
            : string.Empty;
        var source = exactCatch
            ? "exact_latest_catch"
            : "available_unprotected";
        return timing + source + (candidate.AvailableStack == 1
            ? "_singleton"
            : "_stack_drain");
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
