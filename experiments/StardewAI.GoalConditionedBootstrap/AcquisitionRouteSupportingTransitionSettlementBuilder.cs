using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionSettlementBuilder
{
    public static ReservationPortfolioSupportingTransitionSettlementRequest
        BuildRequest(
            AcquisitionRouteSupportingTransitionRequestInputs inputs,
            string supportRequestPath,
            string supportCommitReceiptPath,
            string committedLedgerPath,
            string commitResultPath,
            string compilationPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string supportingTransitionReceiptPath,
            string runId,
            string executorVersion) => CanonicalRequest(Prepare(
                inputs,
                supportRequestPath,
                supportCommitReceiptPath,
                committedLedgerPath,
                commitResultPath,
                compilationPath,
                executionReceiptPath,
                afterSnapshotPath,
                supportingTransitionReceiptPath,
                runId,
                executorVersion));

    public static AcquisitionRouteSupportingTransitionSettlementReceipt
        BuildReceipt(
            AcquisitionRouteSupportingTransitionRequestInputs inputs,
            string supportRequestPath,
            string supportCommitReceiptPath,
            string committedLedgerPath,
            string commitResultPath,
            string compilationPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string supportingTransitionReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath)
    {
        var context = Prepare(
            inputs,
            supportRequestPath,
            supportCommitReceiptPath,
            committedLedgerPath,
            commitResultPath,
            compilationPath,
            executionReceiptPath,
            afterSnapshotPath,
            supportingTransitionReceiptPath,
            runId,
            executorVersion);
        var requestPath = Path.GetFullPath(settlementRequestPath);
        var resultPath = Path.GetFullPath(settlementResultPath);
        var ledgerPath = Path.GetFullPath(settledLedgerPath);
        var request = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioSupportingTransitionSettlementRequest>(
            requestPath,
            "Acquisition support settlement request");
        Require(EqualJson(request, CanonicalRequest(context)),
            "Support settlement request drifted.");
        var result = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioSupportingTransitionSettlementResult>(
            resultPath,
            "Acquisition support settlement result");
        using var afterDocument = JsonDocument.Parse(File.ReadAllText(
            Path.GetFullPath(afterSnapshotPath)));
        var settled = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            afterDocument.RootElement).Ledger;
        return BuildReceiptCore(
            context,
            request,
            result,
            settled,
            CurrentTeacherFrontierSupport.HashFile(requestPath),
            CurrentTeacherFrontierSupport.HashFile(resultPath),
            CurrentTeacherFrontierSupport.HashFile(ledgerPath));
    }

    internal static ReservationPortfolioSupportingTransitionSettlementRequest
        BuildRequestCore(
            AcquisitionRouteSupportingTransitionRequest request,
            AcquisitionRouteSupportingTransitionCommitReceipt commit,
            AcquisitionRouteDispatchCompilation compilation,
            AcquisitionRouteSupportingTransitionReceipt transition,
            SnapshotEnvelope after,
            StrategyCommitmentLedger ledger,
            string transitionReceiptSha256) => CanonicalRequest(
                WithTransitionHash(
                    CreateContext(
                        request,
                        commit,
                        compilation,
                        transition,
                        after,
                        ledger),
                    transitionReceiptSha256));

    internal static AcquisitionRouteSupportingTransitionSettlementReceipt
        BuildReceiptCore(
            AcquisitionRouteSupportingTransitionRequest supportRequest,
            AcquisitionRouteSupportingTransitionCommitReceipt commit,
            AcquisitionRouteDispatchCompilation compilation,
            AcquisitionRouteSupportingTransitionReceipt transition,
            SnapshotEnvelope after,
            StrategyCommitmentLedger baseLedger,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ReservationPortfolioSupportingTransitionSettlementResult result,
            StrategyCommitmentLedger settled,
            string transitionReceiptSha256) => BuildReceiptCore(
                WithTransitionHash(
                    CreateContext(
                        supportRequest,
                        commit,
                        compilation,
                        transition,
                        after,
                        baseLedger),
                    transitionReceiptSha256),
                request,
                result,
                settled,
                string.Empty,
                string.Empty,
                string.Empty);

    private static SettlementContext WithTransitionHash(
        SettlementContext context,
        string sha256)
    {
        Require(sha256.Length == 64 && sha256.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "Supporting-transition receipt hash is invalid.");
        return context with
        {
            SupportingTransitionReceiptSha256 = sha256
        };
    }

    private static AcquisitionRouteSupportingTransitionSettlementReceipt
        BuildReceiptCore(
            SettlementContext context,
            ReservationPortfolioSupportingTransitionSettlementRequest request,
            ReservationPortfolioSupportingTransitionSettlementResult result,
            StrategyCommitmentLedger settled,
            string requestSha256,
            string resultSha256,
            string settledLedgerSha256)
    {
        var reasons = ValidateSettlement(
            context,
            request,
            result,
            settled);
        var verified = reasons.Length == 0;
        return new AcquisitionRouteSupportingTransitionSettlementReceipt
        {
            Status = verified
                ? "verified_supporting_transition_claim_settlement"
                : "blocked_supporting_transition_claim_settlement",
            GoalId = context.Request.GoalId,
            SupportRequestId = context.Request.SupportRequestId,
            RouteOccurrenceId = context.Request.RouteOccurrenceId,
            RouteSourceDecisionId = context.ConsumedClaim.SourceDecisionId,
            AfterStateHash = context.AfterSnapshot.StateHash,
            SupportRequestSha256 = context.SupportRequestSha256,
            SupportCommitReceiptSha256 =
                context.SupportCommitReceiptSha256,
            SupportingTransitionReceiptSha256 =
                context.SupportingTransitionReceiptSha256,
            BaseLedgerSha256 = context.BaseLedgerSha256,
            SettlementRequestSha256 = requestSha256,
            SettlementResultSha256 = resultSha256,
            SettledLedgerSha256 = settledLedgerSha256,
            BaseLedgerRevision = context.BaseLedger.Revision,
            SettledLedgerRevision = settled.Revision,
            ConsumedMaterialReservationId =
                context.ConsumedClaim.ReservationId,
            ConsumedQuantity = context.ConsumedClaim.Quantity,
            SupportingTransitionVerified =
                context.TransitionReceipt.SupportingTransitionVerified,
            ExactSettlementReplayVerified = !reasons.Contains(
                "support_settlement_exact_replay_mismatch",
                StringComparer.Ordinal),
            ReservationLifecycleVerified = verified,
            RouteTerminalCompletionRecorded = settled.History.Any(row =>
                row.CommitmentId == context.Request.SupportRequestId &&
                row.Operation == "reservation_portfolio_route_complete"),
            FreshReplanRequired = verified,
            TerminalReceiptEligible = false,
            FormalTrainingAuthorized = false,
            BlockingReasons = reasons
        };
    }

}
