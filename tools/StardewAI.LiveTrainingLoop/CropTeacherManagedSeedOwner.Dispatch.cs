using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<T> ExecuteSeedAsync<T>(CropTeacherManagedSeedOperation operation,
        Func<ICropTeacherSeedRuntime, Task<T>> executeOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (!HasOwnedDispatchProtocol(operation) || operation.Prepared is null || operation.PreparedRuntime is null || operation.Dispatch is not null)
                throw new InvalidOperationException("Original seed execution requires its owned prepared runtime and an unclaimed execution loop.");
            RequireUninvokedRuntime(operation);
            var record = new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_seed_dispatch.v1", ["sequence"] = operation.Sequence,
                ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
                ["admission_sha256"] = HashFile(operation.RecordPath),
                ["prepared_sha256"] = HashFile(RecordPath(operation.Sequence, "prepared")),
                ["original_intent_sha256"] = Text(operation.Prepared, "original_intent_sha256"),
                ["execution_loop_invocation_claimed"] = true, ["native_request_performed"] = null,
                ["formal_product_training_authorized"] = false
            };
            var path = RecordPath(operation.Sequence, "dispatch");
            await SaveAsync(path, record).ConfigureAwait(false);
            operation.Dispatch = record;
            TrackFrontier(path);
            operation.ExecutionActive = true;
            return await executeOriginal(operation.PreparedRuntime).ConfigureAwait(false);
        }
        finally { operation.ExecutionActive = false; gate.Release(); }
    }

    private void AttachPreparedRuntime(CropTeacherManagedSeedOperation operation, ICropTeacherSeedRuntime runtime, Action? artifactGuard = null)
    {
        if (operation.PreparedRuntime is not null || runtime is not ICropTeacherSeedManagedRuntime original)
            throw new InvalidOperationException("Managed seed runtime is already attached or is not original.");
        original.BindExecutionGuard(() =>
        {
            if (disposed || !operation.ExecutionActive || !ReferenceEquals(Pending, operation))
                throw new InvalidOperationException("Managed seed dispatch requires its original owner's active execution lease.");
        });
        operation.PreparedRuntime = runtime;
        operation.RuntimeArtifactGuard = artifactGuard;
    }

    private void LoadDispatchClaim(CropTeacherManagedSeedOperation operation, JsonObject dispatch)
    {
        if (!HasOwnedDispatchProtocol(operation) || operation.Prepared is null ||
            operation.Prepared["dispatch_protocol"]?.GetValue<string>() != "owner_loop.v1" ||
            Text(dispatch, "schema_version") != "crop_teacher_managed_seed_dispatch.v1" ||
            dispatch["sequence"]?.GetValue<int>() != operation.Sequence || Text(dispatch, "run_id") != options.RunId ||
            Text(dispatch, "support_step_id") != operation.SupportStepId || Text(dispatch, "admission_sha256") != HashFile(operation.RecordPath) ||
            Text(dispatch, "prepared_sha256") != HashFile(RecordPath(operation.Sequence, "prepared")) ||
            Text(dispatch, "original_intent_sha256") != Text(operation.Prepared, "original_intent_sha256") ||
            dispatch["execution_loop_invocation_claimed"]?.GetValue<bool>() != true ||
            !dispatch.ContainsKey("native_request_performed") || dispatch["native_request_performed"] is not null ||
            dispatch["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Managed seed dispatch claim changed.");
        operation.Dispatch = dispatch;
    }

    private static void RequireUninvokedRuntime(CropTeacherManagedSeedOperation operation)
    {
        var root = operation.RuntimeDirectory;
        if (new[] { "native-transition-request.json", "support-proof.json", "completion-summary.json", "prefix-controller-stop.json" }
                .Any(name => File.Exists(Path.Combine(root, name))) ||
            Directory.EnumerateFiles(root, "prefix-dispatch-*.json").Any())
            throw new InvalidOperationException("Retained original execution artifacts require original result/stop recovery, not another invocation.");
    }
}
