using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedTransferRuntimeOptions(string BackendUrl, string EvidenceDirectory,
    string RunId, string ExecutorVersion);

public sealed class CropTeacherSeedTransferRuntime : ICropTeacherSeedRuntime, ICropTeacherSeedManagedRuntime
{
    private readonly CropTeacherSeedRuntimeTransaction transaction;

    private CropTeacherSeedTransferRuntime(CropTeacherSeedRuntimeTransaction transaction) => this.transaction = transaction;

    public static async Task<CropTeacherSeedTransferRuntime> PrepareAsync(HttpClient http,
        CropTeacherSeedTransferRuntimeOptions options, CropTeacherSeedTransferSupport scope,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write, Func<CropTeacherSeedTransferMarker, CropTeacherSeedTransferExecutionPlan, Task<string>> compileQueue,
        string? expectedLocationId = null)
    {
        var plan = scope.PlanExecution();
        if (expectedLocationId is not null && expectedLocationId != plan.LocationId)
            throw new InvalidDataException("Managed seed transfer route location differs from its original step.");
        CropTeacherSeedTransferMarker? marker = null;
        var binding = CropSeedRuntimeBindings.Transfer(scope, value => marker = value);
        return new(await CropTeacherSeedRuntimeTransaction.PrepareAsync(http,
            new(options.BackendUrl, options.EvidenceDirectory, options.RunId, options.ExecutorVersion), binding,
            commit, settle, write, () => compileQueue(marker!, plan)));
    }

    public void ValidateDispatchQueue(string path) => transaction.ValidateDispatchQueue(path);
    CropTeacherSeedPreparedBoundary ICropTeacherSeedManagedRuntime.ReadPreparedBoundary() => transaction.ReadPreparedBoundary();
    CropTeacherSeedLiveBoundary ICropTeacherSeedManagedRuntime.ReadVerifiedBoundary() => transaction.ReadVerifiedBoundary();
    public Task<StrategyLedgerEvidence> CompleteAsync(string queuePath, string receiptPath, string afterSnapshotPath, string selectedCandidateId) =>
        transaction.CompleteAsync(queuePath, receiptPath, afterSnapshotPath, selectedCandidateId);
    public Task<StrategyLedgerEvidence> CompleteAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition) => transaction.CompleteAsync(transition);
}
