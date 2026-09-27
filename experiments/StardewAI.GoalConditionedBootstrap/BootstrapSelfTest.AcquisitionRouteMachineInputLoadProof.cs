using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunMachineInputLoadTerminalCoverage(
        string executionInputsPath)
    {
        var inputs = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBindingInputs>(
            Path.GetFullPath(executionInputsPath),
            "Machine-input load terminal coverage execution inputs");
        _ = VerifyMachineInputLoadSupportingTransitionProof(inputs);
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageSource
        VerifyMachineInputLoadSupportingTransitionProof(
            AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "machine-input-load-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var supportRoot = Path.Combine(root, "load-support");
        Directory.CreateDirectory(supportRoot);
        var beforeSnapshotPath = Path.Combine(
            supportRoot,
            "before-snapshot.json");
        var before = WriteMachineInputLoadProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(supportRoot, "strategy-ledger.json");
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.machine-input-load-file-backed",
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
            new[] { "farm.process_machines" },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: baseLedger);
        var ranking = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            authority.GoalId);
        Require(ranking.Count(candidate =>
                    candidate.Kind == "load_machine_input_tile" &&
                    candidate.Available) == 1,
            "File-backed machine load snapshot emitted no unique load candidate: " +
            string.Join("|", availability.Options.Select(option =>
                option.OptionId + "[" +
                string.Join(",", option.BlockingReasons) + "]<missing=" +
                string.Join(",", option.MissingStateFactors) + ">{" +
                string.Join(";", option.EventCandidates.Select(candidate =>
                    candidate.Kind + ":" + candidate.Available + ":" +
                    string.Join(",", candidate.BlockReasons))) + "}")));
        var rankingPath = Path.Combine(supportRoot, "ranking.json");
        Write(rankingPath, CollectionRanking(before.StateHash, ranking));
        var supportInputs = MachineCapacityProofSupportInputs(
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
                var after = WriteMachineInputLoadProofAfterSnapshot(
                    beforeSnapshotPath,
                    afterPath);
                return new FileBackedSupportingTransitionExecution(
                    after,
                    MachineSupportingExecutionReceipt(
                        compilation,
                        before,
                        after));
            });
        Require(built.Request.SupportTransitionKind ==
                    AcquisitionRouteSupportingTransitionKinds
                        .MachineInputLoad &&
                built.Compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.load_machine_input" &&
                built.TransitionReceipt.MachineInputTransition is
                {
                    Verified: true,
                    BeforeCapacityState: "idle",
                    AfterCapacityState: "processing"
                },
            "File-backed machine load proof lost its native transition identity.");

        return VerifyMachineCapacityProofTerminalRoute(
            authority,
            supportInputs,
            built.Proof,
            built.AfterSnapshotPath,
            built.SettledLedgerPath,
            root);
    }
}
