using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    // The game has already loaded. Only the original ledger rebind is invoked here.
    public async Task<CropTeacherManagedSeedOperation> AdmitSavedContentReadbackAsync(string observationPath, string runtimeDirectory)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var record = CreateAdmissionRecord(headSnapshotPath, headLedgerPath, runtimeDirectory);
            record["operation_kind"] = "saved_content_readback";
            record["reload_observation_path"] = Path.GetFullPath(observationPath);
            var scope = OpenSavedReadback(record);
            record["original_intent_sha256"] = SupportingTransitionIntentHash(record, scope);
            return await PersistAdmissionAsync(record, new(this, record, RecordPath(Sequence(record), "intent"), null,
                savedReadback: scope)).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public Task RunSavedContentReadbackAsync(CropTeacherManagedSeedOperation operation, HttpClient http,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post) =>
        RunSupportingTransitionAsync(operation, http, commit, post, CropTeacherSupportingOperationKind.SavedReadback);

    private CropTeacherNativeSavedContentReadback OpenSavedReadback(JsonObject record)
    {
        var scope = CropTeacherNativeSavedContentReadback.Open(StoredPath(record, "reload_observation_path"), Text(record, "support_step_id"));
        var observation = scope.Observation;
        var binding = options.NativeSaveBinding ?? throw new InvalidDataException("Managed saved readback requires its original native save binding.");
        if (record["step"] is not null || record["prior_verified_snapshot_path"] is not null ||
            !SamePath(StoredPath(record, "before_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(record, "before_ledger_path"), headLedgerPath) ||
            observation.RunId != options.RunId || !SamePath(observation.SourceSnapshotPath, headSnapshotPath) ||
            !SamePath(observation.LedgerPath, headLedgerPath) ||
            !SamePath(observation.SaveBinding.SaveIsolationPath, binding.SaveIsolationPath) ||
            !SamePath(Path.Combine(observation.SaveBinding.SaveIsolationPath, observation.SaveBinding.SaveSlot),
                Path.Combine(binding.SaveIsolationPath, binding.SaveSlot)))
            throw new InvalidDataException("Managed saved readback differs from its original owner/run/source/slot boundary.");
        var savedPath = AcceptedNativeSaveBoundaryPath();
        if (!SamePath(observation.OvernightSaveBoundaryPath, savedPath))
            throw new InvalidDataException("Managed saved readback must consume this owner's accepted original overnight save report.");
        var expected = frontierHashes.TryGetValue(headSnapshotPath, out var hash) ? hash : fixedHashes[headSnapshotPath];
        if (scope.InputHashes[headSnapshotPath] != expected)
            throw new InvalidDataException("Managed saved readback source has no accepted continuity.");
        var runtime = StoredPath(record, "runtime_directory");
        if (scope.InputHashes.Keys.Any(path => Within(runtime, path)))
            throw new IOException("Managed saved readback runtime overlaps retained inputs.");
        RequireOutsideNativeSaveSlot(new[] { runtime }, binding);
        return scope;
    }

    private string AcceptedNativeSaveBoundaryPath()
    {
        if (lastAcceptedOperation?.IsOvernight == true)
        {
            var path = OriginalOvernightSavePath(lastAcceptedOperation);
            if (Text(Read<JsonObject>(lastAcceptedPath!), "overnight_save_boundary_sha256") != HashFile(path))
                throw new InvalidDataException("Managed saved readback original accepted save report changed.");
            return path;
        }
        if (lastAcceptedOperation?.SavedReadback is { } readback)
        {
            readback.ValidateInputsUnchanged();
            return readback.Observation.OvernightSaveBoundaryPath;
        }
        throw new InvalidOperationException("Managed saved readback requires an accepted original overnight save boundary.");
    }

    private JsonObject CreateSavedReadbackIntent(JsonObject record, CropTeacherNativeSavedContentReadback scope) => new()
    {
        ["schema_version"] = "crop_teacher_managed_saved_readback.v1", ["run_id"] = options.RunId,
        ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
        ["runtime_directory"] = StoredPath(record, "runtime_directory"), ["support_step_id"] = Text(record, "support_step_id"),
        ["reload_observation_path"] = StoredPath(record, "reload_observation_path"),
        ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope.InputHashes, JsonOptions),
        ["rebind_request"] = JsonSerializer.SerializeToNode(scope.RebindRequest, JsonOptions),
        ["native_dispatch_performed_by_adapter"] = false, ["native_crop_lifecycle_verified"] = false,
        ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
    };

    private static void AddSavedReadbackEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.SavedReadback is not { } scope) return;
        scope.ValidateInputsUnchanged();
        accepted["native_saved_content_readback_verified"] = scope.SemanticSavedContentVerified;
        accepted["native_reload_verified"] = scope.NativeReloadVerified;
        accepted["reload_observation_sha256"] = HashFile(StoredPath(operation.Record, "reload_observation_path"));
        accepted["saved_content_boundary_sha256"] = HashFile(scope.Observation.OvernightSaveBoundaryPath);
    }

    private static void ValidateSavedReadbackEvidence(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        var fields = new[] { "native_saved_content_readback_verified", "native_reload_verified", "reload_observation_sha256", "saved_content_boundary_sha256" };
        if (operation.IsSavedContentReadback)
        {
            var expected = new JsonObject(); AddSavedReadbackEvidence(operation, expected);
            if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])) ||
                new[] { "route_target_reached", "route_native_claim_sha256", "route_native_result_sha256",
                    "maintenance_native_claim_sha256", "maintenance_native_result_sha256", "native_watering_prefix_verified",
                    "native_refill_control_verified", "native_refill_approach_verified", "native_refill_resource_effects_verified" }
                    .Any(name => accepted[name] is not null))
                throw new InvalidDataException("Managed saved readback changed or claimed unrelated native progress.");
        }
        else if (fields.Any(name => accepted[name] is not null))
            throw new InvalidDataException("A different phase cannot claim saved-content readback evidence.");
    }
}
