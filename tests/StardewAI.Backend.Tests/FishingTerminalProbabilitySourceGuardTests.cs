namespace StardewAI.Backend.Tests;

public sealed class FishingTerminalProbabilitySourceGuardTests
{
    [Fact]
    public void BridgePublishesExactSeededRollAndHonestConditionResolution()
    {
        var source = FishingReadAdapterSources.All;

        Assert.Contains("player.stats.Get(\"PreciseFishCaught\")", source);
        Assert.Contains("Utility.CreateRandom(", source);
        Assert.Contains("preciseFishCaught * 859", source);
        Assert.Contains(".NextBool(spawnChance)", source);
        Assert.Contains("spawn_probability_kind", source);
        Assert.Contains("deterministic_fish_caught_seed", source);
        Assert.Contains("seeded_spawn_roll_passed", source);
        Assert.Contains("condition_probability_resolved", source);
        Assert.Contains("non_mutating_local_rng_preview_not_probability_evidence", source);
        Assert.Contains("GameStateQuery.Parse(condition)", source);
        Assert.Contains("string.Equals(key, \"RANDOM\"", source);
    }

    [Fact]
    public void DemandOnlyForecastReadsOneExplicitLoadedLocation()
    {
        var source = FishingReadAdapterSources.All;

        Assert.Contains("CollectForecast(tick, player)", source);
        Assert.Contains("Game1.getLocationFromName(locationId!)", source);
        Assert.Contains("complete_single_requested_loaded_map_no_cap", source);
        Assert.Contains("deferPlayerPositionToTerminalStand: true", source);
        Assert.Contains("player_position_requires_terminal_stand_check", source);
        Assert.DoesNotContain("Utility.ForEachLocation", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.RequireLocation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ForecastCacheIdentityIncludesLocationAndRodSlot()
    {
        var root = FindRepositoryRoot();
        var entry = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));
        var context = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "State",
            "SnapshotProfileContext.cs"));

        Assert.Contains("fishing_forecast", entry);
        Assert.Contains("SnapshotFishingLocationId(request, profile)", entry);
        Assert.Contains("SnapshotFishingRodSlotIndex(request, profile)", entry);
        Assert.Contains("SnapshotCacheKey(", entry);
        Assert.Contains("item.FishingLocationId", entry);
        Assert.Contains("item.FishingRodSlotIndex", entry);
        Assert.Contains("profileSnapshots.TryGetValue(cacheKey", entry);
        var forecastDomainsStart = entry.IndexOf(
            "if (profile is \"fishing_forecast\")",
            StringComparison.Ordinal);
        var defaultDomainsStart = entry.IndexOf(
            "var domains = new HashSet<string>",
            forecastDomainsStart,
            StringComparison.Ordinal);
        Assert.True(forecastDomainsStart >= 0 &&
                    defaultDomainsStart > forecastDomainsStart);
        var forecastDomains = entry[forecastDomainsStart..defaultDomainsStart];
        Assert.Contains("\"world\"", forecastDomains);
        Assert.Contains("\"fishing\"", forecastDomains);
        Assert.Contains("\"unavailable_fields\"", forecastDomains);
        Assert.DoesNotContain("\"player\"", forecastDomains);
        Assert.DoesNotContain("\"menus\"", forecastDomains);
        Assert.DoesNotContain("\"options\"", forecastDomains);
        Assert.Contains("TargetFishingLocationId", context);
        Assert.Contains("TargetFishingRodSlotIndex", context);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(
                   directory.FullName,
                   "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ??
               throw new InvalidOperationException("Cannot find repository root.");
    }
}
