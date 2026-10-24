using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutFreshSaveEpisodeManifest(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("schedule_id")] string ScheduleId,
    [property: JsonPropertyName("goal_id")] string GoalId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("training_label_eligible")] bool TrainingLabelEligible,
    [property: JsonPropertyName("source_run_id")] string SourceRunId,
    [property: JsonPropertyName("episode_paths")] string[] EpisodePaths);

public static class GoldenWalnutFreshSaveEpisodeManifestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static GoldenWalnutFreshSaveEpisodeManifest BuildFromDirectory(
        string episodeDirectory,
        string scheduleId,
        string runId)
    {
        if (string.IsNullOrWhiteSpace(episodeDirectory))
            throw new ArgumentException("Episode directory is required.", nameof(episodeDirectory));
        if (string.IsNullOrWhiteSpace(scheduleId))
            throw new ArgumentException("Schedule ID is required.", nameof(scheduleId));
        if (string.IsNullOrWhiteSpace(runId))
            throw new ArgumentException("Run ID is required.", nameof(runId));

        var fullDirectory = Path.GetFullPath(episodeDirectory);
        Require(Directory.Exists(fullDirectory),
            "Golden Walnut episode directory is missing: " + fullDirectory);
        var archivedPaths = Directory.EnumerateFiles(
                fullDirectory,
                GoldenWalnutFreshSaveEpisodeArchive.EpisodeSearchPattern,
                SearchOption.TopDirectoryOnly)
            .ToArray();
        var episodePaths = archivedPaths.Length > 0
            ? archivedPaths
            : Directory.EnumerateFiles(
                fullDirectory,
                "plan-execution-episode-*.json",
                SearchOption.TopDirectoryOnly);
        var rows = episodePaths
            .Select(path => new EpisodeRow(
                Path.GetFullPath(path),
                ReadEpisode(path)))
            .Where(row =>
                row.Episode.RunId == runId &&
                row.Episode.Success &&
                row.Episode.AfterSnapshotFresh)
            .OrderBy(row => row.Episode.BeforeGameTick)
            .ThenBy(row => row.Episode.AfterGameTick)
            .ThenBy(row => row.Path, StringComparer.Ordinal)
            .ToArray();
        Require(rows.Length > 0,
            "No successful fresh episodes exist for run ID: " + runId);

        string? previousAfterStateHash = null;
        long previousAfterTick = -1;
        var episodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var episode = row.Episode;
            Require(
                episode.SchemaVersion == "plan_execution_episode.v1" &&
                !string.IsNullOrWhiteSpace(episode.EpisodeId) &&
                episodeIds.Add(episode.EpisodeId) &&
                !string.IsNullOrWhiteSpace(episode.SourceStateHash) &&
                !string.IsNullOrWhiteSpace(episode.AfterStateHash) &&
                !string.IsNullOrWhiteSpace(episode.BeforeSnapshotPath) &&
                !string.IsNullOrWhiteSpace(episode.AfterSnapshotPath),
                "Golden Walnut run contains an invalid or duplicate episode: " + row.Path);
            Require(
                previousAfterStateHash is null ||
                previousAfterStateHash == episode.SourceStateHash,
                "Golden Walnut run contains a state-hash gap: " + row.Path);
            Require(
                episode.BeforeGameTick >= previousAfterTick &&
                episode.AfterGameTick >= episode.BeforeGameTick,
                "Golden Walnut run episode ticks are not monotonic: " + row.Path);
            Require(File.Exists(ResolveEpisodeArtifact(row.Path, episode.BeforeSnapshotPath)) &&
                    File.Exists(ResolveEpisodeArtifact(row.Path, episode.AfterSnapshotPath)),
                "Golden Walnut run episode snapshot is missing: " + row.Path);
            previousAfterStateHash = episode.AfterStateHash;
            previousAfterTick = episode.AfterGameTick;
        }

        return new GoldenWalnutFreshSaveEpisodeManifest(
            GoldenWalnutFreshSaveEpisodeScheduleBuilder.ManifestSchemaVersion,
            scheduleId,
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GoalId,
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion,
            false,
            runId,
            rows.Select(row => row.Path).ToArray());
    }

    public static GoldenWalnutFreshSaveEpisodeManifest WriteFromDirectory(
        string outputPath,
        string episodeDirectory,
        string scheduleId,
        string runId)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("Manifest output path is required.", nameof(outputPath));
        var manifest = BuildFromDirectory(episodeDirectory, scheduleId, runId);
        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath) ??
            Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDirectory);
        var temporaryPath = fullOutputPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(fullOutputPath))
                File.Replace(temporaryPath, fullOutputPath, null);
            else
                File.Move(temporaryPath, fullOutputPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return manifest;
    }

    private static PlanExecutionEpisodeEnvelope ReadEpisode(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(
                       File.ReadAllText(path),
                       JsonOptions) ??
                   throw new InvalidDataException("Episode JSON is empty: " + path);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Episode JSON is invalid: " + path, exception);
        }
    }

    private static string ResolveEpisodeArtifact(
        string episodePath,
        string artifactPath) => Path.GetFullPath(
        Path.IsPathRooted(artifactPath)
            ? artifactPath
            : Path.Combine(
                Path.GetDirectoryName(episodePath) ?? Directory.GetCurrentDirectory(),
                artifactPath));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record EpisodeRow(
        string Path,
        PlanExecutionEpisodeEnvelope Episode);
}
