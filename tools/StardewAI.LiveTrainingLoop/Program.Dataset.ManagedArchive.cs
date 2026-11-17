using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

static partial class Program
{
    private static GoldenWalnutFreshSaveEpisodeArchiveResult CaptureGoldenWalnutPlanExecutionEpisode(
        LiveTrainingOptions options, string episodePath, int iteration)
    {
        if (!options.UseManagedCropTeacher || iteration < 1)
            return GoldenWalnutFreshSaveEpisodeArchive.Capture(episodePath, iteration, options.RunId);

        var original = File.ReadAllText(episodePath);
        var episode = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(original, JsonOptions)
            ?? throw new InvalidDataException("Managed archive episode is empty.");
        if (!episode.AfterSnapshotFresh || episode.SchemaVersion != "plan_execution_episode.v1" ||
            episode.RunId != options.RunId || string.IsNullOrWhiteSpace(episode.EpisodeId) ||
            string.IsNullOrWhiteSpace(episode.SourceStateHash) || string.IsNullOrWhiteSpace(episode.AfterStateHash))
            return GoldenWalnutFreshSaveEpisodeArchive.Capture(episodePath, iteration, options.RunId);
        var root = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(episodePath))!);
        var before = ResolveManagedEpisodeArtifact(root, episode.BeforeSnapshotPath);
        var after = ResolveManagedEpisodeArtifact(root, episode.AfterSnapshotPath);
        var receipt = string.IsNullOrWhiteSpace(episode.ExecutionResultPath) ? null :
            ResolveManagedEpisodeArtifact(root, episode.ExecutionResultPath);
        if (SameManagedArchivePath(Path.GetDirectoryName(before)!, root) ||
            !SameManagedArchivePath(Path.GetDirectoryName(after)!, root) ||
            !File.Exists(after) || receipt is not null &&
                (!SameManagedArchivePath(Path.GetDirectoryName(receipt)!, root) || !File.Exists(receipt)))
            return GoldenWalnutFreshSaveEpisodeArchive.Capture(episodePath, iteration, options.RunId);

        if (!SameManagedArchivePath(root, options.SnapshotDir))
            throw new InvalidDataException("Managed archive input is outside its original snapshot directory.");
        if ((File.GetAttributes(episodePath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Managed archive episode must not be a reparse point.");
        RequireManagedArchiveDescendant(before, root);
        var episodeHash = PortfolioWireHash(File.ReadAllBytes(episodePath));
        var sourceHash = PortfolioWireHash(File.ReadAllBytes(before));
        var text = ContentAddressedJsonArtifactReader.ReadAllText(before);
        var logicalHash = PortfolioWireHash(System.Text.Encoding.UTF8.GetBytes(text));
        var alias = Path.Combine(root, "golden-walnut-managed-before-" + logicalHash + ".json");
        // A plain alias preserves even noncanonical logical bytes; gzip writing would normalize JSON.
        WriteManagedArchiveInputImmutable(alias, text);
        var input = JsonNode.Parse(original)!.AsObject();
        input["before_snapshot_path"] = alias;
        input["managed_archive_source_reference"] = new JsonObject
        {
            ["schema_version"] = "managed_episode_archive_source.v1",
            ["original_episode_path"] = Path.GetFullPath(episodePath), ["original_episode_sha256"] = episodeHash,
            ["original_before_snapshot_path"] = before, ["original_before_file_sha256"] = sourceHash,
            ["before_logical_sha256"] = logicalHash, ["snapshot_alias_path"] = alias,
            ["native_dispatch_performed"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false
        };
        var inputPath = Path.Combine(root, "golden-walnut-managed-input-" + episodeHash + ".json");
        WriteManagedArchiveInputImmutable(inputPath, input.ToJsonString(JsonOptions));
        if (File.ReadAllText(episodePath) != original || PortfolioWireHash(File.ReadAllBytes(episodePath)) != episodeHash ||
            PortfolioWireHash(File.ReadAllBytes(before)) != sourceHash ||
            ContentAddressedJsonArtifactReader.ReadAllText(before) != text)
            throw new InvalidDataException("Managed archive original input changed during alias capture.");
        return GoldenWalnutFreshSaveEpisodeArchive.Capture(inputPath, iteration, options.RunId);
    }

    private static string ResolveManagedEpisodeArtifact(string root, string path) =>
        string.IsNullOrWhiteSpace(path) ? throw new InvalidDataException("Managed archive artifact path is missing.") :
        Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));

    private static bool SameManagedArchivePath(string first, string second) => string.Equals(
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void RequireManagedArchiveDescendant(string path, string root)
    {
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || !File.Exists(path))
            throw new InvalidDataException("Managed archive source must be an existing descendant of its original snapshot directory.");
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Managed archive source must not traverse a reparse point.");
            if (SameManagedArchivePath(current, root)) break;
        }
    }

    private static void WriteManagedArchiveInputImmutable(string path, string text)
    {
        if (File.Exists(path))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || File.ReadAllText(path) != text)
                throw new InvalidDataException("Managed archive alias/input changed after capture: " + path);
            return;
        }
        WritePortfolioJournalFileAsync(path, text).GetAwaiter().GetResult();
    }
}
