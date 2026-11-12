public sealed partial class LiveTrainingOptions
{
    public string ResumeManagedCropOwnerDirectory { get; set; } = string.Empty;
    public string ManagedCropReloadObservationPath { get; set; } = string.Empty;
    public int ManagedCropReloadProcessId { get; set; }
    public string ManagedCropReloadExecutablePath { get; set; } = string.Empty;
    public string ManagedCropReloadLogPath { get; set; } = string.Empty;

    /// <summary>Validates explicit resume inputs; owner-only mode supplies no native reload or dispatch authority.</summary>
    public void ValidateManagedCropTeacherResumeMode()
    {
        var observation = !string.IsNullOrEmpty(ManagedCropReloadObservationPath);
        var executable = !string.IsNullOrEmpty(ManagedCropReloadExecutablePath);
        var log = !string.IsNullOrEmpty(ManagedCropReloadLogPath);
        var actual = ManagedCropReloadProcessId != 0 || executable || log;
        if (string.IsNullOrEmpty(ResumeManagedCropOwnerDirectory) && !observation && !actual) return;

        if (!UseManagedCropTeacher)
            throw new ArgumentException("Managed owner resume requires --use-managed-crop-teacher.");
        // Preserve the original save/execution/legacy-fork guards rather than duplicating them.
        ValidateManagedCropTeacherMode();
        var ownerPath = NormalizeManagedCropResumePath(ResumeManagedCropOwnerDirectory, "--resume-managed-crop-owner");
        if (observation && actual)
            throw new ArgumentException("Managed reload observation and actual process/executable/log inputs are mutually exclusive.");
        if (actual && (ManagedCropReloadProcessId <= 0 || !executable || !log))
            throw new ArgumentException("Managed reload capture requires a positive process ID, executable and log together.");

        var observationPath = observation
            ? NormalizeManagedCropResumePath(ManagedCropReloadObservationPath, "--managed-crop-reload-observation") : string.Empty;
        var executablePath = actual
            ? NormalizeManagedCropResumePath(ManagedCropReloadExecutablePath, "--managed-crop-reload-executable") : string.Empty;
        var logPath = actual
            ? NormalizeManagedCropResumePath(ManagedCropReloadLogPath, "--managed-crop-reload-log") : string.Empty;
        if (actual)
        {
            if (!Path.GetFileNameWithoutExtension(executablePath).Equals("StardewModdingAPI", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Managed reload capture requires the original StardewModdingAPI executable.");
            if (!Uri.TryCreate(BridgeSnapshotUrl, UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme is not ("http" or "https") || !endpoint.IsLoopback ||
                endpoint.AbsolutePath != "/api/v1/snapshot" || endpoint.UserInfo.Length != 0 ||
                endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
                throw new ArgumentException("Managed reload capture requires a loopback HTTP(S) /api/v1/snapshot bridge URL.");
        }

        ResumeManagedCropOwnerDirectory = ownerPath;
        ManagedCropReloadObservationPath = observationPath;
        ManagedCropReloadExecutablePath = executablePath;
        ManagedCropReloadLogPath = logPath;
    }

    private static string NormalizeManagedCropResumePath(string value, string flag)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new ArgumentException(flag + " requires a nonempty absolute path.");
        return Path.GetFullPath(value);
    }

    private static string ReadManagedCropResumeArgument(string[] args, ref int index)
    {
        var flag = args[index];
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) ||
            args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException(flag + " requires a value.");
        return args[++index];
    }
}
