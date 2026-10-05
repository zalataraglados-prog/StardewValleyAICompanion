namespace StardewAI.Core.Tests;

public sealed class DeployScriptSourceGuardTests
{
    [Fact]
    public void SharedDeployGuardRejectsStaleOutputsAndVerifiesCopiedHashes()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Deploy.Common.ps1"));

        Assert.Contains("LastWriteTimeUtc", source, StringComparison.Ordinal);
        Assert.Contains(
            "NoBuild refused because build output is stale",
            source,
            StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", source, StringComparison.Ordinal);
        Assert.Contains(
            "Deployed file hash mismatch",
            source,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Deploy-RuntimeTestHarnessToRuntime.ps1")]
    [InlineData("Deploy-TransparentBridgeToRuntime.ps1")]
    public void ModDeployScriptsUseTheSharedFailClosedGuard(string fileName)
    {
        var source = File.ReadAllText(FindRepositoryFile("scripts", fileName));

        Assert.Contains("Deploy.Common.ps1", source, StringComparison.Ordinal);
        Assert.Contains(
            "if ($NoBuild -and -not $DryRun)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Assert-StardewAIBuildOutputFresh",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Copy-StardewAIVerifiedFile",
            source,
            StringComparison.Ordinal);
        Assert.Contains("deployed_sha256", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solution = Path.Combine(
                current.FullName,
                "StardewValleyAICompanion.sln");
            if (File.Exists(solution))
            {
                return Path.Combine(
                    new[] { current.FullName }.Concat(segments).ToArray());
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
