namespace StardewAI.FriendshipTeacherRollout;

public sealed class RolloutOptions
{
    public string ProjectRoot { get; private set; } = string.Empty;

    public string OutputRoot { get; private set; } = string.Empty;

    public string RunId { get; private set; } = string.Empty;

    public string BackendUrl { get; private set; } = string.Empty;

    public string SnapshotUrl { get; private set; } = string.Empty;

    public string LoopSnapshotUrl { get; private set; } = string.Empty;

    public string ExecutorUrl { get; private set; } = string.Empty;

    public string SaveIsolationPath { get; private set; } = string.Empty;

    public string SaveSlot { get; private set; } = string.Empty;

    public string CalibrationPath { get; private set; } = string.Empty;

    public string LiveTrainingLoopDll { get; private set; } = string.Empty;

    public int DeadlineTotalDaysExclusive { get; private set; }

    public int MaxDayTransitions { get; private set; } = 1;

    public int MaxObjectivesPerDay { get; private set; } = 2;

    public int MaxConsecutiveNoProgressDays { get; private set; } = 3;

    public int ObjectiveMaxAttempts { get; private set; } = 64;

    public int SaveBoundaryMaxAttempts { get; private set; } = 64;

    public int ObjectiveTimeoutSeconds { get; private set; } = 600;

    public int RecoveryTimeoutSeconds { get; private set; } = 120;

    public int StoryEventTimeoutSeconds { get; private set; } = 180;

    public int MaxStoryEventAdvances { get; private set; } = 8;

    public int StoryEventExecutionMaxAttempts { get; private set; } = 8;

    public int StopAfterStoryEventAdvances { get; private set; }

    public string RequiredStoryEventId { get; private set; } = string.Empty;

    public int SaveBoundaryTimeoutSeconds { get; private set; } = 300;

    public int MinFreeSpaceMb { get; private set; } = 8192;

    public string SnapshotArtifactMode { get; private set; } =
        "content_addressed_gzip";

    public static RolloutOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (index + 1 >= args.Length || !args[index].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Every rollout option must be formatted as --name value.");
            values[args[index][2..]] = args[index + 1];
        }

