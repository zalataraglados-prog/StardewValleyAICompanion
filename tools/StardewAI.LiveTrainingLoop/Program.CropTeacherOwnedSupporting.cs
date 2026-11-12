using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task ExecuteCropTeacherOwnedSupportingAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, bool watering)
        => await ExecuteCropTeacherOwnedSupportingCoreAsync(http, executorHttp, options, iteration, owner, operation,
            watering ? CropTeacherSupportingOperationKind.Watering : CropTeacherSupportingOperationKind.Route).ConfigureAwait(false);

    private static async Task ExecuteCropTeacherOwnedSupportingCoreAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, CropTeacherSupportingOperationKind kind)
    {
        var watering = kind == CropTeacherSupportingOperationKind.Watering;
        var harvest = kind == CropTeacherSupportingOperationKind.Harvest;
        var overnight = kind == CropTeacherSupportingOperationKind.Overnight;
        var control = kind == CropTeacherSupportingOperationKind.Control;
        owner.ValidateOriginalSupportingExecutor(options, watering);
        if (overnight) owner.ValidateOriginalOvernightSaveBinding(options);
        if (control ? operation.ControlStart is null : overnight ? operation.OvernightStart is null : harvest ? operation.HarvestStart is null : watering ? operation.WateringStart is null : operation.RouteStart is null)
            throw new InvalidOperationException(watering ? "Owned watering has no original compilation." : "Owned crop route has no original compilation.");
        var start = operation.OriginalStart!;
        var transition = operation.Record[control ? "control_transition" : overnight ? "overnight_transition" : harvest ? "harvest_transition" : watering ? "maintenance_transition" : "route_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(transition.ExecutionReceiptPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json")), comparison) ||
            !string.Equals(Path.GetFullPath(transition.AfterSnapshotPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json")), comparison))
            throw new InvalidDataException(watering ? "Owned watering executor output slot changed before dispatch." : "Owned crop route executor output slot changed before dispatch.");
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.SourcePath))!.AsObject();
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        RequireSelectedRefillToolObservation(options, operation.Record, queue);
        Task<JsonObject> Execute() => ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp,
            options, iteration, start.SourcePath, before, queue, ReadString(before, "state_hash"), ReadString(queue, "queue_id"),
            operation.Record[watering || harvest ? "original_model_action_path" : "original_model_plan_path"]!.GetValue<string>(), string.Empty, start.QueuePath, start.SourcePath,
            ReadString(before, "state_hash"), start.CandidateId, -1, start.QueuePath, null, new List<JsonObject>(),
            useDailyPlanDecisionLease: false,
            executionSnapshotUrlOverride: SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true),
            executionSnapshotProfileOverride: "full");
        if (control) await owner.ExecuteOriginalControlAsync(operation, http, Execute).ConfigureAwait(false);
        else if (overnight) await owner.ExecuteOriginalOvernightAsync(operation, http, Execute).ConfigureAwait(false);
        else if (harvest) await owner.ExecuteOriginalHarvestAsync(operation, http, Execute).ConfigureAwait(false);
        else if (watering) await owner.ExecuteOriginalWateringAsync(operation, http, Execute).ConfigureAwait(false);
        else await owner.ExecuteOriginalRouteAsync(operation, http, Execute).ConfigureAwait(false);
        if (control) await owner.RunControlRecoveryAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        else if (overnight) await owner.RunOvernightAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        else if (harvest) await owner.RunHarvestAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        else if (watering) await owner.RunWateringMaintenanceAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        else await owner.RunSupportingRouteAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
    }

    private static void RequireSelectedRefillToolObservation(LiveTrainingOptions options, JsonObject record, JsonObject queue)
    {
        if (record["maintenance_control_kind"]?.GetValue<string>() != "refill_approach_resources") return;
        RequireSelectedRefillToolInputs(options, queue);
        NativeOutputObservationMode.Validate(options);
    }

    private static void RequireSelectedRefillToolInputs(LiveTrainingOptions options, JsonObject queue)
    {
        var items = queue["items"]?.AsArray() ?? throw new InvalidDataException("Selected refill has no original queue items.");
        if (items.Any(item => item?["option_id"]?.GetValue<string>() == "executor.clear_obstacle") &&
            (!options.ObserveNativeOutputs || !options.ObserveNativeClearanceToolInputs))
            throw new InvalidDataException("Selected refill clearance requires its original tool-input observation before dispatch.");
    }
}
