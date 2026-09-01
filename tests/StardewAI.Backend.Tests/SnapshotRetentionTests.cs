using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;
using Xunit;

namespace StardewAI.Backend.Tests;

public sealed class SnapshotRetentionTests
{
    [Fact]
    public void SnapshotStoreEvictsOldestBeforeParsingNextSnapshot()
    {
        var store = new StateStore();

        store.StoreSnapshot(Snapshot("state-1", 1));
        store.StoreSnapshot(Snapshot("state-2", 2));
        store.PrepareForSnapshotIngest();

        Assert.DoesNotContain("state-1", store.Snapshots.Keys);
        Assert.Contains("state-2", store.Snapshots.Keys);

        store.StoreSnapshot(Snapshot("state-3", 3));

        Assert.Equal(2, store.Snapshots.Count);
        Assert.Equal("state-3", store.LatestSnapshot()!.StateHash);
    }

    [Fact]
    public void LiveLoopArtifactBudgetSurvivesProcessRestart()
    {
        var source = LiveTrainingLoopSources.All;

        Assert.Contains("NextArtifactIteration(options.SnapshotDir)", source, StringComparison.Ordinal);
        Assert.Contains("nextArtifactIteration + attemptOrdinal - 1", source, StringComparison.Ordinal);
        Assert.Contains("MaxPersistedIterations { get; set; } = 64", source, StringComparison.Ordinal);
        Assert.Contains("ArtifactRetentionMode { get; set; } = \"stop\"", source, StringComparison.Ordinal);
        Assert.Contains("ApplyRollingArtifactRetention(options, iteration)", source, StringComparison.Ordinal);
        Assert.Contains("RollingArtifactRetention.Apply(", source, StringComparison.Ordinal);
        Assert.Contains("MinFreeSpaceMb { get; set; } = 8192", source, StringComparison.Ordinal);

        var launcher = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "StardewAI.Core",
            "Training",
            "StardewTrainingSessionLauncher.cs"));
        Assert.Contains(
            "\"--min-free-space-mb\", Math.Max(",
            launcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--max-persisted-iterations\", manifest.MaxPersistedIterations.ToString()",
            launcher,
            StringComparison.Ordinal);

        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "start-formal-product-training-attached.sh"));
        Assert.Contains(
            "STARDEWAI_FORMAL_MIN_FREE_SPACE_MB:-8192",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"min_free_space_mb\": $MIN_FREE_SPACE_MB",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_FORMAL_MAX_PERSISTED_ITERATIONS:-4",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"max_persisted_iterations\": $MAX_PERSISTED_ITERATIONS",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "formal training executable bit is missing",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "[ ! -x \"$executable\" ]",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RollingRetentionDeletesOnlyOldIterationFamilies()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-retention-" + Guid.NewGuid().ToString("N"));
        var run = Path.Combine(root, "run");
        var snapshots = Path.Combine(run, "live-snapshots");
        Directory.CreateDirectory(snapshots);
        var files = new[]
        {
            "before-snapshot-0001.json",
            "after-snapshot-0001-item-001.json",
            "before-snapshot-0002.json",
            "compiled-queue-0002.json",
            "before-snapshot-0003.json",
            "unrelated.json",
            "foreign-0001.json"
        };
        try
        {
            foreach (var file in files)
            {
                File.WriteAllText(Path.Combine(snapshots, file), "{}");
            }

            var retained = RollingArtifactRetention.Apply(
                run,
                snapshots,
                retainedIterations: 2,
                nextIteration: 4);

            Assert.Equal(1, retained);
            Assert.False(File.Exists(Path.Combine(snapshots, files[0])));
            Assert.False(File.Exists(Path.Combine(snapshots, files[1])));
            Assert.False(File.Exists(Path.Combine(snapshots, files[2])));
            Assert.False(File.Exists(Path.Combine(snapshots, files[3])));
            Assert.True(File.Exists(Path.Combine(snapshots, files[4])));
            Assert.True(File.Exists(Path.Combine(snapshots, files[5])));
            Assert.True(File.Exists(Path.Combine(snapshots, files[6])));
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(snapshots))
            {
                File.Delete(file);
            }
            Directory.Delete(snapshots);
            Directory.Delete(run);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void RollingRetentionRejectsUnscopedDirectory()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-retention-" + Guid.NewGuid().ToString("N"));
        var run = Path.Combine(root, "run");
        var wrong = Path.Combine(run, "other");
        Directory.CreateDirectory(wrong);
        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                RollingArtifactRetention.Apply(run, wrong, 2, 4));
            Assert.Equal(
                "rolling_artifact_retention_snapshot_directory_not_scoped",
                error.Message);
        }
        finally
        {
            Directory.Delete(wrong);
            Directory.Delete(run);
            Directory.Delete(root);
        }
    }

    private static SnapshotEnvelope Snapshot(string stateHash, long gameTick)
    {
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = gameTick
        };
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
