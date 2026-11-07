using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static Task<CropTeacherSeedRecoveredBoundary> RecoverCropTeacherSeedRuntimeAsync(HttpClient http,
        LiveTrainingOptions options, string runtimeDirectory, string output, string expectedIntentSha256,
        CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step) => CropTeacherSeedRuntimeRecovery.RecoverAsync(http,
        new(options.BackendUrl, runtimeDirectory, options.SnapshotDir, output, options.RunId,
            options.PolicyTrajectoryExecutorVersion, expectedIntentSha256), scopes, step,
        async (endpoint, raw) =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            return await PostJsonStringAsync(http, endpoint, raw, timeout.Token);
        }, WritePortfolioJournalFileAsync);
}
