namespace StardewAI.Core.Tests;

public sealed class Smapi432CompatibilityTests
{
    [Fact]
    public void ServerModsDeclareJunimoSmapiCompatibility()
    {
        Assert.Contains("\"MinimumApiVersion\": \"4.3.2\"", File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "manifest.json")), StringComparison.Ordinal);
        Assert.Contains("\"MinimumApiVersion\": \"4.3.2\"", File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "manifest.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void InputOverrideCompatibilityProbeIncludesNonPublicSmapiImplementations()
    {
        Assert.Contains(
            "BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic",
            RuntimeHarnessSources.All,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MonoHttpListenerUsesWildcardForAllInterfaceBinding()
    {
        var bridgeSource = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "ModEntry.cs"));

        Assert.Contains("string.Equals(config.Host, \"0.0.0.0\"", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("string.Equals(config.ExecutorHost, \"0.0.0.0\"", RuntimeHarnessSources.All, StringComparison.Ordinal);
        Assert.Contains("? \"*\"", bridgeSource, StringComparison.Ordinal);
        Assert.Contains("? \"*\"", RuntimeHarnessSources.All, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Cannot find repository root.")
            : Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
