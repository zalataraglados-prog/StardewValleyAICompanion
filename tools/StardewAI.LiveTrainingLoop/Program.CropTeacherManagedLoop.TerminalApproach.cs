using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl?> BuildCropTeacherOwnedJointTerminalApproachAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep completed, MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request, CropTeacherManagedSeedOwner owner)
    {
        if (!options.UseJointDailyCalendar || completed.Stage != CropTeacherRunStageKinds.Complete)
            throw new InvalidDataException("Joint terminal approach requires a completed original crop root.");
        owner.ValidateOriginalRouteExecutor(options);
        if (owner.Pending is not null || owner.HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the retained joint phase before terminal ingress.");
        var path = owner.CurrentSnapshotPath;
        var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(path))!.AsObject();
        if (HasPriorityStoryEventInterrupt(source))
            throw new InvalidDataException("Managed crop control interrupt requires its original verified owner boundary before another crop dispatch.");
        if (CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(source))
            return await BuildCropTeacherOwnedDialogueRecoveryQueueAsync(http, options, iteration, owner).ConfigureAwait(false);
        if (ReadSnapshotEnvelopeInt(source, "time", "total_days") != completed.TotalDay)
            throw new InvalidDataException("Joint terminal ingress changed its original completion day.");
        var location = source["state"]?["player"]?["location_id"];
        if (location?["status"]?.GetValue<string>() is not ("available" or "derived") ||
            string.IsNullOrWhiteSpace(location["value"]?.GetValue<string>()))
            throw new InvalidDataException("Joint terminal ingress has no readable original player location.");
        if (location["value"]!.GetValue<string>() == "Farm") return null;
        var step = completed with
        {
            Stage = CropTeacherRunStageKinds.RouteToFarm, TargetLocationId = "Farm", ActionBatch = null,
            Reason = "crop_teacher_joint_terminal_ingress"
        };
        var operation = await PrepareCropTeacherOwnedRouteCoreAsync(http, options, iteration, step, plan, request, owner,
            Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")),
            lateDayRecovery: false, terminalIngress: true).ConfigureAwait(false);
        var start = operation.OriginalStart!;
        return new(StandardResult(JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject(),
            operation.Record["original_model_plan_path"]!.GetValue<string>(), string.Empty, path, source,
            ReadString(source, "state_hash"), options, "crop_teacher_joint_terminal_ingress", "full"), operation);
    }
}
