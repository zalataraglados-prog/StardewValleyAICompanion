namespace StardewAI.ProductExecutor;

public sealed class ProductExecutorOptions
{
    public string ListenUrl { get; init; } = "http://127.0.0.1:8768";
    public string NativeExecutorUrl { get; init; } = "http://127.0.0.1:8767";
    public string BridgeSnapshotUrl { get; init; } =
        "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1";
    public string JournalRoot { get; init; } = @"E:\StardewAITraining\product-executor";
    public string AllowedSaveRoot { get; init; } =
        @"E:\StardewValleyAICompanion-runtime\saves";
    public string RequiredRunId { get; init; } = string.Empty;
    public int MaxRequestAgeSeconds { get; init; } = 900;
    public int NativeTimeoutSeconds { get; init; } = 600;

    public static ProductExecutorOptions FromEnvironment() => new()
    {
        ListenUrl = Read("STARDEWAI_PRODUCT_EXECUTOR_URL", "http://127.0.0.1:8768"),
        NativeExecutorUrl = Read("STARDEWAI_NATIVE_EXECUTOR_URL", "http://127.0.0.1:8767"),
        BridgeSnapshotUrl = Read(
            "STARDEWAI_BRIDGE_SNAPSHOT_URL",
            "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"),
        JournalRoot = Read(
            "STARDEWAI_PRODUCT_JOURNAL_ROOT",
            @"E:\StardewAITraining\product-executor"),
        AllowedSaveRoot = Read(
            "STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT",
            @"E:\StardewValleyAICompanion-runtime\saves"),
        RequiredRunId = Read("STARDEWAI_PRODUCT_RUN_ID", string.Empty),
        MaxRequestAgeSeconds = ReadInt("STARDEWAI_PRODUCT_MAX_REQUEST_AGE_SECONDS", 900, 30, 86400),
        NativeTimeoutSeconds = ReadInt("STARDEWAI_PRODUCT_NATIVE_TIMEOUT_SECONDS", 600, 30, 3600)
    };

    public void Validate()
    {
        ValidateLoopbackUrl(ListenUrl, nameof(ListenUrl));
        ValidateLoopbackUrl(NativeExecutorUrl, nameof(NativeExecutorUrl));
        ValidateLoopbackUrl(BridgeSnapshotUrl, nameof(BridgeSnapshotUrl));
        ValidateDataPath(JournalRoot, nameof(JournalRoot));
        ValidateDataPath(AllowedSaveRoot, nameof(AllowedSaveRoot));
    }

    private static void ValidateLoopbackUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !uri.IsLoopback)
        {
            throw new InvalidOperationException(name + " must be an absolute loopback HTTP URL.");
        }
    }

    private static void ValidateDataPath(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException(name + " must be an absolute path.");
        var full = Path.GetFullPath(value);
        var root = Path.GetPathRoot(full);
        if (string.Equals(
            full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            root?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(name + " cannot be a drive root.");
        }
    }

    private static string Read(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static int ReadInt(string name, int fallback, int minimum, int maximum) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var value)
            ? Math.Clamp(value, minimum, maximum)
            : fallback;
}
