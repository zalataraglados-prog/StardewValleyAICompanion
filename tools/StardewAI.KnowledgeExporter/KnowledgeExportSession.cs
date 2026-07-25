using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.KnowledgeExporter;

internal sealed class KnowledgeExportSession
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static readonly JsonSerializerOptions ManifestOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true
    };

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string runDirectory;
    private readonly Queue<ExportWorkItem> pending = new();
    private readonly KnowledgeExportManifest manifest;
    private readonly RuntimeSemanticInventory runtimeSemantics = new();
    private string? lastAsset;

    public KnowledgeExportSession(
        IModHelper helper,
        IMonitor monitor,
        string outputRoot,
        IReadOnlyList<ContentFileRecord> contentFiles,
        bool exportDynamicStringAssets)
    {
        this.helper = helper;
        this.monitor = monitor;
        var runName = $"game-{SafeSegment(Game1.version)}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssZ}";
        runDirectory = Path.Combine(outputRoot, runName);
        Directory.CreateDirectory(runDirectory);

        manifest = new KnowledgeExportManifest
        {
            GameVersion = Game1.version,
            SmapiVersion = Constants.ApiVersion.ToString(),
            Locale = LocalizedContentManager.CurrentLanguageCode.ToString(),
            StartedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            RuntimeContentRoot = Path.GetFullPath(Path.Combine(Constants.GamePath, "Content")),
            ContentFiles = contentFiles.ToList()
        };

        EnqueueDataLoaderAssets();
        EnqueueMapAssets(contentFiles);
        if (exportDynamicStringAssets)
        {
            EnqueueDynamicStringAssets(contentFiles);
        }

        manifest.ExpectedExports = pending.Count;
        WriteProgress();
    }

    public bool IsComplete => pending.Count == 0;

    public string RunDirectory => runDirectory;

    public void ProcessNext()
    {
        if (!pending.TryDequeue(out var work))
        {
            return;
        }

        lastAsset = work.AssetName;
        try
        {
            var value = work.Load();
            var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(value, work.DeclaredType, PayloadOptions);
            runtimeSemantics.Inspect(work.AssetName, payloadBytes);
            var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();
            var outputFile = AssetFileName(work.AssetName);
            var outputPath = Path.Combine(runDirectory, outputFile);
            WritePayload(outputPath, work, value, payloadHash);
            var outputHash = HashFile(outputPath);
            manifest.Exports.Add(new ContentExportRecord(
                work.AssetName,
                work.SourceKind,
                work.Loader,
                work.DeclaredType.FullName ?? work.DeclaredType.Name,
                "available",
                EntryCount(value),
                outputFile,
                new FileInfo(outputPath).Length,
                outputHash,
                payloadHash,
                null));
        }
        catch (Exception ex)
        {
            var cause = ex is TargetInvocationException { InnerException: not null }
                ? ex.InnerException
                : ex;
            manifest.Exports.Add(new ContentExportRecord(
                work.AssetName,
                work.SourceKind,
                work.Loader,
                work.DeclaredType.FullName ?? work.DeclaredType.Name,
                "error",
                null,
                null,
                null,
                null,
                null,
                $"{cause.GetType().FullName}: {cause.Message}"));
            monitor.Log($"Knowledge export failed for {work.AssetName}: {cause}", LogLevel.Error);
        }

        WriteProgress();
    }

    public void Complete()
    {
        const string runtimeSemanticsFile = "runtime-semantics.json";
        var runtimeSemanticsPath = Path.Combine(runDirectory, runtimeSemanticsFile);
        WriteJsonAtomic(runtimeSemanticsPath, runtimeSemantics.BuildOutput());
        manifest.RuntimeSemanticsFile = runtimeSemanticsFile;
        manifest.RuntimeSemanticsSha256 = HashFile(runtimeSemanticsPath);
        manifest.Status = manifest.FailedExports == 0 ? "complete" : "partial";
        manifest.CompletedAtUtc = DateTimeOffset.UtcNow.ToString("O");
        WriteJsonAtomic(Path.Combine(runDirectory, "manifest.json"), manifest);
        var progressPath = Path.Combine(runDirectory, "progress.json");
        if (File.Exists(progressPath))
        {
            File.Move(progressPath, Path.Combine(runDirectory, "progress.complete.json"), overwrite: true);
        }
    }

    private void EnqueueDataLoaderAssets()
    {
        var methods = typeof(DataLoader)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType != typeof(void))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 1 && parameters[0].ParameterType == typeof(LocalizedContentManager);
            })
            .OrderBy(method => method.Name, StringComparer.Ordinal);

        foreach (var method in methods)
        {
            var captured = method;
            pending.Enqueue(new ExportWorkItem(
                AssetNameForDataLoader(captured.Name),
                "runtime_game_content_dataloader",
                $"StardewValley.DataLoader.{captured.Name}",
                captured.ReturnType,
                () => captured.Invoke(null, new object[] { Game1.content })));
        }
    }

    private void EnqueueDynamicStringAssets(IEnumerable<ContentFileRecord> contentFiles)
    {
        foreach (var file in contentFiles
                     .Where(file => IsDynamicStringAsset(file.AssetName))
                     .Where(file => !IsLocalizedVariant(file.AssetName))
                     .Where(file => !pending.Any(work => string.Equals(
                         work.AssetName,
                         file.AssetName,
                         StringComparison.OrdinalIgnoreCase)))
                     .OrderBy(file => file.AssetName, StringComparer.OrdinalIgnoreCase))
        {
            var assetName = file.AssetName;
            pending.Enqueue(new ExportWorkItem(
                assetName,
                "runtime_game_content_dynamic_family",
                "IModHelper.GameContent.Load<Dictionary<string,string>>",
                typeof(Dictionary<string, string>),
                () => helper.GameContent.Load<Dictionary<string, string>>(assetName)));
        }
    }

    private void EnqueueMapAssets(IEnumerable<ContentFileRecord> contentFiles)
    {
        foreach (var file in contentFiles
                     .Where(file => file.AssetName.StartsWith("Maps/", StringComparison.OrdinalIgnoreCase))
                     .Where(file => !IsLocalizedVariant(file.AssetName))
                     .OrderBy(file => file.AssetName, StringComparer.OrdinalIgnoreCase))
        {
            var assetName = file.AssetName;
            pending.Enqueue(new ExportWorkItem(
                assetName,
                "runtime_game_content_map_projection",
                "IModHelper.GameContent.Load<object> with runtime type classification",
                typeof(RuntimeMapAssetProjection),
                () => MapProjection.Build(assetName, helper.GameContent.Load<object>(assetName))));
        }
    }

    private void WritePayload(string outputPath, ExportWorkItem work, object? value, string payloadHash)
    {
        var tempPath = outputPath + ".tmp";
        using (var stream = File.Create(tempPath))
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", "stardewai.knowledge_asset.v1");
            writer.WriteString("asset_name", work.AssetName);
            writer.WriteString("source_kind", work.SourceKind);
            writer.WriteString("loader", work.Loader);
            writer.WriteString("declared_type", work.DeclaredType.FullName ?? work.DeclaredType.Name);
            writer.WriteString("payload_sha256", payloadHash);
            writer.WritePropertyName("payload");
            JsonSerializer.Serialize(writer, value, work.DeclaredType, PayloadOptions);
            writer.WriteEndObject();
        }

        File.Move(tempPath, outputPath, overwrite: true);
    }

    private void WriteProgress()
    {
        WriteJsonAtomic(Path.Combine(runDirectory, "progress.json"), new KnowledgeExportProgress
        {
            Processed = manifest.Exports.Count,
            Total = manifest.ExpectedExports,
            Failures = manifest.FailedExports,
            LastAsset = lastAsset,
            UpdatedAtUtc = DateTimeOffset.UtcNow.ToString("O")
        });
    }

    private static void WriteJsonAtomic<T>(string path, T value)
    {
        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, JsonSerializer.SerializeToUtf8Bytes(value, ManifestOptions));
        File.Move(tempPath, path, overwrite: true);
    }

    private static int? EntryCount(object? value)
    {
        return value switch
        {
            null => 0,
            string => 1,
            ICollection collection => collection.Count,
            _ => value.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance)?.GetValue(value) as int?
        };
    }

    private static bool IsDynamicStringAsset(string assetName)
    {
        return assetName.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("Data/Festivals/", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("Data/Dialogue/", StringComparison.OrdinalIgnoreCase) ||
               assetName.Equals("Data/ExtraDialogue", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("Characters/Dialogue/", StringComparison.OrdinalIgnoreCase) ||
               assetName.StartsWith("Characters/schedules/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalizedVariant(string assetName)
    {
        var fileName = Path.GetFileName(assetName);
        if (fileName.Length < 6 || fileName[^6] != '.')
        {
            return false;
        }

        return char.IsLower(fileName[^5]) &&
               char.IsLower(fileName[^4]) &&
               fileName[^3] == '-' &&
               char.IsUpper(fileName[^2]) &&
               char.IsUpper(fileName[^1]);
    }

    private static string AssetNameForDataLoader(string methodName)
    {
        return methodName switch
        {
            "AnimationDescriptions" => "Data/animationDescriptions",
            "Hats" => "Data/hats",
            "Mail" => "Data/mail",
            "NpcGiftTastes" => "Data/NPCGiftTastes",
            _ when methodName.StartsWith("Festivals_", StringComparison.Ordinal) =>
                "Data/Festivals/" + methodName["Festivals_".Length..],
            _ when methodName.StartsWith("Tv_", StringComparison.Ordinal) =>
                "Data/TV/" + methodName["Tv_".Length..],
            _ => "Data/" + methodName
        };
    }

    private static string SafeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return string.Concat(value.Select(character =>
            invalid.Contains(character) || character is '/' or '\\' ? '_' : character));
    }

    private static string AssetFileName(string assetName)
    {
        var nameHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(assetName)))
            .ToLowerInvariant()[..12];
        return $"{SafeSegment(assetName)}-{nameHash}.json";
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
    }

    private sealed record ExportWorkItem(
        string AssetName,
        string SourceKind,
        string Loader,
        Type DeclaredType,
        Func<object?> Load);
}
