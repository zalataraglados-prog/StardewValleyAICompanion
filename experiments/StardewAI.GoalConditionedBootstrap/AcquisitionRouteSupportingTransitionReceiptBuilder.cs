using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    public static AcquisitionRouteSupportingTransitionReceipt Build(
        string compilationPath,
        string beforeSnapshotPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string runId,
        string executorVersion)
    {
        var compilationFullPath = Path.GetFullPath(compilationPath);
        var beforeFullPath = Path.GetFullPath(beforeSnapshotPath);
        var executionFullPath = Path.GetFullPath(executionReceiptPath);
        var afterFullPath = Path.GetFullPath(afterSnapshotPath);
        return BuildCore(
            CurrentTeacherFrontierSupport.Read<
                AcquisitionRouteDispatchCompilation>(
                compilationFullPath,
                "Acquisition supporting-transition compilation"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                beforeFullPath,
                "Acquisition supporting-transition before snapshot"),
            CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(
                executionFullPath,
                "Acquisition supporting-transition execution receipt"),
            CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                afterFullPath,
                "Acquisition supporting-transition after snapshot"),
            runId,
            executorVersion,
            CurrentTeacherFrontierSupport.HashFile(compilationFullPath),
            CurrentTeacherFrontierSupport.HashFile(beforeFullPath),
            CurrentTeacherFrontierSupport.HashFile(executionFullPath),
            CurrentTeacherFrontierSupport.HashFile(afterFullPath));
    }

    internal static AcquisitionRouteSupportingTransitionReceipt BuildCore(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope execution,
        SnapshotEnvelope after,
        string runId,
        string executorVersion,
        string compilationSha256 = "",
        string beforeSnapshotSha256 = "",
        string executionReceiptSha256 = "",
        string afterSnapshotSha256 = "")
    {
        var queue = compilation.ActionQueue;
        var result = new AcquisitionRouteSupportingTransitionReceipt
        {
            GoalId = compilation.GoalId,
            RouteOccurrenceId = compilation.RouteOccurrenceId,
            SourceCandidateId = compilation.SourceCandidateId,
            SelectedCandidateId = compilation.SelectedCandidateId,
            QueueId = queue?.QueueId ?? string.Empty,
            CompilationSha256 = compilationSha256,
            BeforeSnapshotSha256 = beforeSnapshotSha256,
            ExecutionReceiptSha256 = executionReceiptSha256,
            AfterSnapshotSha256 = afterSnapshotSha256,
            FreshReplanRequired = true,
            TerminalReceiptEligible = false,
            FormalTrainingAuthorized = false
        };
        var compilationReasons = ValidateCompilation(compilation, before);
        var queueReasons = queue is null
            ? new[] { "supporting_transition_action_queue_missing" }
            : QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                execution,
                after,
                runId,
                executorVersion,
                compilation.SelectedCandidateId,
                compilation.SourceStateHash,
                requireTeacherPreferenceStateRebound: false);
        var snapshotReasons = ValidateSnapshots(before, after);
        var transition = queue is null
            ? BlockedTransition("supporting_transition_action_queue_missing")
            : VerifyCropPlanting(queue, before, after);
        result.CropPlantingTransition = transition;
        result.QueueExecutionVerified = queueReasons.Length == 0;
        result.SupportingTransitionVerified =
            compilationReasons.Length == 0 &&
            queueReasons.Length == 0 &&
            snapshotReasons.Length == 0 &&
            transition.Verified;
        result.Status = result.SupportingTransitionVerified
            ? "verified_supporting_transition_fresh_replan_required"
            : "blocked_supporting_transition_receipt";
        result.BlockingReasons = compilationReasons
            .Concat(queueReasons)
            .Concat(snapshotReasons)
            .Concat(transition.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return result;
    }

    private static string[] ValidateCompilation(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before)
    {
        var queue = compilation.ActionQueue;
        var reasons = new List<string>();
        if (compilation.SchemaVersion !=
                "acquisition_route_dispatch_compilation.v1" ||
            compilation.Status !=
                "ready_for_supporting_transition_dispatch" ||
            compilation.SelectedRouteOptionRole != "supporting_transition" ||
            compilation.TerminalReceiptEligible ||
            !compilation.FreshReplanRequiredAfterSuccess ||
            !compilation.SupportReservationCommitVerified ||
            !IsSha256(compilation.SupportRequestSha256) ||
            !IsSha256(compilation.SupportCommitReceiptSha256) ||
            !compilation.SupportDeadlineTotalDay.HasValue ||
            !compilation.SupportExpectedReadyTotalDay.HasValue ||
            compilation.SupportExpectedReadyTotalDay.Value >
                compilation.SupportDeadlineTotalDay.Value ||
            !compilation.DispatchReady ||
            compilation.FormalTrainingAuthorized ||
            compilation.BlockingReasons.Length != 0 ||
            queue is null ||
            queue.Items.Length != 1)
        {
            reasons.Add("supporting_transition_compilation_not_ready");
        }
        if (string.IsNullOrWhiteSpace(compilation.RouteOccurrenceId) ||
            string.IsNullOrWhiteSpace(compilation.SourceCandidateId) ||
            string.IsNullOrWhiteSpace(compilation.SelectedCandidateId) ||
            !string.Equals(compilation.SourceStateHash, before.StateHash,
                StringComparison.Ordinal))
        {
            reasons.Add("supporting_transition_compilation_identity_mismatch");
        }
        if (queue is not null && queue.Items.Any(item =>
                !HasUniqueParameter(
                    item.NormalizedCommand?.Parameters,
                    "acquisition_route_option_role",
                    "supporting_transition") ||
                !HasUniqueParameter(
                    item.NormalizedCommand?.Parameters,
                    "acquisition_support_request_sha256",
                    compilation.SupportRequestSha256) ||
                !HasUniqueParameter(
                    item.NormalizedCommand?.Parameters,
                    "acquisition_support_commit_receipt_sha256",
                    compilation.SupportCommitReceiptSha256) ||
                !HasUniqueParameter(
                    item.NormalizedCommand?.Parameters,
                    "acquisition_support_deadline_total_day",
                    compilation.SupportDeadlineTotalDay?.ToString() ??
                        string.Empty) ||
                !HasUniqueParameter(
                    item.NormalizedCommand?.Parameters,
                    "acquisition_support_expected_ready_total_day",
                    compilation.SupportExpectedReadyTotalDay?.ToString() ??
                        string.Empty)))
        {
            reasons.Add(
                "supporting_transition_queue_commit_lineage_missing_or_ambiguous");
        }
        return reasons.ToArray();
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string[] ValidateSnapshots(
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var reasons = new List<string>();
        if (!string.Equals(before.StateHash,
                SnapshotHash.ComputeStateHash(before.State),
                StringComparison.Ordinal) ||
            !string.Equals(after.StateHash,
                SnapshotHash.ComputeStateHash(after.State),
                StringComparison.Ordinal))
        {
            reasons.Add("supporting_transition_snapshot_hash_mismatch");
        }
        if (!TryStateInt(before, "time", "total_days", out var beforeDay) ||
            !TryStateInt(after, "time", "total_days", out var afterDay) ||
            beforeDay != afterDay)
        {
            reasons.Add("supporting_transition_crossed_target_day");
        }
        return reasons.ToArray();
    }

}
