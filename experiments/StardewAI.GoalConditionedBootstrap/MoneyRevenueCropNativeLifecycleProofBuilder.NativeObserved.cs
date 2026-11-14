using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static LoadedTransition LoadNativeObservedTransition(MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        var transition = LoadTransition(request);
        transition = transition with { NativeObserved = true };
        if (request.NativeRefillPreparation is not null)
            return LoadNativePreparation(transition);
        if (IsNativeControl(transition))
        {
            ValidateNativeControl(transition);
            return transition with { NativeNonProduction = true };
        }
        if (transition.Receipt.Success || request.TransitionKind != CropActionsKind ||
            transition.Queue.Items is not { Length: > 0 } items || items.Any(item => item.OptionId != PlantOption))
            return transition;

        // Reuse the original owned-planting prefix contract; never complete or trim the persisted queue/receipt.
        var applied = CropTeacherSeedPlantingReceipt.Validate(transition.Queue, transition.Before, transition.Receipt,
            transition.After, request);
        var directory = Path.GetDirectoryName(transition.ReceiptPath)! + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(PathComparer);
        var current = transition.Before;
        for (var index = 0; index < transition.Receipt.StepResults.Length; index++)
        {
            var step = transition.Receipt.StepResults[index];
            var path = RequiredFile(step.AfterSnapshotPath, "Native lifecycle original planting prefix step");
            Require(path.StartsWith(directory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    seen.Add(path) && !SamePath(path, transition.BeforePath) && !SamePath(path, transition.QueuePath) &&
                    !SamePath(path, transition.ReceiptPath) &&
                    (!SamePath(path, transition.AfterPath) || index == transition.Receipt.StepResults.Length - 1),
                "Native lifecycle planting prefix step escaped, repeated or aliased its original source.");
            var hash = CurrentTeacherFrontierSupport.HashFile(path);
            var next = SamePath(path, transition.AfterPath) ? transition.After :
                CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "Native lifecycle original planting prefix step");
            CropTeacherNativeStepBoundary.Verify(current, next, transition.Before, step, request.TotalDay);
            if (index >= applied) CropSeedResourceBoundary.VerifyUnconsumedPlanting(current, next);
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Native lifecycle planting prefix step changed.");
            current = next;
        }
        Require(BootstrapArtifactGuard.EqualJson(current, transition.After),
            "Native lifecycle planting prefix does not reach its original terminal capture.");
        return transition with { NativeAppliedItemCount = applied };
    }
}
