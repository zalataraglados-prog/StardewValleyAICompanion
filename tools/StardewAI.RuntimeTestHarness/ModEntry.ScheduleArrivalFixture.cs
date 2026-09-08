using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.GameData;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupScheduleArrivalFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.Date.Season = Season.Spring;
        Game1.Date.DayOfMonth = 16;
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 16;
        Game1.timeOfDay = 600;
        Game1.netWorldState.Value.ActivePassiveFestivals.Clear();
        Game1.netWorldState.Value.ActivePassiveFestivals.Add("DesertFestival");

        NPC? maru = null;
        var loadedCount = 0;
        Utility.ForEachVillager(npc =>
        {
            npc.ignoreScheduleToday = false;
            npc.ClearSchedule();
            if (npc.TryLoadSchedule())
                loadedCount++;
            if (npc.Name == "Maru")
                maru = npc;
            return true;
        }, includeEventActors: false);

        var arrivalEntry = maru?.Schedule?
            .OrderBy(entry => entry.Key)
            .FirstOrDefault(entry =>
                entry.Value.targetLocationName == "Desert" &&
                entry.Value.targetTile.X == 40 &&
                entry.Value.targetTile.Y == 41);
        var verified = Utility.GetDayOfPassiveFestival("DesertFestival") == 2 &&
            maru is not null &&
            !maru.isMarried() &&
            maru.ScheduleKey == "DesertFestival_2" &&
            maru.Schedule is not null &&
            arrivalEntry.HasValue &&
            arrivalEntry.Value.Value.route is not null;
        var observed =
            $"season={Game1.currentSeason};day={Game1.dayOfMonth};time={Game1.timeOfDay};" +
            $"festival_day={Utility.GetDayOfPassiveFestival("DesertFestival")};" +
            $"loaded_schedules={loadedCount};maru_key={maru?.ScheduleKey ?? string.Empty};" +
            $"maru_arrival_departure={arrivalEntry?.Key.ToString() ?? string.Empty};" +
            $"maru_arrival_route_points={arrivalEntry?.Value.route?.Count.ToString() ?? string.Empty}";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_schedule_arrival_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_try_load_schedule_generated_desert_festival_arrival_path" }
                : new[] { "schedule_arrival_fixture_postcondition_mismatch" },
            RequestedEffect = "native_schedule_arrival_fixture=DesertFestival_2",
            ObservedEffect = observed,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "schedule_arrival_fixture_postcondition_mismatch" }
        };
    }
}
