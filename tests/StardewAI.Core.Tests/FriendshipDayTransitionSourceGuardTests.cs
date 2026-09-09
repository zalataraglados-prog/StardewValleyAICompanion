namespace StardewAI.Core.Tests;

public sealed class FriendshipDayTransitionSourceGuardTests
{
    [Fact]
    public void BridgePublishesEveryNativeDayTransitionInputWithoutWritingFriendshipState()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "NpcReadAdapter.cs"));

        Assert.Contains("friendship_row_exists", source, StringComparison.Ordinal);
        Assert.Contains("is_npc_married = npc.isMarried()", source, StringComparison.Ordinal);
        Assert.Contains("is_player_spouse", source, StringComparison.Ordinal);
        Assert.Contains("friendship!.IsDating()", source, StringComparison.Ordinal);
        Assert.Contains("friendship!.IsDivorced()", source, StringComparison.Ordinal);
        Assert.Contains("last_gift_date_total_sunday_weeks", source, StringComparison.Ordinal);
        Assert.Contains("npc.SpeaksDwarvish()", source, StringComparison.Ordinal);
        Assert.Contains("Utility.GetMaximumHeartsForCharacter(npc)", source, StringComparison.Ordinal);
        Assert.Contains("player.stats.Get(\"Book_Friendship\")", source, StringComparison.Ordinal);
        Assert.Contains("next_total_days", source, StringComparison.Ordinal);
        Assert.Contains("next_total_sunday_weeks", source, StringComparison.Ordinal);
        Assert.Contains("complete_live_native_fields", source, StringComparison.Ordinal);

        Assert.DoesNotContain("friendship!.Points =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("friendship!.TalkedToToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("friendship!.GiftsThisWeek =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SimulatorPreservesNativeModifierOrderAndDoesNotOwnSleepExecution()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "StardewAI.Core",
            "Training",
            "FriendshipDayTransitionSimulator.cs"));

        Assert.Contains("effective * 1.1f", source, StringComparison.Ordinal);
        Assert.Contains("effective * 0.66f", source, StringComparison.Ordinal);
        Assert.Contains("weekly_two_gift_bonus", source, StringComparison.Ordinal);
        Assert.Contains("usesLowerDecayCap", source, StringComparison.Ordinal);
        Assert.DoesNotContain("executor.sleep", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Friendship.Points", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSmokeClonesSaveAndRequiresFreshFullSnapshots()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "scripts",
            "Invoke-RuntimeFriendshipDayTransitionSmoke.ps1"));

        Assert.Contains("Copy-Item -LiteralPath $sourceSavePath", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES = $clonedSavesRoot", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SAVE_ISOLATION_PATH = $clonedSavesRoot", source, StringComparison.Ordinal);
        Assert.Contains("?profile=full&fresh=true", source, StringComparison.Ordinal);
        Assert.Contains("audit-friendship-day-transition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultiDayRuntimeSmokeRetainsIsolationAndRequiresNativeBranchCoverage()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "scripts",
            "Invoke-RuntimeFriendshipMultiDaySmoke.ps1"));
        var fixture = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.FriendshipTransitionFixture.cs"));

        Assert.Contains("Copy-Item -LiteralPath $sourceSavePath", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES = $clonedSavesRoot", source, StringComparison.Ordinal);
        Assert.Contains("for ($index = 0; $index -lt $TransitionCount; $index++)", source, StringComparison.Ordinal);
        Assert.Contains("weekly_two_gift_bonus_covered", source, StringComparison.Ordinal);
        Assert.Contains("ordinary_not_talked_decay_covered", source, StringComparison.Ordinal);
        Assert.Contains("source_save_untouched", source, StringComparison.Ordinal);
        Assert.Contains("Get-SaveFingerprint", source, StringComparison.Ordinal);
        Assert.Contains("audit-friendship-day-transition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item", source, StringComparison.Ordinal);

        Assert.Contains("const string npcName = \"Linus\"", fixture, StringComparison.Ordinal);
        Assert.Contains("friendship.GiftsThisWeek = 2", fixture, StringComparison.Ordinal);
        Assert.Contains("friendship.LastGiftDate = previousWeek", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("resetFriendshipsForNewDay", fixture, StringComparison.Ordinal);
        Assert.DoesNotContain("updateFriendshipGifts", fixture, StringComparison.Ordinal);

        var sleepFixture = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.PartnershipFixture.cs"));
        Assert.Contains("home.currentEvent = null", sleepFixture, StringComparison.Ordinal);
        Assert.Contains("Game1.currentSpeaker = null", sleepFixture, StringComparison.Ordinal);
        Assert.Contains("home.getEntryLocation()", sleepFixture, StringComparison.Ordinal);
        Assert.Contains("sleep_fixture_nonempty_native_path_required", sleepFixture, StringComparison.Ordinal);
        Assert.Contains("Game1.freezeControls = false", sleepFixture, StringComparison.Ordinal);
        Assert.Contains("Game1.player.controller = null", sleepFixture, StringComparison.Ordinal);
    }

    [Fact]
    public void ScheduleArrivalTimingRetainsNativeRouteAndClockFormula()
    {
        var adapter = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "NpcReadAdapter.cs"));
        var resolver = File.ReadAllText(Path.Combine(
            RepositoryRoot(),
            "src",
            "StardewAI.Core",
            "Training",
            "NpcFutureScheduleResolver.ArrivalTiming.cs"));

        Assert.Contains("adjacent_route_pixel_distance", adapter, StringComparison.Ordinal);
        Assert.Contains("distance = checked(distance + 64)", adapter, StringComparison.Ordinal);
        Assert.Contains("AdjacentRoutePixelDistance / 2", resolver, StringComparison.Ordinal);
        Assert.Contains("RealMillisecondsPerGameTenMinutes.Value / 1000 * 60", resolver, StringComparison.Ordinal);
        Assert.Contains("Math.Round", resolver, StringComparison.Ordinal);
        Assert.Contains("previousDepartureTime", resolver, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
