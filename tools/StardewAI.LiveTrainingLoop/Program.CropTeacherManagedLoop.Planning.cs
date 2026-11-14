using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static CropTeacherRunStep EvaluateCropTeacherCurrentRunStep(
        MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot, IReadOnlySet<string> preparedCommitmentIds,
        CropTeacherTransitionLedger? legacyTransitions, CropTeacherManagedSeedOwner? managedOwner)
    {
        var accepted = managedOwner?.ReadPlanningProgress();
        var ledger = managedOwner is null ? null : JsonSerializer.Deserialize<StrategyCommitmentLedger>(
            ContentAddressedJsonArtifactReader.ReadAllText(managedOwner.CurrentLedgerPath), JsonOptions);
        return CropTeacherRunPlanner.Evaluate(plan, request, snapshot, preparedCommitmentIds,
            accepted?.CompletedHarvestTileKeys ?? legacyTransitions?.CompletedHarvestTileKeys,
            accepted?.CompletedDayBoundaryKeys ?? legacyTransitions?.CompletedDayBoundaryKeys, ledger);
    }
}
