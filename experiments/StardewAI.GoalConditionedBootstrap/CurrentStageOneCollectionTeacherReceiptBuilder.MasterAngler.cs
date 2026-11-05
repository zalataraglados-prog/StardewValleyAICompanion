using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static GoalRequirementSet? ReadMasterAnglerTransitionInventory(
        CurrentStageOneCollectionTeacherPreferenceLabel preference, string inventoryPath)
    {
        if (!preference.SelectedCandidate!.RequirementCredits.Any(credit =>
                credit.RequirementSetId == "master_angler" &&
                credit.BindingKind == "authoritative_window_terminal_attempt"))
            return null;
        var inventory = CurrentTeacherFrontierSupport.Read<AuthoritativeRequirementInventoryReport>(
            inventoryPath, "Master Angler transition requirement inventory");
        return CurrentTeacherFrontierSupport.SingleSet(inventory.RequirementSets,
            set => set.RequirementSetId, "master_angler", "Master Angler transition inventory");
    }

    private static PolicyTeacherRequirementTransition VerifyMasterAnglerCollectionTransition(
        CurrentCollectionRequirementCredit credit, SnapshotEnvelope before, SnapshotEnvelope after,
        GoalRequirementSet? inventory)
    {
        const string kind = "native_collection_false_to_true";
        if (credit.RequirementSetId != "master_angler" || inventory?.RequirementSetId != "master_angler")
            return Transition(credit, kind, "unavailable_authority", "unavailable_authority", false);
        try
        {
            var beforeProgress = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(before, inventory);
            var afterProgress = CurrentMasterAnglerTeacherFrontierBuilder.ReadExactProgress(after, inventory);
            if (beforeProgress.Any(row => row.Value && !afterProgress[row.Key]))
                return Transition(credit, kind, "existing_caught_species", "collection_progress_regressed", false);
        }
        catch (InvalidDataException)
        {
            return Transition(credit, kind, "unavailable_exact_denominator", "unavailable_exact_denominator", false);
        }
        return VerifyCollectionBoolean(credit, before, after, "fish_collection_progress", "items",
            "qualified_item_id", "caught", kind);
    }
}