        var options = new RolloutOptions
        {
            ProjectRoot = FullPath(Required(values, "project-root")),
            OutputRoot = FullPath(Required(values, "output-root")),
            RunId = Required(values, "run-id"),
            BackendUrl = AbsoluteHttp(values, "backend-url"),
            SnapshotUrl = AbsoluteHttp(values, "snapshot-url"),
            LoopSnapshotUrl = AbsoluteHttp(values, "loop-snapshot-url"),
            ExecutorUrl = AbsoluteHttp(values, "executor-url").TrimEnd('/'),
            SaveIsolationPath = FullPath(Required(values, "save-isolation-path")),
            SaveSlot = Required(values, "save-slot"),
            CalibrationPath = FullPath(Required(values, "calibration")),
            LiveTrainingLoopDll = FullPath(Required(values, "live-training-loop-dll")),
            DeadlineTotalDaysExclusive = PositiveInt(values, "deadline-total-days-exclusive"),
            MaxDayTransitions = PositiveInt(values, "max-day-transitions", 1),
            MaxObjectivesPerDay = PositiveInt(values, "max-objectives-per-day", 2),
            MaxConsecutiveNoProgressDays = PositiveInt(values, "max-no-progress-days", 3),
            ObjectiveMaxAttempts = PositiveInt(values, "objective-max-attempts", 64),
            SaveBoundaryMaxAttempts = PositiveInt(values, "save-boundary-max-attempts", 64),
            ObjectiveTimeoutSeconds = PositiveInt(values, "objective-timeout-seconds", 600),
            RecoveryTimeoutSeconds = PositiveInt(values, "recovery-timeout-seconds", 120),
            StoryEventTimeoutSeconds = PositiveInt(values, "story-event-timeout-seconds", 180),
            MaxStoryEventAdvances = PositiveInt(values, "max-story-event-advances", 8),
            StoryEventExecutionMaxAttempts = PositiveInt(
                values,
                "story-event-execution-max-attempts",
                8),
            StopAfterStoryEventAdvances = NonNegativeInt(
                values,
                "stop-after-story-event-advances",
                0),
            RequiredStoryEventId = OptionalString(values, "required-story-event-id"),
            SaveBoundaryTimeoutSeconds = PositiveInt(values, "save-boundary-timeout-seconds", 300),
            MinFreeSpaceMb = PositiveInt(values, "min-free-space-mb", 8192),
            SnapshotArtifactMode = SnapshotArtifactModeValue(values)
        };
        RequireFreshSnapshot(options.SnapshotUrl);
        RequireFreshSnapshot(options.LoopSnapshotUrl);
        options.ValidateStoryEventAdmission();
        options.ValidatePaths();
        return options;
    }

    private void ValidateStoryEventAdmission()
    {
        if (StopAfterStoryEventAdvances > 0 &&
            string.IsNullOrWhiteSpace(RequiredStoryEventId))
        {
            throw new ArgumentException(
                "--required-story-event-id is required when " +
                "--stop-after-story-event-advances is enabled.");
        }
        if (StopAfterStoryEventAdvances == 0 &&
            !string.IsNullOrWhiteSpace(RequiredStoryEventId))
        {
            throw new ArgumentException(
                "--required-story-event-id requires " +
                "--stop-after-story-event-advances.");
        }
    }

    private void ValidatePaths()
    {
        if (!Directory.Exists(ProjectRoot))
            throw new DirectoryNotFoundException("Project root not found: " + ProjectRoot);
        if (!Directory.Exists(SaveIsolationPath))
            throw new DirectoryNotFoundException("Save isolation root not found: " + SaveIsolationPath);
        if (!Directory.Exists(Path.Combine(SaveIsolationPath, SaveSlot)))
            throw new DirectoryNotFoundException("Save slot not found under isolation root.");
        if (!File.Exists(CalibrationPath))
            throw new FileNotFoundException("Route timing calibration not found.", CalibrationPath);
        if (!File.Exists(LiveTrainingLoopDll))
            throw new FileNotFoundException("LiveTrainingLoop assembly not found.", LiveTrainingLoopDll);
    }

    private static string Required(
        IReadOnlyDictionary<string, string> values,
        string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException("Missing required option --" + name + ".");

    private static string AbsoluteHttp(
        IReadOnlyDictionary<string, string> values,
        string name)
    {
        var value = Required(values, name);
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
               uri.Scheme is "http" or "https"
            ? value
            : throw new ArgumentException("--" + name + " must be an absolute HTTP URL.");
    }

    private static int PositiveInt(
        IReadOnlyDictionary<string, string> values,
        string name,
        int? fallback = null)
    {
        if (!values.TryGetValue(name, out var raw))
            return fallback ?? throw new ArgumentException("Missing required option --" + name + ".");
        return int.TryParse(raw, out var value) && value > 0
            ? value
            : throw new ArgumentException("--" + name + " must be a positive integer.");
    }

    private static int NonNegativeInt(
        IReadOnlyDictionary<string, string> values,
        string name,
        int fallback)
    {
        if (!values.TryGetValue(name, out var raw))
            return fallback;
        return int.TryParse(raw, out var value) && value >= 0
            ? value
            : throw new ArgumentException(
                "--" + name + " must be a non-negative integer.");
    }

    private static string OptionalString(
        IReadOnlyDictionary<string, string> values,
        string name) =>
        values.TryGetValue(name, out var value) ? value.Trim() : string.Empty;

    private static string FullPath(string path) => Path.GetFullPath(path);

    private static string SnapshotArtifactModeValue(
        IReadOnlyDictionary<string, string> values)
    {
        var value = values.TryGetValue("snapshot-artifact-mode", out var configured)
            ? configured.Trim()
            : "content_addressed_gzip";
        return value is "plain" or "content_addressed_gzip"
            ? value
            : throw new ArgumentException(
                "--snapshot-artifact-mode must be plain or content_addressed_gzip.");
    }

    private static void RequireFreshSnapshot(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var fresh = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .FirstOrDefault(parts => string.Equals(
                Uri.UnescapeDataString(parts[0]),
                "fresh",
                StringComparison.OrdinalIgnoreCase));
        var enabled = fresh is { Length: 2 } &&
            (Uri.UnescapeDataString(fresh[1]) == "1" ||
             string.Equals(
                 Uri.UnescapeDataString(fresh[1]),
                 "true",
                 StringComparison.OrdinalIgnoreCase));
        if (!enabled)
        {
            throw new ArgumentException(
                "--snapshot-url must request fresh=true or fresh=1.");
        }
    }
}
