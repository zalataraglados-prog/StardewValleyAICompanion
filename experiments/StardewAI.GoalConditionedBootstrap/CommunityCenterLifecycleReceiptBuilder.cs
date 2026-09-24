using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class CommunityCenterLifecycleReceiptBuilder
{
    public static CommunityCenterLifecycleReceiptAdmission Build(
        string queuePath,
        string beforeSnapshotPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string transitionKind,
        string runId,
        string executorVersion)
    {
        var queueFullPath = Path.GetFullPath(queuePath);
        var beforeFullPath = Path.GetFullPath(beforeSnapshotPath);
        var receiptFullPath = Path.GetFullPath(executionReceiptPath);
        var afterFullPath = Path.GetFullPath(afterSnapshotPath);
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queueFullPath,
            "Community Center lifecycle queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforeFullPath,
            "Community Center lifecycle before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptFullPath,
            "Community Center lifecycle execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterFullPath,
            "Community Center lifecycle after snapshot");
        var result = new CommunityCenterLifecycleReceiptAdmission
        {
            TransitionKind = transitionKind,
            QueueSha256 = CurrentTeacherFrontierSupport.HashFile(queueFullPath),
            BeforeSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                beforeFullPath),
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                receiptFullPath),
            AfterSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                afterFullPath)
        };
        var reasons = QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                receipt,
                after,
                runId,
                executorVersion,
                receipt.SelectedCandidateId,
                queue.StateHash,
                requireTeacherPreferenceStateRebound: false)
            .ToList();
        if (!QueueMatchesTransition(queue, transitionKind))
            reasons.Add("community_center_lifecycle_queue_transition_mismatch");
        var transition = CommunityCenterLifecycleTransitionVerifier.Verify(
            transitionKind,
            before,
            after);
        reasons.AddRange(transition.BlockingReasons);
        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        result.TransitionEvidence = transition;
        result.BlockingReasons = distinct;
        result.TrainingLabelEligible = distinct.Length == 0;
        result.Status = result.TrainingLabelEligible
            ? "ready"
            : "blocked_fresh_native_lifecycle_transition_missing";
        return result;
    }

    private static bool QueueMatchesTransition(
        ActionQueueEnvelope queue,
        string transitionKind)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        return transitionKind switch
        {
            CommunityCenterLifecycleTransitionVerifier.InitialUnlock =>
                HasStoryEvent(items, "611439"),
            CommunityCenterLifecycleTransitionVerifier.FirstJunimoNote =>
                items.Any(item =>
                    item.OptionId == "executor.interact" &&
                    ReadParameter(item, "interaction_kind") ==
                        "community_center_note" &&
                    ReadParameter(item, "expected_action_type") ==
                        "CommunityCenterBundleNote" &&
                    ReadParameter(item, "bundle_area_id") == "1"),
            CommunityCenterLifecycleTransitionVerifier.JunimoTextUnlock =>
                HasStoryEvent(items, "112"),
            CommunityCenterLifecycleTransitionVerifier.FinalCeremony =>
                HasStoryEvent(items, "191393"),
            CommunityCenterLifecycleTransitionVerifier.RoomMailSettlement =>
                items.Any(item =>
                    item.OptionId is "recovery.stabilize_day" or
                        "executor.sleep" ||
                    ReadParameter(item, "execution_option_id") ==
                        "executor.sleep"),
            _ => false
        };
    }

    private static bool HasStoryEvent(
        IEnumerable<ActionQueueItem> items,
        string eventId) => items.Any(item =>
            item.OptionId == "executor.advance_story_event" &&
            ReadParameter(item, "story_event_id") == eventId);

    private static string ReadParameter(ActionQueueItem item, string name) =>
        (item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>())
        .SingleOrDefault(value => value.Name == name)?.Value ?? string.Empty;
}
