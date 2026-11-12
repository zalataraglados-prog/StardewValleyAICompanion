using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<ICropTeacherSeedRuntime> RestorePreparedSeedAsync(HttpClient http, string evidenceDirectory,
        Func<string, string, Task<JsonObject>> post,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var operation = Pending ?? throw new InvalidOperationException("Managed crop owner has no pending seed preparation.");
            RequireOperation(operation);
            if (!HasOwnedDispatchProtocol(operation) || operation.Dispatch is not null || operation.PreparedRuntime is not null)
                throw new InvalidOperationException("Only an uninvoked owned seed runtime may restore its original preparation.");
            RequireUninvokedRuntime(operation);
            RequireOutsideNativeSaveSlot(new[] { Path.GetFullPath(evidenceDirectory) }, options.NativeSaveBinding);
            var restored = await CropTeacherSeedRuntimeRecovery.RestorePreparationAsync(http,
                new(options.BackendUrl, operation.RuntimeDirectory, options.PortfolioJournalDirectory, evidenceDirectory,
                    options.RunId, options.ExecutorVersion, Text(operation.Record, "original_intent_sha256"))
                    { PreparationReference = ReadPreparationReference(operation) },
                operation.Scopes, operation.ReadStep(), post, commit, settle, write).ConfigureAwait(false);
            await InstallRestoredPreparationAsync(operation, restored).ConfigureAwait(false);
            return restored.Runtime;
        }
        finally { gate.Release(); }
    }

    private async Task InstallRestoredPreparationAsync(CropTeacherManagedSeedOperation operation,
        CropTeacherSeedRuntimeRecovery.RestoredPreparation restored)
    {
        RequireOperation(operation);
        RequireUninvokedRuntime(operation);
        if (operation.Prepared is null) await PinPreparedCoreAsync(operation, restored.Runtime).ConfigureAwait(false);
        else
        {
            var prepared = ((ICropTeacherSeedManagedRuntime)restored.Runtime).ReadPreparedBoundary();
            RequirePrepared(operation, prepared.OriginalIntentSha256);
            RequireIdentity(operation, prepared.RuntimeDirectory, prepared.BackendUrl, prepared.RunId,
                prepared.ExecutorVersion, prepared.SupportStepId);
            if (Text(operation.Prepared, "queue_sha256") != prepared.QueueSha256 ||
                !SamePath(StoredPath(operation.Prepared, "queue_path"), prepared.QueuePath) ||
                ReadPreparationReference(operation) != prepared.PreparationReference)
                throw new InvalidDataException("Restored original preparation differs from its pinned queue.");
            AttachPreparedRuntime(operation, restored.Runtime);
        }
        operation.RuntimeArtifactGuard = restored.Validate;
        restored.Validate();
    }
}
