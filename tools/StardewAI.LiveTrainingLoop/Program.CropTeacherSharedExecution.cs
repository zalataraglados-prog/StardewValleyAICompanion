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
        await CompleteCropTeacherSharedSourceReturnAsync(http, options, owner, operation).ConfigureAwait(false);
        return execution;
    }

    private static async Task CompleteCropTeacherSharedSourceReturnAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation)
    {
        var transition = CropTeacherManagedSeedOwner.OriginalSupportingTransition(operation);
        var cookingSettlement = operation.SharedSourceStart!.RequiresConsumptionSettlement;
        var transferSettlement = operation.SharedSourceStart.RequiresTransferSettlement || cookingSettlement;
        if (transferSettlement)
        {
            _ = cookingSettlement ? operation.SharedSourceStart.ReadConsumptionSettlementRequest() : operation.SharedSourceStart.ReadTransferSettlementRequest();
            var transferInputs = new CropSeedRuntimeArtifacts(operation.SharedSourceStart.InputHashes);
            await IngestCropTeacherManagedSnapshotAsync(http, options, transition.BeforeSnapshotPath, transferInputs).ConfigureAwait(false);
            await IngestCropTeacherManagedSnapshotAsync(http, options, transition.AfterSnapshotPath, transferInputs).ConfigureAwait(false);
            if (cookingSettlement)
                await owner.SettleSharedCookingExecutionAsync(operation, http,
                    (diagnostic, request) => PostJournaledPortfolioSettlementAsync(http, options, diagnostic, request)).ConfigureAwait(false);
            else
                await owner.SettleSharedTransferExecutionAsync(operation, http,
                    (diagnostic, request) => PostJournaledPortfolioSettlementAsync(http, options, diagnostic, request)).ConfigureAwait(false);
        }
        await owner.VerifySharedSourceExecutionAsync(operation).ConfigureAwait(false);
        // Reuse the original source ingest after bounded native polling may have evicted the old head.
        if (!transferSettlement)
        {
            var retained = new CropSeedRuntimeArtifacts(operation.SupportingTransition!.InputHashes);
            await IngestCropTeacherManagedSnapshotAsync(http, options, transition.BeforeSnapshotPath, retained).ConfigureAwait(false);
            await IngestCropTeacherManagedSnapshotAsync(http, options, transition.AfterSnapshotPath, retained).ConfigureAwait(false);
        }
        await owner.RunSharedSourceReturnAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
    }

    private static CropTeacherDailySelection CompleteCropTeacherSharedSelection(CropTeacherManagedSeedOwner owner,
        CropTeacherManagedSeedOperation operation, CropTeacherDailySelection selection)
    {
        if (owner.Pending is not null || operation.SharedSourceStart is null || operation.SharedSourceReturn is not { } returned)
            throw new InvalidOperationException("Retain selected work until its original source return is accepted.");
        returned.ValidateInputsUnchanged();
        var bound = owner.ReadSharedSourceExecutionSelection(operation);
        var candidate = selection.Lease.CandidateAt(bound.SelectedQueueIndex);
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(ContentAddressedJsonArtifactReader.ReadAllText(
            CropTeacherManagedSeedOwner.OriginalSupportingTransition(operation).ExecutionReceiptPath), JsonOptions)!;
        if (!SameSharedPath(selection.SourceSnapshotPath, bound.DecisionSnapshotPath) ||
            !SameSharedPath(selection.PlanPath, bound.DecisionPlanPath) || !SameSharedPath(selection.RankingPath, bound.DecisionRankingPath) ||
            !SameSharedPath(owner.CurrentSnapshotPath, returned.AfterSnapshotPath) ||
            selection.OwnedCandidateIds.Contains(candidate.CandidateId) ||
            receipt.SelectedCandidateId != operation.SharedSourceStart.CandidateId)
            throw new InvalidDataException("Shared native completion does not retire this original selected decision.");
        if (!receipt.SelectedCandidateCompleted && !operation.NativeSharedObjectiveCompleted)
        {
            if (!operation.SharedSourceStart.AppliedNativeWorkQueue || candidate.ObjectiveContinuation is null)
                throw new InvalidDataException("Only applied native work may retain its original pending objective.");
            return selection;
        }
        var completed = new HashSet<string>(selection.CompletedForeignCandidateIds, StringComparer.Ordinal) { candidate.CandidateId };
        return selection with { CompletedForeignCandidateIds = completed };
    }

    private static bool SameSharedPath(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
