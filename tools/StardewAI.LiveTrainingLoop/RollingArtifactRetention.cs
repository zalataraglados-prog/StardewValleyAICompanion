using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StardewAI.LiveTrainingLoop;

public static class RollingArtifactRetention
{
    public const string StateFileName = "artifact-retention-state.json";

    private static readonly Regex ArtifactIterationPattern = new(
        @"^(?:(?:before-snapshot|model-plan|daily-plan-response|ranking-response|model-action|compiled-queue|plan-execution-episode|execution|after-snapshot|dispatch-readiness|replan-model-plan|replan-daily-plan-response|replan-compiled-queue|replan-ranking-response|continuation-compiled-plan|continuation-compile-response|continuation-compiled-queue|continuation-refresh-evidence|candidate-compiled-plan|candidate-compile-response|candidate-compiled-queue|candidate-refresh-evidence|interrupt-compiled-plan|interrupt-compile-response|interrupt-compiled-queue|interrupt-ranking|native-save-boundary-plan|native-save-boundary-response|native-save-boundary-ranking|stage-one-collection-snapshot|stage-one-collection-intents|stage-one-collection-ranking|stage-one-collection-preference|stage-one-collection-compiled-plan|stage-one-collection-compiled-queue)-(?<iteration>[0-9]+)(?:-(?:item|dispatch|dispatch-recovery)-[0-9]+|-boundary-[0-9]+-item-[0-9]+)?|crop-teacher-[a-z0-9-]+-(?<crop_iteration>[0-9]+)(?:-[a-z0-9-]+)?)\.json$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static int Apply(
        string runDirectory,
        string snapshotDirectory,
        int retainedIterations,
        int nextIteration)
    {
        if (retainedIterations < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedIterations));
        }

        var fullSnapshotDirectory = Path.GetFullPath(snapshotDirectory);
        var fullRunDirectory = Path.GetFullPath(runDirectory);
        if (!IsStrictDescendant(fullSnapshotDirectory, fullRunDirectory) ||
            !string.Equals(
                Path.GetFileName(fullSnapshotDirectory),
                "live-snapshots",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "rolling_artifact_retention_snapshot_directory_not_scoped");
        }

        var minimumIterationToKeep = Math.Max(
            1,
            nextIteration - retainedIterations + 1);
        var deletedLogicalArtifactCount = 0;
        foreach (var path in Directory.EnumerateFiles(
            fullSnapshotDirectory,
            "*.json",
            SearchOption.TopDirectoryOnly))
        {
            var fullPath = Path.GetFullPath(path);
            if (!IsStrictDescendant(fullPath, fullSnapshotDirectory))
            {
                throw new InvalidOperationException(
                    "rolling_artifact_retention_file_not_scoped");
            }

            var match = ArtifactIterationPattern.Match(Path.GetFileName(fullPath));
            if (!match.Success ||
                !TryReadIteration(match, out var artifactIteration) ||
                artifactIteration >= minimumIterationToKeep)
            {
                continue;
            }

            File.Delete(fullPath);
            deletedLogicalArtifactCount++;
        }

        var retainedIterationCount = Directory.EnumerateFiles(
                fullSnapshotDirectory,
                "before-snapshot-*.json",
                SearchOption.TopDirectoryOnly)
            .Count(path => Regex.IsMatch(
                Path.GetFileName(path),
                @"^before-snapshot-[0-9]+\.json$",
                RegexOptions.CultureInvariant));
        var prune = ContentAddressedJsonArtifactRetention
            .PruneUnreferencedBlobs(fullSnapshotDirectory);
        WriteState(
            fullSnapshotDirectory,
            retainedIterations,
            nextIteration,
            minimumIterationToKeep,
            retainedIterationCount,
            deletedLogicalArtifactCount,
            prune);
        return retainedIterationCount;
    }

    private static bool TryReadIteration(Match match, out int iteration)
    {
        var value = match.Groups["iteration"].Success
            ? match.Groups["iteration"].Value
            : match.Groups["crop_iteration"].Value;
        return int.TryParse(value, out iteration);
    }

    private static void WriteState(
        string snapshotDirectory,
        int retainedIterations,
        int nextIteration,
        int minimumIterationToKeep,
        int retainedIterationCount,
        int deletedLogicalArtifactCount,
        ContentAddressedJsonArtifactPruneResult prune)
    {
        var statePath = Path.Combine(snapshotDirectory, StateFileName);
        var temporaryPath = statePath + ".tmp-" + Guid.NewGuid().ToString("N");
        var state = new
        {
            schema_version = "artifact_retention_state.v1",
            updated_at_utc = DateTimeOffset.UtcNow,
            retained_iteration_limit = retainedIterations,
            next_iteration = nextIteration,
            minimum_iteration_kept = minimumIterationToKeep,
            retained_iteration_count = retainedIterationCount,
            deleted_logical_artifact_count = deletedLogicalArtifactCount,
            blob_prune_completed = prune.Completed,
            referenced_blob_count = prune.ReferencedBlobCount,
            deleted_blob_count = prune.DeletedBlobCount,
            deleted_blob_bytes = prune.DeletedBytes,
            blob_prune_skip_reason = prune.SkipReason
        };
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    state,
                    new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool IsStrictDescendant(string path, string parent)
    {
        var relative = Path.GetRelativePath(parent, path);
        return relative.Length > 0 &&
            relative != "." &&
            !relative.StartsWith(
                ".." + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }
}
