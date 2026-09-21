using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSettlementBuilder
{
    private static SettlementContext Prepare(
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
        var bindingPath = Path.GetFullPath(executionBindingPath);
        var freshPath = Path.GetFullPath(freshTerminalReceiptPath);
        var afterPath = Path.GetFullPath(afterSnapshotPath);
        var binding = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBinding>(
            bindingPath,
            "Acquisition route execution binding");
        var expectedBinding = continuation is null
            ? AcquisitionRouteExecutionBindingBuilder.Build(inputs)
            : AcquisitionRouteExecutionBindingBuilder
                .BuildContinuation(
                    continuation,
                    continuationRequestPath,
                    inputs);
        Require(EqualJson(binding, expectedBinding),
            "Route execution binding drifted from deterministic source compilation.");
        var fresh = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteFreshTerminalReceiptAdmission>(
            freshPath,
            "Acquisition route fresh terminal receipt");
        var expectedFresh = continuation is null
            ? AcquisitionRouteFreshTerminalReceiptBuilder.Build(
                inputs,
                bindingPath,
                executionReceiptPath,
                afterPath,
                runId,
                executorVersion)
            : AcquisitionRouteFreshTerminalReceiptBuilder
                .BuildContinuation(
                    continuation,
                    continuationRequestPath,
                    inputs,
                    bindingPath,
                    executionReceiptPath,
                    afterPath,
                    runId,
                    executorVersion);
        Require(EqualJson(fresh, expectedFresh),
            "Fresh terminal receipt drifted from deterministic source compilation.");
        Require(binding.DispatchBindingReady &&
                binding.PortfolioReservationCommitVerified &&
                binding.PortfolioTeacherPreferenceVerified &&
                !binding.FormalTrainingAuthorized &&
                fresh.FreshTerminalReceiptVerified &&
                fresh.RouteTrainingEvidenceEligible &&
                !fresh.FormalTrainingAuthorized &&
                fresh.RouteOccurrenceId == binding.RouteOccurrenceId &&
                fresh.GoalId == binding.GoalId,
            "Route settlement source evidence is not verified.");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Acquisition route settlement after snapshot");
        using var afterDocument = JsonDocument.Parse(File.ReadAllText(afterPath));
        var baseLedgerPath = Path.GetFullPath(
            inputs.CommittedStrategyLedgerPath);
        var baseLedger = AcquisitionStrategyLedgerReader.Read(
            baseLedgerPath,
            afterDocument.RootElement).Ledger;
        Require(baseLedger.Revision ==
                    binding.CommittedStrategyLedgerRevision &&
                CurrentTeacherFrontierSupport.HashFile(baseLedgerPath) ==
                    binding.CommittedStrategyLedgerSha256,
            "Route settlement base ledger drifted from execution binding.");
        var sourceDecisionId =
            AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix +
            binding.RouteOccurrenceId;
        var reservations = baseLedger.MaterialReservations
            .Where(row => row.Status == StrategyCommitmentStatuses.Active &&
                row.SourceDecisionId == sourceDecisionId)
            .Select(row => (row.ReservationId, row.GoalId))
            .Concat(baseLedger.CurrencyReservations.Where(row =>
                    row.Status == StrategyCommitmentStatuses.Active &&
                    row.SourceDecisionId == sourceDecisionId)
                .Select(row => (row.ReservationId, row.GoalId)))
            .ToArray();
        Require(reservations.All(row => row.GoalId == binding.GoalId),
            "Route settlement active reservations have a different goal.");
        return new SettlementContext(
            binding,
            fresh,
            after,
            baseLedger,
            baseLedgerPath,
            sourceDecisionId,
            reservations.Select(row => row.ReservationId)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            CurrentTeacherFrontierSupport.HashFile(freshPath));
    }

    private static ReservationPortfolioRouteSettlementRequest CanonicalRequest(
        SettlementContext context) => new()
        {
            StateHash = context.AfterSnapshot.StateHash,
            ExpectedLedgerRevision = context.BaseLedger.Revision,
            PortfolioId = context.Binding.ReservationPortfolioId,
            GoalId = context.Binding.GoalId,
            RouteSourceDecisionId = context.RouteSourceDecisionId,
            FreshTerminalReceiptSha256 = context.FreshTerminalReceiptSha256,
            ReservationIds = context.ActiveReservationIds,
            Reason = SettlementReason
        };

    private sealed record SettlementContext(
        AcquisitionRouteExecutionBinding Binding,
        AcquisitionRouteFreshTerminalReceiptAdmission FreshTerminalReceipt,
        SnapshotEnvelope AfterSnapshot,
        StrategyCommitmentLedger BaseLedger,
        string BaseLedgerPath,
        string RouteSourceDecisionId,
        string[] ActiveReservationIds,
        string FreshTerminalReceiptSha256);

}
