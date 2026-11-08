using System.Text;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<T> PrepareSeedAsync<T>(CropTeacherManagedSeedOperation operation, Func<Task<T>> prepareOriginal)
        where T : class, ICropTeacherSeedRuntime
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            RequireOperation(operation);
            if (operation.IsLifecycle || operation.Prepared is not null || operation.Record["original_intent_sha256"] is null)
                throw new InvalidOperationException("Original seed preparation requires a new, intent-pinned seed admission.");
            await ClaimRuntimeInvocationAsync(operation).ConfigureAwait(false);
            var runtime = await prepareOriginal().ConfigureAwait(false);
            await PinPreparedCoreAsync(operation, runtime).ConfigureAwait(false);
            return runtime;
        }
        finally { gate.Release(); }
    }

    private string SeedIntentHash(JsonObject record, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step)
    {
        var binding = CropTeacherSeedRuntimeRecovery.Bind(scopes, step);
        var intent = CropTeacherSeedRuntimeTransaction.CreateIntent(
            new(options.BackendUrl, StoredPath(record, "runtime_directory"), options.RunId, options.ExecutorVersion), binding, binding.ReadPreparationRequest());
        CropTeacherSeedRuntimeTransaction.AddPreparationDetails(intent, binding);
        return PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(intent.ToJsonString(JsonOptions)));
    }

    private void ValidateSeedIntent(CropTeacherManagedSeedOperation operation)
    {
        if (operation.Record["original_intent_sha256"] is not null)
            RequireAdmittedSeedIntent(operation, SeedIntentHash(operation.Record, operation.Scopes, operation.ReadStep()));
    }

    private static void RequireAdmittedSeedIntent(CropTeacherManagedSeedOperation operation, string actual)
    {
        if (operation.Record["original_intent_sha256"] is not null && Text(operation.Record, "original_intent_sha256") != actual)
            throw new InvalidDataException("Original seed intent differs from its pre-mutation admission.");
    }

    private async Task PersistPreparedAsync(CropTeacherManagedSeedOperation operation, string intentHash, string queuePath,
        string queueHash, bool recoveredAfterNativeVerification = false)
    {
        RequireAdmittedSeedIntent(operation, intentHash);
        var record = new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_seed_preparation.v1", ["sequence"] = operation.Sequence,
            ["run_id"] = options.RunId, ["support_step_id"] = operation.SupportStepId,
            ["admission_sha256"] = HashFile(operation.RecordPath), ["original_intent_sha256"] = intentHash,
            ["queue_path"] = queuePath, ["queue_sha256"] = queueHash,
            ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
        };
        if (HasOwnedDispatchProtocol(operation)) record["dispatch_protocol"] = "owner_loop.v1";
        if (recoveredAfterNativeVerification) record["reconstructed_after_original_native_verification"] = true;
        var path = RecordPath(operation.Sequence, "prepared");
        await SaveAsync(path, record).ConfigureAwait(false);
        operation.Prepared = record;
        TrackFrontier(path);
    }
}
