using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedPlantingRuntimeOptions(string BackendUrl, string EvidenceDirectory,
    string RunId, string ExecutorVersion);

public sealed class CropTeacherSeedPlantingRuntime : ICropTeacherSeedRuntime, ICropTeacherSeedPrefixRuntime, ICropTeacherSeedManagedRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CropTeacherSeedRuntimeTransaction transaction;
    private readonly CropTeacherSeedPlantingPrefix prefix;
    private readonly string output;
    private readonly Func<string, string, Task> write;

    internal CropTeacherSeedPlantingRuntime(CropTeacherSeedRuntimeTransaction transaction, CropTeacherSeedPlantingPrefix prefix,
        string output, Func<string, string, Task> write)
    { this.transaction = transaction; this.prefix = prefix; this.output = output; this.write = write; }

    public static async Task<CropTeacherSeedPlantingRuntime> PrepareAsync(HttpClient http,
        CropTeacherSeedPlantingRuntimeOptions options, CropTeacherSeedPlantingSupport scope,
        MoneyRevenueCropNativeProductionActionBatch batch, string? reservationId,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write, Func<CropTeacherSeedPlantingMarker, MoneyRevenueCropNativeProductionActionBatch, Task<string>> compileQueue)
    {
        var bounded = scope.ValidateExecutionPreflight(batch, reservationId);
        CropTeacherSeedPlantingMarker? marker = null;
        var binding = CropSeedRuntimeBindings.Planting(scope, batch, bounded, reservationId, value => marker = value);
        var transaction = await CropTeacherSeedRuntimeTransaction.PrepareAsync(http,
            new(options.BackendUrl, options.EvidenceDirectory, options.RunId, options.ExecutorVersion), binding,
            commit, settle, write, () => compileQueue(marker!, bounded));
        var prefix = marker!.OpenPrefix(transaction.PreparedQueuePath, transaction.PreparedQueueSha256);
        return new(transaction, prefix, Path.GetFullPath(options.EvidenceDirectory), write);
    }

    public void ValidateDispatchQueue(string path) => transaction.ValidateDispatchQueue(path);
    CropTeacherSeedPreparedBoundary ICropTeacherSeedManagedRuntime.ReadPreparedBoundary() => transaction.ReadPreparedBoundary();
    CropTeacherSeedLiveBoundary ICropTeacherSeedManagedRuntime.ReadVerifiedBoundary() => transaction.ReadVerifiedBoundary();
    CropTeacherSeedNoDispatchBoundary ICropTeacherSeedManagedRuntime.ReadVerifiedStopBoundary() => transaction.ReadVerifiedStopBoundary();
    void ICropTeacherSeedManagedRuntime.BindExecutionGuard(Action guard) => transaction.BindExecutionGuard(guard);

    public async Task ValidateBeforeDispatchAsync(int index, JsonObject item, JsonObject current, string currentSnapshotPath)
    {
        transaction.RequireExecutionLease();
        // This read is explicitly the locked original authority, never a stale hash presented as current native state.
        var authority = await transaction.ReadOriginalAuthorityAsync();
        prefix.ValidateBeforeDispatch(index, item.Deserialize<ActionQueueItem>(JsonOptions)!,
            current.Deserialize<SnapshotEnvelope>(JsonOptions)!, authority.Ledger);
        await write(Path.Combine(output, "prefix-dispatch-" + index.ToString("D2") + ".json"), JsonSerializer.Serialize(new
        {
            schema_version = "crop_teacher_seed_prefix_dispatch.v1", index, current_snapshot_path = currentSnapshotPath,
            native_state_hash = prefix.CurrentStateHash, authority_ledger_path = authority.Path,
            authority_state_hash = authority.Ledger.SourceStateHash, verified_prefix_count = prefix.VerifiedCount,
            native_request_performed = false, formal_product_training_authorized = false
        }, JsonOptions));
    }

    public Task ObserveNativeStepAsync(JsonObject execution, string afterSnapshotPath)
    {
        prefix.ObserveNativeStep(execution.Deserialize<QueueExecutionStepReceipt>(JsonOptions)!, afterSnapshotPath);
        return Task.CompletedTask;
    }

    public Task RecordControllerStopAsync(int index, string currentSnapshotPath, string reason) =>
        write(Path.Combine(output, "prefix-controller-stop.json"), JsonSerializer.Serialize(new
        {
            schema_version = "crop_teacher_seed_prefix_controller_stop.v1", index, current_snapshot_path = currentSnapshotPath,
            reason, verified_prefix_count = prefix.VerifiedCount, last_verified_state_hash = prefix.CurrentStateHash,
            next_native_request_performed = false, formal_product_training_authorized = false
        }, JsonOptions));

    public Task<StrategyLedgerEvidence> CompleteAsync(string queuePath, string receiptPath, string afterSnapshotPath, string selectedCandidateId) =>
        CompleteAsync(transaction.CreateTransition(queuePath, receiptPath, afterSnapshotPath, selectedCandidateId));

    public async Task<StrategyLedgerEvidence> CompleteAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition)
    {
        transaction.ValidateTransitionIdentity(transition);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(await File.ReadAllTextAsync(transition.ExecutionReceiptPath), JsonOptions)
            ?? throw new InvalidDataException("Owned planting runtime receipt is empty.");
        if (receipt.ExecutedItemCount == 0 && receipt.StepResults.Length == 0 && prefix.VerifiedCount == 0)
            return await transaction.CompleteWithoutNativeDispatchAsync(transition);
        if (prefix.VerifiedCount == 0 || receipt.StepResults.Count(row => row.Status == "applied" && row.PrimitiveVerificationStatus == "verified") != prefix.VerifiedCount)
            throw new InvalidDataException("Owned planting runtime receipt differs from its observed native prefix.");
        prefix.SealObservedReceipt(receipt);
        return await transaction.CompleteAsync(transition);
    }
}
