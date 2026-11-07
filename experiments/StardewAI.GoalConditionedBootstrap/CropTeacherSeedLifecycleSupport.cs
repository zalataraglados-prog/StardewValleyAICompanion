using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedLifecycleSupport
{
    private readonly Dictionary<string, string> inputHashes;
    internal SnapshotEnvelope Snapshot { get; }
    internal StrategyCommitmentLedger Ledger { get; }
    internal CropPlantingCommitment Crop { get; }

    private CropTeacherSeedLifecycleSupport(string snapshotPath, string ledgerPath, string commitmentId, string releaseStepId)
    {
        var paths = new[] { snapshotPath, ledgerPath }.Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).Count() == paths.Length,
            "Crop seed lifecycle source paths overlap.");
        inputHashes = paths.ToDictionary(path => path, CurrentTeacherFrontierSupport.HashFile, CropTeacherSeedTransferNativeOutcome.PathComparer);
        Snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[0], "crop seed lifecycle source");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(Snapshot, "crop seed lifecycle source");
        Ledger = AcquisitionStrategyLedgerReader.Read(paths[1], Snapshot).Ledger;
        var crops = Ledger.CropPlantingCommitments.Where(row => row.CommitmentId == commitmentId).ToArray();
        Require(Ledger.SourceStateHash == Snapshot.StateHash && Ledger.PreparedMaterialTransfer is null && crops.Length == 1 &&
                crops[0].Status == StrategyCommitmentStatuses.Active && !string.IsNullOrWhiteSpace(crops[0].SourceDecisionId) &&
                !string.IsNullOrWhiteSpace(crops[0].SeedQualifiedItemId),
            "Crop seed lifecycle requires a current active crop without pending transfer.");
        Crop = crops[0];
        Require(!string.IsNullOrWhiteSpace(releaseStepId) && releaseStepId != Crop.SourceDecisionId && releaseStepId != commitmentId &&
                !Ledger.History.Any(row => row.CommitmentId == releaseStepId) &&
                !Ledger.CropPlantingCommitments.Any(row => row.CommitmentId == releaseStepId || row.SourceDecisionId == releaseStepId),
            "Crop seed lifecycle release identity is not new/distinct.");
        ReleaseStepId = releaseStepId;
        SnapshotPath = paths[0];
        // Releasing an allocation is not a native acquisition/consumption; no inventory is changed or invented here.
        CropTeacherSeedRelease.ValidateOwnedClaims(Ledger, Crop);
        ValidateInputsUnchanged();
    }

    public static CropTeacherSeedLifecycleSupport Open(string snapshotPath, string ledgerPath, string commitmentId,
        string releaseStepId) => new(snapshotPath, ledgerPath, commitmentId, releaseStepId);

    public string ReleaseStepId { get; }
    public string SnapshotPath { get; }
    public string CommitmentId => Crop.CommitmentId;
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(Ledger);
    public IReadOnlyDictionary<string, string> InputHashes => new Dictionary<string, string>(inputHashes, CropTeacherSeedTransferNativeOutcome.PathComparer);

    public CropTeacherSeedCancellation BeginCancellation(string reason)
    {
        ValidateInputsUnchanged();
        Require(!string.IsNullOrWhiteSpace(reason), "Crop seed cancellation requires an explicit reason.");
        return new(this, new StrategyCommitmentCancelRequest
        {
            StateHash = Snapshot.StateHash, ExpectedLedgerRevision = Ledger.Revision, Reason = reason
        });
    }

    public CropTeacherSeedRelease VerifyCompletionReadback(StrategyCommitmentLedger observed)
    {
        ValidateInputsUnchanged();
        AcquisitionStrategyLedgerReader.Read(observed, Snapshot);
        var expected = StrategyCommitmentReadReconciler.Reconcile(Ledger, Snapshot, observed.UpdatedAt);
        Require(EqualJson(expected, observed) && expected.CropPlantingCommitments.Single(row => row.CommitmentId == Crop.CommitmentId)
                .Status == StrategyCommitmentStatuses.Completed,
            "Crop seed completion readback is not the original complete reconciliation.");
        return new(this, observed);
    }

    public void ValidateInputsUnchanged()
    {
        foreach (var (path, hash) in inputHashes)
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Crop seed lifecycle input changed: " + path);
    }
}
