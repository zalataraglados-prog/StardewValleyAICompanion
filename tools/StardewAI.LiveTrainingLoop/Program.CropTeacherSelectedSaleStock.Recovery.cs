using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static Task<string> RecoverCropTeacherSelectedSaleStockAsync(HttpClient http, LiveTrainingOptions options) =>
        OpenCropTeacherSelectedSaleStockEntryAsync(http, options, continueUnattempted: false);

    private static Task<string> ContinueCropTeacherSelectedSaleStockAsync(HttpClient http, LiveTrainingOptions options) =>
        OpenCropTeacherSelectedSaleStockEntryAsync(http, options, continueUnattempted: true);

    private static async Task<string> OpenCropTeacherSelectedSaleStockEntryAsync(HttpClient http, LiveTrainingOptions options, bool continueUnattempted)
    {
        options.ValidateManagedCropTeacherResumeMode();
        if (string.IsNullOrWhiteSpace(options.ResumeManagedCropOwnerDirectory) ||
            !string.IsNullOrEmpty(options.ManagedCropReloadObservationPath) || options.ManagedCropReloadProcessId != 0)
            throw new ArgumentException("Shared preparation recovery requires only its original owner identity, not a native reload.");
        await using var owner = await CropTeacherManagedOwnerResume.OpenAsync(options.ResumeManagedCropOwnerDirectory,
            options.RunId, options.PolicyTrajectoryExecutorVersion, options.BackendUrl, options.SnapshotDir,
            new(options.SaveIsolationPath, options.SaveSlot), WritePortfolioJournalFileAsync).ConfigureAwait(false);
        return await CropTeacherSelectedSaleStockEntryCoreAsync(http, options, owner, continueUnattempted).ConfigureAwait(false);
    }

    private static Task<string> RecoverCropTeacherSelectedSaleStockWithOwnerAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner) => CropTeacherSelectedSaleStockEntryCoreAsync(http, options, owner, continueUnattempted: false);

    private static Task<string> ContinueCropTeacherSelectedSaleStockWithOwnerAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner) => CropTeacherSelectedSaleStockEntryCoreAsync(http, options, owner, continueUnattempted: true);

    private static async Task<string> CropTeacherSelectedSaleStockEntryCoreAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner, bool continueUnattempted)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        var operation = owner.Pending ?? throw new InvalidOperationException("No original selected transfer preparation is pending.");
        var decision = operation.Record["shared_transfer_decision"]?.Deserialize<CropTeacherSharedTransferDecision>(JsonOptions);
        if (decision?.OriginalLedgerPath is null || operation.Prepared is not null)
            throw new InvalidOperationException("Explicit shared recovery requires an original selected sale-stock preparation, not another phase or a redispatch.");
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        await IngestCropTeacherManagedSnapshotAsync(http, options, owner.CurrentSnapshotPath, artifacts).ConfigureAwait(false);
        var selection = continueUnattempted
            ? await owner.ContinueUnattemptedSharedTransferPreparationAsync(operation, http,
                (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
                (ledger, output) => CompileCropTeacherSelectedSaleStockAsync(http, options, operation, ledger, output)).ConfigureAwait(false)
            : await owner.RecoverSharedTransferPreparationAsync(operation, http,
                (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw),
                (ledger, output) => CompileCropTeacherSelectedSaleStockAsync(http, options, operation, ledger, output)).ConfigureAwait(false);
        artifacts.Validate();
        var path = Path.Combine(owner.EvidenceDirectory, "shared-preparation-recovery-" + Guid.NewGuid().ToString("N") + ".json");
        var preparedPath = Path.Combine(owner.EvidenceDirectory, $"operation-{operation.Sequence:D6}-prepared.json");
        await WritePortfolioJournalFileAsync(path, new JsonObject
        {
            ["schema_version"] = "crop_teacher_selected_shared_preparation_recovery.v1", ["run_id"] = options.RunId,
            ["owner_directory"] = owner.EvidenceDirectory, ["support_step_id"] = operation.SupportStepId,
            ["prepared_record_path"] = preparedPath, ["prepared_record_sha256"] = PortfolioMutationRequestJournal.HashFile(preparedPath),
            ["queue_path"] = selection.QueuePath, ["queue_sha256"] = PortfolioMutationRequestJournal.HashFile(selection.QueuePath),
            ["original_preparation_recovered"] = !continueUnattempted, ["first_preparation_continued"] = continueUnattempted,
            ["preparation_only"] = true,
            ["native_dispatch_performed"] = false, ["formal_product_training_authorized"] = false
        }.ToJsonString(JsonOptions)).ConfigureAwait(false);
        return path;
    }
}
