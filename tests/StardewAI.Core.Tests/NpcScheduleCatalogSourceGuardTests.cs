namespace StardewAI.Core.Tests;

public sealed class NpcScheduleCatalogSourceGuardTests
{
    [Fact]
    public void ScheduleCatalogIsRestrictedToSocialAndFullProfiles()
    {
        var root = RepositoryRoot();
        var context = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "State",
            "SnapshotProfileContext.cs"));
        var modEntry = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));

        Assert.Contains("IncludesNpcScheduleCatalog", context, StringComparison.Ordinal);
        Assert.Contains("Current is \"social\" or \"social_future\" or \"full\"", context, StringComparison.Ordinal);
        Assert.Contains("or \"social\" or \"social_future\" or \"machine\"", modEntry, StringComparison.Ordinal);
        Assert.Contains("profile is \"route\" or \"shop\" or \"social\" or \"social_future\"", modEntry, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleCatalogReadsLiveNativeContentWithoutMutatingNpcSchedule()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "NpcReadAdapter.ScheduleCatalog.cs"));

        Assert.Contains("Utility.ForEachVillager", source, StringComparison.Ordinal);
        Assert.Contains("includeEventActors: false", source, StringComparison.Ordinal);
        Assert.Contains("npc.getMasterScheduleRawData()", source, StringComparison.Ordinal);
        Assert.Contains("raw_schedule_sha256", source, StringComparison.Ordinal);
        Assert.Contains("catalog_sha256", source, StringComparison.Ordinal);
        Assert.Contains("NPC.TryLoadSchedule", source, StringComparison.Ordinal);
        Assert.Contains("NPC.parseMasterSchedule", source, StringComparison.Ordinal);
        Assert.Contains("required_future_scenario_inputs", source, StringComparison.Ordinal);
        Assert.Contains("rain2_random_branch_when_present", source, StringComparison.Ordinal);
        Assert.Contains("current_selection_context", source, StringComparison.Ordinal);
        Assert.Contains("Game1.shortDayNameFromDayOfSeason", source, StringComparison.Ordinal);
        Assert.Contains("Utility.GetDayOfPassiveFestival", source, StringComparison.Ordinal);
        Assert.Contains("NetWorldState.checkAnywhereForWorldStateID", source, StringComparison.Ordinal);
        Assert.Contains("Game1.isLocationAccessible", source, StringComparison.Ordinal);
        Assert.Contains("maximum_farmer_friendship_hearts", source, StringComparison.Ordinal);
        Assert.Contains("npc.islandScheduleName.Value ?? string.Empty", source, StringComparison.Ordinal);
        Assert.Contains("vanilla_social_query_supported", source, StringComparison.Ordinal);
        Assert.Contains("character_master_data_present", source, StringComparison.Ordinal);
        Assert.Contains("can_socialize_condition", source, StringComparison.Ordinal);
        Assert.Contains("can_socialize_now = npc.CanSocialize", source, StringComparison.Ordinal);
        Assert.Contains("can_receive_gifts_data", source, StringComparison.Ordinal);
        Assert.Contains("can_receive_gifts_now = npc.CanReceiveGifts()", source, StringComparison.Ordinal);
        Assert.Contains("gift_taste_master_data_present", source, StringComparison.Ordinal);
        Assert.Contains("is_birthday_on_capture_date", source, StringComparison.Ordinal);
        Assert.Contains("gifts_today", source, StringComparison.Ordinal);
        Assert.Contains("gifts_this_week", source, StringComparison.Ordinal);
        Assert.Contains("friendship_is_divorced", source, StringComparison.Ordinal);
        Assert.Contains("capture_total_days", source, StringComparison.Ordinal);
        Assert.Contains("complete_live_current_inputs_except_unobserved_rain2_roll", source, StringComparison.Ordinal);
        Assert.Contains("complete_live_master_schedule_catalog_conditional_future_resolution_pending", source, StringComparison.Ordinal);

        Assert.DoesNotContain("npc.TryLoadSchedule(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("npc.parseMasterSchedule(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentSeason =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.dayOfMonth =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialRouteDateEvidenceIsProfileLimitedNativeAndReadOnly()
    {
        var root = RepositoryRoot();
        var adapter = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.cs"));
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.SocialRouteDateEvidence.cs"));

        Assert.Contains(
            "SnapshotProfileContext.IncludesSocialFutureRouteDateEvidence",
            adapter,
            StringComparison.Ordinal);
        Assert.Contains("if (socialRouteEvidenceRequested)", adapter, StringComparison.Ordinal);
        Assert.Contains("social_route_date_evidence", adapter, StringComparison.Ordinal);
        Assert.Contains("Game1.locations", source, StringComparison.Ordinal);
        Assert.Contains("location.IsTileBlockedBy(", source, StringComparison.Ordinal);
        Assert.Contains("CollisionMask.Characters", source, StringComparison.Ordinal);
        Assert.Contains("CollisionMask.Farmers", source, StringComparison.Ordinal);
        Assert.Contains("CollisionMask.All", source, StringComparison.Ordinal);
        Assert.Contains("ReadActionGates(location)", source, StringComparison.Ordinal);
        Assert.Contains("unsupported_route_action_tiles", source, StringComparison.Ordinal);
        Assert.Contains(
            "player_route_movement_timing_not_yet_attached",
            source,
            StringComparison.Ordinal);

        Assert.DoesNotContain("Game1.Date =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.timeOfDay =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.currentLocation =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialRouteGraphCoversNativeLockerAndSewerWarpBranches()
    {
        var root = RepositoryRoot();
        var routeGraph = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.RouteGraph.cs"));
        var gates = File.ReadAllText(Path.Combine(
            root,
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "ShopAccessReadAdapter.GatesBlockers.cs"));

        Assert.Contains("WarpMensLocker", routeGraph, StringComparison.Ordinal);
        Assert.Contains("WarpWomensLocker", routeGraph, StringComparison.Ordinal);
        Assert.Contains("EnterSewer", routeGraph, StringComparison.Ordinal);
        Assert.Contains("target_location = \"Sewer\"", routeGraph, StringComparison.Ordinal);
        Assert.Contains("target_x = 16", routeGraph, StringComparison.Ordinal);
        Assert.Contains("target_y = 11", routeGraph, StringComparison.Ordinal);

        Assert.Contains("Game1.player?.IsMale", gates, StringComparison.Ordinal);
        Assert.Contains("Game1.player?.hasRustyKey", gates, StringComparison.Ordinal);
        Assert.Contains("mailReceived.Contains(\"OpenedSewer\")", gates, StringComparison.Ordinal);
        Assert.Contains("enter_sewer_unlock_interaction_required", gates, StringComparison.Ordinal);
        Assert.Contains("required_action_count", gates, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", routeGraph, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.warpFarmer", gates, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("Cannot find repository root.");
    }
}
