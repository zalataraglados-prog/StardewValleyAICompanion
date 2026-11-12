using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static string InvocationSchema(CropTeacherManagedSeedOperation operation) => operation.IsTerminalShipping
        ? "crop_teacher_managed_terminal_start.v1" : operation.IsControlRecovery
        ? "crop_teacher_managed_control_start.v1" : operation.IsOvernight
        ? "crop_teacher_managed_overnight_start.v1" : operation.IsHarvest
        ? "crop_teacher_managed_harvest_start.v1" : operation.IsWateringMaintenance
        ? "crop_teacher_managed_watering_start.v1" : operation.IsSupportingRoute
        ? "crop_teacher_managed_route_start.v1" : operation.IsLifecycle
            ? "crop_teacher_managed_lifecycle_start.v1" : "crop_teacher_managed_seed_start.v2";

    private static bool HasOwnedDispatchProtocol(CropTeacherManagedSeedOperation operation) => !operation.IsLifecycle && !operation.IsSupportingTransition &&
        operation.Started?["schema_version"]?.GetValue<string>() == "crop_teacher_managed_seed_start.v2" &&
        operation.Started["dispatch_protocol"]?.GetValue<string>() == "owner_loop.v1";

    private async Task ClaimRuntimeInvocationAsync(CropTeacherManagedSeedOperation operation)
    {
        if (operation.Started is not null)
            throw new InvalidOperationException("Managed crop invocation is already claimed; recover original evidence instead of repeating it.");
        var started = new JsonObject
        {
            ["schema_version"] = InvocationSchema(operation), ["sequence"] = operation.Sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
            ["admission_sha256"] = HashFile(operation.RecordPath),
            ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"),
            ["runtime_invocation_claimed"] = true, ["formal_product_training_authorized"] = false
        };
        if (!operation.IsLifecycle && !operation.IsSupportingTransition) started["dispatch_protocol"] = "owner_loop.v1";
        var path = RecordPath(operation.Sequence, "started");
        await SaveAsync(path, started).ConfigureAwait(false);
        operation.Started = started;
        TrackFrontier(path);
    }
}
