using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public static class StageOneMandatoryMenuRecovery
{
    public const string CloseMenuKind = "recovery_close_menu";
    public const string FishingItemGrabRewardKind =
        "recovery_claim_fishing_item_grab_reward";

    public static bool RequiresPreflight(SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var activeMenu = ReadStateFieldValue(
            snapshot,
            "menus",
            "active_menu");
        if (!activeMenu.HasValue)
        {
            return false;
        }

        if (activeMenu.Value.ValueKind == JsonValueKind.String)
        {
            return !string.Equals(
                activeMenu.Value.GetString(),
                "none",
                StringComparison.OrdinalIgnoreCase);
        }

        return activeMenu.Value.ValueKind == JsonValueKind.Object &&
            ReadBool(activeMenu.Value, "is_open") == true;
    }

    public static AvailabilityAwarePolicyPredictionEnvelope BuildRanking(
        OptionAvailabilityEnvelope availability,
        string goalId)
    {
        if (availability is null)
        {
            throw new ArgumentNullException(nameof(availability));
        }

        return new AvailabilityAwarePolicyPredictionEnvelope
        {
            Availability = availability,
            RankedEventCandidates = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                goalId)
        };
    }

    public static PolicyEventCandidatePrediction? TrySelect(
        AvailabilityAwarePolicyPredictionEnvelope ranking)
    {
        if (ranking is null)
        {
            throw new ArgumentNullException(nameof(ranking));
        }

        return ranking.RankedEventCandidates
            .Where(candidate =>
                candidate.Available &&
                string.Equals(
                    candidate.OptionId,
                    "recovery.stabilize_day",
                    StringComparison.Ordinal) &&
                IsMandatoryMenuRecovery(candidate.Kind))
            .OrderBy(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsMandatoryMenuRecovery(string kind) =>
        string.Equals(kind, CloseMenuKind, StringComparison.Ordinal) ||
        string.Equals(
            kind,
            FishingItemGrabRewardKind,
            StringComparison.Ordinal);
}
