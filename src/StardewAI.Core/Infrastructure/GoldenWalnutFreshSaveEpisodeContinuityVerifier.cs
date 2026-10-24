using System;
using System.IO;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public static class GoldenWalnutFreshSaveEpisodeContinuityVerifier
{
    public const long MaximumPassiveGapTicks = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static void Validate(
        string previousEpisodePath,
        PlanExecutionEpisodeEnvelope previousEpisode,
        string currentEpisodePath,
        PlanExecutionEpisodeEnvelope currentEpisode)
    {
        if (previousEpisode.AfterStateHash == currentEpisode.SourceStateHash)
        {
            return;
        }

        var previousAfter = ReadSnapshot(
            previousEpisodePath,
            previousEpisode.AfterSnapshotPath);
        var currentBefore = ReadSnapshot(
            currentEpisodePath,
            currentEpisode.BeforeSnapshotPath);
        Validate(
            previousEpisodePath,
            previousEpisode,
            previousAfter,
            currentEpisodePath,
            currentEpisode,
            currentBefore);
    }

    public static void Validate(
        string previousEpisodePath,
        PlanExecutionEpisodeEnvelope previousEpisode,
        SnapshotEnvelope previousAfter,
        string currentEpisodePath,
        PlanExecutionEpisodeEnvelope currentEpisode,
        SnapshotEnvelope currentBefore)
    {
        if (previousEpisode.AfterStateHash == currentEpisode.SourceStateHash)
        {
            return;
        }

        var gapTicks = currentEpisode.BeforeGameTick -
            previousEpisode.AfterGameTick;
        Require(
            gapTicks >= 0 && gapTicks <= MaximumPassiveGapTicks,
            "Golden Walnut passive continuity gap exceeds the bounded " +
            "observation window: " + currentEpisodePath);
        Require(
            previousAfter.StateHash == previousEpisode.AfterStateHash &&
            currentBefore.StateHash == currentEpisode.SourceStateHash,
            "Golden Walnut passive continuity snapshot identity is invalid: " +
            currentEpisodePath);
        Require(
            !string.IsNullOrWhiteSpace(previousAfter.SaveId.Value) &&
            previousAfter.SaveId.Value == currentBefore.SaveId.Value &&
            !string.IsNullOrWhiteSpace(previousAfter.PlayerId.Value) &&
            previousAfter.PlayerId.Value == currentBefore.PlayerId.Value,
            "Golden Walnut passive continuity crossed a save or player " +
            "identity: " + currentEpisodePath);
        Require(
            ReadStateValue(previousAfter, "time", "total_days").GetInt32() ==
                ReadStateValue(currentBefore, "time", "total_days").GetInt32() &&
            previousAfter.InGameTime.Value == currentBefore.InGameTime.Value,
            "Golden Walnut passive continuity crossed game time: " +
            currentEpisodePath);
        Require(
            SameJsonValue(
                ReadStateValue(
                    previousAfter,
                    "locations",
                    "ginger_island_access"),
                ReadStateValue(
                    currentBefore,
                    "locations",
                    "ginger_island_access")) &&
            SameJsonValue(
                ReadStateValue(
                    previousAfter,
                    "world_progress",
                    "golden_walnut_sources"),
                ReadStateValue(
                    currentBefore,
                    "world_progress",
                    "golden_walnut_sources")),
            "Golden Walnut passive continuity contains unreceipted island " +
            "progress: " + currentEpisodePath);
    }

    private static SnapshotEnvelope ReadSnapshot(
        string episodePath,
        string artifactPath)
    {
        Require(
            !string.IsNullOrWhiteSpace(artifactPath),
            "Golden Walnut passive continuity snapshot path is missing: " +
            episodePath);
        var fullPath = Path.GetFullPath(
            Path.IsPathRooted(artifactPath)
                ? artifactPath
                : Path.Combine(
                    Path.GetDirectoryName(episodePath) ??
                        Directory.GetCurrentDirectory(),
                    artifactPath));
        Require(
            File.Exists(fullPath),
            "Golden Walnut passive continuity snapshot is missing: " +
            fullPath);
        return JsonSerializer.Deserialize<SnapshotEnvelope>(
                   ContentAddressedJsonArtifactReader.ReadAllText(fullPath),
                   JsonOptions) ??
               throw new InvalidDataException(
                   "Golden Walnut passive continuity snapshot is unreadable: " +
                   fullPath);
    }

    private static JsonElement ReadStateValue(
        SnapshotEnvelope snapshot,
        string domain,
        string key)
    {
        if (!snapshot.State.TryGetValue(domain, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty(key, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("value", out var value))
        {
            throw new InvalidDataException(
                $"Golden Walnut passive continuity field is unavailable: " +
                $"{domain}.{key}.");
        }
        return value;
    }

    private static bool SameJsonValue(JsonElement left, JsonElement right) =>
        left.GetRawText() == right.GetRawText();

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
