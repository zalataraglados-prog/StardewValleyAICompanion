using StardewAI.LiveTrainingLoop;

partial class Program
{
    private const string BeforeSnapshotPrefix = "before-snapshot-";

    private static int NextArtifactIteration(string snapshotDirectory)
    {
        return Directory.EnumerateFiles(
                snapshotDirectory,
                BeforeSnapshotPrefix + "*.json",
                SearchOption.TopDirectoryOnly)
            .Select(TryReadPrimaryBeforeSnapshotIteration)
            .Where(iteration => iteration.HasValue)
            .Select(iteration => iteration!.Value)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static int CountPersistedIterations(string snapshotDirectory) =>
        Directory.EnumerateFiles(
                snapshotDirectory,
                BeforeSnapshotPrefix + "*.json",
                SearchOption.TopDirectoryOnly)
            .Count(path => TryReadPrimaryBeforeSnapshotIteration(path).HasValue);

    private static int? TryReadPrimaryBeforeSnapshotIteration(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith(BeforeSnapshotPrefix, StringComparison.Ordinal) &&
            int.TryParse(name[BeforeSnapshotPrefix.Length..], out var iteration)
                ? iteration
                : null;
    }

    private static string GetArtifactBudgetBlock(
        LiveTrainingOptions options,
        int persistedIterationCount)
    {
        if (options.ArtifactRetentionMode == "stop" &&
            persistedIterationCount >= options.MaxPersistedIterations)
        {
            return "max_persisted_iterations_reached count=" + persistedIterationCount +
                   " limit=" + options.MaxPersistedIterations;
        }

        var root = Path.GetPathRoot(Path.GetFullPath(options.SnapshotDir));
        if (string.IsNullOrWhiteSpace(root))
            return "snapshot_directory_root_unresolved";

        var availableBytes = new DriveInfo(root).AvailableFreeSpace;
        var requiredBytes = options.MinFreeSpaceMb * 1024L * 1024L;
        if (availableBytes < requiredBytes)
        {
            return "minimum_free_space_not_met available_bytes=" + availableBytes +
                   " required_bytes=" + requiredBytes;
        }

        return string.Empty;
    }

    private static int ApplyRollingArtifactRetention(
        LiveTrainingOptions options,
        int nextIteration)
    {
        if (options.ArtifactRetentionMode != "rolling")
            return CountPersistedIterations(options.SnapshotDir);

        return RollingArtifactRetention.Apply(
            options.RunDir,
            options.SnapshotDir,
            options.MaxPersistedIterations,
            nextIteration);
    }
}
