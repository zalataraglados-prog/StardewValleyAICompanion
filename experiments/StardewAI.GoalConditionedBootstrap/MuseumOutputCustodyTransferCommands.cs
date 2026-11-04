using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MuseumOutputCustodyTransferCommands
{
    internal static void Prepare(Arguments options, Action<string, object> write)
    {
        var snapshot = AcquisitionReadOnlySnapshotProjection.Read(options.Required("snapshot"));
        var ledger = AcquisitionStrategyLedgerReader.Read(options.Required("strategy-ledger"), snapshot).Ledger;
        var result = MuseumOutputCustodyTransferRequestBuilder.Build(snapshot, ledger,
            options.Required("reservation-id"), options.Required("destination-node"));
        write(options.Required("output"), new
        {
            schema_version = "museum_output_custody_transfer_preparation.v1", ready = result.Errors.Length == 0, result.Request, result.Candidate,
            blocking_reasons = result.Errors, execution_performed = false, formal_training_authorized = false
        });
        if (result.Request is not null && options.Optional("commit-request-output") is { } requestOutput)
            write(requestOutput, result.Request);
        if (result.Errors.Length > 0) Environment.ExitCode = 2;
    }

    internal static void Compile(Arguments options, Action<string, object> write)
    {
        var snapshot = AcquisitionReadOnlySnapshotProjection.Read(options.Required("snapshot"));
        var before = AcquisitionStrategyLedgerReader.Read(options.Required("strategy-ledger"), snapshot).Ledger;
        var committed = CurrentTeacherFrontierSupport.Read<ReservationPortfolioCommitResult>(
            options.Required("commit-result"), "Output custody transfer commit result");
        var result = MuseumOutputCustodyTransferRequestBuilder.Compile(snapshot, before,
            options.Required("reservation-id"), options.Required("destination-node"), committed);
        write(options.Required("output"), new
        {
            schema_version = "museum_output_custody_transfer_compilation.v1", ready = result.Errors.Length == 0, result.Plan, result.Queue,
            blocking_reasons = result.Errors, execution_performed = false, formal_training_authorized = false
        });
        if (result.Queue is not null && options.Optional("selected-queue-output") is { } queueOutput) write(queueOutput, result.Queue);
        if (result.Plan is not null && options.Optional("selected-plan-output") is { } planOutput) write(planOutput, result.Plan);
        if (result.Errors.Length > 0) Environment.ExitCode = 2;
    }

    internal static void Settle(Arguments options, Action<string, object> write)
    {
        var before = AcquisitionReadOnlySnapshotProjection.Read(options.Required("before-snapshot"));
        var after = AcquisitionReadOnlySnapshotProjection.Read(options.Required("after-snapshot"));
        var ledger = AcquisitionStrategyLedgerReader.Read(options.Required("strategy-ledger"), before).Ledger;
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(options.Required("queue"), "Output custody transfer queue");
        var item = CurrentTeacherFrontierSupport.Read<ActionQueueItem>(options.Required("dispatched-item"), "Actual dispatched output custody transfer");
        var receipt = CurrentTeacherFrontierSupport.Read<QueueExecutionStepReceipt>(options.Required("receipt"), "Output custody native receipt");
        var result = MuseumOutputCustodyTransferSettlementRequestBuilder.Build(queue, item, receipt, before, after, ledger);
        var errors = result.Errors.ToList();
        var resultPath = options.Optional("settlement-result");
        if (resultPath is not null && result.Request is not null)
            errors.AddRange(MuseumOutputCustodyTransferSettlementRequestBuilder.Verify(queue, item, receipt, before, after, ledger,
                CurrentTeacherFrontierSupport.Read<ReservationPortfolioSupportingTransitionSettlementResult>(resultPath, "Output custody transfer settlement result")));
        write(options.Required("output"), new
        {
            schema_version = "museum_output_custody_transfer_settlement.v1", ready = errors.Count == 0,
            request = errors.Count == 0 ? result.Request : null,
            settlement_verified = resultPath is not null && errors.Count == 0,
            blocking_reasons = errors, execution_performed = false, formal_training_authorized = false
        });
        if (errors.Count == 0 && result.Request is not null && options.Optional("settlement-request-output") is { } requestOutput) write(requestOutput, result.Request);
        if (errors.Count > 0) Environment.ExitCode = 2;
    }
}
