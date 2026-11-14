using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static void RequireSharedSourceDispatch(CropTeacherManagedSeedOwner owner,
        CropTeacherManagedSeedOperation operation, string queuePath)
    {
        if (!ReferenceEquals(operation.Owner, owner) || !ReferenceEquals(owner.Pending, operation) ||
            operation.SharedSourceStart is not { } start ||
            !SameSharedPath(start.QueuePath, queuePath) || !SameSharedPath(start.SourcePath, owner.CurrentSnapshotPath))
            throw new InvalidOperationException("Global selected work requires its exact pending source yield, not crop dispatch authority.");
        start.ValidateInputsUnchanged();
    }

    private static async Task<JsonObject> ExecuteCropTeacherSharedOperationAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, string queuePath,
        Func<Task<JsonObject>> executeOriginal)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        RequireSharedSourceDispatch(owner, operation, queuePath);
        var claim = await owner.ClaimSharedSourceExecutionAsync(operation, http).ConfigureAwait(false);
        var execution = await executeOriginal().ConfigureAwait(false);
        if (!SameSharedPath(ReadString(execution, "execution_path"), claim.Transition.ExecutionReceiptPath) ||
            !SameSharedPath(ReadString(execution, "after_snapshot_path"), claim.Transition.AfterSnapshotPath))
            throw new InvalidDataException("Selected global executor returned different original output paths.");
        await owner.VerifySharedSourceExecutionAsync(operation).ConfigureAwait(false);
        // Reuse the original source ingest after bounded native polling may have evicted the old head.
        var retained = new CropSeedRuntimeArtifacts(operation.SupportingTransition!.InputHashes);
        await IngestCropTeacherManagedSnapshotAsync(http, options, claim.Transition.BeforeSnapshotPath, retained).ConfigureAwait(false);
        await IngestCropTeacherManagedSnapshotAsync(http, options, claim.Transition.AfterSnapshotPath, retained).ConfigureAwait(false);
        await owner.RunSharedSourceReturnAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        return execution;
    }

    private static CropTeacherDailySelection CompleteCropTeacherSharedSelection(CropTeacherManagedSeedOwner owner,
        CropTeacherManagedSeedOperation operation, CropTeacherDailySelection selection)
    {
        if (owner.Pending is not null || operation.SharedSourceStart is null || operation.SharedSourceReturn is not { } returned)
            throw new InvalidOperationException("Retain selected work until its original source return is accepted.");
        returned.ValidateInputsUnchanged();
        var bound = operation.Record["shared_source_selection"]!.Deserialize<CropTeacherSharedSourceSelection>(JsonOptions)!;
        var candidate = selection.Lease.CandidateAt(bound.SelectedQueueIndex);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(ContentAddressedJsonArtifactReader.ReadAllText(
            CropTeacherManagedSeedOwner.OriginalSupportingTransition(operation).ExecutionReceiptPath), JsonOptions)!;
        if (!SameSharedPath(selection.SourceSnapshotPath, bound.DecisionSnapshotPath) ||
            !SameSharedPath(selection.PlanPath, bound.DecisionPlanPath) || !SameSharedPath(selection.RankingPath, bound.DecisionRankingPath) ||
            !SameSharedPath(owner.CurrentSnapshotPath, returned.AfterSnapshotPath) ||
            selection.OwnedCandidateIds.Contains(candidate.CandidateId) || !receipt.SelectedCandidateCompleted ||
            receipt.SelectedCandidateId != operation.SharedSourceStart.CandidateId)
            throw new InvalidDataException("Shared native completion does not retire this original selected decision.");
        var completed = new HashSet<string>(selection.CompletedForeignCandidateIds, StringComparer.Ordinal) { candidate.CandidateId };
        return selection with { CompletedForeignCandidateIds = completed };
    }

    private static bool SameSharedPath(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
