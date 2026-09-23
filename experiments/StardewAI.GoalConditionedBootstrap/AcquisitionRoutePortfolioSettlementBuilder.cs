using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    private const string SettlementReason =
        "verified_fresh_terminal_route_completion";

    public static ReservationPortfolioRouteSettlementRequest BuildRequest(
        AcquisitionRouteExecutionBindingInputs inputs,
        string executionBindingPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string freshTerminalReceiptPath,
        string runId,
        string executorVersion) => BuildRequestCore(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            null,
            string.Empty);

    private static ReservationPortfolioRouteSettlementRequest
        BuildRequestCore(
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            AcquisitionRoutePortfolioVerifiedCheckpoint? continuation,
            string continuationRequestPath)
    {
        var context = Prepare(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            continuation,
            continuationRequestPath);
        return CanonicalRequest(context);
    }

    public static AcquisitionRoutePortfolioSettlementReceipt BuildReceipt(
        AcquisitionRouteExecutionBindingInputs inputs,
        string executionBindingPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string freshTerminalReceiptPath,
        string runId,
        string executorVersion,
        string settlementRequestPath,
        string settlementResultPath,
        string settledLedgerPath) => BuildReceiptCore(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            settlementRequestPath,
            settlementResultPath,
            settledLedgerPath,
            null,
            string.Empty);

    private static AcquisitionRoutePortfolioSettlementReceipt
        BuildReceiptCore(
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            AcquisitionRoutePortfolioVerifiedCheckpoint? continuation,
            string continuationRequestPath)
    {
        var context = Prepare(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            continuation,
            continuationRequestPath);
        var requestPath = Path.GetFullPath(settlementRequestPath);
        var resultPath = Path.GetFullPath(settlementResultPath);
        var ledgerPath = Path.GetFullPath(settledLedgerPath);
        var request = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioRouteSettlementRequest>(
            requestPath,
            "Acquisition route portfolio settlement request");
        Require(EqualJson(request, CanonicalRequest(context)),
            "Route settlement request drifted from deterministic source compilation.");
        var settlement = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioRouteSettlementResult>(
            resultPath,
            "Acquisition route portfolio settlement result");
        using var afterDocument = JsonDocument.Parse(
            File.ReadAllText(Path.GetFullPath(afterSnapshotPath)));
        var settled = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            afterDocument.RootElement).Ledger;
        var receipt = BaseReceipt(
            context,
            executionBindingPath,
            requestPath,
            resultPath,
            ledgerPath);
        var reasons = ValidateSettlement(
            context,
            request,
            settlement,
            settled);
        var exactReplay = !reasons.Contains(
            "route_settlement_exact_replay_mismatch",
            StringComparer.Ordinal) &&
            !reasons.Contains(
                "route_settlement_marker_invalid",
                StringComparer.Ordinal);
        var lifecycle = reasons.Length == 0;
        receipt.SettledLedgerRevision = settled.Revision;
        receipt.CompletedReservationIds = request.ReservationIds
            .Order(StringComparer.Ordinal)
            .ToArray();
        receipt.ExactSettlementReplayVerified = exactReplay;
        receipt.ReservationLifecycleVerified = lifecycle;
        receipt.FreshReplanRequired = lifecycle;
        receipt.Status = lifecycle
            ? "verified_route_reservation_settlement"
            : "blocked_route_reservation_settlement";
        receipt.BlockingReasons = reasons;
        return receipt;
    }

    private static AcquisitionRoutePortfolioSettlementReceipt BaseReceipt(
        SettlementContext context,
        string executionBindingPath,
        string settlementRequestPath,
        string settlementResultPath,
        string settledLedgerPath) => new()
        {
            GoalId = context.Binding.GoalId,
            PortfolioId = context.Binding.ReservationPortfolioId,
            RouteOccurrenceId = context.Binding.RouteOccurrenceId,
            RouteSourceDecisionId = context.RouteSourceDecisionId,
            AfterStateHash = context.AfterSnapshot.StateHash,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    context.Binding.CommunityCenterProvenance),
            ExecutionBindingSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(executionBindingPath)),
            FreshTerminalReceiptSha256 =
                context.FreshTerminalReceiptSha256,
            BaseLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    context.BaseLedgerPath),
            SettlementRequestSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settlementRequestPath)),
            SettlementResultSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settlementResultPath)),
            SettledLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settledLedgerPath)),
            BaseLedgerRevision = context.BaseLedger.Revision,
            FreshTerminalReceiptVerified = true,
            FormalTrainingAuthorized = false
        };

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

}
