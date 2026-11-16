using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropSeedAcquisitionProofBuilder
{
    private static bool IsVerifiedNativeFarmTutorialStep(SnapshotEnvelope before, SnapshotEnvelope after,
        QueueExecutionStepReceipt step, ActionQueueItem item) =>
        item.OptionId == "executor.traverse_connector" && step.OptionId == item.OptionId &&
        step.Status == "applied" && step.AfterSnapshotFresh && step.PrimitiveVerificationStatus == "verified" &&
        step.SourceStateHash == before.StateHash && step.AfterStateHash == after.StateHash &&
        step.BeforeGameTick == before.GameTick && step.AfterGameTick == after.GameTick && after.GameTick > before.GameTick &&
        FullShipmentRecoverySettlementBoundaryProjector.MatchesFirstFarmEntryTutorial(before, after);
}
