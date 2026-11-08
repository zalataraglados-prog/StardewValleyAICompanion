using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static string InvocationSchema(CropTeacherManagedSeedOperation operation) => operation.IsLifecycle
        ? "crop_teacher_managed_lifecycle_start.v1" : "crop_teacher_managed_seed_start.v1";

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
        var path = RecordPath(operation.Sequence, "started");
        await SaveAsync(path, started).ConfigureAwait(false);
        operation.Started = started;
        TrackFrontier(path);
    }
}
