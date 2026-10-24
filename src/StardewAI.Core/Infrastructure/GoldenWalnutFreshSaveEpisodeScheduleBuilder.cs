using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Infrastructure;

public static class GoldenWalnutFreshSaveEpisodeScheduleBuilder
{
    public const string ManifestSchemaVersion =
        "golden_walnut_fresh_save_episode_manifest.v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static GoldenWalnutFreshSaveSourceSchedule Build(string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(manifestPath))
            throw new ArgumentException("Episode manifest path is required.", nameof(manifestPath));

        var fullManifestPath = Path.GetFullPath(manifestPath);
        var manifest = ReadWithHash<GoldenWalnutFreshSaveEpisodeManifest>(
            fullManifestPath,
            out var manifestSha256);
        Require(
            manifest.SchemaVersion == ManifestSchemaVersion &&
            manifest.GoalId == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GoalId &&
            manifest.GameVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.GameVersion &&
            !manifest.TrainingLabelEligible &&
            !string.IsNullOrWhiteSpace(manifest.ScheduleId) &&
            !string.IsNullOrWhiteSpace(manifest.SourceRunId) &&
            manifest.EpisodePaths is { Length: > 0 },
            "Golden Walnut fresh-save episode manifest metadata is invalid.");

        var manifestDirectory = Path.GetDirectoryName(fullManifestPath) ??
            Directory.GetCurrentDirectory();
        var episodePaths = manifest.EpisodePaths
            .Select(path => ResolvePath(manifestDirectory, path))
            .ToArray();
        Require(
            episodePaths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == episodePaths.Length,
            "Golden Walnut fresh-save episode manifest contains duplicate paths.");

        string? saveId = null;
        string? previousAfterStateHash = null;
        long previousAfterTick = -1;
        int? firstIslandArrivalDay = null;
        int? firstParrotUnlockDay = null;
        int? westernTurtleUnlockDay = null;
        int? islandHouseUnlockDay = null;
        var sourceCompletions = new Dictionary<string, GoldenWalnutFreshSaveSourceCompletion>(
            StringComparer.Ordinal);
        var eligibleSources = GoldenWalnutNativeSourceCatalog.Sources
            .Where(IsEligibleDeterministicSource)
            .ToDictionary(source => source.SourceKey, StringComparer.Ordinal);
        var evidenceHashes = new List<string>();
        var firstEpisode = true;

