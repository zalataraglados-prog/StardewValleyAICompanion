using StardewAI.Core.Infrastructure;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    public static MoneyRevenueTerminalQueueTransitionEvidence[] VerifyNativeObservedControlTail(string sourcePath,
        MoneyRevenueCropNativeLifecycleTransitionRequest[] transitions, string terminalPath)
    {
        Require(transitions is { Length: > 0 } && transitions.All(row => row.NativeRefillPreparation is null) &&
                transitions.All(row => row.RunId == transitions[0].RunId && row.ExecutorVersion == transitions[0].ExecutorVersion &&
                    row.TotalDay == transitions[0].TotalDay) &&
                transitions.Select(row => Path.GetFullPath(row.QueuePath)).Distinct(PathComparer).Count() == transitions.Length &&
                transitions.Select(row => Path.GetFullPath(row.ExecutionReceiptPath)).Distinct(PathComparer).Count() == transitions.Length,
            "Native crop control tail is empty, duplicated or contains preparation.");
        var current = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(RequiredFile(sourcePath,
            "Native crop control tail source"), "Native crop control tail source");
        var evidence = new List<MoneyRevenueTerminalQueueTransitionEvidence>();
        foreach (var request in transitions)
        {
            var transition = LoadTransition(request);
            Require(IsNativeControl(transition) && BootstrapArtifactGuard.EqualJson(current, transition.Before),
                "Native crop control tail is not an exact contiguous control observation.");
            ValidateNativeControl(transition);
            evidence.Add(new(evidence.Count, transition.Queue.QueueId,
                CurrentTeacherFrontierSupport.HashFile(transition.QueuePath), CurrentTeacherFrontierSupport.HashFile(transition.BeforePath),
                CurrentTeacherFrontierSupport.HashFile(transition.ReceiptPath), CurrentTeacherFrontierSupport.HashFile(transition.AfterPath),
                transition.Before.StateHash, transition.After.StateHash, transition.Before.GameTick, transition.After.GameTick,
                transition.Queue.Items.Select(item => item.OptionId).ToArray()));
            current = transition.After;
        }
        var terminal = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(RequiredFile(terminalPath,
            "Native crop control tail terminal"), "Native crop control tail terminal");
        Require(BootstrapArtifactGuard.EqualJson(current, terminal),
            "Native crop control tail does not reach its accepted head.");
        return evidence.ToArray();
    }

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
