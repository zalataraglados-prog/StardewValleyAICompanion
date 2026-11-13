using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> BuildCropTeacherOwnedSiteTillAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan plan,
        CropTeacherManagedSeedOwner owner, string beforePath, JsonObject before, string hash, CropTeacherControlQueueBuildResult site)
    {
        JsonObject? queue = null; string? modelPath = null;
        var operation = await owner.CompileAndAdmitOriginalTillingAsync(async () =>
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(beforePath), Path.GetFullPath(owner.CurrentSnapshotPath), comparer))
                throw new InvalidDataException("Selected till no longer continues its original accepted source.");
            var originalPrefix = CropTeacherArtifactPrefix(options, iteration, "site");
            var requestPath = originalPrefix + "-request.json";
            var planPath = originalPrefix + "-plan.json";
            var prefix = CropTeacherArtifactPrefix(options, iteration, "owned-site-till");
            modelPath = prefix + "-model-action.json";
            var queuePath = prefix + "-compiled-queue.json";
            var receiptPath = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { modelPath, queuePath, receiptPath, afterPath }.Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Selected till needs new artifact slots; retain/recover original inputs.");
            var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
            foreach (var path in new[] { beforePath, owner.CurrentLedgerPath, plan.RequestPath,
                         site.BeforeSnapshotPath, requestPath, planPath, site.ModelPlanPath }) artifacts.Pin(path);
            var original = MoneyRevenueCropSitePreparationPlanBuilder.Build(requestPath);
            var selected = MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(original);
            if (selected is not { PlanStatus: "direct_till_ready", ActionOptionId: "executor.till_soil" })
                throw new InvalidDataException("Selected till cannot skip the original first clearance/frontier.");
            var parameters = selected.Parameters.Concat(new[] { Parameter("precondition", "candidate_id:" + selected.CandidateId) }).ToArray();
            var model = BuildCropTeacherActionModel(options, hash, "authoritative_crop_site_preparation_plan.v1",
                selected.CandidateId, selected.ActionOptionId, parameters);
            await IngestCropTeacherManagedSnapshotAsync(http, options, beforePath, artifacts).ConfigureAwait(false);
            await WriteJsonAsync(modelPath, model).ConfigureAwait(false);
            queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile",
                model.ToJsonString(JsonOptions)).ConfigureAwait(false);
            await WriteJsonAsync(queuePath, queue).ConfigureAwait(false);
            RequireCompiledQueue(queue, new[] { "executor.till_soil" }, 1, "crop_teacher_owned_site_till_compile_invalid");
            artifacts.Validate();
            return new CropTeacherOriginalTillingCompilation(step.CommitmentId, site.BeforeSnapshotPath,
                requestPath, planPath, site.ModelPlanPath, modelPath, queuePath, receiptPath, afterPath,
                Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")));
        }).ConfigureAwait(false);
        return new(StandardResult(queue!, modelPath!, string.Empty, beforePath, before, hash, options,
            snapshotProfile: "full"), operation);
    }
}
