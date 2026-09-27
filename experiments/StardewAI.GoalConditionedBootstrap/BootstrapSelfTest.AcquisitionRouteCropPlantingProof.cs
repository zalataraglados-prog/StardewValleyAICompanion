using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunCropPlantingTerminalCoverage(
        string executionInputsPath)
    {
        var inputs = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBindingInputs>(
            Path.GetFullPath(executionInputsPath),
            "Crop-planting terminal coverage execution inputs");
        _ = VerifyCropPlantingSupportingTransitionProof(inputs);
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageSource
        VerifyCropPlantingSupportingTransitionProof(
        AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "crop-planting-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var supportRoot = Path.Combine(root, "planting-support");
        Directory.CreateDirectory(supportRoot);
        var beforeSnapshotPath = Path.Combine(
            supportRoot,
            "before-snapshot.json");
        var before = WriteCropPlantingProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(supportRoot, "strategy-ledger.json");
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.crop-planting-file-backed",
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
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            before,
            new[] { "farm.maintain_crops" },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: baseLedger);
        var ranking = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            authority.GoalId);
        Require(ranking.Count(candidate =>
                    candidate.Kind == "plant_seed_tile" &&
                    candidate.Available) == 1,
            "File-backed crop snapshot emitted no unique planting candidate: " +
            string.Join("|", availability.Options.Select(option =>
                option.OptionId + "[" +
                string.Join(",", option.BlockingReasons) + "]<missing=" +
                string.Join(",", option.MissingStateFactors) + ">{" +
                string.Join(";", option.EventCandidates.Select(candidate =>
                    candidate.Kind + ":" + candidate.Available + ":" +
                    string.Join(",", candidate.BlockReasons))) + "}")));
        var plantingCandidate = ranking.Single(candidate =>
            candidate.Kind == "plant_seed_tile" && candidate.Available);
        Require(plantingCandidate.ItemId == "472" &&
                plantingCandidate.QualifiedItemId == "(O)472" &&
                plantingCandidate.Parameters.Any(parameter =>
                    parameter.Name == "harvest_item_qualified_id" &&
                    parameter.Value == "(O)24"),
            "File-backed crop planting candidate lost exact seed/harvest identity.");
        var rankingPath = Path.Combine(supportRoot, "ranking.json");
        Write(rankingPath, CollectionRanking(before.StateHash, ranking));
        var supportInputs = SupportingTransitionProofInputs(
            portfolioInputs,
            rankingPath,
            authority.CropRouteOccurrenceId,
            supportDeadlineTotalDay: 10);
        var built = BuildFileBackedSupportingTransitionProof(
            supportRoot,
            supportInputs,
            baseLedger,
            before,
            (compilation, afterPath) =>
            {
                var after = WriteCropPlantingProofAfterSnapshot(
                    beforeSnapshotPath,
                    afterPath);
                return new FileBackedSupportingTransitionExecution(
                    after,
                    SupportingExecutionReceipt(
                        compilation,
                        before,
                        after));
            });
        Require(built.Request.SupportTransitionKind ==
                    AcquisitionRouteSupportingTransitionKinds.CropPlanting &&
                built.Compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.plant_seed" &&
                built.TransitionReceipt.CropPlantingTransition is
                {
                    Verified: true,
                    BeforeSeedQuantity: 3,
                    AfterSeedQuantity: 2,
                    SeedQuantityDecrease: 1,
                    BeforeTargetCropPresent: false,
                    AfterTargetCropPresent: true,
                    AfterCropDead: false,
                    AfterCropReadyForHarvest: false
                } &&
                built.SettlementReceipt.ConsumedQuantity == 1 &&
                built.SettlementReceipt.CompletedMaterialReservationIds
                    .Length == 1 &&
                built.SettlementReceipt.ActiveMaterialReservationIds
                    .Length == 0,
            "File-backed crop planting proof lost its native transition or " +
            "exact seed settlement identity.");

        return VerifySupportingTransitionTerminalRoute(
            authority,
            supportInputs,
            built.Proof,
            built.AfterSnapshotPath,
            built.SettledLedgerPath,
            root,
            terminalRouteOccurrenceId: authority.CropShopRouteOccurrenceId,
            supportRouteOccurrenceId: authority.CropRouteOccurrenceId);
    }
}
