namespace StardewAI.Backend.Tests;

public sealed class BackendSnapshotRequestLimitTests
{
    [Fact]
    public void BackendAcceptsBoundedFullTransparencySnapshotsAboveKestrelDefault()
    {
        var source = RuntimeHarnessSources.RepositoryFile(
            "src",
            "StardewAI.Backend",
            "Program.cs");

        Assert.Contains(
            "MaxTransparentSnapshotRequestBodyBytes",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "128L * 1024 * 1024",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "options.Limits.MaxRequestBodySize",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SnapshotValidator.ValidateAsync",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MaxRetainedSnapshots = 2",
            source,
            StringComparison.Ordinal);
    }
}
