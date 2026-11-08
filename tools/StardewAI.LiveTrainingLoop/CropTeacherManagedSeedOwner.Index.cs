using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private void LoadIndex()
    {
        var recognized = new HashSet<string>(PathComparer);
        var records = Directory.EnumerateFiles(options.EvidenceDirectory, "operation-*-intent.json")
            .Select(path => (Path: path, Value: Read<JsonObject>(path)))
            .OrderBy(row => Sequence(row.Value)).ToArray();
        foreach (var (path, record) in records)
        {
            var sequence = (lastAcceptedOperation?.Sequence ?? 0) + 1;
            if (Pending is not null || !SamePath(path, RecordPath(sequence, "intent")) ||
                record["sequence"]?.GetValue<int>() != sequence || Text(record, "run_id") != options.RunId ||
                Text(record, "schema_version") != "crop_teacher_managed_seed_admission.v1" ||
                Text(record, "support_step_id") != StepId(sequence) || Text(record, "previous_accepted_sha256") != lastAcceptedHash ||
                record["native_dispatch_performed"]?.GetValue<bool>() != false || record["formal_product_training_authorized"]?.GetValue<bool>() != false ||
                !Within(options.EvidenceDirectory, StoredPath(record, "runtime_directory")) ||
                SamePath(options.EvidenceDirectory, StoredPath(record, "runtime_directory")))
                throw new InvalidDataException("Managed crop admission index is incomplete, reordered or changed.");
            var operation = OpenIndexedOperation(record, path);
            if (!JsonNode.DeepEquals(record["input_artifact_hashes"], JsonSerializer.SerializeToNode(OperationInputs(operation), JsonOptions)))
                throw new InvalidDataException("Managed crop admission inputs changed after ownership was recorded.");
            recognized.Add(path);
            var startedPath = RecordPath(sequence, "started");
            if (File.Exists(startedPath))
            {
                LoadRuntimeStart(operation, Read<JsonObject>(startedPath));
                recognized.Add(startedPath);
            }
            var preparedPath = RecordPath(sequence, "prepared");
            var continuationPath = RecordPath(sequence, "continuation");
            if (File.Exists(continuationPath))
            {
                LoadContinuation(operation, Read<JsonObject>(continuationPath));
                recognized.Add(continuationPath);
            }
            if (File.Exists(preparedPath))
            {
                var prepared = Read<JsonObject>(preparedPath);
                if (operation.IsLifecycle || Text(prepared, "schema_version") != "crop_teacher_managed_seed_preparation.v1" ||
                    prepared["sequence"]?.GetValue<int>() != sequence || Text(prepared, "run_id") != options.RunId ||
                    Text(prepared, "support_step_id") != operation.SupportStepId || Text(prepared, "admission_sha256") != HashFile(path) ||
                    prepared["native_dispatch_performed"]?.GetValue<bool>() != false || prepared["formal_product_training_authorized"]?.GetValue<bool>() != false ||
                    Text(prepared, "original_intent_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "support-intent.json")) ||
                    Text(prepared, "queue_sha256") != HashFile(StoredPath(prepared, "queue_path")))
                    throw new InvalidDataException("Managed crop preparation index changed.");
                operation.Prepared = prepared;
                RequireAdmittedSeedIntent(operation, Text(prepared, "original_intent_sha256"));
                if (HasOwnedDispatchProtocol(operation) != (prepared["dispatch_protocol"]?.GetValue<string>() == "owner_loop.v1"))
                    throw new InvalidDataException("Managed crop preparation dispatch protocol changed.");
                _ = ReadPreparationReference(operation);
                recognized.Add(preparedPath);
            }
            var dispatchPath = RecordPath(sequence, "dispatch");
            if (File.Exists(dispatchPath))
            {
                LoadDispatchClaim(operation, Read<JsonObject>(dispatchPath));
                recognized.Add(dispatchPath);
            }
            var acceptedPath = RecordPath(sequence, "accepted");
            if (File.Exists(acceptedPath))
            {
                var accepted = Read<JsonObject>(acceptedPath);
                if (Text(accepted, "schema_version") != "crop_teacher_managed_seed_acceptance.v1" ||
                    accepted["sequence"]?.GetValue<int>() != sequence || Text(accepted, "run_id") != options.RunId ||
                    Text(accepted, "support_step_id") != operation.SupportStepId || Text(accepted, "previous_accepted_sha256") != lastAcceptedHash ||
                    Text(accepted, "admission_sha256") != HashFile(path) ||
                    Text(accepted, "after_snapshot_sha256") != HashFile(StoredPath(accepted, "after_snapshot_path")) ||
                    Text(accepted, "after_ledger_sha256") != HashFile(StoredPath(accepted, "after_ledger_path")) ||
                    accepted["original_boundary_verified"]?.GetValue<bool>() != true ||
                    accepted["execution_authorized"]?.GetValue<bool>() != false || accepted["formal_product_training_authorized"]?.GetValue<bool>() != false)
                    throw new InvalidDataException("Managed crop acceptance index changed.");
                ValidateAcceptedPhaseEvidence(operation, accepted);
                SetHead(operation, accepted, acceptedPath);
                recognized.Add(acceptedPath);
            }
            else Pending = operation;
        }
        if (Directory.EnumerateFiles(options.EvidenceDirectory, "operation-*.json").Any(path => !recognized.Contains(path)))
            throw new InvalidDataException("Managed crop index contains an orphan phase record.");
        frontierHashes[Path.Combine(options.EvidenceDirectory, "owner-intent.json")] = headerHash;
        if (Pending is not null)
            TrackOperationRecords(Pending);
        HeadRecoveryRequired = Pending is null && lastAcceptedOperation is not null;
    }

    private void SetHead(CropTeacherManagedSeedOperation operation, JsonObject accepted, string path)
    {
        headSnapshotPath = Text(accepted, "after_snapshot_path");
        headLedgerPath = Text(accepted, "after_ledger_path");
        lastAcceptedOperation = operation;
        lastAcceptedPath = path;
        lastAcceptedHash = HashFile(path);
        frontierHashes.Clear();
        frontierHashes[Path.Combine(options.EvidenceDirectory, "owner-intent.json")] = headerHash;
        frontierHashes[headSnapshotPath] = Text(accepted, "after_snapshot_sha256");
        frontierHashes[headLedgerPath] = Text(accepted, "after_ledger_sha256");
        TrackFrontier(lastAcceptedPath);
        TrackOperationRecords(operation);
    }

    private void TrackOperationRecords(CropTeacherManagedSeedOperation operation)
    {
        TrackFrontier(operation.RecordPath);
        if (operation.Prepared is not null) TrackFrontier(RecordPath(operation.Sequence, "prepared"));
        if (operation.Started is not null) TrackFrontier(RecordPath(operation.Sequence, "started"));
        if (operation.Dispatch is not null) TrackFrontier(RecordPath(operation.Sequence, "dispatch"));
        if (operation.Continuation is not null) TrackFrontier(RecordPath(operation.Sequence, "continuation"));
        if (ReadPreparationReference(operation) is { } reference) TrackFrontier(reference.Path);
    }
}
