using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static bool IsNativeControl(LoadedTransition transition) =>
        transition.Request.TransitionKind == SleepIngressRouteKind &&
        transition.Queue.Items is [var item] &&
        item.OptionId is "executor.wait_ticks" or "executor.close_menu";

    private static void ValidateNativeControl(LoadedTransition transition)
    {
        ValidateQueueReceipt(transition);
        var day = transition.Request.TotalDay;
        if (transition.Queue.Items[0].OptionId == "executor.wait_ticks")
        {
            Require(QueueExecutionReceiptValidator.IsExactNativeControlObservation(transition.Queue,
                    transition.Before, transition.Receipt, transition.After),
                "Native lifecycle wait is not an exact original control observation.");
            ValidateSupportingRoutes(day, new() { transition }, ReadPlayerLocation(transition.Before));
        }
        else
        {
            Require(SafeSpeakerlessDialogueRecovery.HasSafeSpeakerlessDialogue(transition.Before),
                "Native lifecycle close lacks its original safe speakerless dialogue.");
            MoneyRevenueResourceNeutralTerminalRouteVerifier.VerifyRecoveryControl(transition.Before, new()
            {
                TransitionId = transition.Queue.QueueId, QueuePath = transition.QueuePath,
                BeforeSnapshotPath = transition.BeforePath, ExecutionReceiptPath = transition.ReceiptPath,
                AfterSnapshotPath = transition.AfterPath, RunId = transition.Request.RunId,
                ExecutorVersion = transition.Request.ExecutorVersion, SelectedCandidateId = transition.Request.SelectedCandidateId
            });
            Require(MoneyRevenueResourceNeutralTerminalRouteVerifier.TryStateFieldValue(transition.After,
                    "menus", "active_menu", out var menu) &&
                    menu.GetProperty("is_open").ValueKind == System.Text.Json.JsonValueKind.False,
                "Native lifecycle recovery did not close its original menu.");
        }
        Require(transition.Receipt.StepResults.Length == 1, "Native lifecycle control requires one original step.");
        CropTeacherNativeStepBoundary.Verify(transition.Before, transition.After, transition.Before,
            transition.Receipt.StepResults[0], day, "Native lifecycle control");
        foreach (var field in new[] { "tile_x", "tile_y" })
            Require(MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(transition.Before, "player", field) ==
                    MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(transition.After, "player", field),
                "Native lifecycle control moved the original actor.");
    }
}
