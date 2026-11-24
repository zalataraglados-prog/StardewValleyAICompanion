namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    internal static CropTeacherHarvest ReplaySettlement(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string expectedProductionPlanPath)
    {
        var reference = transition.NativeHarvestSettlement ?? throw new InvalidDataException("Native harvest settlement reference is missing.");
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        Require(transition.TransitionKind == "crop_actions" && transition.NativeRefillPreparation is null &&
                transition.NativeWateringPreparation is null && transition.NativeSharedSource is null &&
                new[] { reference.RunPlanPath, reference.LedgerPath, reference.ModelActionPath, reference.SettlementPath,
                    reference.SettledAfterSnapshotPath }.All(Path.IsPathFullyQualified), "Native harvest settlement crossed its original role or file boundary.");
        var plan = CurrentTeacherFrontierSupport.Read<MoneyRevenueCropNativeProductionRunPlan>(reference.RunPlanPath, "native harvest settlement run plan");
        Require(comparer.Equals(Path.GetFullPath(plan.CropProductionPlanPath), Path.GetFullPath(expectedProductionPlanPath)),
            "Native harvest settlement changed its original production plan.");
        var scope = Preparation.Open(reference.RunPlanPath, transition.BeforeSnapshotPath, reference.LedgerPath,
            reference.CoveredCommitmentIds, reference.CommitmentId, reference.StepId, transition.TotalDay,
            reference.BatchIndex, reference.ModelActionPath, transition.QueuePath).VerifyTransition(transition, reference.SettlementPath);
        Require(comparer.Equals(scope.AfterSnapshotPath, reference.SettledAfterSnapshotPath),
            "Native harvest settlement changed its verified closing frame.");
        return scope;
    }
}
