using System.Text.Json.Nodes;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> BuildCropTeacherOwnedLateDayQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep interruptedStep,
        MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        CropTeacherManagedSeedOwner owner)
    {
        var sourcePath = owner.CurrentSnapshotPath;
        var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
        if (!GameClockBudgetPolicy.RecoveryWindowStarted(ReadInGameTime(source)) || HasPriorityStoryEventInterrupt(source) ||
            CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(source))
            throw new InvalidDataException("Owned late-day recovery must follow the original clock and higher-priority controls.");
        var homeField = source["state"]?["current_location"]?["home_context"];
        var home = homeField?["value"]?["home_location_id"]?.GetValue<string>();
        if (homeField?["status"]?.GetValue<string>() is not ("available" or "derived") || string.IsNullOrWhiteSpace(home))
            throw new InvalidDataException("Owned late-day recovery has no readable original home location.");
        var current = source["state"]?["player"]?["location_id"]?["value"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(current) || interruptedStep.TotalDay != ReadSnapshotEnvelopeInt(source, "time", "total_days"))
            throw new InvalidDataException("Owned late-day recovery changed the accepted source location/day.");
        var effective = interruptedStep with
        {
            Stage = current == home ? CropTeacherRunStageKinds.NativeSleep : CropTeacherRunStageKinds.RouteHome,
            CommitmentId = string.Empty, TargetLocationId = home, HomeLocationId = home,
            SeedQualifiedItemId = string.Empty, ShopId = string.Empty, MaxUnitPrice = 0, RemainingQuantity = 0,
            ActionBatch = null, SeedTransferIntent = null, SeedReservationId = null, Reason = "crop_teacher_late_day_recovery"
        };
        var runtime = Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4"));
        var operation = effective.Stage == CropTeacherRunStageKinds.NativeSleep
            ? await PrepareCropTeacherOwnedOvernightAsync(http, options, iteration, effective, plan, request, owner, runtime).ConfigureAwait(false)
            : await PrepareCropTeacherOwnedRouteCoreAsync(http, options, iteration, effective, plan, request, owner, runtime,
                lateDayRecovery: true).ConfigureAwait(false);
        var start = operation.OriginalStart!;
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        var modelPath = operation.Record["original_model_plan_path"]!.GetValue<string>();
        return new(StandardResult(queue, modelPath, string.Empty, sourcePath, source, ReadString(source, "state_hash"),
            options, interruptKind: "crop_teacher_late_day_recovery") with { ExecutionStepOverride = effective }, operation);
    }
}
