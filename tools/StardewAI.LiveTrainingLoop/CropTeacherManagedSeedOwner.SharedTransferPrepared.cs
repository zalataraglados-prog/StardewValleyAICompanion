using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private async Task PersistSharedTransferPreparedAsync(CropTeacherManagedSeedOperation operation,
        CropTeacherSharedSourceSelection selection, CropTeacherSharedTransferPreparationReference reference, string intentHash)
    {
        var record = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_shared_transfer_preparation.v1", ["sequence"] = operation.Sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId, ["admission_sha256"] = HashFile(operation.RecordPath),
            ["original_intent_sha256"] = intentHash, ["original_transfer_preparation"] = JsonSerializer.SerializeToNode(reference, JsonOptions),
            ["shared_source_selection"] = JsonSerializer.SerializeToNode(selection, JsonOptions),
            ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
        };
        var execution = SharedPreparedExecutionRecord(operation.Record, selection, reference);
        record["shared_source_transition"] = execution["shared_source_transition"]!.DeepClone();
        var start = OpenSharedSourceStart(execution,
            operation.SharedTransferSupport!.PreparationRequest.GoalId);
        record["input_artifact_hashes"] = JsonSerializer.SerializeToNode(start.InputHashes, JsonOptions);
        var path = RecordPath(operation.Sequence, "prepared");
        await SaveAsync(path, record).ConfigureAwait(false);
        LoadSharedTransferPrepared(operation, record);
        TrackFrontier(path);
    }

    private void ValidateSharedTransferPrepared(CropTeacherManagedSeedOperation operation, JsonObject prepared)
    {
        var support = operation.SharedTransferSupport ?? throw new InvalidOperationException("Shared preparation has no original admission.");
        var reference = prepared["original_transfer_preparation"]?.Deserialize<CropTeacherSharedTransferPreparationReference>(JsonOptions)
            ?? throw new InvalidDataException("Shared prepared record lacks its original RPC reference.");
        var selection = SharedSourceSelection(prepared);
        var root = StoredPath(operation.Record, "preparation_runtime_directory");
        var journal = PortfolioMutationRequestJournal.Describe(options.BackendUrl, options.PortfolioJournalDirectory,
            "commit", support.PreparationRequest, JsonOptions);
        if (Text(prepared, "schema_version") != "crop_teacher_managed_shared_transfer_preparation.v1" ||
            prepared["sequence"]?.GetValue<int>() != operation.Sequence || Text(prepared, "run_id") != options.RunId ||
            Text(prepared, "support_step_id") != operation.SupportStepId || Text(prepared, "admission_sha256") != HashFile(operation.RecordPath) ||
            prepared["native_dispatch_performed"]?.GetValue<bool>() != false || prepared["formal_product_training_authorized"]?.GetValue<bool>() != false ||
            Text(prepared, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
            Text(prepared, "original_intent_sha256") != HashFile(Path.Combine(root, "support-intent.json")) ||
            reference.BackendUrl != options.BackendUrl || !SamePath(reference.RequestPath, journal.RequestPath) ||
            !SamePath(reference.JournalPath, journal.JournalPath) || !SamePath(reference.ReplyPath, Path.Combine(root, "marker-reply.json")) ||
            new[] { selection.ModelPlanPath, selection.QueuePath, selection.EvidencePath }.Any(path => !Within(root, path)))
            throw new InvalidDataException("Shared prepared record changed its original owner/preparation boundary.");
        _ = support.VerifyPreparationReference(reference);
        _ = ValidateSharedTransferSelection(operation, selection, reference, selection.QueuePath);
        var execution = SharedPreparedExecutionRecord(operation.Record, selection, reference);
        if (!JsonNode.DeepEquals(prepared["shared_source_transition"], execution["shared_source_transition"]))
            throw new InvalidDataException("Shared prepared record changed its original native transition.");
        var hashes = prepared["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonOptions)!;
        new CropSeedRuntimeArtifacts(hashes).Validate();
        var fresh = OpenSharedSourceStart(execution, support.PreparationRequest.GoalId);
        if (!JsonNode.DeepEquals(prepared["input_artifact_hashes"], JsonSerializer.SerializeToNode(fresh.InputHashes, JsonOptions)))
            throw new InvalidDataException("Shared prepared record lost its complete original input hashes.");
    }

    private void LoadSharedTransferPrepared(CropTeacherManagedSeedOperation operation, JsonObject prepared)
    {
        ValidateSharedTransferPrepared(operation, prepared);
        operation.Prepared = prepared;
        operation.SharedSourceStart = OpenSharedSourceStart(SharedPreparedExecutionRecord(operation.Record, SharedSourceSelection(prepared),
            prepared["original_transfer_preparation"]!.Deserialize<CropTeacherSharedTransferPreparationReference>(JsonOptions)!),
            operation.SharedTransferSupport!.PreparationRequest.GoalId);
    }

    private JsonObject SharedExecutionRecord(JsonObject record)
    {
        if (!IsSharedTransferPreparation(record)) return record;
        var prepared = Read<JsonObject>(RecordPath(Sequence(record), "prepared"));
        // Replay built this boundary when loading/publishing prepared; hot consumers need no world re-projection.
        var execution = (JsonObject)record.DeepClone();
        execution["shared_source_execution_protocol"] = "selected_work_yield.v2";
        foreach (var name in new[] { "shared_source_selection", "original_transfer_preparation", "shared_source_transition" })
            execution[name] = prepared[name]!.DeepClone();
        return execution;
    }

    private JsonObject SharedPreparedExecutionRecord(JsonObject admission, CropTeacherSharedSourceSelection selection,
        CropTeacherSharedTransferPreparationReference reference)
    {
        var record = (JsonObject)admission.DeepClone();
        record["shared_source_execution_protocol"] = "selected_work_yield.v2";
        record["shared_source_selection"] = JsonSerializer.SerializeToNode(selection, JsonOptions);
        record["original_transfer_preparation"] = JsonSerializer.SerializeToNode(reference, JsonOptions);
        var snapshot = Read<StardewAI.Contracts.State.SnapshotEnvelope>(StoredPath(admission, "before_snapshot_path"));
        var ranking = Read<JsonObject>(selection.DecisionRankingPath);
        var decision = Read<StardewAI.Contracts.Execution.SmallModelPlanEnvelope>(selection.DecisionPlanPath);
        var selected = SelectedQueueDecisionLease.CreateFromPlan(decision, ranking).CandidateAt(selection.SelectedQueueIndex);
        record["shared_source_transition"] = JsonSerializer.SerializeToNode(new MoneyRevenueCropNativeLifecycleTransitionRequest(
            "shared_source_return", SnapshotInt(snapshot, "time", "total_days"), selection.QueuePath,
            StoredPath(admission, "before_snapshot_path"), StoredPath(admission, "native_receipt_path"),
            StoredPath(admission, "native_after_snapshot_path"), Text(admission, "run_id"),
            options.ExecutorVersion,
            selected.CandidateId), JsonOptions);
        return record;
    }

    internal CropTeacherSharedSourceSelection ReadSharedSourceExecutionSelection(CropTeacherManagedSeedOperation operation) =>
        SharedSourceSelection(SharedExecutionRecord(operation.Record));
}
