using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Network;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string NativeKentEventId = "63";
    private const string NativeCatAdoptionEventId = "1590166";
    // In vanilla 1.6.15, legacy `d` maps to NotDayOfWeek, so this key allows
    // Wednesday and Friday rather than the five days listed after `d`.
    private const string NativeCatAdoptionEventKey =
        "1590166/m 1000/t 600 930/d Mon Tue Thu Sat Sun/w sunny/h cat/H";

    private TrainingExecutionResult ExecutePrepareNativeStoryHandoff(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            return NativeStoryHandoffBlocked(
                request,
                "native_story_handoff_world_unavailable");
        }
        if (Game1.eventUp || Game1.CurrentEvent is not null ||
            Game1.farmEvent is not null || Game1.currentMinigame is not null)
        {
            return NativeStoryHandoffBlocked(
                request,
                "native_story_handoff_requires_clear_native_boundary");
        }

        return request.StoryEventBoundaryKind switch
        {
            "native_kent_post_save" => PrepareNativeKentPostSaveHandoff(request),
            "native_cat_adoption_unsafe_question" =>
                PrepareNativeCatAdoptionUnsafeQuestion(request),
            _ => NativeStoryHandoffBlocked(
                request,
                "native_story_handoff_profile_invalid")
        };
    }

    private TrainingExecutionResult PrepareNativeKentPostSaveHandoff(
        TrainingExecutionRequest request)
    {
        if (Game1.player.eventsSeen.Contains(NativeKentEventId) ||
            Game1.eventsSeenSinceLastLocationChange.Contains(NativeKentEventId))
        {
            return NativeStoryHandoffBlocked(
                request,
                "native_kent_event_must_be_unseen");
        }

        var result = ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_native_story_handoff",
            "native_story_handoff_sleep_path_ready");
        if (result.Status != "applied")
        {
            return result;
        }

        var originalYear = Game1.year;
        var originalSeason = Game1.currentSeason;
        var originalDay = Game1.dayOfMonth;
        var originalDaysPlayed = Game1.stats.DaysPlayed;
        var originalTime = Game1.timeOfDay;
        var farmWeather = Game1.getFarm().GetWeather();
        var originalWeather = CaptureNativeStoryWeather(farmWeather);

        ApplySunnyNativeStoryWeather(farmWeather);
        Game1.year = 2;
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 2;
        Game1.stats.DaysPlayed = 114;
        Game1.timeOfDay = 600;

        if (!TryReadFirstEligibleFarmDataEvent(
                out var firstEventId,
                out var firstEventKey,
                out var firstEventScript) ||
            !string.Equals(
                firstEventId,
                NativeKentEventId,
                StringComparison.Ordinal) ||
            !firstEventScript.Contains(
                "/broadcastEvent/",
                StringComparison.Ordinal) ||
            !firstEventScript.Contains(
                "/skippable/",
                StringComparison.Ordinal) ||
            firstEventScript.Contains(
                "/question ",
                StringComparison.Ordinal) ||
            firstEventScript.Contains(
                "/quickQuestion ",
                StringComparison.Ordinal) ||
            firstEventScript.Contains(
                "/catQuestion",
                StringComparison.Ordinal))
        {
            Game1.year = originalYear;
            Game1.currentSeason = originalSeason;
            Game1.dayOfMonth = originalDay;
            Game1.stats.DaysPlayed = originalDaysPlayed;
            Game1.timeOfDay = originalTime;
            RestoreNativeStoryWeather(farmWeather, originalWeather);
            return NativeStoryHandoffBlocked(
                request,
                "native_kent_event_not_first_eligible_farm_event:" +
                (firstEventId ?? "none"));
        }

        Game1.dayOfMonth = 1;
        Game1.stats.DaysPlayed = 113;

        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Append("locked_farm_event_63_first_eligible_on_exact_wake_date")
            .Append("projected_native_wake_weather_matches_preflight")
            .Append("fixture_did_not_construct_or_start_event")
            .ToArray();
        result.RequestedEffect +=
            ";post_save_location_event=63;trigger=next_real_farm_entry";
        result.ObservedEffect +=
            ";event_63_first_eligible_on_wake_date=true" +
            ";event_63_data_key=" + firstEventKey +
            ";event_63_script_boundary=automatic_dialogue" +
            ";projected_wake_weather=Sun" +
            ";event_constructed=false" +
            ";date_before_sleep=2:spring:1" +
            ";stats_days_played_before_sleep=113";
        return result;
    }

    private TrainingExecutionResult PrepareNativeCatAdoptionUnsafeQuestion(
        TrainingExecutionRequest request)
    {
        if (Game1.player.hasPet() ||
            Game1.player.mailReceived.Contains("rejectedPet") ||
            Game1.player.eventsSeen.Contains(NativeCatAdoptionEventId) ||
            Game1.eventsSeenSinceLastLocationChange.Contains(
                NativeCatAdoptionEventId))
        {
            return NativeStoryHandoffBlocked(
                request,
                "native_cat_adoption_fresh_save_preconditions_required");
        }
        if (!Pet.TryGetData("Cat", out var petData) ||
            !string.Equals(
                petData.AdoptionEventLocation,
                "Farm",
                StringComparison.Ordinal) ||
            !string.Equals(
                petData.AdoptionEventId,
                NativeCatAdoptionEventId,
                StringComparison.Ordinal))
        {
            return NativeStoryHandoffBlocked(
                request,
                "locked_cat_adoption_data_mismatch");
        }
        if (!TryReadLockedCatAdoptionEvent(out var catEventScript) ||
            !catEventScript.Contains(
                "/catQuestion",
                StringComparison.Ordinal))
        {
            return NativeStoryHandoffBlocked(
                request,
                "locked_cat_adoption_event_key_or_script_mismatch");
        }

        var result = ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_native_story_handoff",
            "native_story_handoff_sleep_path_ready");
        if (result.Status != "applied")
        {
            return result;
        }

        var farmWeather = Game1.getFarm().GetWeather();
        var originalYear = Game1.year;
        var originalSeason = Game1.currentSeason;
        var originalDay = Game1.dayOfMonth;
        var originalDaysPlayed = Game1.stats.DaysPlayed;
        var originalTime = Game1.timeOfDay;
        var originalPetType = Game1.player.whichPetType;
        var originalTotalMoneyEarned = Game1.player.totalMoneyEarned;
        var originalWeather = CaptureNativeStoryWeather(farmWeather);

        var preparedTotalMoneyEarned = Math.Max(
            Game1.player.totalMoneyEarned,
            1000u);
        var projectedShippingIncome = Game1.getFarm()
            .getShippingBin(Game1.player)
            .OfType<StardewValley.Object>()
            .Aggregate(
                0UL,
                (total, item) => total +
                    (ulong)Math.Max(0, item.sellToStorePrice(-1L)) *
                    (uint)Math.Max(0, item.Stack));
        var projectedWakeTotalMoneyEarned = (uint)Math.Min(
            uint.MaxValue,
            (ulong)preparedTotalMoneyEarned + projectedShippingIncome);
        Game1.player.whichPetType = "Cat";
        Game1.player.totalMoneyEarned = projectedWakeTotalMoneyEarned;
        ApplySunnyNativeStoryWeather(farmWeather);
        Game1.year = 1;
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 26;
        Game1.stats.DaysPlayed = 26;
        Game1.timeOfDay = 600;

        if (!TryReadFirstEligibleFarmDataEvent(
                out var firstEventId,
                out var firstEventKey,
                out var firstEventScript) ||
            !string.Equals(
                firstEventId,
                NativeCatAdoptionEventId,
                StringComparison.Ordinal) ||
            !firstEventScript.Contains(
                "/catQuestion",
                StringComparison.Ordinal))
        {
            Game1.year = originalYear;
            Game1.currentSeason = originalSeason;
            Game1.dayOfMonth = originalDay;
            Game1.stats.DaysPlayed = originalDaysPlayed;
            Game1.timeOfDay = originalTime;
            Game1.player.whichPetType = originalPetType;
            Game1.player.totalMoneyEarned = originalTotalMoneyEarned;
            RestoreNativeStoryWeather(farmWeather, originalWeather);
            return NativeStoryHandoffBlocked(
                request,
                "native_cat_adoption_not_first_eligible_farm_event:" +
                (firstEventId ?? "none"));
        }

        Game1.dayOfMonth = 25;
        Game1.stats.DaysPlayed = 25;
        Game1.player.totalMoneyEarned = preparedTotalMoneyEarned;

        result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
            .Append("locked_cat_adoption_1590166_preconditions_ready")
            .Append("native_cat_adoption_first_eligible_on_exact_wake_date")
            .Append("native_next_day_friday_sunny_and_earned_money_ready")
            .Append("fixture_did_not_construct_start_or_answer_event")
            .ToArray();
        result.RequestedEffect +=
            ";post_save_location_event=1590166;expected_boundary=catQuestion";
        result.ObservedEffect +=
            ";cat_adoption_data_locked=true" +
            ";cat_adoption_first_eligible_on_wake_date=true" +
            ";cat_adoption_data_key=" + firstEventKey +
            ";projected_shipping_income=" + projectedShippingIncome +
            ";projected_wake_total_money_earned=" +
                projectedWakeTotalMoneyEarned +
            ";event_constructed=false";
        return result;
    }

    private static bool TryReadFirstEligibleFarmDataEvent(
        out string? eventId,
        out string eventKey,
        out string eventScript)
    {
        eventId = null;
        eventKey = string.Empty;
        eventScript = string.Empty;
        var farm = Game1.getFarm();
        var events = Game1.content.Load<Dictionary<string, string>>(
            "Data\\Events\\Farm");
        foreach (var pair in events)
        {
            var eligible = farm.checkEventPrecondition(pair.Key);
            if (string.IsNullOrEmpty(eligible) ||
                string.Equals(eligible, "-1", StringComparison.Ordinal) ||
                !GameLocation.IsValidLocationEvent(pair.Key, pair.Value))
            {
                continue;
            }

            eventId = eligible;
            eventKey = pair.Key;
            eventScript = pair.Value;
            return true;
        }

        return false;
    }

    private static NativeStoryWeatherState CaptureNativeStoryWeather(
        LocationWeather farmWeather) =>
        new(
            Game1.weatherForTomorrow,
            Game1.netWorldState.Value.WeatherForTomorrow,
            farmWeather.WeatherForTomorrow,
            farmWeather.Weather,
            farmWeather.IsRaining,
            farmWeather.IsSnowing,
            farmWeather.IsLightning,
            farmWeather.IsDebrisWeather,
            farmWeather.IsGreenRain);

    private static void ApplySunnyNativeStoryWeather(LocationWeather farmWeather)
    {
        Game1.weatherForTomorrow = Game1.weather_sunny;
        Game1.netWorldState.Value.WeatherForTomorrow = Game1.weather_sunny;
        farmWeather.WeatherForTomorrow = Game1.weather_sunny;
        farmWeather.Weather = Game1.weather_sunny;
        farmWeather.IsRaining = false;
        farmWeather.IsSnowing = false;
        farmWeather.IsLightning = false;
        farmWeather.IsDebrisWeather = false;
        farmWeather.IsGreenRain = false;
    }

    private static void RestoreNativeStoryWeather(
        LocationWeather farmWeather,
        NativeStoryWeatherState state)
    {
        Game1.weatherForTomorrow = state.GameWeatherForTomorrow;
        Game1.netWorldState.Value.WeatherForTomorrow =
            state.WorldWeatherForTomorrow;
        farmWeather.WeatherForTomorrow = state.FarmWeatherForTomorrow;
        farmWeather.Weather = state.FarmWeather;
        farmWeather.IsRaining = state.FarmRaining;
        farmWeather.IsSnowing = state.FarmSnowing;
        farmWeather.IsLightning = state.FarmLightning;
        farmWeather.IsDebrisWeather = state.FarmDebrisWeather;
        farmWeather.IsGreenRain = state.FarmGreenRain;
    }

    private sealed record NativeStoryWeatherState(
        string GameWeatherForTomorrow,
        string WorldWeatherForTomorrow,
        string FarmWeatherForTomorrow,
        string FarmWeather,
        bool FarmRaining,
        bool FarmSnowing,
        bool FarmLightning,
        bool FarmDebrisWeather,
        bool FarmGreenRain);

    private static bool TryReadLockedCatAdoptionEvent(out string eventScript)
    {
        var events = Game1.content.Load<Dictionary<string, string>>(
            "Data\\Events\\Farm");
        if (!events.TryGetValue(
                NativeCatAdoptionEventKey,
                out var lockedScript) ||
            string.IsNullOrWhiteSpace(lockedScript))
        {
            eventScript = string.Empty;
            return false;
        }

        eventScript = lockedScript;
        return true;
    }

    private static TrainingExecutionResult NativeStoryHandoffBlocked(
        TrainingExecutionRequest request,
        params string[] reasons) =>
        BlockedWithPrimitive(
            request,
            "debug_prepare_native_story_handoff",
            "native_story_handoff_preconditions=ready",
            "native_story_handoff_preconditions=blocked",
            reasons);
}
