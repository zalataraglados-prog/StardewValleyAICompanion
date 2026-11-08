using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

internal sealed record CropTeacherSeedPreparedBoundary(string RuntimeDirectory, string BackendUrl, string RunId, string ExecutorVersion,
    string SupportStepId, string CommitmentId, IReadOnlyDictionary<string, string> InputHashes,
    ReservationPortfolioCommitRequest PreparationRequest, System.Text.Json.Nodes.JsonObject? PreparationDetails,
    string OriginalIntentSha256, string QueuePath, string QueueSha256, Action Validate,
    CropSeedPreparationReference? PreparationReference = null);

internal sealed record CropTeacherSeedLiveBoundary(CropTeacherSeedPreparedBoundary Preparation,
    StrategyLedgerEvidence Evidence, string AfterSnapshotPath, Action Validate);

internal sealed record CropTeacherSeedNoDispatchBoundary(CropTeacherSeedPreparedBoundary Preparation,
    StrategyLedgerEvidence Evidence, string SnapshotPath, string TransitionPath, string ReceiptPath, Action Validate);

internal interface ICropTeacherSeedManagedRuntime
{
    CropTeacherSeedPreparedBoundary ReadPreparedBoundary();
    CropTeacherSeedLiveBoundary ReadVerifiedBoundary();
    CropTeacherSeedNoDispatchBoundary ReadVerifiedStopBoundary();
    void BindExecutionGuard(Action guard);
}
