using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunMachineInputPurchaseTerminalCoverage(
        string executionInputsPath)
    {
        var inputs = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBindingInputs>(
            Path.GetFullPath(executionInputsPath),
            "Machine-input purchase terminal coverage execution inputs");
        _ = VerifyMachineInputPurchaseSupportingTransitionProof(inputs);
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageSource
        VerifyMachineInputPurchaseSupportingTransitionProof(
            AcquisitionRouteExecutionBindingInputs template)
    {
        var root = Path.Combine(
            Path.GetDirectoryName(template.BeforeSnapshotPath)!,
            "machine-input-purchase-support-proof");
        Directory.CreateDirectory(root);
        var authority = BuildMachineCapacityProofAuthority(
            template,
            Path.Combine(root, "authority"));
        var supportRoot = Path.Combine(root, "purchase-support");
        Directory.CreateDirectory(supportRoot);
        var beforeSnapshotPath = Path.Combine(
            supportRoot,
            "before-snapshot.json");
        var before = WriteMachineInputPurchaseProofBeforeSnapshot(
            template.BeforeSnapshotPath,
            beforeSnapshotPath);
        var baseLedgerPath = Path.Combine(supportRoot, "strategy-ledger.json");
        var baseLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.machine-input-purchase-file-backed",
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
            new[] { "economy.buy_supplies" },
            includeExecutorCalibrationOptions: true,
            commitmentLedger: baseLedger);
        var ranking = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            authority.GoalId);
        Require(ranking.Count(candidate =>
                    candidate.OptionId == "economy.buy_supplies" &&
                    candidate.Kind == "buy_shop_item" &&
                    candidate.Available &&
                    candidate.ShopId == "FixtureShop" &&
                    candidate.QualifiedItemId == "(O)262" &&
                    candidate.UnitPrice == 80) == 1,
            "File-backed machine-input purchase snapshot emitted no unique " +
            "exact purchase candidate: " +
            string.Join("|", availability.Options.Select(option =>
                option.OptionId + "[" +
                string.Join(",", option.BlockingReasons) + "]<missing=" +
                string.Join(",", option.MissingStateFactors) + ">{" +
                string.Join(";", option.EventCandidates.Select(candidate =>
                    candidate.Kind + ":" + candidate.Available + ":" +
                    string.Join(",", candidate.BlockReasons))) + "}")));
        var rankingPath = Path.Combine(supportRoot, "ranking.json");
        Write(rankingPath, CollectionRanking(before.StateHash, ranking));
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
                var after = WriteMachineInputPurchaseProofAfterSnapshot(
                    beforeSnapshotPath,
                    afterPath);
                return new FileBackedSupportingTransitionExecution(
                    after,
                    PurchaseExecutionReceipt(
                        compilation,
                        before,
                        after));
            });
        Require(built.Request.SupportTransitionKind ==
                    AcquisitionRouteSupportingTransitionKinds
                        .MachineInputPurchase &&
                built.Request.PurchaseStage == "purchase" &&
                built.Compilation.ActionQueue?.Items.Select(item =>
                    item.OptionId).SequenceEqual(
                    new[]
                    {
                        "executor.buy_shop_item",
                        "executor.close_menu"
                    },
                    StringComparer.Ordinal) == true &&
                built.TransitionReceipt.PurchaseTransition is
                {
                    Verified: true,
                    Stage: "purchase",
                    ShopId: "FixtureShop",
                    StockId: "fixture-wheat-seed",
                    QualifiedItemId: "(O)262",
                    CurrencyBefore: 1_000,
                    CurrencyAfter: 920,
                    ObservedCurrencyDecrease: 80,
                    ItemQuantityBefore: 0,
                    ItemQuantityAfter: 1,
                    ObservedItemIncrease: 1
                } &&
                built.SettlementReceipt.CurrencySettlements is
                [
                    {
                        ConsumedAmount: 80,
                        RemainingAmount: 0
                    }
                ] &&
                built.SettlementReceipt.CompletedCurrencyReservationIds
                    .Length == 1,
            "File-backed machine-input purchase lost its native purchase or " +
            "currency-settlement identity.");

        return VerifySupportingTransitionTerminalRoute(
            authority,
            supportInputs,
            built.Proof,
            built.AfterSnapshotPath,
            built.SettledLedgerPath,
            root);
    }
}
