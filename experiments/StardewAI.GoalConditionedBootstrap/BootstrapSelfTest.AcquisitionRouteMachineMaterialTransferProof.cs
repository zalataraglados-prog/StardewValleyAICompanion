using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunMachineMaterialTransferTerminalCoverage(
        string executionInputsPath)
    {
        var inputs = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBindingInputs>(
            Path.GetFullPath(executionInputsPath),
            "Machine material-transfer terminal coverage execution inputs");
        _ = VerifyMachineMaterialTransferSupportingTransitionProof(inputs);
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageSource
        VerifyMachineMaterialTransferSupportingTransitionProof(
            AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "machine-material-transfer-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var supportRoot = Path.Combine(root, "material-transfer-support");
        Directory.CreateDirectory(supportRoot);
        var beforeSnapshotPath = Path.Combine(
            supportRoot,
            "before-snapshot.json");
        var before = WriteMachineMaterialTransferProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(supportRoot, "strategy-ledger.json");
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.machine-material-transfer-file-backed",
            SaveId = before.SaveId.Value!,
            PlayerId = before.PlayerId.Value!,
            Revision = 3,
            UpdatedAt = "2026-09-28T00:00:00Z",
            SourceStateHash = before.StateHash
        };
        Write(baseLedgerPath, baseLedger);
        var portfolioInputs = BuildTargetDatePortfolioInputs(
            authority.Template,
            beforeSnapshotPath,
            baseLedgerPath,
            Path.Combine(supportRoot, "unused-proposal.json"),
            supportRoot,
            targetTotalDay: 0);
        var reservations = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateReservationReport>(
            portfolioInputs.TargetDateReservationPath,
            "Machine material-transfer reservation report");
        var reservation = reservations.Routes.Single(route =>
            route.RouteOccurrenceId == authority.MachineRouteOccurrenceId);
        var candidates = AcquisitionMachineInputMaterialStaging
            .BuildCandidates(before, reservation, out var candidateReasons);
        Require(candidateReasons.Length == 0 && candidates is
            [
                {
                    OptionId: "inventory.transfer_item",
                    Kind: "transfer_inventory_item",
                    Available: true
                }
            ],
            "File-backed machine material transfer did not emit one exact " +
            "candidate: " + string.Join(",", candidateReasons));
        var rankingPath = Path.Combine(supportRoot, "ranking.json");
        Write(
            rankingPath,
            CollectionRanking(before.StateHash, candidates));
        var supportInputs = SupportingTransitionProofInputs(
            portfolioInputs,
            rankingPath,
            authority.MachineRouteOccurrenceId);
        var built = BuildFileBackedSupportingTransitionProof(
            supportRoot,
            supportInputs,
            baseLedger,
            before,
            (compilation, afterPath) =>
            {
                var after = WriteMachineMaterialTransferProofAfterSnapshot(
                    beforeSnapshotPath,
                    afterPath);
                return new FileBackedSupportingTransitionExecution(
                    after,
                    MaterialStagingExecutionReceipt(
                        compilation,
                        before,
                        after));
            });
        Require(built.Request.SupportTransitionKind ==
                    AcquisitionRouteSupportingTransitionKinds
                        .MachineInputMaterialTransfer &&
                built.Compilation.ActionQueue?.Items.Select(item =>
                    item.OptionId).SequenceEqual(
                    new[]
                    {
                        "executor.move_to_tile",
                        "executor.transfer_material"
                    },
                    StringComparer.Ordinal) == true &&
                built.TransitionReceipt.MaterialTransferTransition is
                {
                    Verified: true,
                    SourceQuantityBefore: 1,
                    SourceQuantityAfter: 0,
                    DestinationQuantityBefore: 0,
                    DestinationQuantityAfter: 1
                } &&
                built.SettlementReceipt.RelocatedMaterialReservationIds
                    .Length == 1,
            "File-backed machine material transfer lost its native " +
            "relocation identity.");

        return VerifySupportingTransitionTerminalRoute(
            authority,
            supportInputs,
            built.Proof,
            built.AfterSnapshotPath,
            built.SettledLedgerPath,
            root);
    }
}
