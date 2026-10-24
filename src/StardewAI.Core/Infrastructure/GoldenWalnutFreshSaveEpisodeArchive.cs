using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutFreshSaveEpisodeArchiveResult(
    bool Captured,
    string ArchiveEpisodePath);

public static class GoldenWalnutFreshSaveEpisodeArchive
{
    public const string EpisodeSearchPattern =
        "golden-walnut-fresh-save-episode-*.json";

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static GoldenWalnutFreshSaveEpisodeArchiveResult Capture(
        string episodePath,
        int iteration,
        string runId)
    {
        if (string.IsNullOrWhiteSpace(episodePath))
            throw new ArgumentException("Episode path is required.", nameof(episodePath));
        if (iteration < 1)
            throw new ArgumentOutOfRangeException(nameof(iteration));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));

        var fullEpisodePath = Path.GetFullPath(episodePath);
        Require(File.Exists(fullEpisodePath),
            "Golden Walnut source episode is missing: " + fullEpisodePath);
        var episode = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(
                          File.ReadAllText(fullEpisodePath),
                          JsonOptions) ??
                      throw new InvalidDataException(
                          "Golden Walnut source episode is empty: " + fullEpisodePath);
        Require(episode.SchemaVersion == "plan_execution_episode.v1" &&
                episode.RunId == runId &&
                !string.IsNullOrWhiteSpace(episode.EpisodeId) &&
                !string.IsNullOrWhiteSpace(episode.SourceStateHash) &&
                !string.IsNullOrWhiteSpace(episode.AfterStateHash),
            "Golden Walnut source episode identity is invalid: " + fullEpisodePath);
        if (!episode.Success || !episode.AfterSnapshotFresh)
            return new GoldenWalnutFreshSaveEpisodeArchiveResult(false, string.Empty);

        var directory = Path.GetDirectoryName(fullEpisodePath) ??
            throw new InvalidDataException(
                "Golden Walnut source episode directory is unavailable.");
        var beforePath = ResolveArtifact(directory, episode.BeforeSnapshotPath);
        var afterPath = ResolveArtifact(directory, episode.AfterSnapshotPath);
        Require(File.Exists(beforePath) && File.Exists(afterPath),
            "Golden Walnut source episode snapshot is missing: " + fullEpisodePath);
        Require(SameDirectory(directory, beforePath) && SameDirectory(directory, afterPath),
            "Golden Walnut durable aliases require episode snapshots in one artifact directory.");

        var suffix = iteration.ToString("D8") + "-" +
            Sha256(Encoding.UTF8.GetBytes(episode.EpisodeId))[..12];
        var archiveBeforePath = Path.Combine(
            directory,
            "golden-walnut-fresh-save-before-" + suffix + ".json");
        var archiveAfterPath = Path.Combine(
            directory,
            "golden-walnut-fresh-save-after-" + suffix + ".json");
        var archiveEpisodePath = Path.Combine(
            directory,
            "golden-walnut-fresh-save-episode-" + suffix + ".json");

        CopyImmutable(beforePath, archiveBeforePath);
        CopyImmutable(afterPath, archiveAfterPath);
        episode.BeforeSnapshotPath = Path.GetFileName(archiveBeforePath);
        episode.AfterSnapshotPath = Path.GetFileName(archiveAfterPath);
        WriteImmutable(
            archiveEpisodePath,
            JsonSerializer.Serialize(episode, JsonOptions));
        return new GoldenWalnutFreshSaveEpisodeArchiveResult(
            true,
            archiveEpisodePath);
    }

    private static string ResolveArtifact(string directory, string path)
    {
        Require(!string.IsNullOrWhiteSpace(path),
            "Golden Walnut source episode artifact path is missing.");
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(directory, path));
    }

    private static bool SameDirectory(string directory, string path) =>
        string.Equals(
            Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
            (Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static void CopyImmutable(string sourcePath, string targetPath) =>
        WriteImmutable(targetPath, File.ReadAllBytes(sourcePath));

    private static void WriteImmutable(string path, string content) =>
        WriteImmutable(path, Utf8WithoutBom.GetBytes(content));

    private static void WriteImmutable(string path, byte[] content)
    {
        if (File.Exists(path))
        {
            Require(
                CryptographicOperations.FixedTimeEquals(
                    Hash(File.ReadAllBytes(path)),
                    Hash(content)),
                "Golden Walnut durable artifact changed after capture: " + path);
            return;
        }

        var temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string Sha256(byte[] value) =>
        BitConverter.ToString(Hash(value))
            .Replace("-", string.Empty)
            .ToLowerInvariant();

    private static byte[] Hash(byte[] value)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
