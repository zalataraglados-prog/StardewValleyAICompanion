partial class Program
{
    private static int NextArtifactIteration(string snapshotDirectory)
    {
        const string prefix = "before-snapshot-";
        return Directory.EnumerateFiles(
                snapshotDirectory,
                prefix + "*.json",
                SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null && name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => int.TryParse(name![prefix.Length..], out var iteration) ? iteration : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private static string GetArtifactBudgetBlock(
        LiveTrainingOptions options,
        int persistedIterationCount)
    {
        if (persistedIterationCount >= options.MaxPersistedIterations)
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
}
