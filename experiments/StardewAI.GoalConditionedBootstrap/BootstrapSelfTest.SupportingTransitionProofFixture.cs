using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static FileBackedSupportingTransitionProof
        BuildFileBackedSupportingTransitionProof(
            string root,
            AcquisitionRouteSupportingTransitionRequestInputs supportInputs,
            StrategyCommitmentLedger baseLedger,
            SnapshotEnvelope before,
            Func<
                AcquisitionRouteDispatchCompilation,
                string,
                FileBackedSupportingTransitionExecution> execute)
    {
        Directory.CreateDirectory(root);
        var supportRequestPath = Path.Combine(root, "support-request.json");
        var request = AcquisitionRouteSupportingTransitionRequestBuilder.Build(
            supportInputs);
        Require(request.SupportRequestReady &&
                request.AtomicCommitRequest is not null,
            "File-backed support request was not admitted: " +
            string.Join(",", request.BlockingReasons));
        Write(supportRequestPath, request);
        var atomicCommitRequest = request.AtomicCommitRequest ??
            throw new InvalidDataException(
                "File-backed support request has no atomic commit request.");

        var service = new ReservationPortfolioLedgerService();
        var commitResult = service.Commit(
            baseLedger,
            before,
            atomicCommitRequest,
            "2026-09-28T00:00:01Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "File-backed support commit failed: " +
            string.Join(",", commitResult.Errors));
        var commitResultPath = Path.Combine(root, "commit-result.json");
        var committedLedgerPath = Path.Combine(root, "committed-ledger.json");
        Write(commitResultPath, commitResult);
        Write(committedLedgerPath, commitResult.Ledger!);
        var commitReceiptPath = Path.Combine(root, "commit-receipt.json");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.Build(
                supportInputs,
                supportRequestPath,
                committedLedgerPath,
                commitResultPath);
        Require(commitReceipt.SupportReservationCommitVerified,
            "File-backed support commit receipt failed: " +
            string.Join(",", commitReceipt.BlockingReasons));
        Write(commitReceiptPath, commitReceipt);

        var compilationPath = Path.Combine(root, "compilation.json");
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.Build(
                supportInputs,
                supportRequestPath,
                commitReceiptPath,
                committedLedgerPath,
                commitResultPath);
        Require(compilation.DispatchReady &&
                compilation.ActionQueue is not null &&
                compilation.FreshReplanRequiredAfterSuccess &&
                !compilation.TerminalReceiptEligible,
            "File-backed support compilation failed: " +
            string.Join(",", compilation.BlockingReasons));
        Write(compilationPath, compilation);

        var afterSnapshotPath = Path.Combine(root, "after-snapshot.json");
        var executed = execute(compilation, afterSnapshotPath);
        Require(executed.Receipt.SourceStateHash == before.StateHash &&
                executed.Receipt.AfterStateHash == executed.After.StateHash &&
                executed.Receipt.AfterSnapshotFresh &&
                executed.Receipt.Success,
            "File-backed support execution receipt identity drifted.");
        var executionReceiptPath = Path.Combine(root, "execution-receipt.json");
        Write(executionReceiptPath, executed.Receipt);
        var transitionReceiptPath = Path.Combine(
            root,
            "supporting-transition-receipt.json");
        var transitionReceipt =
            AcquisitionRouteSupportingTransitionReceiptBuilder.Build(
                compilationPath,
                supportInputs.SnapshotPath,
                executionReceiptPath,
                afterSnapshotPath,
                executed.Receipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(transitionReceipt.SupportingTransitionVerified &&
                transitionReceipt.FreshReplanRequired &&
                !transitionReceipt.TerminalReceiptEligible,
            "File-backed support transition receipt failed: " +
            string.Join(",", transitionReceipt.BlockingReasons));
        Write(transitionReceiptPath, transitionReceipt);

        var settlementRequestPath = Path.Combine(
            root,
            "settlement-request.json");
        var settlementRequest =
            AcquisitionRouteSupportingTransitionSettlementBuilder.BuildRequest(
                supportInputs,
                supportRequestPath,
                commitReceiptPath,
                committedLedgerPath,
                commitResultPath,
                compilationPath,
                executionReceiptPath,
                afterSnapshotPath,
                transitionReceiptPath,
                executed.Receipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Write(settlementRequestPath, settlementRequest);
        var settlement = service.SettleSupportingTransition(
            commitResult.Ledger,
            executed.After,
            settlementRequest,
            "2026-09-28T00:00:03Z");
        Require(settlement.Accepted && settlement.Ledger is not null,
            "File-backed support settlement failed: " +
            string.Join(",", settlement.Errors));
        var settlementResultPath = Path.Combine(root, "settlement-result.json");
        var settledLedgerPath = Path.Combine(root, "settled-ledger.json");
        Write(settlementResultPath, settlement);
        Write(settledLedgerPath, settlement.Ledger!);
        var settlementReceiptPath = Path.Combine(
            root,
            "settlement-receipt.json");
        var settlementReceipt =
            AcquisitionRouteSupportingTransitionSettlementBuilder.BuildReceipt(
                supportInputs,
                supportRequestPath,
                commitReceiptPath,
                committedLedgerPath,
                commitResultPath,
                compilationPath,
                executionReceiptPath,
                afterSnapshotPath,
                transitionReceiptPath,
                executed.Receipt.RunId,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath);
        Require(settlementReceipt.ReservationLifecycleVerified &&
                settlementReceipt.FreshReplanRequired &&
                !settlementReceipt.TerminalReceiptEligible &&
                !settlementReceipt.FormalTrainingAuthorized,
            "File-backed support settlement receipt failed: " +
            string.Join(",", settlementReceipt.BlockingReasons));
        Write(settlementReceiptPath, settlementReceipt);

        return new FileBackedSupportingTransitionProof(
            request,
            compilation,
            transitionReceipt,
            settlementReceipt,
            new AcquisitionRouteSupportingTransitionSettlementProof
            {
                SupportRequestPath = supportRequestPath,
                SupportCommitReceiptPath = commitReceiptPath,
                CommittedLedgerPath = committedLedgerPath,
                CommitResultPath = commitResultPath,
                CompilationPath = compilationPath,
                ExecutionReceiptPath = executionReceiptPath,
                AfterSnapshotPath = afterSnapshotPath,
                SupportingTransitionReceiptPath = transitionReceiptPath,
                RunId = executed.Receipt.RunId,
                ExecutorVersion =
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                SettlementRequestPath = settlementRequestPath,
                SettlementResultPath = settlementResultPath,
                SettledLedgerPath = settledLedgerPath,
                SettlementReceiptPath = settlementReceiptPath
            },
            afterSnapshotPath,
            settledLedgerPath);
    }

    private sealed record FileBackedSupportingTransitionExecution(
        SnapshotEnvelope After,
        QueueExecutionReceiptEnvelope Receipt);

    private sealed record FileBackedSupportingTransitionProof(
        AcquisitionRouteSupportingTransitionRequest Request,
        AcquisitionRouteDispatchCompilation Compilation,
        AcquisitionRouteSupportingTransitionReceipt TransitionReceipt,
        AcquisitionRouteSupportingTransitionSettlementReceipt SettlementReceipt,
        AcquisitionRouteSupportingTransitionSettlementProof Proof,
        string AfterSnapshotPath,
        string SettledLedgerPath);
}