        foreach (var episodePath in episodePaths)
        {
            var episode = ReadWithHash<PlanExecutionEpisodeEnvelope>(
                episodePath,
                out var episodeSha256);
            evidenceHashes.Add(episodeSha256);
            Require(
                episode.SchemaVersion == "plan_execution_episode.v1" &&
                episode.Success &&
                episode.AfterSnapshotFresh &&
                !string.IsNullOrWhiteSpace(episode.RunId),
                "Golden Walnut source schedule requires successful episodes with fresh after snapshots: " + episodePath);
            Require(manifest.SourceRunId == episode.RunId,
                "Golden Walnut source schedule cannot combine different run IDs.");
            Require(
                previousAfterStateHash is null ||
                previousAfterStateHash == episode.SourceStateHash,
                "Golden Walnut episode sequence has a state-hash gap: " + episodePath);
            Require(episode.BeforeGameTick >= previousAfterTick,
                "Golden Walnut episode sequence is not ordered by game tick: " + episodePath);
            Require(episode.AfterGameTick >= episode.BeforeGameTick,
                "Golden Walnut episode ends before it starts: " + episodePath);

            var episodeDirectory = Path.GetDirectoryName(episodePath) ?? manifestDirectory;
            var beforePath = ResolvePath(
                episodeDirectory,
                episode.BeforeSnapshotPath);
            var afterPath = ResolvePath(
                episodeDirectory,
                episode.AfterSnapshotPath);
            var before = ReadWithHash<SnapshotEnvelope>(beforePath, out var beforeSha256);
            var after = ReadWithHash<SnapshotEnvelope>(afterPath, out var afterSha256);
            evidenceHashes.Add(beforeSha256);
            evidenceHashes.Add(afterSha256);
            Require(
                before.GameVersion == manifest.GameVersion &&
                after.GameVersion == manifest.GameVersion &&
                before.StateHash == episode.SourceStateHash &&
                after.StateHash == episode.AfterStateHash,
                "Golden Walnut episode snapshot identity does not match its receipt: " + episodePath);
            Require(
                before.SaveId.Value is not null &&
                before.SaveId.Value == after.SaveId.Value,
                "Golden Walnut episode must bind one readable save identity: " + episodePath);
            saveId ??= before.SaveId.Value;
            Require(saveId == before.SaveId.Value,
                "Golden Walnut source schedule cannot combine different saves.");

            var beforeDay = ReadStateFieldInt(before, "time", "total_days");
            var afterDay = ReadStateFieldInt(after, "time", "total_days");
            Require(afterDay >= beforeDay,
                "Golden Walnut episode total day regressed: " + episodePath);

            var beforeAccess = ReadStateFieldValue(
                before, "locations", "ginger_island_access");
            var afterAccess = ReadStateFieldValue(
                after, "locations", "ginger_island_access");
            CaptureTransitionDay(
                beforeAccess, afterAccess, afterDay,
                new[] { "journey", "island_reached" },
                ref firstIslandArrivalDay);
            CaptureTransitionDay(
                beforeAccess, afterAccess, afterDay,
                new[] { "first_parrot", "complete_or_pending" },
                ref firstParrotUnlockDay);
            CaptureTransitionDay(
                beforeAccess, afterAccess, afterDay,
                new[] { "western_turtle_parrot", "complete_or_pending" },
                ref westernTurtleUnlockDay);
            CaptureTransitionDay(
                beforeAccess, afterAccess, afterDay,
                new[] { "island_house_parrot", "complete_or_pending" },
                ref islandHouseUnlockDay);

            var beforeSources = ReadWalnutSources(before);
            var afterSources = ReadWalnutSources(after);
            if (firstEpisode)
            {
                Require(!ReadNestedBool(beforeAccess, new[] { "journey", "island_reached" }) &&
                        !ReadNestedBool(beforeAccess, new[] { "first_parrot", "complete_or_pending" }) &&
                        !ReadNestedBool(beforeAccess, new[] { "western_turtle_parrot", "complete_or_pending" }) &&
                        !ReadNestedBool(beforeAccess, new[] { "island_house_parrot", "complete_or_pending" }) &&
                        eligibleSources.Keys.All(key => beforeSources.GetValueOrDefault(key) == 0),
                    "Golden Walnut episode manifest does not begin before Island progress or deterministic source collection.");
                firstEpisode = false;
            }
            foreach (var source in eligibleSources.Values)
            {
                if (!beforeSources.TryGetValue(source.SourceKey, out var beforeQuantity) ||
                    !afterSources.TryGetValue(source.SourceKey, out var afterQuantity))
                {
                    throw new InvalidDataException(
                        "Golden Walnut transparent source row is missing: " + source.SourceKey);
                }
                Require(afterQuantity >= beforeQuantity && afterQuantity <= source.NativeQuantity,
                    "Golden Walnut source quantity regressed or exceeded its native denominator: " + source.SourceKey);
                if (afterQuantity == beforeQuantity)
                    continue;

                // The schema has one row per source. Recording the latest observed total is
                // conservative when a limited-drop source advances on multiple days.
                sourceCompletions[source.SourceKey] = new GoldenWalnutFreshSaveSourceCompletion(
                    source.SourceKey,
                    afterQuantity,
                    afterDay);
            }

            previousAfterStateHash = episode.AfterStateHash;
            previousAfterTick = episode.AfterGameTick;
        }

        Require(firstIslandArrivalDay.HasValue,
            "Fresh-save episodes do not contain the native first-island-arrival transition.");
        Require(firstParrotUnlockDay.HasValue,
            "Fresh-save episodes do not contain the native First Parrot transition.");
        Require(westernTurtleUnlockDay.HasValue,
            "Fresh-save episodes do not contain the native Western Turtle transition.");
        Require(islandHouseUnlockDay.HasValue,
            "Fresh-save episodes do not contain the native Island House transition.");

