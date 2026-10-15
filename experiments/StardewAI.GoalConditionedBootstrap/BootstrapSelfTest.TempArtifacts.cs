namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static readonly string TempArtifactSessionRoot = Path.Combine(
        Path.GetTempPath(),
        "stardewai-bootstrap-self-test",
        $"{Environment.ProcessId}-{Guid.NewGuid():N}");

    private static string TempArtifactRoot(
        string fixtureKind,
        params string[] segments)
    {
        var path = Path.Combine(TempArtifactSessionRoot, fixtureKind);
        foreach (var segment in segments)
            path = Path.Combine(path, segment);
        return path;
    }
}
