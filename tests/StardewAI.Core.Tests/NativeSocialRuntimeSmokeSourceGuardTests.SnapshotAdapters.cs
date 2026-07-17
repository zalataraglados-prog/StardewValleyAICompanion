using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class NativeSocialRuntimeSmokeSourceGuardTests
{
    [Fact]
    public void SmokeScriptRequiresFullSnapshotProfile()
    {
        var source = SocialSmokeSource;

        Assert.Contains("?profile=full", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"/api/v1/snapshot\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/snapshot`\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/snapshot'", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("/api/v1/snapshot", StringComparison.Ordinal) &&
                !line.Contains("?profile=full", StringComparison.Ordinal) &&
                !line.Contains("?profile=fishing", StringComparison.Ordinal) &&
                !line.Contains("?profile=route", StringComparison.Ordinal) &&
                !line.Contains("?profile=light", StringComparison.Ordinal) &&
                !line.Contains("?profile=machine", StringComparison.Ordinal) &&
                !line.Contains("?profile=training_machine", StringComparison.Ordinal) &&
                !line.Contains("/api/v1/snapshots", StringComparison.Ordinal))
            {
                Assert.Fail("Script uses a bare /api/v1/snapshot URL without a profile parameter: " + line.Trim());
            }
        }
    }

    [Fact]
    public void WaitWorldSnapshotRequiresFullDomainSet()
    {
        var source = SocialSmokeSource;

        Assert.Contains("requiredDomains", source, StringComparison.Ordinal);
        Assert.Contains("missingDomains", source, StringComparison.Ordinal);
        Assert.Contains("\"player\"", source, StringComparison.Ordinal);
        Assert.Contains("\"farm\"", source, StringComparison.Ordinal);
        Assert.Contains("\"current_location\"", source, StringComparison.Ordinal);
        Assert.Contains("\"locations\"", source, StringComparison.Ordinal);
        Assert.Contains("\"npcs\"", source, StringComparison.Ordinal);
        Assert.Contains("\"quests\"", source, StringComparison.Ordinal);
        Assert.Contains("\"world_progress\"", source, StringComparison.Ordinal);
        Assert.Contains("\"mods\"", source, StringComparison.Ordinal);
        Assert.Contains("\"modded_state\"", source, StringComparison.Ordinal);
        Assert.Contains("\"fishing\"", source, StringComparison.Ordinal);
        Assert.Contains("\"mining\"", source, StringComparison.Ordinal);
        Assert.Contains("$missingDomains -join", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterEnumeratesAllLocationsWithForEachLocationIncludeInteriorsAndGenerated()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation", source, StringComparison.Ordinal);
        Assert.Contains("includeInteriors: true", source, StringComparison.Ordinal);
        Assert.Contains("includeGenerated: true", source, StringComparison.Ordinal);
        Assert.Contains("CollectAllLoadedNpcs", source, StringComparison.Ordinal);

        Assert.DoesNotContain("= Game1.currentLocation.characters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterProvenanceNamesForEachLocationAndConcreteSources()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation(includeInteriors:true, includeGenerated:true)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels", source, StringComparison.Ordinal);

        Assert.Contains("gift_tastes", source, StringComparison.Ordinal);
        Assert.Contains("getGiftTasteForThisItem", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterCurrentInstanceLoadedChecksActualCharacterCollection()
    {
        var source = NpcAdapterSource;

        Assert.Contains("npcCurrentLocation.characters.Any", source, StringComparison.Ordinal);
        Assert.Contains("instanceLoaded", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceEquals(npc.currentLocation, Game1.currentLocation)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterVisibleOnScreenFalseForNonCurrentLocation()
    {
        var source = NpcAdapterSource;

        Assert.Contains("isCurrentLocation", source, StringComparison.Ordinal);
        Assert.Contains("isCurrentLocation && Utility.isOnScreen", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialCandidateBuilderRemoteStandSkippingGuard()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.Core", "OptionRegistry", "SocialCandidateBuilder.cs"));

        Assert.Contains("npcLocationId", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_not_in_player_location_stand_skipped", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectAllLoadedNpcsUsesReferenceIdentityNotCompositeStringKey()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation", source, StringComparison.Ordinal);
        Assert.Contains("ReferenceEqualityComparer.Instance", source, StringComparison.Ordinal);
        Assert.Contains("seen.Add(npc)", source, StringComparison.Ordinal);

        Assert.DoesNotContain("npc.Name + \"|\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HashSet<string>", source, StringComparison.Ordinal);

        Assert.Contains("var firstSeen = new List<NPC>()", source, StringComparison.Ordinal);
        Assert.Contains("firstSeen.Add(npc)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDeterministicRouteReachableNpcFilter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ordinaryNpcs", source, StringComparison.Ordinal);
        Assert.Contains("reachableLocations", source, StringComparison.Ordinal);
        Assert.Contains("route_reachable", source, StringComparison.Ordinal);
        Assert.Contains("npcs-considered-for-route.json", source, StringComparison.Ordinal);
        Assert.Contains("vanilla_social_query_supported", source, StringComparison.Ordinal);
        Assert.Contains("can_socialize_complete", source, StringComparison.Ordinal);
        Assert.Contains("is_sleeping -ne", source, StringComparison.Ordinal);
        Assert.Contains("is_invisible -ne", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsSuffixedRunIdPatterns()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("$RunId.route-bfs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.talk", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.close-dialogue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.gift", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresAllExecutionPhasesUseExactBaseRunId()
    {
        var source = SocialSmokeSource;

        Assert.Contains("run_id = \"$RunId\"", source, StringComparison.Ordinal);

        Assert.Contains("$talkRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $talkRunId", source, StringComparison.Ordinal);

        Assert.Contains("$closeRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $closeRunId", source, StringComparison.Ordinal);

        Assert.Contains("$giftRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $giftRunId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HasSeparatePhaseRootDirectories()
    {
        var source = SocialSmokeSource;

        Assert.Contains("$talkLoopRoot = Join-Path $runDirectory \"talk-loop\"", source, StringComparison.Ordinal);
        Assert.Contains("$giftLoopRoot = Join-Path $runDirectory \"gift-loop\"", source, StringComparison.Ordinal);
        Assert.Contains("--root (Join-Path $runDirectory \"close-dialogue-loop\")", source, StringComparison.Ordinal);

        Assert.DoesNotContain("$talkLoopRoot = $runDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$giftLoopRoot = $runDirectory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceIdentityHashSetRetainsDistinctInstancesCollapsesSameReference()
    {
        var a = new NpcReferenceTestProxy { Name = "Robin", Location = "Farm", TileX = 5, TileY = 10 };
        var b = new NpcReferenceTestProxy { Name = "Robin", Location = "Farm", TileX = 5, TileY = 10 };

        var set = new HashSet<NpcReferenceTestProxy>(ReferenceEqualityComparer.Instance);
        Assert.True(set.Add(a));
        Assert.True(set.Add(b));

        Assert.Equal(2, set.Count);
        Assert.Contains(a, set);
        Assert.Contains(b, set);

        Assert.False(set.Add(a));
        Assert.Equal(2, set.Count);
    }

    private sealed class NpcReferenceTestProxy
    {
        public string Name { get; init; } = "";
        public string Location { get; init; } = "";
        public int TileX { get; init; }
        public int TileY { get; init; }
    }

}