        var arrivalDay = firstIslandArrivalDay.GetValueOrDefault();
        var firstParrotDay = firstParrotUnlockDay.GetValueOrDefault();
        var westernTurtleDay = westernTurtleUnlockDay.GetValueOrDefault();
        var islandHouseDay = islandHouseUnlockDay.GetValueOrDefault();
        return new GoldenWalnutFreshSaveSourceSchedule(
            GoldenWalnutFreshSaveUnlockTimelineProofBuilder.SourceSchemaVersion,
            manifest.ScheduleId,
            manifest.GoalId,
            manifest.GameVersion,
            false,
            manifestSha256,
            HashText(string.Join("\n", evidenceHashes)),
            manifest.SourceRunId,
            saveId!,
            episodePaths.Length,
            arrivalDay,
            firstParrotDay,
            westernTurtleDay,
            islandHouseDay,
            sourceCompletions.Values
                .OrderBy(row => row.PlannedCompletionTotalDay)
                .ThenBy(row => row.SourceKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static bool IsEligibleDeterministicSource(
        GoldenWalnutNativeSourceDefinition source) =>
        GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(
            source.AcquisitionOptionId) &&
        source.OptionalUnlockWalnutCost == 0 &&
        !source.ResortOnly &&
        source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal);

    private static Dictionary<string, int> ReadWalnutSources(SnapshotEnvelope snapshot)
    {
        var catalog = JsonSerializer.Deserialize<GoldenWalnutSourceCatalogRef>(
            ReadStateFieldValue(snapshot, "world_progress", "golden_walnut_sources")
                .GetRawText(),
            JsonOptions) ?? throw new InvalidDataException(
                "Golden Walnut transparent source catalog is unreadable.");
        return catalog.Sources.ToDictionary(
            source => source.SourceKey,
            source => source.CollectedQuantity,
            StringComparer.Ordinal);
    }

    private static int ReadStateFieldInt(
        SnapshotEnvelope snapshot,
        string domain,
        string key)
    {
        var value = ReadStateFieldValue(snapshot, domain, key);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw new InvalidDataException($"Snapshot field {domain}.{key} is not an integer.");
        return result;
    }

    private static JsonElement ReadStateFieldValue(
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
            throw new InvalidDataException($"Snapshot field {domain}.{key} is unavailable.");
        }
        return value;
    }

    private static void CaptureTransitionDay(
        JsonElement before,
        JsonElement after,
        int afterDay,
        IReadOnlyList<string> path,
        ref int? capturedDay)
    {
        var beforeValue = ReadNestedBool(before, path);
        var afterValue = ReadNestedBool(after, path);
        Require(!beforeValue || afterValue,
            "Ginger Island native progress regressed at " + string.Join(".", path) + ".");
        if (!beforeValue && afterValue)
        {
            Require(!capturedDay.HasValue,
                "Ginger Island native transition occurred more than once at " + string.Join(".", path) + ".");
            capturedDay = afterDay;
        }
    }

    private static bool ReadNestedBool(JsonElement root, IReadOnlyList<string> path)
    {
        var cursor = root;
        foreach (var segment in path)
        {
            if (cursor.ValueKind != JsonValueKind.Object ||
                !cursor.TryGetProperty(segment, out var child))
            {
                throw new InvalidDataException(
                    "Ginger Island transparent field is missing: " + string.Join(".", path));
            }
            cursor = child;
        }
        Require(cursor.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "Ginger Island transparent field is not boolean: " + string.Join(".", path));
        return cursor.GetBoolean();
    }

    private static string ResolvePath(string baseDirectory, string path)
    {
        Require(!string.IsNullOrWhiteSpace(path), "Artifact path is missing.");
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path));
    }

    private static T ReadWithHash<T>(string path, out string sha256)
    {
        Require(File.Exists(path), "Required artifact is missing: " + path);
        var bytes = Encoding.UTF8.GetBytes(
            ContentAddressedJsonArtifactReader.ReadAllText(path));
        using var hash = SHA256.Create();
        sha256 = Hex(hash.ComputeHash(bytes));
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions) ??
            throw new InvalidDataException("Artifact JSON is unreadable: " + path);
    }

    private static string HashText(string value)
    {
        using var sha256 = SHA256.Create();
        return Hex(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string Hex(byte[] value) =>
        BitConverter.ToString(value).Replace("-", string.Empty).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
