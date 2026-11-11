using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task ExecuteCropTeacherOwnedSupportingAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation, bool watering)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering);
        if (watering ? operation.WateringStart is null : operation.RouteStart is null)
            throw new InvalidOperationException(watering ? "Owned watering has no original compilation." : "Owned crop route has no original compilation.");
        var start = operation.OriginalStart!;
        var transition = operation.Record[watering ? "maintenance_transition" : "route_transition"]!.Deserialize<MoneyRevenueCropNativeLifecycleTransitionRequest>(JsonOptions)!;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFullPath(transition.ExecutionReceiptPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json")), comparison) ||
            !string.Equals(Path.GetFullPath(transition.AfterSnapshotPath), Path.GetFullPath(Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json")), comparison))
            throw new InvalidDataException(watering ? "Owned watering executor output slot changed before dispatch." : "Owned crop route executor output slot changed before dispatch.");
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.SourcePath))!.AsObject();
        var queue = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(start.QueuePath))!.AsObject();
        RequireSelectedRefillToolObservation(options, operation.Record, queue);
        Task<JsonObject> Execute() => ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp,
            options, iteration, start.SourcePath, before, queue, ReadString(before, "state_hash"), ReadString(queue, "queue_id"),
            operation.Record[watering ? "original_model_action_path" : "original_model_plan_path"]!.GetValue<string>(), string.Empty, start.QueuePath, start.SourcePath,
            ReadString(before, "state_hash"), start.CandidateId, -1, start.QueuePath, null, new List<JsonObject>(),
            useDailyPlanDecisionLease: false,
            executionSnapshotUrlOverride: SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true),
            executionSnapshotProfileOverride: "full");
        if (watering) await owner.ExecuteOriginalWateringAsync(operation, http, Execute).ConfigureAwait(false);
        else await owner.ExecuteOriginalRouteAsync(operation, http, Execute).ConfigureAwait(false);
        if (watering) await owner.RunWateringMaintenanceAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
        else await owner.RunSupportingRouteAsync(operation, http,
            (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
            (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw, CancellationToken.None)).ConfigureAwait(false);
    }

    private static void RequireSelectedRefillToolObservation(LiveTrainingOptions options, JsonObject record, JsonObject queue)
    {
        if (record["maintenance_control_kind"]?.GetValue<string>() != "refill_approach_resources") return;
        var items = queue["items"]?.AsArray() ?? throw new InvalidDataException("Selected refill has no original queue items.");
        if (items.Any(item => item?["option_id"]?.GetValue<string>() == "executor.clear_obstacle") &&
            (!options.ObserveNativeOutputs || !options.ObserveNativeClearanceToolInputs))
            throw new InvalidDataException("Selected refill clearance requires its original tool-input observation before dispatch.");
    }
}
