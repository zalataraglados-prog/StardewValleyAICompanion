using System.Globalization;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherPreferenceBuilder
{
    private static bool ValidateManagedMuseumLedger(PolicyEventCandidatePrediction[] candidates,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger, CurrentStageOneCollectionTeacherPreferenceLabel label)
    {
        var managed = candidates.Where(candidate => candidate.OptionId == "museum.donate_items" &&
            candidate.Parameters?.Any(parameter => parameter is not null && parameter.Name == "material_reservation_ledger_id") == true).ToArray();
        if (managed.Length == 0)
            return true;
        if (ledger is null)
            return Block("teacher_managed_museum_strategy_ledger_required");
        var fresh = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
                new[] { "museum.donate_items" }, commitmentLedger: ledger)
            .Options.Single().EventCandidates.ToDictionary(candidate => candidate.CandidateId, StringComparer.Ordinal);
        foreach (var candidate in managed)
        {
            if (!HasUniqueBinding(candidate, "material_reservation_ledger_id", ledger.LedgerId) ||
                !HasUniqueBinding(candidate, "material_reservation_ledger_revision", ledger.Revision.ToString(CultureInfo.InvariantCulture)) ||
                !fresh.TryGetValue(candidate.CandidateId, out var actual) || !actual.Available ||
                actual.SlotIndex != candidate.SlotIndex || actual.QualifiedItemId != candidate.QualifiedItemId)
                return Block("teacher_managed_museum_strategy_ledger_or_candidate_drifted");
        }
        return true;

        bool Block(string reason)
        {
            label.Status = "blocked_managed_museum_ledger_evidence";
            label.BlockingReasons = new[] { reason };
            return false;
        }
    }

    private static bool HasUniqueBinding(PolicyEventCandidatePrediction candidate, string name, string value)
    {
        var parameters = candidate.Parameters.Where(parameter => parameter?.Name == name).ToArray();
        return parameters.Length == 1 && parameters[0].Value == value;
    }
}
