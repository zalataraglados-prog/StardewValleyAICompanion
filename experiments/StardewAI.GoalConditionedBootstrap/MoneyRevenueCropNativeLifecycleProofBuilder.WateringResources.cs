namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static LoadedTransition LoadNativeWateringPreparation(LoadedTransition transition)
    {
        var reference = transition.Request.NativeWateringPreparation!;
        Require(transition.Request.TransitionKind == CropActionsKind && reference.CoveredCommitmentIds is { Length: > 0 } &&
            reference.CoveredCommitmentIds.Distinct(StringComparer.Ordinal).Count() == reference.CoveredCommitmentIds.Length &&
            reference.CoveredCommitmentIds.Contains(reference.CommitmentId, StringComparer.Ordinal),
            "Native watering reference changed its covered crop obligation.");
        var preparation = CropTeacherWateringMaintenance.Preparation.OpenWithObservedResourceEffects(reference.RunRequestPath,
            transition.BeforePath, reference.LedgerPath, reference.CoveredCommitmentIds, reference.CommitmentId,
            reference.StepId, reference.ModelActionPath, transition.QueuePath);
        var maintenance = preparation.VerifyTransition(transition.Request);
        Require(maintenance.SelectedWateringResourcesVerified && !maintenance.FormalProductTrainingAuthorized,
            "Native watering reference lost its verified original resource boundary.");
        maintenance.ValidateInputsUnchanged();
        return transition with { NativeAppliedItemCount = maintenance.AppliedTileCount,
            NativeNonProduction = maintenance.RefillControlVerified, NativeWateringMaintenance = maintenance };
    }
}
