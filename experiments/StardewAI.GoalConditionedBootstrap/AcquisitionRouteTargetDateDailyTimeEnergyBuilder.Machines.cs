using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private const int MachineItemPlacedInTrigger = 1;

    internal static AcquisitionRouteTargetDateDailyTimeEnergy EvaluateMachine(
        AcquisitionRouteTargetDateStochasticRetry route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionDailyTimeEnergySnapshotState state)
    {
        var source = staticRoute.MachineSource;
        if (source is null)
        {
            return MachineBlocked(
                route,
                "authoritative_machine_source_missing");
        }

        var inputTriggers = source.Triggers.Where(value =>
                (value.Trigger & MachineItemPlacedInTrigger) != 0)
            .ToArray();
        if (inputTriggers.Length > 0 &&
            inputTriggers.Length != source.Triggers.Length)
        {
            return MachineBlocked(
                route,
                "machine_mixed_trigger_daily_budget_requires_route_expansion");
        }

        return inputTriggers.Length > 0
            ? EvaluateManualMachine(route, staticRoute, state)
            : EvaluateAutomaticMachine(route, staticRoute, state);
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateManualMachine(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionDailyTimeEnergySnapshotState state)
    {
        var processing = route.UpstreamRoute;
        var source = staticRoute.MachineSource!;
        if (!route.RequiredAttemptCount.HasValue ||
            route.RequiredAttemptCount < 0 ||
            route.RequiredAttemptCount > 0 &&
            (source.DaysUntilReady >= 0 ||
             source.MinutesUntilReady < 0))
        {
            return MachineBlocked(
                route,
                "manual_machine_attempt_or_duration_evidence_incomplete");
        }

        var evaluations = processing.Evaluations
            .Where(value =>
                value.MachineScheduleBinding is not null &&
                (value.MachineScheduleBinding.ScheduledAttemptCount > 0 ||
                 value.MachineScheduleBinding
                     .CreditedExistingOutputQuantity > 0))
            .OrderBy(value => value.TargetLocationId, StringComparer.Ordinal)
            .ThenBy(value => value.MachineScheduleBinding!.TargetTileY)
            .ThenBy(value => value.MachineScheduleBinding!.TargetTileX)
            .ToArray();
        var bindings = evaluations.Select(value =>
                value.MachineScheduleBinding!)
            .ToArray();
        var attempts = bindings.SelectMany(value => value.AttemptSchedule)
            .OrderBy(value => value.AttemptOrdinal)
            .ToArray();
        if (bindings.Sum(value => value.ScheduledAttemptCount) !=
                route.RequiredAttemptCount ||
            attempts.Length != route.RequiredAttemptCount ||
            !attempts.Select(value => value.AttemptOrdinal)
                .SequenceEqual(Enumerable.Range(
                    1,
                    route.RequiredAttemptCount.Value)))
        {
            return MachineBlocked(
                route,
                "manual_machine_attempt_schedule_incomplete_or_drifted");
        }

        var targets = new List<MachineDailyTargetSeed>();
        var requiredExistingOutputSlots = 0;
        for (var index = 0; index < evaluations.Length; index++)
        {
            var evaluation = evaluations[index];
            var binding = bindings[index];
            if (!state.RouteState.MachineFleet.TryGet(
                     evaluation.TargetLocationId,
                     binding.TargetTileX,
                     binding.TargetTileY,
                     out var machine) ||
                 machine.QualifiedItemId != binding.MachineQualifiedItemId ||
                 machine.CapacityState != binding.InitialCapacityState ||
                 machine.MinutesUntilReady !=
                     binding.InitialMinutesUntilReady)
            {
                return MachineBlocked(
                    route,
                    "manual_machine_initial_capacity_drifted:" +
                    evaluation.TargetLocationId + ":" +
                    binding.TargetTileX + "," + binding.TargetTileY);
            }
            if (binding.AttemptSchedule.Length !=
                    binding.ScheduledAttemptCount ||
                binding.AttemptSchedule.Select(value =>
                        value.MachineAttemptOrdinal)
                    .Order()
                    .SequenceEqual(Enumerable.Range(
                        1,
                        binding.ScheduledAttemptCount)) == false)
            {
                return MachineBlocked(
                    route,
                    "manual_machine_local_attempt_schedule_drifted");
            }
            var clearsExistingOutput = binding.InitialCapacityState is
                "processing" or "ready_output";
            if (clearsExistingOutput &&
                (!machine.ActiveOutputEvidenceAvailable ||
                 machine.ActiveOutput is null ||
                 !machine.MachineHasOutput))
            {
                return MachineBlocked(
                    route,
                    "manual_machine_existing_output_evidence_incomplete:" +
                    evaluation.TargetLocationId + ":" +
                    binding.TargetTileX + "," + binding.TargetTileY);
            }
            if (binding.CreditedExistingOutputQuantity > 0 &&
                (machine.ActiveOutput is not { } activeOutput ||
                 activeOutput.QualifiedItemId != staticRoute.QualifiedItemId ||
                 activeOutput.Quality < staticRoute.MinimumQuality ||
                 activeOutput.Stack <
                     binding.CreditedExistingOutputQuantity ||
                 activeOutput.RouteSources.Length != 1 ||
                 activeOutput.RouteSources[0].RouteKind !=
                     staticRoute.RouteKind ||
                 activeOutput.RouteSources[0].SourceId !=
                     staticRoute.SourceId ||
                 activeOutput.RouteSources[0].QualifiedItemId !=
                     staticRoute.QualifiedItemId))
            {
                return MachineBlocked(
                    route,
                    "manual_machine_existing_output_credit_drifted:" +
                    evaluation.TargetLocationId + ":" +
                    binding.TargetTileX + "," + binding.TargetTileY);
            }
            if (clearsExistingOutput)
                requiredExistingOutputSlots++;
            targets.Add(new MachineDailyTargetSeed(
                evaluation.TargetLocationId,
                binding.TargetTileX,
                binding.TargetTileY,
                binding.ScheduledAttemptCount,
                source.MinutesUntilReady,
                Math.Max(1, source.MinimumStack),
                binding.CreditedExistingOutputQuantity,
                clearsExistingOutput
                    ? MachineDailyAction.CollectOutput
                    : MachineDailyAction.LoadInput,
                binding.InitialCapacityState == "processing"
                    ? GameClockBudgetPolicy.AddClockMinutes(
                        state.RouteState.CurrentTime,
                        binding.InitialMinutesUntilReady)
                    : state.RouteState.CurrentTime,
                clearsExistingOutput,
                true));
        }

        if (requiredExistingOutputSlots > 0 &&
            (!state.EmptyInventorySlots.HasValue ||
             state.EmptyInventorySlots < requiredExistingOutputSlots))
        {
            return MachineBlocked(
                route,
                state.InventoryCapacityBlockingReasons
                    .Append(
                        "manual_machine_existing_output_inventory_capacity_shortfall:" +
                        (state.EmptyInventorySlots?.ToString() ?? "unknown") +
                        ":" + requiredExistingOutputSlots)
                    .ToArray());
        }

        return EvaluateMachineSchedules(
            route,
            state,
            targets.ToArray(),
            route.RequiredAttemptCount.Value,
            staticRoute.RequiredAmount,
            "native_manual_machine_load_collect_route_input_profile");
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateAutomaticMachine(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionDailyTimeEnergySnapshotState state)
    {
        var targets = route.UpstreamRoute.Evaluations
            .Where(value =>
                value.OutputReadyOnTargetDate == true &&
                value.ProvenOutputQuantityLowerBound > 0 &&
                value.ProvenMinimumQuality >= staticRoute.MinimumQuality &&
                value.MachineScheduleBinding is
                {
                    ActiveOutputRouteMatches: true,
                    CompletionOffsetMinutes: not null
                })
            .OrderBy(value => value.TargetLocationId, StringComparer.Ordinal)
            .ThenBy(value => value.MachineScheduleBinding!.TargetTileY)
            .ThenBy(value => value.MachineScheduleBinding!.TargetTileX)
            .Select(value => new MachineDailyTargetSeed(
                value.TargetLocationId,
                value.MachineScheduleBinding!.TargetTileX,
                value.MachineScheduleBinding.TargetTileY,
                0,
                0,
                value.ProvenOutputQuantityLowerBound!.Value,
                0,
                MachineDailyAction.CollectOutput,
                GameClockBudgetPolicy.AddClockMinutes(
                    state.RouteState.CurrentTime,
                    value.MachineScheduleBinding.CompletionOffsetMinutes!.Value),
                false,
                false))
            .ToArray();
        if (targets.Length == 0 ||
            targets.Sum(value => value.OutputQuantityPerCollection) <
                staticRoute.RequiredAmount)
        {
            return MachineBlocked(
                route,
                "automatic_machine_collectible_output_schedule_missing");
        }

        return EvaluateMachineSchedules(
            route,
            state,
            targets,
            targets.Length,
            staticRoute.RequiredAmount,
            "native_automatic_machine_collect_route_input_profile");
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy
        EvaluateMachineSchedules(
            AcquisitionRouteTargetDateStochasticRetry route,
            AcquisitionDailyTimeEnergySnapshotState state,
            MachineDailyTargetSeed[] targets,
            int attemptCount,
            int requiredOutputQuantity,
            string executionAssumption)
    {
        var windows = MachineWindows(route, targets);
        if (windows.Length == 0)
        {
            return MachineBlocked(
                route,
                "machine_authoritative_time_window_missing");
        }

        var schedules = new List<MachineDailySchedule>();
        var routeBlocks = new List<string>();
        foreach (var window in windows)
        {
            var schedule = BuildMachineDailySchedule(
                state,
                targets,
                requiredOutputQuantity,
                window,
                routeBlocks);
            if (schedule is not null)
                schedules.Add(schedule);
        }
        if (schedules.Count == 0)
        {
            return MachineBlocked(
                route,
                routeBlocks
                    .Append("machine_terminal_route_production_incomplete")
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
        var final = selected.Steps[^1];
        var timingIds = selected.Steps
            .Select(value => value.TimingEvidenceId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (timingIds.Length != 1)
        {
            throw new InvalidDataException(
                "Machine daily route mixed timing evidence identities.");
        }
        var actionMinutes = selected.Steps.Sum(value =>
            value.ActionGameMinutes);
        var evaluatedAttemptCount = targets.Any(value =>
                value.IsManualProductionTarget)
            ? attemptCount
            : selected.Steps.Count(value =>
                value.ActionKind == MachineDailyAction.CollectOutput);
        var evaluation = new AcquisitionDailyTimeEnergyEvaluation(
            final.TargetLocationId,
            final.TargetTileX,
            final.TargetTileY,
            final.StandTileX,
            final.StandTileY,
            state.RouteState.CurrentTime,
            selected.Steps[0].GuaranteedArrivalByTime,
            selected.Window.StartTime,
            selected.Window.EndTime,
            actionMinutes,
            selected.GuaranteedCompletionByTime,
            evaluatedAttemptCount,
            null,
            state.AvailableEnergy,
            0d,
            0d,
            0d,
            selected.TimeMatches,
            true,
            executionAssumption,
            timingIds[0],
            new[]
            {
                "target_date_processing_lead_time.routes[].evaluations[].machine_schedule_binding.attempt_schedule[]",
                "target_date_stochastic_retry_budget.routes[].required_attempt_count",
                "state.farm.machines.value[]",
                "state.player.inventory_capacity.value",
                "state.locations.route_graph.value",
                "state.locations.social_route_date_evidence.value",
                "route_timing_calibration",
                "compiler:MachineInteractionBudgetPolicy"
            })
        {
            TerminalRouteSteps = selected.Steps
        };
        return selected.TimeMatches
            ? Result(
                route,
                "native_machine_interaction_schedule",
                "resolved_daily_time_energy_budget_match",
                true,
                true,
                evaluation,
                Array.Empty<string>(),
                Array.Empty<string>())
            : Result(
                route,
                "native_machine_interaction_schedule",
                "resolved_daily_time_budget_miss",
                true,
                false,
                evaluation,
                new[]
                {
                    "machine_terminal_route_does_not_fit_source_window"
                },
                Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy MachineBlocked(
        AcquisitionRouteTargetDateStochasticRetry route,
        params string[] reasons) => Result(
            route,
            "native_machine_interaction_schedule",
            "blocked_daily_terminal_budget_evidence",
            false,
            null,
            null,
            Array.Empty<string>(),
            reasons);
}
