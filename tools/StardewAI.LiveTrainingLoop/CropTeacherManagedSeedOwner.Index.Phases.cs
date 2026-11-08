using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private CropTeacherManagedSeedOperation OpenIndexedOperation(JsonObject record, string path)
    {
        var kind = record["operation_kind"]?.GetValue<string>();
        if (kind is null or "seed")
        {
            var seed = new CropTeacherManagedSeedOperation(this, record, path, OpenScopes(record));
            ValidateSeedIntent(seed);
            return seed;
        }
        if (kind != "lifecycle") throw new InvalidDataException("Managed crop phase kind is unsupported.");
        try
        {
            var operation = new CropTeacherManagedSeedOperation(this, record, path, null, OpenLifecycle(record));
            ValidateLifecycle(operation);
            return operation;
        }
        catch (ArgumentException error) { throw new InvalidDataException("Managed retained lifecycle options changed.", error); }
    }

    private void AddAcceptedPhaseEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.IsLifecycle)
        {
            if (operation.Started is null) throw new InvalidDataException("Managed lifecycle has no original invocation claim.");
            accepted["operation_kind"] = "lifecycle";
            accepted["runtime_start_sha256"] = HashFile(RecordPath(operation.Sequence, "started"));
            accepted["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256");
            accepted["original_closure_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "verified-closure-ledger.json"));
            accepted["native_outcome_verified"] = false;
        }
        else
        {
            if (operation.Dispatch is not null) accepted["dispatch_sha256"] = HashFile(RecordPath(operation.Sequence, "dispatch"));
            if (operation.Started is not null) accepted["runtime_start_sha256"] = HashFile(RecordPath(operation.Sequence, "started"));
            accepted["prepared_sha256"] = HashFile(RecordPath(operation.Sequence, "prepared"));
            accepted["original_intent_sha256"] = Text(operation.Prepared!, "original_intent_sha256");
            accepted["original_proof_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "support-proof.json"));
        }
    }

    private void ValidateAcceptedPhaseEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.IsLifecycle)
        {
            if (operation.Started is null || Text(accepted, "runtime_start_sha256") != HashFile(RecordPath(operation.Sequence, "started")) ||
                accepted["operation_kind"]?.GetValue<string>() != "lifecycle" || accepted["prepared_sha256"] is not null ||
                accepted["original_proof_sha256"] is not null || accepted["dispatch_sha256"] is not null || accepted["native_outcome_verified"]?.GetValue<bool>() != false ||
                Text(accepted, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
                Text(accepted, "original_intent_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "lifecycle-intent.json")) ||
                Text(accepted, "original_closure_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "verified-closure-ledger.json")))
                throw new InvalidDataException("Managed crop lifecycle acceptance evidence changed.");
        }
        else if (operation.Prepared is null || accepted["operation_kind"]?.GetValue<string>() is not (null or "seed") ||
            (operation.Dispatch is null ? accepted["dispatch_sha256"] is not null :
                Text(accepted, "dispatch_sha256") != HashFile(RecordPath(operation.Sequence, "dispatch"))) ||
            (operation.Started is null ? accepted["runtime_start_sha256"] is not null :
                Text(accepted, "runtime_start_sha256") != HashFile(RecordPath(operation.Sequence, "started"))) ||
            Text(accepted, "prepared_sha256") != HashFile(RecordPath(operation.Sequence, "prepared")) ||
            Text(accepted, "original_intent_sha256") != Text(operation.Prepared, "original_intent_sha256") ||
            Text(accepted, "original_proof_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "support-proof.json")))
            throw new InvalidDataException("Managed crop seed acceptance evidence changed.");
    }

    private void LoadRuntimeStart(CropTeacherManagedSeedOperation operation, JsonObject started)
    {
        var schema = Text(started, "schema_version");
        var supported = operation.IsLifecycle ? schema == InvocationSchema(operation) :
            schema == "crop_teacher_managed_seed_start.v1" ||
            schema == InvocationSchema(operation) && started["dispatch_protocol"]?.GetValue<string>() == "owner_loop.v1";
        if (!supported ||
            started["sequence"]?.GetValue<int>() != operation.Sequence || Text(started, "run_id") != options.RunId ||
            Text(started, "support_step_id") != operation.SupportStepId || Text(started, "admission_sha256") != HashFile(operation.RecordPath) ||
            Text(started, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
            started["runtime_invocation_claimed"]?.GetValue<bool>() != true || started["formal_product_training_authorized"]?.GetValue<bool>() != false)
            throw new InvalidDataException("Managed crop invocation claim changed.");
        operation.Started = started;
    }
}
