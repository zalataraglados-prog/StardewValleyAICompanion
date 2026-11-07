using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedCancellation
{
    private readonly CropTeacherSeedLifecycleSupport scope;
    private readonly StrategyCommitmentCancelRequest request;

    internal CropTeacherSeedCancellation(CropTeacherSeedLifecycleSupport scope, StrategyCommitmentCancelRequest request)
    {
        this.scope = scope;
        this.request = request;
    }

    public string CommitmentId => scope.Crop.CommitmentId;
    public StrategyCommitmentCancelRequest Request
    {
        get { scope.ValidateInputsUnchanged(); return CropTeacherSeedSupportSource.Copy(request); }
    }

    public CropTeacherSeedRelease VerifyReply(StrategyCommitmentMutationResult result)
    {
        scope.ValidateInputsUnchanged();
        Require(result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Crop seed cancellation reply is rejected/incomplete.");
        var replay = new CropCommitmentLedgerService().Cancel(scope.Ledger, scope.Snapshot, CommitmentId, request, result.Ledger!.UpdatedAt);
        Require(replay.Accepted && EqualJson(replay, result), "Crop seed cancellation complete reply does not replay exactly.");
        return new(scope, result.Ledger!);
    }

    public CropTeacherSeedRelease ResumeFromReadback(StrategyCommitmentLedger observed)
    {
        scope.ValidateInputsUnchanged();
        AcquisitionStrategyLedgerReader.Read(observed, scope.Snapshot);
        var events = observed.History.Where(row => row.CommitmentId == CommitmentId && row.Operation == "cancel" &&
            row.CommitmentRevision == scope.Crop.Revision + 1 && row.LedgerRevision == scope.Ledger.Revision + 1 &&
            row.SourceStateHash == scope.Snapshot.StateHash && row.SourceDecisionId == scope.Crop.SourceDecisionId &&
            row.Reason == request.Reason).ToArray();
        Require(events.Length == 1 && !string.IsNullOrWhiteSpace(events[0].RecordedAt),
            "Crop seed cancellation recovery has no unique original cancel event.");
        var replay = new CropCommitmentLedgerService().Cancel(scope.Ledger, scope.Snapshot, CommitmentId, request, events[0].RecordedAt);
        Require(replay.Accepted && replay.Ledger is not null && EqualJson(
                StrategyCommitmentReadReconciler.Reconcile(replay.Ledger!, scope.Snapshot, observed.UpdatedAt), observed),
            "Crop seed cancellation recovery differs from its original mutation/readback.");
        return new(scope, observed);
    }
}
