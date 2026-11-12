using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<string> ResumeCropTeacherManagedOwnerAsync(HttpClient http, LiveTrainingOptions options, string outputDirectory)
    {
        options.ValidateManagedCropTeacherResumeMode();
        if (string.IsNullOrWhiteSpace(options.ResumeManagedCropOwnerDirectory))
            throw new ArgumentException("Retained managed resume requires an explicit original owner directory.");
        options.BackendUrl = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        var binding = new CropTeacherNativeSaveBinding(options.SaveIsolationPath, options.SaveSlot);
        await using var owner = await CropTeacherManagedOwnerResume.OpenAsync(options.ResumeManagedCropOwnerDirectory,
            options.RunId, options.PolicyTrajectoryExecutorVersion, options.BackendUrl, options.SnapshotDir,
            binding, WritePortfolioJournalFileAsync).ConfigureAwait(false);
        var output = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(owner.EvidenceDirectory, "reload-entry-" + Guid.NewGuid().ToString("N")) : Path.GetFullPath(outputDirectory);
        owner.ValidateResumeOutputDirectory(output);
        Directory.CreateDirectory(output);
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        artifacts.Pin(Path.Combine(owner.EvidenceDirectory, "owner-intent.json"));
        var readback = await RestoreCropTeacherManagedRuntimeAsync(http, options, owner, output, artifacts).ConfigureAwait(false);
        var alreadyAccepted = readback is null && ManagedResumeMatchesReadbackObservation(owner.AcceptedSavedReadbackOperation,
            options.ManagedCropReloadObservationPath) ? owner.AcceptedSavedReadbackOperation : null;
        var restoredHistoricalReadback = readback is null && owner.AcceptedSavedContentReadbackVerified;
        string? observationPath = null;
        var freshReadback = false;
        if (readback is null && alreadyAccepted is null &&
            (!string.IsNullOrWhiteSpace(options.ManagedCropReloadObservationPath) || options.ManagedCropReloadProcessId > 0))
        {
            observationPath = options.ManagedCropReloadObservationPath;
            if (string.IsNullOrWhiteSpace(observationPath))
                observationPath = await CaptureCropTeacherNativeReloadAsync(http, options.RunId, owner.CurrentSnapshotPath,
                    owner.CurrentLedgerPath, await owner.ReadAcceptedNativeSaveBoundaryPathAsync().ConfigureAwait(false), binding,
                    options.ManagedCropReloadProcessId, options.ManagedCropReloadExecutablePath, options.ManagedCropReloadLogPath,
                    options.BridgeSnapshotUrl, Path.Combine(output, "native-load")).ConfigureAwait(false);
            var operation = await owner.AdmitSavedContentReadbackAsync(observationPath, Path.Combine(output, "readback-runtime")).ConfigureAwait(false);
            var scope = operation.SavedReadback!;
            foreach (var (path, hash) in scope.InputHashes)
                if (artifacts.Pin(path) != hash) throw new InvalidDataException("Managed resume readback inputs changed.");
            await IngestCropTeacherManagedResumeSnapshotAsync(http, options, scope.AfterSnapshotPath, artifacts).ConfigureAwait(false);
            await owner.RunSavedContentReadbackAsync(operation, http,
                (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
                (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw)).ConfigureAwait(false);
            readback = operation;
        }
        if ((readback ?? alreadyAccepted) is { } verifiedReadback)
        {
            var scope = verifiedReadback.SavedReadback!;
            observationPath = verifiedReadback.Record["reload_observation_path"]!.GetValue<string>();
            foreach (var (path, hash) in scope.InputHashes)
                if (artifacts.Pin(path) != hash) throw new InvalidDataException("Managed resume accepted readback inputs changed.");
            if (owner.Pending is not null || owner.HeadRecoveryRequired || !ManagedResumeSamePath(owner.CurrentSnapshotPath, scope.AfterSnapshotPath))
                throw new InvalidDataException("Managed resume readback did not reach its original accepted head.");
            artifacts.Pin(Path.Combine(owner.EvidenceDirectory, $"operation-{verifiedReadback.Sequence:D6}-accepted.json"));
            freshReadback = readback is not null && scope.NativeReloadVerified && scope.SemanticSavedContentVerified;
        }
        var source = artifacts.Read<SnapshotEnvelope>(owner.CurrentSnapshotPath);
        var ledger = artifacts.Read<StrategyCommitmentLedger>(owner.CurrentLedgerPath);
        var independent = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
            source.StateHash, JsonOptions, "Retained managed resume explicit accepted head").ConfigureAwait(false);
        CropTeacherSeedRuntimeTransaction.RequireLedger(ledger, independent.Ledger, "retained managed resume accepted head");
        artifacts.Pin(independent.Path);
        if (source.StateHash != ledger.SourceStateHash || source.SaveId.Value != ledger.SaveId || source.PlayerId.Value != ledger.PlayerId)
            throw new InvalidDataException("Managed resume snapshot/ledger lost their accepted source/actor binding.");
        var savedCloseout = freshReadback && owner.HasCompletedTerminalDeposits
            ? await owner.ReadSavedTerminalCloseoutAsync(Path.Combine(owner.EvidenceDirectory, "settlement-terminal", "terminal-evidence.json")).ConfigureAwait(false)
            : null;
        if (savedCloseout is not null)
        {
            savedCloseout.ValidateInputsUnchanged();
            foreach (var (path, hash) in savedCloseout.InputHashes)
                if (artifacts.Pin(path) != hash) throw new InvalidDataException("Managed resume saved closeout changed.");
        }
        artifacts.Validate();
        var reportPath = Path.Combine(output, "managed-owner-resume.json");
        await WritePortfolioJournalFileAsync(reportPath, new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_owner_resume.v1", ["run_id"] = options.RunId,
            ["owner_directory"] = owner.EvidenceDirectory, ["snapshot_path"] = owner.CurrentSnapshotPath,
            ["ledger_path"] = owner.CurrentLedgerPath, ["state_hash"] = source.StateHash,
            ["reload_observation_path"] = observationPath, ["restored_historical_readback_verified"] = restoredHistoricalReadback,
            ["native_saved_content_readback_verified"] = freshReadback, ["fresh_accepted_head_verified"] = freshReadback,
            ["native_dispatch_performed"] = false, ["complete_managed_runtime_verified"] = savedCloseout is not null,
            ["terminal_settlement_verified"] = savedCloseout is not null,
            ["saved_terminal_closeout"] = JsonSerializer.SerializeToNode(savedCloseout, JsonOptions),
            ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions)
        }.ToJsonString(JsonOptions)).ConfigureAwait(false);
        artifacts.Validate();
        return reportPath;
    }

    private static async Task IngestCropTeacherManagedResumeSnapshotAsync(HttpClient http, LiveTrainingOptions options,
        string path, CropSeedRuntimeArtifacts artifacts)
    {
        var snapshot = artifacts.Read<SnapshotEnvelope>(path);
        var reply = await PostJsonStringAsync(http, SnapshotIngestUrl(options, "full"),
            ContentAddressedJsonArtifactReader.ReadAllText(path)).ConfigureAwait(false);
        if (reply["accepted"]?.GetValue<bool>() != true || reply["state_hash"]?.GetValue<string>() != snapshot.StateHash)
            throw new InvalidDataException("Managed resume snapshot ingest did not accept its exact retained state hash.");
        artifacts.Validate();
    }

    private static bool ManagedResumeSamePath(string first, string second) =>
        (OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Equals(Path.GetFullPath(first), Path.GetFullPath(second));

    private static bool ManagedResumeMatchesReadbackObservation(CropTeacherManagedSeedOperation? operation, string observationPath) =>
        operation?.IsSavedContentReadback == true && !string.IsNullOrWhiteSpace(observationPath) &&
        ManagedResumeSamePath(operation.Record["reload_observation_path"]!.GetValue<string>(), observationPath);
}
