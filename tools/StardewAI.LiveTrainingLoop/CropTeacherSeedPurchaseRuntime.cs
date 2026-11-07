using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedPurchaseRuntimeOptions(string BackendUrl, string EvidenceDirectory,
    string RunId, string ExecutorVersion);

public sealed class CropTeacherSeedPurchaseRuntime : ICropTeacherSeedRuntime, ICropTeacherSeedManagedRuntime
{
    private readonly CropTeacherSeedRuntimeTransaction transaction;

    private CropTeacherSeedPurchaseRuntime(CropTeacherSeedRuntimeTransaction transaction) => this.transaction = transaction;

    public static async Task<CropTeacherSeedPurchaseRuntime> PrepareAsync(HttpClient http,
        CropTeacherSeedPurchaseRuntimeOptions options, CropTeacherSeedPurchaseSupport scope,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write, Func<Task<string>> compileQueue) => new(await CropTeacherSeedRuntimeTransaction.PrepareAsync(http,
            new(options.BackendUrl, options.EvidenceDirectory, options.RunId, options.ExecutorVersion),
            CropSeedRuntimeBindings.Purchase(scope), commit, settle, write, compileQueue));

    public void ValidateDispatchQueue(string path) => transaction.ValidateDispatchQueue(path);
    CropTeacherSeedPreparedBoundary ICropTeacherSeedManagedRuntime.ReadPreparedBoundary() => transaction.ReadPreparedBoundary();
    CropTeacherSeedLiveBoundary ICropTeacherSeedManagedRuntime.ReadVerifiedBoundary() => transaction.ReadVerifiedBoundary();
    public Task<StrategyLedgerEvidence> CompleteAsync(string queuePath, string receiptPath, string afterSnapshotPath, string selectedCandidateId) =>
        transaction.CompleteAsync(queuePath, receiptPath, afterSnapshotPath, selectedCandidateId);
    public Task<StrategyLedgerEvidence> CompleteAsync(MoneyRevenueCropNativeLifecycleTransitionRequest transition) => transaction.CompleteAsync(transition);
}
