using System;
using System.Collections.Generic;
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
    public const string ProfessionChoiceKind = "choose_profession";
    public const string ProfessionInputSettleKind =
        "recovery_profession_input_settle";
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

    public static bool RequiresExplicitProfessionChoice(
        SnapshotEnvelope snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var activeMenu = ReadStateFieldValue(
            snapshot,
            "menus",
            "active_menu");
        if (!activeMenu.HasValue ||
            activeMenu.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(activeMenu.Value, "type"),
                "LevelUpMenu",
                StringComparison.Ordinal))
        {
            return false;
        }

        var menuState = ReadStateFieldValue(
            snapshot,
            "menus",
            "menu_specific_state");
        if (!menuState.HasValue ||
            menuState.Value.ValueKind != JsonValueKind.Object ||
            !menuState.Value.TryGetProperty(
                "is_profession_chooser",
                out var professionChooser) ||
            professionChooser.ValueKind is not (
                JsonValueKind.True or JsonValueKind.False))
        {
            return true;
        }

        return professionChooser.GetBoolean();
    }

    public static PolicyEventCandidatePrediction? TrySelect(
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        bool professionChoiceRequired = false,
        string preferredProfessionCandidateId = "")
    {
        if (ranking is null)
        {
            throw new ArgumentNullException(nameof(ranking));
        }

        var professionChoices = ranking.RankedEventCandidates
            .Where(candidate =>
                string.Equals(
                    candidate.OptionId,
                    "skills.choose_profession",
                    StringComparison.Ordinal) &&
                string.Equals(
                    candidate.Kind,
                    ProfessionChoiceKind,
                    StringComparison.Ordinal))
            .ToArray();
        if (professionChoiceRequired)
        {
            var professionChoice = string.IsNullOrWhiteSpace(
                    preferredProfessionCandidateId)
                ? OrderCandidates(professionChoices)
                    .FirstOrDefault(candidate => candidate.Available)
                : professionChoices.FirstOrDefault(candidate =>
                    candidate.Available &&
                    string.Equals(
                        candidate.CandidateId,
                        preferredProfessionCandidateId,
                        StringComparison.Ordinal));
            if (professionChoice is not null)
            {
                return professionChoice;
            }

            return OrderCandidates(ranking.RankedEventCandidates
                    .Where(candidate =>
                        candidate.Available &&
                        string.Equals(
                            candidate.OptionId,
                            "recovery.stabilize_day",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            candidate.Kind,
                            ProfessionInputSettleKind,
                            StringComparison.Ordinal)))
                .FirstOrDefault();
        }
        var availableProfessionChoice = OrderCandidates(professionChoices)
            .FirstOrDefault(candidate => candidate.Available);
        if (availableProfessionChoice is not null)
        {
            return availableProfessionChoice;
        }

        return OrderCandidates(ranking.RankedEventCandidates
            .Where(candidate =>
                candidate.Available &&
                string.Equals(
                    candidate.OptionId,
                    "recovery.stabilize_day",
                    StringComparison.Ordinal) &&
                IsMandatoryMenuRecovery(candidate.Kind)))
            .FirstOrDefault();
    }

    private static IOrderedEnumerable<PolicyEventCandidatePrediction>
        OrderCandidates(
            IEnumerable<PolicyEventCandidatePrediction> candidates) =>
        candidates
            .OrderBy(candidate => candidate.Rank)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal);

    private static bool IsMandatoryMenuRecovery(string kind) =>
        string.Equals(kind, CloseMenuKind, StringComparison.Ordinal) ||
        string.Equals(
            kind,
            ProfessionInputSettleKind,
            StringComparison.Ordinal) ||
        string.Equals(
            kind,
            FishingItemGrabRewardKind,
            StringComparison.Ordinal);
}
