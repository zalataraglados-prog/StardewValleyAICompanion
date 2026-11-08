using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherManagedSeedOperation> PrepareCropTeacherOwnedRouteAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan runPlan,
        MoneyRevenueCropNativeProductionRunPlanRequest runRequest, CropTeacherManagedSeedOwner owner, string runtimeDirectory)
    {
        owner.ValidateOriginalRouteExecutor(options);
        var kind = step.Stage switch
        {
            CropTeacherRunStageKinds.RouteToFarm => "supporting_route_to_crop",
            CropTeacherRunStageKinds.RouteHome => "supporting_route_to_sleep",
            _ => throw new InvalidDataException("Owned crop ingress cannot replace procurement/transfer navigation or a non-route stage.")
        };
        return await owner.CompileAndAdmitOriginalRouteAsync(async () =>
        {
            var sourcePath = owner.CurrentSnapshotPath;
            var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
            var stateHash = ReadString(source, "state_hash");
            if (step.TotalDay != ReadSnapshotEnvelopeInt(source, "time", "total_days"))
                throw new InvalidDataException("Owned crop route step changed the accepted source day.");
            var prefix = CropTeacherArtifactPrefix(options, iteration, "route");
            var receiptPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { prefix + "-model-plan.json", prefix + "-compiled-queue.json", receiptPath, afterPath }
                .Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned crop route needs new artifact slots; retain/recover existing compilation instead of overwriting it.");
            var compiled = await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, runPlan, runRequest,
                sourcePath, source, stateHash).ConfigureAwait(false);
            if (compiled.InterruptKind.Length != 0 || compiled.ExecutionStepOverride is not null || compiled.SitePreparationComplete ||
                compiled.StateHash != stateHash || !string.Equals(Path.GetFullPath(compiled.BeforeSnapshotPath), Path.GetFullPath(sourcePath),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("Original crop control selected an interrupt, not the admitted ingress route.");
            return new(kind, compiled.ModelPlanPath, prefix + "-compiled-queue.json", receiptPath, afterPath, runtimeDirectory);
        }).ConfigureAwait(false);
    }

    private static async Task ExecuteCropTeacherOwnedRouteAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation)
    {
        owner.ValidateOriginalRouteExecutor(options);
        var start = operation.RouteStart ?? throw new InvalidOperationException("Owned crop route has no original compilation.");
        var transition = operation.Record["route_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(transition.ExecutionReceiptPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json")), comparison) ||
            !string.Equals(Path.GetFullPath(transition.AfterSnapshotPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json")), comparison))
            throw new InvalidDataException("Owned crop route executor output slot changed before dispatch.");
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.SourcePath))!.AsObject();
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        await owner.ExecuteOriginalRouteAsync(operation, http, () => ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp,
            options, iteration, start.SourcePath, before, queue, ReadString(before, "state_hash"), ReadString(queue, "queue_id"),
            operation.Record["original_model_plan_path"]!.GetValue<string>(), string.Empty, start.QueuePath, start.SourcePath,
            ReadString(before, "state_hash"), start.CandidateId, -1, start.QueuePath, null, new List<JsonObject>(),
            useDailyPlanDecisionLease: false,
            executionSnapshotUrlOverride: SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true),
            executionSnapshotProfileOverride: "full")).ConfigureAwait(false);
        await owner.RunSupportingRouteAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
    }
}
