namespace StardewAI.Core.Tests;

public sealed class CoopCompanionRuntimeGuardTests
{
    [Fact]
    public void RuntimeRequiresExplicitCompanionModeAndFarmhandContext()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("STARDEWAI_COMPANION_MODE", source, StringComparison.Ordinal);
        Assert.Contains("coop_world_required", source, StringComparison.Ordinal);
        Assert.Contains("Context.IsMainPlayer", source, StringComparison.Ordinal);
        Assert.Contains("coop_farmhand_required", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeBindsCompanionRequestsToConfiguredLogicalActor()
    {
        var source = RuntimeHarnessSources.All;
        var config = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "HarnessConfig.cs"));

        Assert.Contains("STARDEWAI_COMPANION_ACTOR_ID", source, StringComparison.Ordinal);
        Assert.Contains("companion_actor_mismatch", source, StringComparison.Ordinal);
        Assert.Contains("CompanionActorId", config, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_COMPANION_FARMER_ID", source, StringComparison.Ordinal);
        Assert.Contains("companion_farmer_id_required", source, StringComparison.Ordinal);
        Assert.Contains("companion_farmer_id_mismatch", source, StringComparison.Ordinal);
        Assert.Contains("CompanionFarmerId", config, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeForbidsDebugFixturesInCompanionMode()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("request.OptionId.StartsWith(\"executor.\"", source, StringComparison.Ordinal);
        Assert.Contains("coop_debug_or_planning_option_forbidden", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Cannot find repository root.");
        }

        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
