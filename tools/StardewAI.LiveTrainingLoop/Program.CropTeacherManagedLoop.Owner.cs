using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed class CropTeacherOwnerLifetime : IAsyncDisposable
    {
        internal CropTeacherManagedSeedOwner? Current { get; private set; }
        internal CropTeacherManagedProductionEvidence? TerminalProduction { get; private set; }
        internal CropTeacherTerminalShippingSelection? TerminalSelection { get; private set; }
        internal void AttachTerminal(CropTeacherManagedProductionEvidence production, CropTeacherTerminalShippingSelection selection)
        {
            if (Current is null || TerminalProduction is not null || TerminalSelection is not null)
                throw new InvalidOperationException("Retain one terminal production boundary for the current managed owner.");
            TerminalProduction = production;
            TerminalSelection = selection;
        }
        internal void Attach(CropTeacherManagedSeedOwner owner)
        {
            if (Current is not null) throw new InvalidOperationException("Complete or recover the original managed crop owner before another dispatch.");
            Current = owner;
        }
        public async ValueTask DisposeAsync()
        {
            if (Current is not null) await Current.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<CropTeacherManagedSeedOwner> OpenCropTeacherManagedOwnerAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherDispatchSession dispatch)
    {
        options.ValidateManagedCropTeacherMode();
        var backend = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        if (!options.UseManagedCropTeacher) throw new InvalidOperationException("Managed crop owner was not explicitly enabled.");
        var manifest = JsonSerializer.Deserialize<MoneyRevenueCropTeacherDispatchManifest>(
            ContentAddressedJsonArtifactReader.ReadAllText(dispatch.TeacherDispatchManifestPath), JsonOptions)
            ?? throw new InvalidDataException("Managed crop dispatch manifest is empty.");
        var capture = JsonSerializer.Deserialize<PlanningHandoffCaptureManifest>(
            ContentAddressedJsonArtifactReader.ReadAllText(dispatch.Capture.ManifestPath), JsonOptions)
            ?? throw new InvalidDataException("Managed crop capture manifest is empty.");
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (manifest.SourceStateHash != dispatch.RunPlan.SourceStateHash || manifest.SourceStateHash != dispatch.Capture.SourceStateHash ||
            !comparer.Equals(Path.GetFullPath(manifest.NativeRunPlanPath), Path.GetFullPath(dispatch.NativeRunPlanPath)) ||
            !comparer.Equals(Path.GetFullPath(manifest.CaptureManifestPath), Path.GetFullPath(dispatch.Capture.ManifestPath)) ||
            manifest.NativeRunPlanSha256 != PortfolioWireHash(await File.ReadAllBytesAsync(dispatch.NativeRunPlanPath)) ||
            manifest.CaptureManifestSha256 != PortfolioWireHash(await File.ReadAllBytesAsync(dispatch.Capture.ManifestPath)) ||
            manifest.TeacherDecisionSha256 != PortfolioWireHash(await File.ReadAllBytesAsync(manifest.TeacherDecisionPath)))
            throw new InvalidDataException("Managed crop owner lost its original dispatch/decision/capture identity.");
        var installation = CropTeacherRuntimeInstallation.Open(manifest.TeacherDecisionPath);
        if (installation.SourceStateHash != manifest.SourceStateHash)
            throw new InvalidDataException("Managed crop installation differs from the original dispatch source.");
        installation.ValidateManagedOwnerSource(dispatch.RunPlan.CoveredCommitmentIds);
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>(comparer));
        foreach (var path in new[] { dispatch.TeacherDispatchManifestPath, dispatch.Capture.ManifestPath,
                     dispatch.NativeRunPlanPath, manifest.TeacherDecisionPath }) artifacts.Pin(path);
        var root = Path.Combine(options.SnapshotDir, "crop-teacher-managed-" + iteration.ToString("D4"));
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("Managed crop start is retained; inspect its original installation/owner instead of installing again.");
        var installed = await InstallCropReservationsCoreAsync(http, installation, backend,
            Path.Combine(root, "installation")).ConfigureAwait(false);
        artifacts.Validate();
        var owner = await CropTeacherManagedSeedOwner.OpenAsync(new(Path.Combine(root, "owner"), options.RunId,
            options.PolicyTrajectoryExecutorVersion, backend, dispatch.NativeRunPlanPath, capture.SourceSnapshotPath,
            installed.Path, options.SnapshotDir)
        {
            NativeSaveBinding = new(options.SaveIsolationPath, options.SaveSlot),
            UseSelectedRefillResourceEffects = true
        }, WritePortfolioJournalFileAsync).ConfigureAwait(false);
        try
        {
            await WritePortfolioJournalFileAsync(Path.Combine(root, "managed-owner-open.json"), new JsonObject
            {
                ["schema_version"] = "crop_teacher_managed_owner_open.v1", ["run_id"] = options.RunId,
                ["native_run_plan_path"] = dispatch.NativeRunPlanPath, ["source_snapshot_path"] = capture.SourceSnapshotPath,
                ["installed_ledger_path"] = installed.Path, ["owner_directory"] = Path.Combine(root, "owner"),
                ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions),
                ["runtime_ledger_installed"] = true, ["native_outcome_verified"] = false,
                ["formal_product_training_authorized"] = false
            }.ToJsonString(JsonOptions)).ConfigureAwait(false);
            artifacts.Validate();
            return owner;
        }
        catch { await owner.DisposeAsync().ConfigureAwait(false); throw; }
    }
}
