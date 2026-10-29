using System;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public static class StageOneMandatoryMenuRecovery
{
    public const string CloseMenuKind = "recovery_close_menu";
    public const string FishingItemGrabRewardKind =
        "recovery_claim_fishing_item_grab_reward";

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
