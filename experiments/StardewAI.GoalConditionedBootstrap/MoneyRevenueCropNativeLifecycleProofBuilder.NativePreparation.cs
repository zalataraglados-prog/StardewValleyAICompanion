namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static LoadedTransition LoadNativePreparation(LoadedTransition transition)
    {
        var reference = transition.Request.NativeRefillPreparation!;
        Require(transition.Request.TransitionKind == CropActionsKind &&
                reference.CoveredCommitmentIds is { Length: > 0 } &&
                reference.CoveredCommitmentIds.Distinct(StringComparer.Ordinal).Count() == reference.CoveredCommitmentIds.Length &&
                reference.ResourceMode is "native_resource_neutral" or "native_selected_resource_effects",
            "Native lifecycle refill preparation reference is invalid.");
        var compilation = CropTeacherRefillApproachCompilation.Open(reference.RunRequestPath, transition.BeforePath,
            reference.LedgerPath, reference.CoveredCommitmentIds, reference.CommitmentId, reference.StepId,
            reference.ModelPlanPath, transition.QueuePath);
        Require(compilation.CandidateId == transition.Request.SelectedCandidateId &&
                compilation.TotalDay == transition.Request.TotalDay,
            "Native lifecycle preparation changed its selected original compilation/day.");
        var trace = reference.ResourceMode == "native_resource_neutral"
            ? CropTeacherRefillApproachNativeTrace.OpenNativeResourceNeutral(compilation, transition.ReceiptPath,
                transition.AfterPath, transition.Request.RunId, transition.Request.ExecutorVersion)
            : CropTeacherRefillApproachNativeTrace.OpenNativeWithSelectedResourceEffects(compilation, transition.ReceiptPath,
                transition.AfterPath, transition.Request.RunId, transition.Request.ExecutorVersion);
        Require(BootstrapArtifactGuard.EqualJson(trace.VerifiedAfterSnapshot, transition.After) &&
                !trace.FormalProductTrainingAuthorized && !trace.ResourceCreditAuthorized,
            "Native lifecycle refill trace changed its original terminal frame or granted credit.");
        trace.ValidateInputsUnchanged();
        var request = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlanRequest>(
            reference.RunRequestPath, "Native lifecycle original refill run request");
        return transition with { NativeNonProduction = true, NativePreparationPlanPath = request.CropProductionPlanPath };
    }

    private static void ValidateNativePreparationContinuations(List<LoadedTransition> actions, LifecycleCommitmentContext[] contexts)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            var transition = actions[index];
            if (transition.NativePreparationPlanPath is null) continue;
            Require(actions.Skip(index + 1).Any(next => !next.NativeNonProduction &&
                    next.Queue.Items.Length > 0 && next.Queue.Items.All(item => item.OptionId == WaterOption) &&
                    ResolveActionCommitmentId(next, contexts) == transition.Request.NativeRefillPreparation!.CommitmentId),
                "Native lifecycle refill preparation has no later same-day watering continuation.");
        }
    }
}
