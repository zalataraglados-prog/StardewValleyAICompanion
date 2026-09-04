namespace StardewAI.Core.Tests;

public sealed class DedicatedHostAiRuntimeGuardTests
{
    [Fact]
    public void RuntimeRequiresExplicitDedicatedHostModeAndMainPlayerContext()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("STARDEWAI_DEDICATED_HOST_MODE", source, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_multiplayer_world_required", source, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_main_player_required", source, StringComparison.Ordinal);
        Assert.Contains("Context.IsMainPlayer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeBindsRequestsToConfiguredHostActorAndFarmer()
    {
        var source = RuntimeHarnessSources.All;
        var config = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "HarnessConfig.cs"));

        Assert.Contains("STARDEWAI_DEDICATED_HOST_ACTOR_ID", source, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_actor_mismatch", source, StringComparison.Ordinal);
        Assert.Contains("DedicatedHostActorId", config, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_DEDICATED_HOST_FARMER_ID", source, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_farmer_id_required", source, StringComparison.Ordinal);
        Assert.Contains("dedicated_host_farmer_id_mismatch", source, StringComparison.Ordinal);
        Assert.Contains("DedicatedHostFarmerId", config, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeForbidsDebugAndPlanningOptionsInDedicatedHostMode()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("dedicated_host_debug_or_planning_option_forbidden", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_DEDICATED_HOST_RUN_ID", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeRestoresVisibleCollidingHostInsteadOfJunimoPlaceholderBehavior()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("Game1.displayFarmer = true", source, StringComparison.Ordinal);
        Assert.Contains("player.hidden.Value = false", source, StringComparison.Ordinal);
        Assert.Contains("player.ignoreCollisions = false", source, StringComparison.Ordinal);
        Assert.Contains("PlayerIsHidden", source, StringComparison.Ordinal);
    }

    [Fact]
    public void VanillaHostModeStartsOriginalMultiplayerBeforeLoadingTheSave()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("STARDEWAI_VANILLA_HOST_MODE", source, StringComparison.Ordinal);
        Assert.Contains("Game1.multiplayerMode = 2", source, StringComparison.Ordinal);
        Assert.Contains("SaveGame.Load(config.SlotName)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.server is not null", source, StringComparison.Ordinal);
        Assert.Contains("Game1.options.pauseWhenOutOfFocus = false", source, StringComparison.Ordinal);
        Assert.Contains("Vanilla AI host ready", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SUPPRESS_LOCAL_RENDER", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_OBSERVER_RENDER_INTERVAL_MS", source, StringComparison.Ordinal);
        Assert.Contains("HostLocalDrawPatch", source, StringComparison.Ordinal);
        Assert.Contains("HostLocalDrawPatch.Configure(observerRenderIntervalMilliseconds)", source, StringComparison.Ordinal);
        Assert.Contains("Environment.TickCount64", source, StringComparison.Ordinal);
        Assert.Contains("Interlocked.CompareExchange", source, StringComparison.Ordinal);
        Assert.Contains("AccessTools.Method(typeof(Game1), \"Draw\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FormalServerDefaultsToHeadlessButSupportsExplicitLowFrequencyObservation()
    {
        var compose = File.ReadAllText(FindRepositoryFile(
            "deploy", "stardew-server", "compose.formal-training.yaml"));

        Assert.Contains("depends_on: !reset {}", compose, StringComparison.Ordinal);
        Assert.Contains(
            "SMAPI_MODS_PATH: \"/data/vanilla-ai-mods\"",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_VANILLA_HOST_MODE: \"1\"",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_ENSURE_JOINABLE_CABIN: \"1\"",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE: \"true\"",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_SUPPRESS_LOCAL_RENDER: \"${STARDEWAI_SUPPRESS_LOCAL_RENDER:-1}\"",
            compose,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_OBSERVER_RENDER_INTERVAL_MS: \"${STARDEWAI_OBSERVER_RENDER_INTERVAL_MS:-0}\"",
            compose,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LightSnapshotsRejectMachineCraftingBeforeRecipeExpansion()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MachineCrafting.cs"));
        var profileGate = source.IndexOf(
            "SnapshotProfileContext.Current is not (\"machine\" or \"training_machine\" or \"full\")",
            StringComparison.Ordinal);
        var recipeExpansion = source.IndexOf(
            "foreach (var recipeName in player.craftingRecipes.Keys",
            StringComparison.Ordinal);

        Assert.True(profileGate >= 0, "Machine crafting must declare an explicit snapshot-profile gate.");
        Assert.True(recipeExpansion >= 0, "Machine crafting recipe expansion was not found.");
        Assert.True(profileGate < recipeExpansion, "The profile gate must run before any recipe expansion.");
    }

    [Fact]
    public void DailySnapshotIncludesDailyPlanningDomainsWithoutEnablingMachineExpansion()
    {
        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        var machine = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MachineCrafting.cs"));

        Assert.Contains("value is \"daily\" or", bridge, StringComparison.Ordinal);
        Assert.Contains("profile is \"daily\" or \"training_machine\"", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("\"daily\" or \"machine\"", machine, StringComparison.Ordinal);
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
