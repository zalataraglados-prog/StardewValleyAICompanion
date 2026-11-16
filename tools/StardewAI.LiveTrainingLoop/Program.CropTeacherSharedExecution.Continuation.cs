using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<string> ContinueCropTeacherNativeSourceReturnAsync(HttpClient http, LiveTrainingOptions options)
    {
        options.ValidateManagedCropTeacherResumeMode();
        if (string.IsNullOrWhiteSpace(options.ResumeManagedCropOwnerDirectory) ||
            options.ManagedCropReloadProcessId != 0 || !string.IsNullOrEmpty(options.ManagedCropReloadObservationPath) ||
            !string.IsNullOrEmpty(options.ManagedCropReloadExecutablePath) || !string.IsNullOrEmpty(options.ManagedCropReloadLogPath))
            throw new ArgumentException("Native source-return continuation requires the original owner, not native reload inputs.");
        options.BackendUrl = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        var binding = new CropTeacherNativeSaveBinding(options.SaveIsolationPath, options.SaveSlot);
        await using var owner = await CropTeacherManagedOwnerResume.OpenAsync(options.ResumeManagedCropOwnerDirectory,
            options.RunId, options.PolicyTrajectoryExecutorVersion, options.BackendUrl, options.SnapshotDir,
            binding, WritePortfolioJournalFileAsync).ConfigureAwait(false);
        var operation = owner.Pending;
        if (operation?.SharedSourceStart?.AppliedNativeWorkQueue != true)
            throw new InvalidOperationException("Native source-return continuation requires the original pending applied-native-work yield.");
        if (operation.Started is not null)
            throw new InvalidOperationException("Started source-return bookkeeping requires the original read-only resume/lookup path.");
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        // No claim or execution callback: the same original suffix must verify the retained claim and
        // all native frames before it may seal the outcome or start/continue its journaled bookkeeping.
        await CompleteCropTeacherSharedSourceReturnAsync(http, options, owner, operation).ConfigureAwait(false);
        if (owner.Pending is not null || operation.SharedSourceReturn is not { } returned ||
            !SameSharedPath(owner.CurrentSnapshotPath, returned.AfterSnapshotPath))
            throw new InvalidDataException("Native source-return continuation did not accept its original observed head.");
        returned.ValidateInputsUnchanged();
        return await WriteCropTeacherNativeSourceReturnReportAsync(options, owner, operation).ConfigureAwait(false);
    }

    private static async Task<string> WriteCropTeacherNativeSourceReturnReportAsync(LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner, CropTeacherManagedSeedOperation operation)
    {
        if (owner.Pending is not null || operation.SharedSourceReturn is not { } returned ||
            !SameSharedPath(owner.CurrentSnapshotPath, returned.AfterSnapshotPath))
            throw new InvalidDataException("A native source-return report requires its accepted original outcome.");
        returned.ValidateInputsUnchanged();
        var output = Path.Combine(owner.EvidenceDirectory, "native-source-return-entry-" + Guid.NewGuid().ToString("N"));
        owner.ValidateResumeOutputDirectory(output);
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, "continued-native-source-return.json");
        await WritePortfolioJournalFileAsync(path, new JsonObject
        {
            ["schema_version"] = "continued_original_native_source_return.v1", ["run_id"] = options.RunId,
            ["owner_directory"] = owner.EvidenceDirectory, ["accepted_sequence"] = operation.Sequence,
            ["state_hash"] = returned.AfterStateHash, ["snapshot_path"] = owner.CurrentSnapshotPath,
            ["ledger_path"] = owner.CurrentLedgerPath, ["owner_pending"] = false,
            ["native_shared_objective_completed"] = operation.NativeSharedObjectiveCompleted,
            ["native_execution_performed"] = false, ["native_reload_performed"] = false,
            ["fresh_saved_content_readback_verified"] = false, ["formal_product_training_authorized"] = false
        }.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return path;
    }
}
