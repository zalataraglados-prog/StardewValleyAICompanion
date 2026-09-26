namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private const int ItemPlacedInMachineTrigger = 1;

    internal static AcquisitionRouteTargetDateProcessing EvaluateMachine(
        AcquisitionRouteTargetDateReservation route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        var source = staticRoute.MachineSource;
        if (source is null)
            return Blocked(route, MachineProduction,
                "authoritative_machine_source_missing");
        var staticBlocking = ValidateMachineProcessingSource(
            source,
            staticRoute.MinimumQuality);
        if (staticBlocking.Length > 0)
            return Blocked(route, MachineProduction, staticBlocking);
        if (!state.MachineFleet.EvidenceAvailable)
        {
            return Blocked(
                route,
                MachineProduction,
                state.MachineFleet.BlockingReasons);
        }
        if (!state.Calendar.Available ||
            state.Calendar.CurrentTotalDay != targetTotalDay)
        {
            return Blocked(
                route,
                MachineProduction,
                state.Calendar.Available
                    ? "machine_calendar_target_date_mismatch"
                    : state.Calendar.BlockingReason);
        }
        if (!TryRemainingPlayableMinutes(
                state.Calendar.TimeOfDay,
                out var remainingPlayableMinutes))
        {
            return Blocked(
                route,
                MachineProduction,
                "machine_calendar_time_outside_playable_day");
        }

        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var targetResult = ReadMachineTargets(
            facility,
            staticRoute,
            state.MachineFleet);
        if (targetResult.BlockingReasons.Length > 0)
        {
            return Blocked(
                route,
                MachineProduction,
                targetResult.BlockingReasons);
        }
        if (targetResult.Targets.Length == 0)
        {
            return Blocked(
                route,
                MachineProduction,
                "matched_machine_route_has_no_exact_machine_target");
        }

        var inputTriggers = source.Triggers.Where(trigger =>
                (trigger.Trigger & ItemPlacedInMachineTrigger) != 0)
            .ToArray();
        if (inputTriggers.Length > 0 &&
            inputTriggers.Length != source.Triggers.Length)
        {
            return Blocked(
                route,
                MachineProduction,
                "machine_mixed_trigger_processing_requires_route_expansion");
        }
        return inputTriggers.Length > 0
            ? EvaluateManualMachineSchedule(
                route,
                staticRoute,
                source,
                targetResult.Targets,
                remainingPlayableMinutes,
                targetTotalDay)
            : EvaluateAutomaticMachineSchedule(
                route,
                staticRoute,
                source,
                targetResult.Targets,
                remainingPlayableMinutes,
                targetTotalDay);
    }

    private static AcquisitionRouteTargetDateProcessing
        EvaluateManualMachineSchedule(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionMachineSourceEvidence source,
            MachineProcessingTarget[] targets,
            int remainingPlayableMinutes,
            int targetTotalDay)
    {
        var resource = route.UpstreamRoute.UpstreamRoute;
        var attemptCounts = resource.InputEvaluations
            .Select(evaluation =>
                evaluation.MachineBinding?.RequiredAttemptCount)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();
        var creditedQuantities = resource.InputEvaluations
            .Select(evaluation =>
                evaluation.MachineBinding?.CreditedExistingOutputQuantity)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray();
        var targetCredit = targets.Sum(value =>
            value.CreditedExistingOutputQuantity);
        if (attemptCounts.Length != 1 || attemptCounts[0] < 0 ||
            creditedQuantities.Length != 1 ||
            creditedQuantities[0] != targetCredit)
        {
            return Blocked(
                route,
                MachineProduction,
                "machine_resource_attempt_or_credit_missing_or_inconsistent");
        }

        var attemptCount = attemptCounts[0];
        var outputPerAttempt = Math.Max(1, source.MinimumStack);
        var quality = MachineMinimumQuality(source);
        var schedules = targets.Select(target => new MutableMachineSchedule(
                target,
                target.Machine.CapacityState == "processing"
                    ? Math.Max(0, target.Machine.MinutesUntilReady)
                    : 0))
            .ToArray();
        for (var attempt = 0; attempt < attemptCount; attempt++)
        {
            var candidate = schedules
                .OrderBy(value => value.AvailableOffsetMinutes)
                .ThenBy(value => value.Target.Machine.LocationId,
                    StringComparer.Ordinal)
                .ThenBy(value => value.Target.Machine.TileY)
                .ThenBy(value => value.Target.Machine.TileX)
                .First();
            if (!TryMachineCompletionOffset(
                    source,
                    candidate.AvailableOffsetMinutes,
                    remainingPlayableMinutes,
                    out var completionOffset))
            {
                break;
            }
            candidate.AttemptSchedule.Add(
                new AcquisitionMachineProcessingAttemptBinding(
                    attempt + 1,
                    candidate.ScheduledAttemptCount + 1,
                    candidate.AvailableOffsetMinutes,
                    completionOffset));
            candidate.ScheduledAttemptCount++;
            candidate.AvailableOffsetMinutes = completionOffset;
            candidate.LastCompletionOffsetMinutes = completionOffset;
        }

        var evaluations = schedules.Select(schedule =>
            {
                var credited = schedule.Target
                    .CreditedExistingOutputQuantity;
                var creditedCompletion = credited > 0
                    ? schedule.Target.Machine.ReadyForHarvest
                        ? 0
                        : Math.Max(
                            0,
                            schedule.Target.Machine.MinutesUntilReady)
                    : (int?)null;
                var completion = schedule.LastCompletionOffsetMinutes;
                if (creditedCompletion.HasValue &&
                    (!completion.HasValue ||
                     creditedCompletion > completion))
                {
                    completion = creditedCompletion;
                }
                var newlyProducedQuantity = schedule.ScheduledAttemptCount > 0
                    ? AcquisitionQuantityMath.Multiply(
                        outputPerAttempt,
                        schedule.ScheduledAttemptCount)
                    : 0;
                var provenQuantity = checked(
                    credited + newlyProducedQuantity);
                return
                MachineEvaluation(
                    schedule.Target,
                    "manual_input_processing",
                    provenQuantity > 0
                        ? "resolved_machine_attempts_ready_on_target_date"
                        : "resolved_machine_no_new_output_ready_on_target_date",
                    source,
                    attemptCount,
                    schedule.ScheduledAttemptCount,
                    completion,
                    remainingPlayableMinutes,
                    provenQuantity,
                    quality,
                     provenQuantity > 0 &&
                         completion <= remainingPlayableMinutes,
                     targetTotalDay,
                     credited > 0,
                     credited > 0 &&
                         schedule.ScheduledAttemptCount == 0 &&
                         schedule.Target.Machine.ReadyForHarvest,
                     schedule.AttemptSchedule.ToArray(),
                     credited);
            })
            .ToArray();
        var scheduledAttempts = schedules.Sum(value =>
            value.ScheduledAttemptCount);
        var provenQuantity = AcquisitionOutputProof.ReadyQuantity(
            evaluations,
            staticRoute.MinimumQuality);
        if (scheduledAttempts >= attemptCount &&
            provenQuantity >= staticRoute.RequiredAmount)
        {
            return ResolvedMatch(route, MachineProduction, evaluations);
        }
        return ResolvedMiss(
            route,
            MachineProduction,
            evaluations,
            "machine_processing_capacity_before_day_end_shortfall:" +
            scheduledAttempts + ":" + attemptCount);
    }

    private static AcquisitionRouteTargetDateProcessing
        EvaluateAutomaticMachineSchedule(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionMachineSourceEvidence source,
            MachineProcessingTarget[] targets,
            int remainingPlayableMinutes,
            int targetTotalDay)
    {
        var evaluations = new List<AcquisitionProcessingLeadTimeEvaluation>();
        var blocking = new List<string>();
        foreach (var target in targets)
        {
            var machine = target.Machine;
            if (!machine.ActiveOutputEvidenceAvailable)
            {
                blocking.Add(
                    "machine_active_output_projection_missing:" +
                    MachineTargetKey(machine));
                continue;
            }

            var active = machine.ActiveOutput;
            if ((active is null &&
                 (machine.ReadyForHarvest || machine.MinutesUntilReady > 0)) ||
                (active is not null &&
                 !machine.ReadyForHarvest &&
                 machine.MinutesUntilReady <= 0))
            {
                blocking.Add(
                    "machine_active_output_timer_inconsistent:" +
                    MachineTargetKey(machine));
                continue;
            }
            bool? routeMatches = null;
            if (active is not null)
            {
                routeMatches = active.RouteSources.Length == 1 &&
                    active.RouteSources[0].RouteKind ==
                        staticRoute.RouteKind &&
                    active.RouteSources[0].SourceId == staticRoute.SourceId &&
                    active.RouteSources[0].QualifiedItemId ==
                        staticRoute.QualifiedItemId;
                if (active.QualifiedItemId == staticRoute.QualifiedItemId &&
                    active.RouteSources.Length != 1)
                {
                    blocking.Add(
                        "machine_active_output_source_unresolved:" +
                        MachineTargetKey(machine));
                    continue;
                }
            }

            var completionOffset = machine.ReadyForHarvest
                ? 0
                : Math.Max(0, machine.MinutesUntilReady);
            var ready = active is not null &&
                routeMatches == true &&
                completionOffset <= remainingPlayableMinutes;
            evaluations.Add(MachineEvaluation(
                target,
                "automatic_trigger_active_output",
                ready
                    ? "resolved_automatic_machine_output_ready_on_target_date"
                    : "resolved_automatic_machine_output_not_ready_on_target_date",
                source,
                AcquisitionQuantityMath.DivideRoundUp(
                    staticRoute.RequiredAmount,
                    Math.Max(1, source.MinimumStack)),
                ready ? 1 : 0,
                active is null ? null : completionOffset,
                remainingPlayableMinutes,
                ready ? active!.Stack : 0,
                ready ? active!.Quality : 0,
                 ready,
                 targetTotalDay,
                 routeMatches,
                 ready && machine.ReadyForHarvest,
                 Array.Empty<AcquisitionMachineProcessingAttemptBinding>(),
                 0));
        }
        if (blocking.Count > 0)
        {
            return Blocked(
                route,
                MachineProduction,
                evaluations.ToArray(),
                blocking.ToArray());
        }

        var provenQuantity = AcquisitionOutputProof.ReadyQuantity(
            evaluations,
            staticRoute.MinimumQuality);
        if (provenQuantity >= staticRoute.RequiredAmount)
            return ResolvedMatch(route, MachineProduction, evaluations.ToArray());
        return ResolvedMiss(
            route,
            MachineProduction,
            evaluations.ToArray(),
            "automatic_machine_target_output_not_materialized_before_day_end");
    }

    private static AcquisitionProcessingLeadTimeEvaluation MachineEvaluation(
        MachineProcessingTarget target,
        string productionStateKind,
        string status,
        AcquisitionMachineSourceEvidence source,
        int requiredAttemptCount,
        int scheduledAttemptCount,
        int? completionOffsetMinutes,
        int remainingPlayableMinutes,
        int provenOutputQuantity,
        int provenMinimumQuality,
        bool outputReady,
        int targetTotalDay,
        bool? activeOutputRouteMatches,
        bool outputMaterializedAtSnapshot,
        AcquisitionMachineProcessingAttemptBinding[] attemptSchedule,
        int creditedExistingOutputQuantity) => new(
            target.Machine.LocationId,
            productionStateKind,
            status,
            "exact_native_machine_timer_lower_bound",
            null,
            outputReady ? 0 : 1,
            outputReady ? targetTotalDay : checked(targetTotalDay + 1),
            provenOutputQuantity,
            provenMinimumQuality,
            outputReady,
            new[]
            {
                "static_calendar_resolution.routes[].machine_source",
                "target_date_facility_capacity.routes[].target_evaluations[]",
                "state.farm.machines.value[]",
                "state.world_progress.game_state_query_calendar_state.value.time_of_day"
            },
            Array.Empty<string>(),
            new AcquisitionMachineProcessingScheduleBinding(
                productionStateKind,
                source.MachineQualifiedItemId,
                target.Machine.TileX,
                target.Machine.TileY,
                target.Machine.CapacityState,
                target.Machine.MinutesUntilReady,
                requiredAttemptCount,
                scheduledAttemptCount,
                source.DaysUntilReady >= 0
                    ? null
                    : Math.Max(0, source.MinutesUntilReady),
                 source.DaysUntilReady >= 0
                     ? source.DaysUntilReady
                     : null,
                 completionOffsetMinutes,
                 remainingPlayableMinutes,
                 activeOutputRouteMatches)
            {
                AttemptSchedule = attemptSchedule,
                CreditedExistingOutputQuantity =
                    creditedExistingOutputQuantity
            },
            outputMaterializedAtSnapshot);

}
