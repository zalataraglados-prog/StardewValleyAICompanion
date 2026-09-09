using System.Text.Json;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FutureNpcContactEligibilityProducerTests
{
    [Fact]
    public void UnconditionalVillagerProducesExactTalkAndOrdinaryGiftWindows()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(),
            12,
            Presence());

        Assert.Equal(FutureNpcContactEligibilityProductionStatus.Exact, result.Status);
        var evidence = Assert.Single(result.Evidence);
        Assert.True(evidence.StateComplete);
        Assert.True(evidence.TalkAllowed);
        Assert.True(evidence.GiftAllowed);
        Assert.Equal((900, 1200),
            (evidence.EligibleFromTime, evidence.EligibleUntilTimeExclusive));
    }

    [Fact]
    public void DailyTalkAndGiftLimitsAreExcludedBeforeContactPlanning()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(talked: true, giftsToday: 1, giftsThisWeek: 2),
            12,
            Presence());

        var evidence = Assert.Single(result.Evidence);
        Assert.False(evidence.TalkAllowed);
        Assert.False(evidence.GiftAllowed);
    }

    [Fact]
    public void BirthdayBypassesWeeklyButNotDailyOrdinaryGiftLimit()
    {
        var weekly = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(isBirthday: true, giftsToday: 0, giftsThisWeek: 2),
            12,
            Presence());
        Assert.True(Assert.Single(weekly.Evidence).GiftAllowed);

        var daily = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(isBirthday: true, giftsToday: 1, giftsThisWeek: 2),
            12,
            Presence());
        Assert.False(Assert.Single(daily.Evidence).GiftAllowed);
    }

    [Fact]
    public void YearOneGreenRainBlocksOrdinaryGiftButNotTalk()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(year: 1, greenRain: true),
            12,
            Presence());

        var evidence = Assert.Single(result.Evidence);
        Assert.True(evidence.TalkAllowed);
        Assert.False(evidence.GiftAllowed);
    }

    [Fact]
    public void TrueSeenEventConditionIsMonotonicForTheRestOfTheDay()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(canSocializeCondition: "PLAYER_HAS_SEEN_EVENT Any 67"),
            12,
            Presence());

        Assert.Equal(FutureNpcContactEligibilityProductionStatus.Exact, result.Status);
        var evidence = Assert.Single(result.Evidence);
        Assert.True(evidence.SocialQueryValueOnCaptureDate);
        Assert.True(evidence.SocialQueryStableThroughDay);
        Assert.Equal(
            "native_true_monotonic_player_seen_event_condition",
            evidence.SocialQueryEvidenceKind);
    }

    [Fact]
    public void NonMonotonicConditionalSocialRuleFailsClosed()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(canSocializeCondition: "TIME 900 1700"),
            12,
            Presence());

        Assert.Equal(FutureNpcContactEligibilityProductionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_contact_conditional_social_query_day_stability_unproven",
            result.BlockingReasons);
    }

    [Fact]
    public void FalseConditionalSocialRuleFailsClosed()
    {
        var result = new FutureNpcContactEligibilityProducer().Produce(
            Catalog(
                canSocializeCondition: "PLAYER_HAS_SEEN_EVENT Any 67",
                canSocializeNow: false),
            12,
            Presence());

        Assert.Equal(FutureNpcContactEligibilityProductionStatus.Blocked, result.Status);
        Assert.Contains(
            "future_contact_conditional_social_query_false_on_capture_date",
            result.BlockingReasons);
    }

    private static NpcFuturePresenceWindowResolution Presence() => new()
    {
        Status = NpcFuturePresenceWindowResolutionStatus.Exact,
        NpcName = "Abigail",
        SelectedScheduleKey = "spring",
        Windows = new[]
        {
            new NpcFuturePresenceWindow
            {
                ScheduleEntryOrdinal = 1,
                LocationName = "Town",
                TileX = 4,
                TileY = 2,
                WindowStartTime = 900,
                WindowEndTimeExclusive = 1200,
                HasStableInterval = true,
                EndpointBehaviorComplete = true
            }
        }
    };

    private static JsonElement Catalog(
        bool talked = false,
        int giftsToday = 0,
        int giftsThisWeek = 0,
        bool isBirthday = false,
        int year = 2,
        bool greenRain = false,
        string? canSocializeCondition = null,
        bool canSocializeNow = true) =>
        JsonSerializer.SerializeToElement(new
        {
            current_selection_context = new
            {
                capture_total_days = 12,
                year,
                is_green_rain = greenRain
            },
            villagers = new[]
            {
                new
                {
                    npc_name = "Abigail",
                    vanilla_social_query_supported = true,
                    character_master_data_present = true,
                    is_villager = true,
                    can_socialize_condition = canSocializeCondition,
                    can_socialize_now = canSocializeNow,
                    can_receive_gifts_data = (bool?)true,
                    can_receive_gifts_now = true,
                    simple_non_villager_npc = false,
                    gift_taste_master_data_present = true,
                    is_child = false,
                    is_player_spouse = false,
                    currently_married = false,
                    is_birthday_on_capture_date = isBirthday,
                    friendship_row_exists = true,
                    talked_to_today = talked,
                    gifts_today = (int?)giftsToday,
                    gifts_this_week = (int?)giftsThisWeek,
                    friendship_is_divorced = false
                }
            }
        });
}
