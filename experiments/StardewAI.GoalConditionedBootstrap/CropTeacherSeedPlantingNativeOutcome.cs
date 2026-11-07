using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropTeacherSeedPlantingNativeOutcome(CropTeacherSeedPlantingProof Proof, SnapshotEnvelope After,
    Dictionary<string, string> ArtifactHashes)
{
    internal static CropTeacherSeedPlantingNativeOutcome Verify(CropTeacherSeedPlantingSupport scope,
        StrategyCommitmentLedger ledger, MoneyRevenueCropNativeProductionActionBatch batch,
        MoneyRevenueCropNativeLifecycleTransitionRequest request, ActionQueueEnvelope queue, string queueHash)
    {
        Require(request.TransitionKind == "owned_seed_planting" && request.TotalDay == scope.TotalDay,
            "Owned planting native transition kind/day differs from its batch.");
        var comparer = CropTeacherSeedTransferNativeOutcome.PathComparer;
        var paths = new[] { request.QueuePath, request.BeforeSnapshotPath, request.ExecutionReceiptPath, request.AfterSnapshotPath }
            .Select(Path.GetFullPath).ToArray();
        Require(paths.Distinct(comparer).Count() == paths.Length, "Owned planting native artifact paths overlap.");
        var hashes = paths.ToDictionary(path => path, CurrentTeacherFrontierSupport.HashFile, comparer);
        Require(hashes[paths[0]] == queueHash, "Owned planting native queue changed after binding.");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[1], "owned planting before");
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionReceiptEnvelope>(paths[2], "owned planting receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(paths[3], "owned planting after");
        Require(EqualJson(before, scope.Source.Before), "Owned planting native source differs from its marker.");
        var consumed = CropTeacherSeedPlantingReceipt.Validate(queue, before, receipt, after, request);
        var crop = ledger.CropPlantingCommitments.Single(row => row.CommitmentId == scope.CommitmentId);
        var current = before;
        var stepPaths = new HashSet<string>(comparer);
        var receiptDirectory = Path.GetDirectoryName(paths[2])! + Path.DirectorySeparatorChar;
        var pathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var index = 0; index < receipt.StepResults.Length; index++)
        {
            var step = receipt.StepResults[index];
            Require(!string.IsNullOrWhiteSpace(step.AfterSnapshotPath), "Owned planting step after path is missing.");
            var path = Path.GetFullPath(step.AfterSnapshotPath);
            Require(path.StartsWith(receiptDirectory, pathComparison) && stepPaths.Add(path) &&
                    (!hashes.ContainsKey(path) || index == receipt.StepResults.Length - 1 && comparer.Equals(path, paths[3])),
                "Owned planting step path escaped, repeated or aliased an input artifact.");
            if (!hashes.ContainsKey(path)) hashes[path] = CurrentTeacherFrontierSupport.HashFile(path);
            var next = comparer.Equals(path, paths[3]) ? after : CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(path, "owned planting step after");
            VerifyStepBoundary(current, next, before, step, scope.TotalDay);
            if (index < consumed)
            {
                Require(CropSeedPlantingBinding.TryInventory(queue.Items[index].NormalizedCommand.Parameters, out var inventory),
                    "Owned planting queue lost its inventory contract.");
                var node = queue.Items[index].NormalizedCommand.Parameters.Single(row => row.Name == "crop_seed_planting_node_id").Value;
                var tile = batch.Tiles[index];
                CropSeedResourceBoundary.VerifyPlanting(current, next, inventory!, node, crop, batch.LocationId, tile.TileX, tile.TileY);
            }
            else CropSeedResourceBoundary.VerifyUnconsumedPlanting(current, next);
            current = next;
        }
        Require(EqualJson(current, after), "Owned planting terminal after differs from its last native step.");
        var reservation = queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "crop_seed_planting_reservation_id").Value;
        var proofInputs = new Dictionary<string, string>(scope.Source.InputHashes, comparer);
        foreach (var (path, hash) in hashes)
        {
            Require(!proofInputs.TryGetValue(path, out var prior) || prior == hash, "Owned planting proof source hashes conflict.");
            proofInputs[path] = hash;
        }
        return new(new("crop_teacher_seed_planting_proof.v1", scope.CommitmentId, scope.MarkerRequest.PortfolioId,
            reservation, consumed, queue.Items.Length, consumed == queue.Items.Length, before.StateHash, after.StateHash,
            request.RunId, request.ExecutorVersion, request.SelectedCandidateId, queueHash, CanonicalSha256(ledger, out _),
            proofInputs, true, false), after, proofInputs);
    }

    private static void VerifyStepBoundary(SnapshotEnvelope before, SnapshotEnvelope after, SnapshotEnvelope source,
        QueueExecutionStepReceipt step, int totalDay)
    {
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(before, "owned planting step before");
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(after, "owned planting step after");
        var clock = MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "time");
        Require(after.SaveId.Value == source.SaveId.Value && after.PlayerId.Value == source.PlayerId.Value &&
                after.GameVersion == source.GameVersion && step.SourceStateHash == before.StateHash &&
                step.AfterStateHash == after.StateHash && step.BeforeGameTick == before.GameTick && step.AfterGameTick == after.GameTick &&
                after.GameTick >= before.GameTick &&
                MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(after, "time", "total_days") == totalDay &&
                clock >= MoneyRevenueCropSeedAcquisitionProofBuilder.ReadInt(before, "time", "time") &&
                FieldEnvelopeValidator.IsReadableStatus(after.InGameTime.Status) && after.InGameTime.Value == clock,
            "Owned planting step source/actor/tick/day/clock drifted.");
    }
}
