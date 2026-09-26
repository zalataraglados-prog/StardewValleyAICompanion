using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private static MachineDailyWindow[] MachineWindows(
        AcquisitionRouteTargetDateStochasticRetry route,
        IReadOnlyCollection<MachineDailyTargetSeed> targets)
    {
        var locations = targets.Select(value => value.LocationId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return RequirementRoute(route).MatchingWindows
            .Where(value => string.IsNullOrWhiteSpace(value.LocationId) ||
                locations.All(location => string.Equals(
                    location,
                    value.LocationId,
                    StringComparison.OrdinalIgnoreCase)))
            .SelectMany(value => value.TimeWindows)
            .Select(value => new MachineDailyWindow(
                value.StartTime,
                value.EndTime))
            .Distinct()
            .OrderBy(value => value.StartTime)
            .ThenBy(value => value.EndTime)
            .ToArray();
    }

    private static MachineDailySchedule? BuildMachineDailySchedule(
        AcquisitionDailyTimeEnergySnapshotState state,
        IReadOnlyCollection<MachineDailyTargetSeed> seeds,
        int requiredOutputQuantity,
        MachineDailyWindow window,
        ICollection<string> routeBlocks)
    {
        var routeState = state.RouteState;
        if (!routeState.RouteEvidenceAvailable || routeState.Timing is null)
        {
            foreach (var reason in routeState.RouteEvidenceBlockingReasons)
                routeBlocks.Add(reason);
            return null;
        }

        var targets = seeds.Select(value => new MutableMachineDailyTarget(value))
            .ToArray();
        var steps = new List<AcquisitionDailyTerminalRouteStep>();
        var currentLocation = routeState.CurrentLocationId;
        var currentX = routeState.CurrentTileX;
        var currentY = routeState.CurrentTileY;
        var currentTime = routeState.CurrentTime;
        var collectedOutput = 0;
        var producer = new FutureRouteDateEvidenceProducer();
        while (!MachineScheduleComplete(
                   targets,
                   collectedOutput,
                   requiredOutputQuantity))
        {
            var approaches = targets
                .Where(value => !value.Done)
                .Select(target => MachineApproach(
                    producer,
                    state,
                    target,
                    currentLocation,
                    currentX,
                    currentY,
                    currentTime,
                    window,
                    routeBlocks))
                .Where(value => value is not null)
                .Select(value => value!)
                .OrderBy(value => value.CompletionTime)
                .ThenBy(value => value.Target.Seed.LocationId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.Target.Seed.TileY)
                .ThenBy(value => value.Target.Seed.TileX)
                .ToArray();
            if (approaches.Length == 0)
                return null;

            var selected = approaches[0];
            var target = selected.Target;
            steps.Add(new AcquisitionDailyTerminalRouteStep(
                steps.Count + 1,
                target.Seed.LocationId,
                target.Seed.TileX,
                target.Seed.TileY,
                selected.StandTileX,
                selected.StandTileY,
                currentTime,
                selected.ArrivalTime,
                selected.ActionStartTime,
                selected.ActionMinutes,
                selected.CompletionTime,
                selected.TimingEvidenceId)
            {
                ActionKind = target.NextAction
            });

            currentLocation = target.Seed.LocationId;
            currentX = selected.StandTileX;
            currentY = selected.StandTileY;
            currentTime = selected.CompletionTime;
            if (target.NextAction == MachineDailyAction.LoadInput)
            {
                target.LoadedAttemptCount++;
                target.NextAction = MachineDailyAction.CollectOutput;
                target.ReadyAtTime = GameClockBudgetPolicy.AddClockMinutes(
                    currentTime,
                    target.Seed.ProcessingMinutes);
            }
            else
            {
                if (target.ClearingExistingOutput)
                {
                    collectedOutput = checked(
                        collectedOutput +
                        target.Seed.CreditedExistingOutputQuantity);
                    target.ClearingExistingOutput = false;
                    if (target.Seed.RequiredAttemptCount == 0)
                    {
                        target.Done = true;
                        continue;
                    }
                    target.NextAction = MachineDailyAction.LoadInput;
                    target.ReadyAtTime = currentTime;
                    continue;
                }
                target.CollectedAttemptCount++;
                collectedOutput = checked(
                    collectedOutput +
                    target.Seed.OutputQuantityPerCollection);
                if (target.LoadedAttemptCount <
                    target.Seed.RequiredAttemptCount)
                {
                    target.NextAction = MachineDailyAction.LoadInput;
                    target.ReadyAtTime = currentTime;
                }
                else
                {
                    target.Done = true;
                }
            }
        }

        var completion = steps[^1].GuaranteedCompletionByTime;
        return new MachineDailySchedule(
            window,
            steps.ToArray(),
            completion,
            GameClockBudgetPolicy.ClockMinutesBetween(
                completion,
                window.EndTime) >= 0);
    }

    private static MachineDailyApproach? MachineApproach(
        FutureRouteDateEvidenceProducer producer,
        AcquisitionDailyTimeEnergySnapshotState state,
        MutableMachineDailyTarget target,
        string currentLocation,
        int currentX,
        int currentY,
        int currentTime,
        MachineDailyWindow window,
        ICollection<string> routeBlocks)
    {
        var routeState = state.RouteState;
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
                TargetLocation = target.Seed.LocationId,
                TargetTileX = target.Seed.TileX,
                TargetTileY = target.Seed.TileY,
                RequireExactTargetTile = false
            },
            routeState.Timing!);
        var approaches = production.Scenario?.ApproachEvidence;
        if (production.Status !=
                FutureRouteDateEvidenceProductionStatus.Produced ||
            !production.GuaranteedArrivalByTime.HasValue ||
            approaches is null ||
            approaches.Length != 1 ||
            !approaches[0].StandTileX.HasValue ||
            !approaches[0].StandTileY.HasValue)
        {
            foreach (var reason in production.BlockingReasons)
                routeBlocks.Add(reason);
            return null;
        }

        var arrival = production.GuaranteedArrivalByTime.Value;
        var actionStart = LatestTime(
            arrival,
            target.ReadyAtTime,
            window.StartTime);
        var actionMinutes = target.NextAction ==
                MachineDailyAction.LoadInput
            ? MachineInteractionBudgetPolicy
                .ConservativeGameMinutesForLoads(1)
            : MachineInteractionBudgetPolicy
                .ConservativeGameMinutesForCollects(1);
        return new MachineDailyApproach(
            target,
            arrival,
            approaches[0].StandTileX.GetValueOrDefault(),
            approaches[0].StandTileY.GetValueOrDefault(),
            actionStart,
            actionMinutes,
            GameClockBudgetPolicy.AddClockMinutes(
                actionStart,
                actionMinutes),
            approaches[0].TimingEvidenceId);
    }

    private static bool MachineScheduleComplete(
        IEnumerable<MutableMachineDailyTarget> targets,
        int collectedOutput,
        int requiredOutputQuantity)
    {
        var rows = targets.ToArray();
        var manual = rows.Any(value =>
            value.Seed.IsManualProductionTarget);
        return manual
            ? rows.All(value => value.Done) &&
                collectedOutput >= requiredOutputQuantity
            : collectedOutput >= requiredOutputQuantity;
    }

    private static int LatestTime(params int[] values) => values
        .OrderBy(value => value / 100 * 60 + value % 100)
        .Last();

    private static class MachineDailyAction
    {
        public const string LoadInput = "load_machine_input";
        public const string CollectOutput = "collect_machine_output";
    }

    private sealed record MachineDailyTargetSeed(
        string LocationId,
        int TileX,
        int TileY,
        int RequiredAttemptCount,
        int ProcessingMinutes,
        int OutputQuantityPerCollection,
        int CreditedExistingOutputQuantity,
        string InitialAction,
        int InitialReadyAtTime,
        bool InitialCollectionClearsExistingOutput,
        bool IsManualProductionTarget);

    private sealed class MutableMachineDailyTarget
    {
        public MutableMachineDailyTarget(MachineDailyTargetSeed seed)
        {
            Seed = seed;
            NextAction = seed.InitialAction;
            ReadyAtTime = seed.InitialReadyAtTime;
            ClearingExistingOutput =
                seed.InitialCollectionClearsExistingOutput;
        }

        public MachineDailyTargetSeed Seed { get; }

        public string NextAction { get; set; }

        public int ReadyAtTime { get; set; }

        public int LoadedAttemptCount { get; set; }

        public int CollectedAttemptCount { get; set; }

        public bool ClearingExistingOutput { get; set; }

        public bool Done { get; set; }
    }

    private sealed record MachineDailyWindow(int StartTime, int EndTime);

    private sealed record MachineDailyApproach(
        MutableMachineDailyTarget Target,
        int ArrivalTime,
        int StandTileX,
        int StandTileY,
        int ActionStartTime,
        int ActionMinutes,
        int CompletionTime,
        string TimingEvidenceId);

    private sealed record MachineDailySchedule(
        MachineDailyWindow Window,
        AcquisitionDailyTerminalRouteStep[] Steps,
        int GuaranteedCompletionByTime,
        bool TimeMatches);
}
