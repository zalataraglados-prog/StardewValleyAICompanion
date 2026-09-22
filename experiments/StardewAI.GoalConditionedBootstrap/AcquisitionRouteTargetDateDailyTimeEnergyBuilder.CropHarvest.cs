using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateCropHarvestRoute(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionDailyTimeEnergySnapshotState state,
            string targetLocation,
            LiveCropState[] crops,
            int requiredCropCount,
            string[] cropEvidencePaths)
    {
        var routeState = state.RouteState;
        if (!routeState.RouteEvidenceAvailable || routeState.Timing is null)
        {
            return Result(
                route,
                "native_ready_crop_harvest",
                "blocked_daily_route_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                routeState.RouteEvidenceBlockingReasons);
        }

        var windows = RequirementRoute(route).MatchingWindows
            .Where(value => string.IsNullOrWhiteSpace(value.LocationId) ||
                string.Equals(
                    value.LocationId,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            .SelectMany(value => value.TimeWindows)
            .Select(value => new CropHarvestWindow(
                value.StartTime,
                value.EndTime))
            .Distinct()
            .OrderBy(value => value.StartTime)
            .ThenBy(value => value.EndTime)
            .ToArray();
        if (windows.Length == 0)
        {
            return Result(
                route,
                "native_ready_crop_harvest",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "ready_crop_authoritative_time_window_missing" });
        }

        var schedules = new List<CropHarvestSchedule>();
        var routeBlocks = new List<string>();
        foreach (var window in windows)
        {
            var schedule = BuildCropHarvestSchedule(
                state,
                targetLocation,
                crops,
                requiredCropCount,
                window,
                routeBlocks);
            if (schedule is not null)
                schedules.Add(schedule);
        }
        if (schedules.Count == 0)
        {
            return Result(
                route,
                "native_ready_crop_harvest",
                "blocked_daily_route_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                routeBlocks
                    .Append("crop_terminal_route_production_incomplete")
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray());
        }

        var selected = schedules
            .OrderByDescending(value => value.TimeMatches)
            .ThenBy(value => value.GuaranteedCompletionByTime)
            .ThenBy(value => value.Window.StartTime)
            .ThenBy(value => value.Window.EndTime)
            .First();
        var finalStep = selected.Steps[^1];
        var totalActionMinutes =
            CropHarvestBudgetPolicy.ConservativeGameMinutesForHarvests(
                requiredCropCount);
        var timingEvidenceIds = selected.Steps
            .Select(value => value.TimingEvidenceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (timingEvidenceIds.Length != 1)
        {
            throw new InvalidDataException(
                "Crop-harvest route mixed timing evidence identities.");
        }
        var evaluation = new AcquisitionDailyTimeEnergyEvaluation(
            targetLocation,
            finalStep.TargetTileX,
            finalStep.TargetTileY,
            finalStep.StandTileX,
            finalStep.StandTileY,
            routeState.CurrentTime,
            selected.Steps[0].GuaranteedArrivalByTime,
            selected.Window.StartTime,
            selected.Window.EndTime,
            totalActionMinutes,
            selected.GuaranteedCompletionByTime,
            requiredCropCount,
            null,
            null,
            null,
            null,
            0d,
            selected.TimeMatches,
            true,
            "native_exact_ready_crop_harvest_route_input_profile",
            timingEvidenceIds[0],
            cropEvidencePaths.Concat(new[]
                {
                    "target_date_processing_lead_time.routes[].evaluations[]",
                    "static_calendar_resolution.routes[].crop_source.harvest_min_stack",
                    "state.locations.route_graph.value",
                    "state.locations.social_route_date_evidence.value",
                    "route_timing_calibration",
                    "compiler:CropHarvestBudgetPolicy"
                })
                .Distinct(StringComparer.Ordinal)
                .ToArray())
        {
            TerminalRouteSteps = selected.Steps
        };
        return selected.TimeMatches
            ? Result(
                route,
                "native_ready_crop_harvest",
                "resolved_daily_time_energy_budget_match",
                true,
                true,
                evaluation,
                Array.Empty<string>(),
                Array.Empty<string>())
            : Result(
                route,
                "native_ready_crop_harvest",
                "resolved_daily_time_budget_miss",
                true,
                false,
                evaluation,
                new[]
                {
                    "crop_terminal_route_does_not_fit_source_window"
                },
                Array.Empty<string>());
    }

    private static CropHarvestSchedule? BuildCropHarvestSchedule(
        AcquisitionDailyTimeEnergySnapshotState state,
        string targetLocation,
        IReadOnlyCollection<LiveCropState> crops,
        int requiredCropCount,
        CropHarvestWindow window,
        ICollection<string> routeBlocks)
    {
        var routeState = state.RouteState;
        var remaining = crops
            .OrderBy(value => value.TileX)
            .ThenBy(value => value.TileY)
            .ToList();
        var steps = new List<AcquisitionDailyTerminalRouteStep>(
            requiredCropCount);
        var currentLocation = routeState.CurrentLocationId;
        var currentX = routeState.CurrentTileX;
        var currentY = routeState.CurrentTileY;
        var currentTime = routeState.CurrentTime;
        var priorActionMinutes = 0;
        var producer = new FutureRouteDateEvidenceProducer();
        while (steps.Count < requiredCropCount)
        {
            var approaches = remaining.Select(crop =>
                {
                    var production = producer.Produce(
                        routeState.RouteGraph,
                        routeState.SocialRouteDateEvidence,
                        new FutureRouteDateEvidenceRequest
                        {
                            TotalDays = state.TargetTotalDay,
                            StartLocation = currentLocation,
                            StartTileX = currentX,
                            StartTileY = currentY,
                            EarliestDepartureTime = currentTime,
                            TargetLocation = targetLocation,
                            TargetTileX = crop.TileX,
                            TargetTileY = crop.TileY,
                            RequireExactTargetTile = false
                        },
                        routeState.Timing!);
                    var approach = production.Scenario?.ApproachEvidence;
                    if (production.Status !=
                            FutureRouteDateEvidenceProductionStatus.Produced ||
                        !production.GuaranteedArrivalByTime.HasValue ||
                        approach is null ||
                        approach.Length != 1 ||
                        !approach[0].StandTileX.HasValue ||
                        !approach[0].StandTileY.HasValue)
                    {
                        foreach (var reason in production.BlockingReasons)
                            routeBlocks.Add(reason);
                        return null;
                    }
                    return new CropHarvestApproach(
                        crop,
                        production.GuaranteedArrivalByTime.Value,
                        approach[0].StandTileX.GetValueOrDefault(),
                        approach[0].StandTileY.GetValueOrDefault(),
                        approach[0].TimingEvidenceId);
                })
                .Where(value => value is not null)
                .Select(value => value!)
                .OrderBy(value => value.ArrivalTime)
                .ThenBy(value => value.Crop.TileX)
                .ThenBy(value => value.Crop.TileY)
                .ToArray();
            if (approaches.Length == 0)
                return null;

            var selected = approaches[0];
            var actionStart = steps.Count == 0
                ? Math.Max(selected.ArrivalTime, window.StartTime)
                : selected.ArrivalTime;
            var cumulativeActionMinutes =
                CropHarvestBudgetPolicy.ConservativeGameMinutesForHarvests(
                    steps.Count + 1);
            var actionMinutes = checked(
                cumulativeActionMinutes - priorActionMinutes);
            var completion = GameClockBudgetPolicy.AddClockMinutes(
                actionStart,
                actionMinutes);
            steps.Add(new AcquisitionDailyTerminalRouteStep(
                steps.Count + 1,
                targetLocation,
                selected.Crop.TileX,
                selected.Crop.TileY,
                selected.StandTileX,
                selected.StandTileY,
                currentTime,
                selected.ArrivalTime,
                actionStart,
                actionMinutes,
                completion,
                selected.TimingEvidenceId));
            priorActionMinutes = cumulativeActionMinutes;
            currentLocation = targetLocation;
            currentX = selected.StandTileX;
            currentY = selected.StandTileY;
            currentTime = completion;
            remaining.Remove(selected.Crop);
        }

        var finalCompletion = steps[^1].GuaranteedCompletionByTime;
        return new CropHarvestSchedule(
            window,
            steps.ToArray(),
            finalCompletion,
            GameClockBudgetPolicy.ClockMinutesBetween(
                finalCompletion,
                window.EndTime) >= 0);
    }

    private sealed record CropHarvestWindow(int StartTime, int EndTime);

    private sealed record CropHarvestApproach(
        LiveCropState Crop,
        int ArrivalTime,
        int StandTileX,
        int StandTileY,
        string TimingEvidenceId);

    private sealed record CropHarvestSchedule(
        CropHarvestWindow Window,
        AcquisitionDailyTerminalRouteStep[] Steps,
        int GuaranteedCompletionByTime,
        bool TimeMatches);
}
