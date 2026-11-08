using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private string ContinuationRoot(CropTeacherManagedSeedOperation operation) =>
        Path.Combine(operation.RuntimeDirectory, "preparation-continuations");

    private CropSeedPreparationReference? ReadPreparationReference(CropTeacherManagedSeedOperation operation)
    {
        if (operation.Prepared?["preparation_reference"] is null) return null;
        if (operation.Prepared["preparation_reference"] is not JsonObject node)
            throw new InvalidDataException("Managed preparation continuation reference is not an object.");
        var reference = node.Deserialize<CropSeedPreparationReference>(JsonOptions)
            ?? throw new InvalidDataException("Managed preparation continuation reference is empty.");
        if (!HasOwnedDispatchProtocol(operation) || operation.Continuation is null ||
            string.IsNullOrWhiteSpace(reference.Path) || !Path.IsPathFullyQualified(reference.Path) || !Within(ContinuationRoot(operation), reference.Path) ||
            Path.GetFileName(reference.Path) != "continued-preparation.json" ||
            string.IsNullOrEmpty(reference.Sha256) || HashFile(reference.Path) != reference.Sha256 ||
            Text(operation.Prepared!, "continuation_sha256") != HashFile(RecordPath(operation.Sequence, "continuation")))
            throw new InvalidDataException("Managed preparation continuation reference changed.");
        return reference;
    }

    private void LoadContinuation(CropTeacherManagedSeedOperation operation, JsonObject value)
    {
        if (!HasOwnedDispatchProtocol(operation) || Text(value, "schema_version") != "crop_teacher_managed_preparation_continuation.v1" ||
            value["sequence"]?.GetValue<int>() != operation.Sequence || Text(value, "run_id") != options.RunId ||
            Text(value, "support_step_id") != operation.SupportStepId || Text(value, "admission_sha256") != HashFile(operation.RecordPath) ||
            Text(value, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
            !SamePath(StoredPath(value, "evidence_root"), ContinuationRoot(operation)) ||
            value["compilation_only"]?.GetValue<bool>() != true || value["native_request_performed"]?.GetValue<bool>() != false ||
            value["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Managed preparation continuation ownership changed.");
        operation.Continuation = value;
    }

    private async Task ClaimContinuationAsync(CropTeacherManagedSeedOperation operation)
    {
        if (operation.Continuation is not null) return;
        var root = ContinuationRoot(operation);
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("Managed preparation continuation namespace already exists without ownership.");
        var value = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_preparation_continuation.v1", ["sequence"] = operation.Sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
            ["admission_sha256"] = HashFile(operation.RecordPath),
            ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"), ["evidence_root"] = root,
            ["compilation_only"] = true, ["native_request_performed"] = false, ["formal_product_training_authorized"] = false
        };
        var path = RecordPath(operation.Sequence, "continuation");
        await SaveAsync(path, value).ConfigureAwait(false);
        operation.Continuation = value;
        TrackFrontier(path);
    }
}
