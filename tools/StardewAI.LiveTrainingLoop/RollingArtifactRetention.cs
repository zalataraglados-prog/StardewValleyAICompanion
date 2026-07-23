using System.Text.RegularExpressions;

namespace StardewAI.LiveTrainingLoop;

public static class RollingArtifactRetention
{
    private static readonly Regex ArtifactIterationPattern = new(
        @"^(?:before-snapshot|model-plan|daily-plan-response|ranking-response|model-action|compiled-queue|plan-execution-episode|execution|after-snapshot|replan-model-plan|replan-daily-plan-response|replan-compiled-queue|replan-ranking-response)-(?<iteration>[0-9]+)(?:-item-[0-9]+)?\.json$",
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
            nextIteration - retainedIterations);
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
                !int.TryParse(
                    match.Groups["iteration"].Value,
                    out var artifactIteration) ||
                artifactIteration >= minimumIterationToKeep)
            {
                continue;
            }

            File.Delete(fullPath);
        }

        return Directory.EnumerateFiles(
            fullSnapshotDirectory,
            "before-snapshot-*.json",
            SearchOption.TopDirectoryOnly).Count();
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
