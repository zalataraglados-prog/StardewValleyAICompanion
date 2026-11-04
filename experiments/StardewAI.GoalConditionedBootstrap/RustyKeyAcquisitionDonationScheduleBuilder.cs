using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static class RustyKeyAcquisitionDonationScheduleBuilder
{
    internal static RustyKeyAcquisitionDonationSchedule Build(int totalDay,
        AcquisitionRouteTargetDateOpportunityCost route, SnapshotEnvelope snapshot,
        Lazy<MuseumDonationRouteScheduleBuilder> scheduler)
    {
        var daily = route.UpstreamRoute;
        var endpoint = daily.Evaluation;
        var reasons = new List<string>();
        var clock = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
        if (!daily.DailyTimeEnergyAxisResolved || daily.DailyTimeEnergyMatchesTargetDate != true ||
            endpoint is null || endpoint.TimeBudgetMatches != true || !clock.HasValue ||
            endpoint.SnapshotStartTime != clock || string.IsNullOrWhiteSpace(endpoint.TimingEvidenceId) ||
            string.IsNullOrWhiteSpace(endpoint.TargetLocationId) || endpoint.StandTileX is null or < 0 ||
            endpoint.StandTileY is null or < 0 || !endpoint.GuaranteedArrivalByTime.HasValue ||
            !endpoint.GuaranteedCompletionByTime.HasValue ||
            !ValidClock(endpoint.GuaranteedArrivalByTime.Value) || !ValidClock(endpoint.GuaranteedCompletionByTime.Value) ||
            endpoint.GuaranteedArrivalByTime < clock || endpoint.GuaranteedCompletionByTime < endpoint.GuaranteedArrivalByTime)
            reasons.Add("rusty_key_acquisition_terminal_schedule_unavailable");
        if (endpoint is { TerminalRouteSteps.Length: > 0 })
        {
            var last = endpoint.TerminalRouteSteps[^1];
            if (!endpoint.TerminalRouteSteps.Select(step => step.Ordinal).SequenceEqual(Enumerable.Range(1, endpoint.TerminalRouteSteps.Length)) ||
                last.TargetLocationId != endpoint.TargetLocationId || last.StandTileX != endpoint.StandTileX ||
                last.StandTileY != endpoint.StandTileY || last.GuaranteedCompletionByTime != endpoint.GuaranteedCompletionByTime)
                reasons.Add("rusty_key_acquisition_terminal_schedule_endpoint_mismatch");
        }
        var schedule = reasons.Count == 0 ? scheduler.Value.BuildContinuation(totalDay, endpoint!.TargetLocationId,
            endpoint.StandTileX!.Value, endpoint.StandTileY!.Value, endpoint.GuaranteedCompletionByTime!.Value) : null;
        if (schedule is not null) reasons.AddRange(schedule.BlockingReasons);
        return new(route.RouteOccurrenceId,
            endpoint is null ? string.Empty : AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(endpoint),
            endpoint?.TargetLocationId ?? string.Empty, endpoint?.StandTileX, endpoint?.StandTileY,
            endpoint?.GuaranteedCompletionByTime, schedule, reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }

    private static bool ValidClock(int value) => value is >= 600 and <= GameClockBudgetPolicy.DayEndTime && value % 100 < 60;
}
