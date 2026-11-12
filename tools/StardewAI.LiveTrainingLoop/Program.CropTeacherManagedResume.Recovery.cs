using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherManagedSeedOperation?> RestoreCropTeacherManagedRuntimeAsync(HttpClient http, LiveTrainingOptions options,
        CropTeacherManagedSeedOwner owner, string output, CropSeedRuntimeArtifacts artifacts)
    {
        var explicitReadback = ManagedResumeMatchesReadbackObservation(owner.Pending, options.ManagedCropReloadObservationPath);
        if (owner.Pending is { Started: null } && !explicitReadback)
            throw new InvalidOperationException("Read-only managed resume cannot create an unclaimed mutation; provide its explicit original readback continuation.");
        await IngestCropTeacherManagedResumeSnapshotAsync(http, options, owner.CurrentSnapshotPath, artifacts).ConfigureAwait(false);
        CropTeacherManagedSeedOperation? completed = null;
        if (owner.Pending is { } pending)
        {
            string? after = pending.SavedReadback?.AfterSnapshotPath;
            if (pending.OriginalStart is { } preparation)
            {
                var scope = preparation.VerifyTransition(CropTeacherManagedSeedOwner.OriginalSupportingTransition(pending));
                scope.ValidateInputsUnchanged();
                after = scope.AfterSnapshotPath;
            }
            else if (!pending.IsSupportingTransition && !pending.IsLifecycle)
            {
                var path = Path.Combine(pending.RuntimeDirectory, "native-transition-request.json");
                if (File.Exists(path)) after = artifacts.Read<MoneyRevenueCropNativeLifecycleTransitionRequest>(path).AfterSnapshotPath;
            }
            if (after is not null) await IngestCropTeacherManagedResumeSnapshotAsync(http, options, after, artifacts).ConfigureAwait(false);
            if (pending.Started is not null)
                await owner.RecoverPendingAsync(http, Path.Combine(output, "pending-recovery"),
                    (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw)).ConfigureAwait(false);
            else if (explicitReadback)
                await owner.RunSavedContentReadbackAsync(pending, http,
                    (diagnostic, request) => PostJournaledPortfolioCommitAsync(http, options, diagnostic, request),
                    (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw)).ConfigureAwait(false);
            else
                throw new InvalidOperationException("Retain the original unclaimed seed/lifecycle preparation; resume cannot infer zero execution or dispatch it.");
            if (explicitReadback) completed = pending;
        }
        else if (owner.HeadRecoveryRequired)
            await owner.RestoreHeadAsync(http, Path.Combine(output, "head-recovery"),
                (endpoint, raw) => PostJsonStringAsync(http, endpoint, raw)).ConfigureAwait(false);
        artifacts.Pin(owner.CurrentSnapshotPath);
        artifacts.Pin(owner.CurrentLedgerPath);
        return completed;
    }
}
