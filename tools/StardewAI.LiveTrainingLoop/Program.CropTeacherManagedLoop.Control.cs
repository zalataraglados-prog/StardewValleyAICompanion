using System.Text.Json.Nodes;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record ManagedCropControl(CropTeacherControlQueueBuildResult Build, CropTeacherManagedSeedOperation? Operation);

    private static async Task<ManagedCropControl> BuildCropTeacherOwnedControlQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step, MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request, CropTeacherManagedSeedOwner owner)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        if (owner.Pending is not null || owner.HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the retained managed crop phase before compiling another step.");
        var beforePath = owner.CurrentSnapshotPath;
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(beforePath))!.AsObject();
        var hash = ReadString(before, "state_hash");
        if (HasPriorityStoryEventInterrupt(before))
            throw new InvalidDataException("Managed crop control interrupt requires its original verified owner boundary before another crop dispatch.");
        if (CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(before))
            return await BuildCropTeacherOwnedDialogueRecoveryQueueAsync(http, options, iteration, owner).ConfigureAwait(false);
        if (step.Stage != CropTeacherRunStageKinds.NativeSleep && GameClockBudgetPolicy.RecoveryWindowStarted(ReadInGameTime(before)))
            return await BuildCropTeacherOwnedLateDayQueueAsync(http, options, iteration, step, plan, request, owner).ConfigureAwait(false);
        if (step.Stage == CropTeacherRunStageKinds.PrepareSite)
        {
            var site = await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, plan, request,
                beforePath, before, hash).ConfigureAwait(false);
            if (!site.SitePreparationComplete)
                throw new InvalidDataException("Retain original site-preparation queue; its resource effects must be bound before managed dispatch.");
            return new(site, null);
        }
        var runtime = Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4"));
        var seed = step.Stage is CropTeacherRunStageKinds.AcquireSeed or CropTeacherRunStageKinds.TransferSeedSupply ||
            step.Stage == CropTeacherRunStageKinds.ExecuteCropBatch && step.ActionBatch?.OptionId == "executor.plant_seed";
        if (seed)
        {
            var operation = await owner.AdmitAsync(step, beforePath, owner.CurrentLedgerPath, runtime).ConfigureAwait(false);
            CropTeacherControlQueueBuildResult? build = null;
            await owner.PrepareSeedAsync<ICropTeacherSeedRuntime>(operation, async () =>
            {
                operation.Scopes.ValidateStep(step);
                build = step.Stage switch
                {
                    CropTeacherRunStageKinds.AcquireSeed => await BuildCropTeacherSeedQueueAsync(http, options, iteration, step,
                        beforePath, before, hash, operation.Scopes.Purchase!, runtime).ConfigureAwait(false),
                    CropTeacherRunStageKinds.TransferSeedSupply => await BuildCropTeacherSeedTransferQueueAsync(http, options, iteration,
                        step, beforePath, before, hash, operation.Scopes.Transfer!, runtime).ConfigureAwait(false),
                    _ => await BuildCropTeacherSeedPlantingQueueAsync(http, options, iteration, step, beforePath, before, hash,
                        operation.Scopes.Planting!, runtime).ConfigureAwait(false)
                };
                return (ICropTeacherSeedRuntime?)build.SeedPurchaseSupport ?? build.SeedTransferSupport ??
                    (ICropTeacherSeedRuntime?)build.SeedPlantingSupport ?? throw new InvalidDataException("Original seed preparation returned no owned runtime.");
            }).ConfigureAwait(false);
            return new(build! with
            {
                SnapshotProfile = "full",
                SnapshotUrl = SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true)
            }, operation);
        }
        var supporting = step.Stage switch
        {
            CropTeacherRunStageKinds.RouteHome or CropTeacherRunStageKinds.RouteToFarm or CropTeacherRunStageKinds.RouteToSeedSupply =>
                await PrepareCropTeacherOwnedRouteAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false),
            CropTeacherRunStageKinds.NativeSleep =>
                await PrepareCropTeacherOwnedOvernightAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false),
            CropTeacherRunStageKinds.ExecuteCropBatch when step.ActionBatch?.OptionId == "executor.water_crop" =>
                await PrepareCropTeacherOwnedWateringAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false),
            CropTeacherRunStageKinds.ExecuteCropBatch when step.ActionBatch?.OptionId == "executor.harvest_crop" =>
                await PrepareCropTeacherOwnedHarvestAsync(http, options, iteration, step, plan, request, owner, runtime).ConfigureAwait(false),
            _ => throw new InvalidDataException("Managed crop stage has no original owner adapter: " + step.Stage)
        };
        var start = supporting.OriginalStart!;
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        var model = supporting.Record[supporting.IsWateringMaintenance || supporting.IsHarvest
            ? "original_model_action_path" : "original_model_plan_path"]!.GetValue<string>();
        return new(StandardResult(queue, model, string.Empty, beforePath, before, hash, options), supporting);
    }
}
