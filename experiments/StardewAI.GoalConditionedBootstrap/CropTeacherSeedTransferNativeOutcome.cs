using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropTeacherSeedTransferNativeOutcome(CropTeacherSeedTransferProof Proof, SnapshotEnvelope After,
    Dictionary<string, string> ArtifactHashes)
{
    internal static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static CropTeacherSeedTransferNativeOutcome Verify(CropTeacherSeedTransferSupport scope,
        StrategyCommitmentLedger committed, MoneyRevenueCropNativeLifecycleTransitionRequest request,
        ActionQueueEnvelope queue, string queueHash, string preparedQueueHash)
    {
        Require(request.TransitionKind == "owned_seed_transfer" && IsLowerSha256(preparedQueueHash) && queueHash == preparedQueueHash,
            "Owned seed transfer native queue differs from its prepared compilation.");
        var paths = new[] { request.QueuePath, request.BeforeSnapshotPath, request.ExecutionReceiptPath, request.AfterSnapshotPath }
            .Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(PathComparer).Count() == paths.Length,
            "Owned seed transfer native artifact paths overlap.");
        var hashes = paths.ToDictionary(path => path, CurrentTeacherFrontierSupport.HashFile, PathComparer);
        Require(hashes[paths[0]] == queueHash, "Owned seed transfer queue changed before verification.");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[1], "owned seed transfer before");
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(paths[2], "owned seed transfer native receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[3], "owned seed transfer after");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "owned seed transfer before");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(after, "owned seed transfer after");
        Require(EqualJson(scope.Source.Before, before), "Owned seed transfer native source differs from its preparation.");
        var day = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "total_days");
        var start = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "time");
        var end = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "time");
        Require(request.TotalDay == day && MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "total_days") == day &&
                day <= scope.Source.Supply.Commitments.Single().PlantingTotalDay && end >= start &&
                FieldEnvelopeValidator.IsReadableStatus(before.InGameTime.Status) && before.InGameTime.Value == start &&
                FieldEnvelopeValidator.IsReadableStatus(after.InGameTime.Status) && after.InGameTime.Value == end,
            "Owned seed transfer crossed its planting deadline/day boundary.");
        var reasons = QueueExecutionReceiptValidator.Validate(queue, before, receipt, after, request.RunId,
            request.ExecutorVersion, request.SelectedCandidateId, before.StateHash, requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0, "Owned seed transfer native receipt rejected: " + string.Join(",", reasons));
        Require(!string.IsNullOrWhiteSpace(receipt.StepResults.Single().AfterSnapshotPath),
            "Owned seed transfer step snapshot path is missing.");
        var stepPath = Path.GetFullPath(receipt.StepResults.Single().AfterSnapshotPath);
        var directory = Path.GetDirectoryName(paths[2])! + Path.DirectorySeparatorChar;
        Require(stepPath.StartsWith(directory, PathComparison),
            "Owned seed transfer step snapshot escaped its receipt directory.");
        if (!hashes.ContainsKey(stepPath))
        {
            hashes[stepPath] = CurrentTeacherFrontierSupport.HashFile(stepPath);
            Require(EqualJson(after, CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(stepPath, "owned seed transfer step after")),
                "Owned seed transfer step snapshot differs from its terminal after-state.");
        }
        else Require(string.Equals(stepPath, paths[3], PathComparison),
            "Owned seed transfer step snapshot aliases a non-terminal artifact.");
        CropSeedTransferResourceBoundary.Verify(before, after, committed.PreparedMaterialTransfer!,
            committed.CropPlantingCommitments.Single(row => row.CommitmentId == scope.CommitmentId).SeedId);
        var prepared = committed.PreparedMaterialTransfer!;
        return new(new CropTeacherSeedTransferProof("crop_teacher_seed_transfer_proof.v1", scope.CommitmentId,
            prepared.PortfolioId, prepared.BindingSha256, request.RunId, request.ExecutorVersion, request.SelectedCandidateId,
            paths[0], queueHash, paths[1], hashes[paths[1]], paths[2], hashes[paths[2]], paths[3], hashes[paths[3]],
            before.StateHash, after.StateHash, before.GameTick, after.GameTick,
            CropTeacherSeedSupportSource.Copy(prepared.MaterialRelocations),
            new Dictionary<string, string>(hashes, PathComparer), true, false), after, hashes);
    }
}
