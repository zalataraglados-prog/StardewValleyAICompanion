using System.Diagnostics;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record CropTeacherManagedContinuation(CropTeacherManagedSeedOwner Owner,
        CropTeacherManagedContinuationSource Source, Process GameProcess, CropTeacherManagedRestoreReport Restore);

    private static async Task<CropTeacherManagedContinuation> ContinueCropTeacherManagedOwnerAsync(HttpClient http, LiveTrainingOptions options)
    {
        options.ValidateManagedCropTeacherContinueMode();
        options.BackendUrl = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        var binding = new CropTeacherNativeSaveBinding(options.SaveIsolationPath, options.SaveSlot);
        var owner = await CropTeacherManagedOwnerResume.OpenAsync(options.ResumeManagedCropOwnerDirectory,
            options.RunId, options.PolicyTrajectoryExecutorVersion, options.BackendUrl, options.SnapshotDir,
            binding, WritePortfolioJournalFileAsync).ConfigureAwait(false);
        Process? process = null;
        try
        {
            var restored = await RestoreCropTeacherManagedOwnerWithReportAsync(http, options, owner, binding,
                Path.Combine(owner.EvidenceDirectory, "continue-entry-" + Guid.NewGuid().ToString("N"))).ConfigureAwait(false);
            if (!restored.FreshReadback || owner.AcceptedSavedReadbackOperation?.SavedReadback is not { } readback)
                throw new InvalidDataException("Managed production continuation lacks a newly accepted native load of this owner.");
            readback.ValidateInputsUnchanged();
            var observed = readback.Observation.Process;
            process = Process.GetProcessById(options.ManagedCropReloadProcessId);
            if (process.HasExited || process.Id != observed.ProcessId ||
                process.StartTime.ToUniversalTime() != DateTimeOffset.Parse(observed.StartedAtUtc).UtcDateTime ||
                process.MainModule?.FileName is not { } executable || !ManagedResumeSamePath(executable, observed.ExecutablePath))
                throw new InvalidDataException("Managed production continuation lost its exact currently live native process.");
            return new(owner, owner.ReadContinuationSource(), process, restored);
        }
        catch
        {
            process?.Dispose();
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
