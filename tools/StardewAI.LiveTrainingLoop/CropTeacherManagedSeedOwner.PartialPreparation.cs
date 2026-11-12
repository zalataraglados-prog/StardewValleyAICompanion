using System.Globalization;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    public async Task<ICropTeacherSeedRuntime> ContinueSeedPreparationAsync(HttpClient http, string evidenceDirectory,
        Func<string, string, Task<JsonObject>> post,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<CropTeacherSeedCompilationContext, Task<string>> compileOriginal)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var operation = Pending ?? throw new InvalidOperationException("Managed crop owner has no partial seed preparation.");
            RequireOperation(operation);
            if (!HasOwnedDispatchProtocol(operation) || operation.Prepared is not null || operation.Dispatch is not null || operation.PreparedRuntime is not null)
                throw new InvalidOperationException("Only an uninvoked owned partial preparation may continue its original compiler.");
            RequireUninvokedRuntime(operation);
            RequireOutsideNativeSaveSlot(new[] { Path.GetFullPath(evidenceDirectory) }, options.NativeSaveBinding);
            await ClaimContinuationAsync(operation).ConfigureAwait(false);
            var (attempt, reference) = SelectContinuationAttempt(operation);
            var originalOptions = new CropTeacherSeedRuntimeRecoveryOptions(options.BackendUrl, operation.RuntimeDirectory,
                options.PortfolioJournalDirectory, attempt, options.RunId, options.ExecutorVersion, Text(operation.Record, "original_intent_sha256"));
            reference ??= await CropTeacherSeedRuntimeRecovery.ContinueOriginalPreparationAsync(http, originalOptions,
                operation.Scopes, operation.ReadStep(), post, write, compileOriginal).ConfigureAwait(false);
            RequireOperation(operation);
            RequireUninvokedRuntime(operation);
            var restored = await CropTeacherSeedRuntimeRecovery.RestorePreparationAsync(http,
                originalOptions with { EvidenceDirectory = evidenceDirectory, PreparationReference = reference },
                operation.Scopes, operation.ReadStep(), post, commit, settle, write).ConfigureAwait(false);
            await InstallRestoredPreparationAsync(operation, restored).ConfigureAwait(false);
            return restored.Runtime;
        }
        finally { gate.Release(); }
    }

    private (string Directory, CropSeedPreparationReference? Reference) SelectContinuationAttempt(CropTeacherManagedSeedOperation operation)
    {
        var root = ContinuationRoot(operation);
        RequireDirectoryPath(root);
        Directory.CreateDirectory(root);
        var index = 0;
        string? latest = null;
        foreach (var path in Directory.EnumerateFileSystemEntries(root).OrderBy(value => value, PathComparer))
        {
            var name = Path.GetFileName(path);
            if (!Directory.Exists(path) || name.Length != 14 || !name.StartsWith("attempt-", StringComparison.Ordinal) ||
                !int.TryParse(name.AsSpan(8), NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value != index + 1)
                throw new InvalidDataException("Original preparation attempts are incomplete, reordered or foreign.");
            RequireDirectoryPath(path);
            index = value;
            latest = path;
        }
        if (latest is not null)
        {
            var view = Path.Combine(latest, "continued-preparation.json");
            if (File.Exists(view)) return (latest, new(view, HashFile(view)));
            if (File.Exists(Path.Combine(latest, "compiled-seed-queue.json"))) return (latest, null);
        }
        if (index == 999999) throw new InvalidOperationException("Original preparation attempt namespace is exhausted.");
        return (Path.Combine(root, "attempt-" + (index + 1).ToString("D6", CultureInfo.InvariantCulture)), null);
    }
}
