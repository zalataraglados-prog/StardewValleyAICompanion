using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingExecutionSnapshotProfileTests
{
    [Fact]
    public void FullProfileRemainsTheDefault()
    {
        var options = LiveTrainingOptions.Parse(Array.Empty<string>());

        Assert.Equal("full", options.ExecutionSnapshotProfile);
    }

    [Fact]
    public void ExplicitExecutionProfileIsParsed()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--execution-snapshot-profile",
            "social"
        });

        Assert.Equal("social", options.ExecutionSnapshotProfile);
    }

    [Fact]
    public void EmptyExecutionProfileIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--execution-snapshot-profile",
            " "
        }));
    }

    [Fact]
    public void SnapshotArtifactsRemainPlainByDefault()
    {
        var options = LiveTrainingOptions.Parse(Array.Empty<string>());

        Assert.Equal(
            ContentAddressedJsonArtifactStore.PlainMode,
            options.SnapshotArtifactMode);
    }

    [Fact]
    public void ContentAddressedSnapshotArtifactsCanBeEnabled()
    {
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-artifact-mode",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode
        });

        Assert.Equal(
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode,
            options.SnapshotArtifactMode);
    }

    [Fact]
    public void UnknownSnapshotArtifactModeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => LiveTrainingOptions.Parse(new[]
        {
            "--snapshot-artifact-mode",
            "lossy"
        }));
    }

    [Fact]
    public void AfterExecutionReadsTheConfiguredProfile()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.QueueInspection.cs"));

        Assert.Contains("options.ExecutionSnapshotProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadFullSnapshotAsync", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
