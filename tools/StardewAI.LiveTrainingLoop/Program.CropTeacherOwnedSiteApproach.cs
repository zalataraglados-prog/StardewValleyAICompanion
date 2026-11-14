using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> BuildCropTeacherOwnedSiteQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request, CropTeacherManagedSeedOwner owner,
        string beforePath, JsonObject before, string hash)
    {
        var site = await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, plan, request,
            beforePath, before, hash).ConfigureAwait(false);
        if (site.SitePreparationComplete) return new(site, null);
        var observedModel = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(site.ModelPlanPath))!.AsObject();
        if (site.Queue is not null && ReadString(observedModel, "source_model") == "authoritative_crop_site_preparation_plan.v1" &&
            ExecutableQueueItems(site.Queue) is [var clear] && ReadString(clear, "option_id") == "executor.clear_obstacle")
            return await BuildCropTeacherOwnedSiteClearanceAsync(http, options, iteration, step, plan, owner,
                beforePath, before, hash, site).ConfigureAwait(false);
        if (site.Queue is not null && ReadString(observedModel, "source_model") == "authoritative_crop_site_preparation_plan.v1" &&
            ExecutableQueueItems(site.Queue) is [var till] && ReadString(till, "option_id") == "executor.till_soil")
            return await BuildCropTeacherOwnedSiteTillAsync(http, options, iteration, step, plan, owner,
                beforePath, before, hash, site).ConfigureAwait(false);
        if (site.Queue is null || ReadString(observedModel, "source_model") != "authoritative_crop_site_approach.v1")
            throw new InvalidDataException("Retain original site-preparation queue; its resource effects must be bound before managed dispatch.");
        JsonObject? queue = null;
        string? modelPath = null;
        var operation = await owner.CompileAndAdmitOriginalRouteAsync(async () =>
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(beforePath), Path.GetFullPath(owner.CurrentSnapshotPath), comparison))
                throw new InvalidDataException("Site approach no longer continues its original accepted source.");
            var prefix = CropTeacherArtifactPrefix(options, iteration, "owned-site-approach");
            modelPath = prefix + "-model-plan.json";
            var queuePath = prefix + "-compiled-queue.json";
            var receiptPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { modelPath, queuePath, receiptPath, afterPath }.Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned site approach needs new artifact slots; retain/recover original inputs.");
            var scope = CropTeacherSiteApproachObservation.Open(plan.RequestPath, beforePath, owner.CurrentLedgerPath,
                plan.CoveredCommitmentIds, step.CommitmentId, "site-approach-compile:" + options.RunId + ":" + iteration,
                site.BeforeSnapshotPath);
            if (scope.SourceStateHash != hash || scope.Target is not { } target || scope.AlreadyAtStand)
                throw new InvalidDataException("Original site approach did not select a new stand from the accepted full source.");
            var candidate = $"crop-site-approach:{iteration}:{target.TileX},{target.TileY}";
            var actor = JsonSerializer.SerializeToNode(options.TargetActor, JsonOptions)!;
            var originalRecipe = CropBoundedApproachPlanBuilder.Build(scope.ObservationStateHash, candidate,
                "authoritative_crop_site_approach.v1", "site_preparation_starts_only_after_approach",
                target.TileX, target.TileY, options.TargetExecutionMode, actor);
            if (!JsonNode.DeepEquals(originalRecipe, observedModel))
                throw new InvalidDataException("Original site model differs from its pinned observation/stand/recipe.");
            var artifacts = new CropSeedRuntimeArtifacts(scope.InputHashes);
            artifacts.Pin(site.ModelPlanPath);
            await IngestCropTeacherManagedSnapshotAsync(http, options, beforePath, artifacts).ConfigureAwait(false);
            var model = CropBoundedApproachPlanBuilder.Build(hash, candidate, "authoritative_crop_site_approach.v1",
                "site_preparation_starts_only_after_approach", target.TileX, target.TileY, options.TargetExecutionMode, actor);
            await WriteJsonAsync(modelPath, model).ConfigureAwait(false);
            queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/plan/action-queue/compile",
                model.ToJsonString(JsonOptions)).ConfigureAwait(false);
            await WriteJsonAsync(queuePath, queue).ConfigureAwait(false);
            RequireCropTeacherBoundedApproachQueue(queue, "crop_teacher_owned_site_approach_compile_invalid");
            scope.ValidateInputsUnchanged(); artifacts.Validate();
            return new CropTeacherOriginalRouteCompilation("supporting_route_to_crop", modelPath, queuePath,
                receiptPath, afterPath, Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")))
            { SiteApproachObservationPath = site.BeforeSnapshotPath, SiteApproachCommitmentId = step.CommitmentId,
                SiteApproachNativeActionEffects = options.UseProductExecutor };
        }).ConfigureAwait(false);
        return new(StandardResult(queue!, modelPath!, string.Empty, beforePath, before, hash, options,
            snapshotProfile: "full"), operation);
    }
}
