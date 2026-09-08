using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FriendshipDayTransitionSimulatorTests
{
    [Fact]
    public void OrdinaryUntalkedVillagerLosesTwoPoints()
    {
        var result = new FriendshipDayTransitionSimulator().Simulate(Input(points: 100));

        Assert.Equal("exact_native_rule_projection_runtime_pending", result.Status);
        Assert.Equal(98, result.PointsAfter);
        var transition = Assert.Single(result.PointTransitions);
        Assert.Equal((-2, -2, -2),
            (transition.RequestedDelta, transition.EffectiveDeltaBeforeClamp, transition.AppliedDelta));
    }

    [Fact]
    public void TalkedVillagerResetsFlagWithoutDecay()
    {
        var input = Input(points: 100);
        input.TalkedToToday = true;

        var result = new FriendshipDayTransitionSimulator().Simulate(input);

        Assert.Equal(100, result.PointsAfter);
        Assert.False(result.TalkedToTodayAfter);
        Assert.Empty(result.PointTransitions);
    }

    [Fact]
    public void SpousePenaltyAndOrdinaryDecayUseNativeSpouseTruncationTwice()
    {
        var input = Input(points: 1000);
        input.IsDatable = true;
        input.IsNpcMarried = true;
        input.IsPlayerSpouse = true;

        var result = new FriendshipDayTransitionSimulator().Simulate(input);

        Assert.Equal(986, result.PointsAfter);
        Assert.Equal(new[] { -13, -1 }, result.PointTransitions.Select(row => row.EffectiveDeltaBeforeClamp).ToArray());
        Assert.Equal(new[] { "spouse_not_talked", "ordinary_not_talked_below_2500" },
            result.PointTransitions.Select(row => row.Reason).ToArray());
    }

    [Fact]
    public void DatableNonDatingVillagerUsesTwoThousandPointDecayCap()
    {
        var below = Input(points: 1999);
        below.IsDatable = true;
        var atCap = Input(points: 2000);
        atCap.IsDatable = true;

        var belowResult = new FriendshipDayTransitionSimulator().Simulate(below);
        var atCapResult = new FriendshipDayTransitionSimulator().Simulate(atCap);

        Assert.Equal(1997, belowResult.PointsAfter);
        Assert.Equal(2000, atCapResult.PointsAfter);
    }

    [Fact]
    public void WeeklyGiftBonusAppliesBookBeforeSpouseMultiplier()
    {
        var input = Input(points: 1000);
        input.TalkedToToday = true;
        input.GiftsThisWeek = 2;
        input.LastGiftDateTotalSundayWeeks = 1;
        input.TransitionDateTotalSundayWeeks = 2;
        input.PlayerHasFriendshipBook = true;
        input.IsPlayerSpouse = true;
        input.IsNpcMarried = true;

        var result = new FriendshipDayTransitionSimulator().Simulate(input);

        Assert.Equal(1007, result.PointsAfter);
        Assert.Equal(0, result.GiftsThisWeekAfter);
        var transition = Assert.Single(result.PointTransitions);
        Assert.Equal((10, 7, "friendship_book_then_spouse_multiplier"),
            (transition.RequestedDelta, transition.EffectiveDeltaBeforeClamp, transition.ModifierStatus));
    }

    [Fact]
    public void DwarvishWeeklyBonusIsRejectedWithoutComprehension()
    {
        var input = Input(points: 1000);
        input.TalkedToToday = true;
        input.GiftsThisWeek = 2;
        input.LastGiftDateTotalSundayWeeks = 1;
        input.TransitionDateTotalSundayWeeks = 2;
        input.SpeaksDwarvish = true;
        input.PlayerCanUnderstandDwarves = false;

        var result = new FriendshipDayTransitionSimulator().Simulate(input);

        Assert.Equal(1000, result.PointsAfter);
        Assert.Equal("dwarvish_positive_change_rejected", Assert.Single(result.PointTransitions).ModifierStatus);
    }

    [Fact]
    public void WeeklyBonusClampsAtNativeMaximum()
    {
        var input = Input(points: 2245);
        input.TalkedToToday = true;
        input.GiftsThisWeek = 2;
        input.LastGiftDateTotalSundayWeeks = 1;
        input.TransitionDateTotalSundayWeeks = 2;
        input.MaximumHearts = 8;

        var result = new FriendshipDayTransitionSimulator().Simulate(input);

        Assert.Equal(2249, result.PointsAfter);
        Assert.Equal(4, Assert.Single(result.PointTransitions).AppliedDelta);
    }

    [Fact]
    public void TransparentGrandpaRowBindsEveryNativeTransitionInput()
    {
        var progress = JsonSerializer.SerializeToElement(new
        {
            projection_status = "complete_live_native_iteration",
            day_transition_inputs_status = "complete_live_native_fields",
            current_total_days = 15,
            current_total_sunday_weeks = 2,
            next_total_days = 16,
            next_total_sunday_weeks = 2,
            player_has_friendship_book = false,
            player_can_understand_dwarves = true,
            eligible_villager_rows = new[]
            {
                new
                {
                    npc_name = "Abigail",
                    is_villager = true,
                    event_actor = false,
                    is_child = false,
                    friendship_row_exists = true,
                    friendship_points = 500,
                    is_datably_flagged = true,
                    is_npc_married = false,
                    is_player_spouse = false,
                    is_dating = false,
                    is_divorced = false,
                    talked_to_today = false,
                    gifts_today = 1,
                    gifts_this_week = 1,
                    last_gift_date_total_days = 15,
                    last_gift_date_total_sunday_weeks = 2,
                    speaks_dwarvish = false,
                    maximum_hearts = 8
                }
            }
        });

        var result = new FriendshipDayTransitionSimulator().SimulateGrandpaRow(progress, "Abigail");

        Assert.Equal("exact_native_rule_projection_runtime_pending", result.Status);
        Assert.Equal(498, result.PointsAfter);
        Assert.Equal(0, result.GiftsTodayAfter);
        Assert.Equal(1, result.GiftsThisWeekAfter);
    }

    [Fact]
    public void MissingTransparentTransitionStatusFailsClosed()
    {
        var progress = JsonSerializer.SerializeToElement(new
        {
            projection_status = "complete_live_native_iteration",
            eligible_villager_rows = Array.Empty<object>()
        });

        var result = new FriendshipDayTransitionSimulator().SimulateGrandpaRow(progress, "Abigail");

        Assert.Equal("blocked", result.Status);
        Assert.Equal("grandpa_friendship_day_transition_projection_incomplete", Assert.Single(result.Issues));
    }

    private static FriendshipDayTransitionInput Input(int points) => new()
    {
        NpcName = "Abigail",
        FriendshipRowExists = true,
        CharacterExists = true,
        IsVillager = true,
        IsChild = false,
        IsDatable = false,
        IsNpcMarried = false,
        IsPlayerSpouse = false,
        IsDating = false,
        IsDivorced = false,
        TalkedToToday = false,
        SpeaksDwarvish = false,
        PlayerCanUnderstandDwarves = true,
        PlayerHasFriendshipBook = false,
        MaximumHearts = 10,
        Points = points,
        GiftsToday = 1,
        GiftsThisWeek = 1,
        LastGiftDateStateComplete = true,
        LastGiftDateTotalDays = 14,
        LastGiftDateTotalSundayWeeks = 2,
        TransitionDateTotalDays = 15,
        TransitionDateTotalSundayWeeks = 2
    };
}
