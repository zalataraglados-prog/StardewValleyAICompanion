public sealed partial class LiveTrainingOptions
{
    public string ManagedCropLiveSessionPath { get; set; } = string.Empty;

    public void ValidateManagedCropTeacherLiveContinueMode()
    {
        if (!UseManagedCropTeacher) throw new ArgumentException("Live continuation requires the original managed crop mode.");
        ValidateManagedCropTeacherResumeMode();
        if (string.IsNullOrWhiteSpace(ResumeManagedCropOwnerDirectory) || string.IsNullOrWhiteSpace(ManagedCropLiveSessionPath) ||
            !Path.IsPathFullyQualified(ManagedCropLiveSessionPath) || ManagedCropReloadProcessId != 0 ||
            !string.IsNullOrEmpty(ManagedCropReloadExecutablePath) || !string.IsNullOrEmpty(ManagedCropReloadLogPath) ||
            !string.IsNullOrEmpty(ManagedCropReloadObservationPath))
            throw new ArgumentException("Same-live continuation requires the original owner/session, never reload inputs.");
        if (!Uri.TryCreate(BridgeSnapshotUrl, UriKind.Absolute, out var endpoint) || !endpoint.IsLoopback ||
            endpoint.Scheme is not ("http" or "https") || endpoint.AbsolutePath != "/api/v1/snapshot" ||
            endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0)
            throw new ArgumentException("Same-live continuation requires the original loopback snapshot endpoint.");
        ManagedCropLiveSessionPath = Path.GetFullPath(ManagedCropLiveSessionPath);
    }
}
