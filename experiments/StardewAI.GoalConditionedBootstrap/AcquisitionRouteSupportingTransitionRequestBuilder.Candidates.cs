using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static SupportCandidateEvaluation EvaluateCandidate(
        string supportTransitionKind,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction? candidate,
        int? currentDay,
        int supportDeadlineTotalDay)
    {
        if (candidate is null)
            return SupportCandidateEvaluation.Empty;
        return supportTransitionKind switch
        {
            "crop_planting" => EvaluateCropCandidate(
                requirement,
                reservation,
                candidate,
                currentDay,
                supportDeadlineTotalDay),
            "machine_input_load" => EvaluateMachineCandidate(
                requirement,
                reservation,
                processing,
                snapshot,
                candidate,
                currentDay,
                supportDeadlineTotalDay),
            _ => SupportCandidateEvaluation.Blocked(
                "support_transition_kind_not_bound")
        };
    }

    private static SupportCandidateEvaluation EvaluateCropCandidate(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateReservation reservation,
        PolicyEventCandidatePrediction candidate,
        int? currentDay,
        int supportDeadlineTotalDay)
    {
        var reasons = new List<string>();
        var adjustedGrowDays = ReadPositiveIntParameter(
            candidate,
            "adjusted_grow_days");
        var daysRemaining = ReadNonNegativeIntParameter(
            candidate,
            "days_remaining_in_season");
        if (!candidate.Available)
            reasons.Add("crop_planting_support_candidate_not_ready_now");
        int? expectedReadyDay = null;
        if (!currentDay.HasValue ||
            !adjustedGrowDays.HasValue ||
            !daysRemaining.HasValue)
        {
            reasons.Add("crop_planting_deadline_evidence_incomplete");
        }
        else
        {
            try
            {
                expectedReadyDay = checked(
                    currentDay.Value + adjustedGrowDays.Value);
            }
            catch (OverflowException)
            {
                reasons.Add("crop_planting_expected_ready_day_overflow");
            }
        }
        var deadlineVerified = expectedReadyDay.HasValue &&
            currentDay.HasValue &&
            adjustedGrowDays.HasValue &&
            daysRemaining.HasValue &&
            supportDeadlineTotalDay > currentDay.Value &&
            expectedReadyDay.Value <= supportDeadlineTotalDay &&
            adjustedGrowDays.Value <= daysRemaining.Value &&
            requirement.MatchingWindows.Any(window =>
                currentDay.Value >= window.FirstTotalDay &&
                expectedReadyDay.Value <= window.LastTotalDay);
        if (!deadlineVerified &&
            !reasons.Contains(
                "crop_planting_expected_ready_day_overflow",
                StringComparer.Ordinal))
        {
            reasons.Add("crop_planting_does_not_fit_support_deadline");
        }
        var claimBound = CandidateCoveredByClaim(
            candidate,
            reservation.ClaimSet,
            "crop_planting");
        if (!claimBound)
            reasons.Add("crop_planting_candidate_seed_claim_mismatch");
        return new SupportCandidateEvaluation(
            expectedReadyDay,
            adjustedGrowDays,
            daysRemaining,
            candidate.QualifiedItemId,
            candidate.SlotIndex,
            1,
            string.Empty,
            null,
            deadlineVerified,
            claimBound,
            reasons.ToArray());
    }

    private static SupportCandidateEvaluation EvaluateMachineCandidate(
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        SnapshotEnvelope snapshot,
        PolicyEventCandidatePrediction candidate,
        int? currentDay,
        int supportDeadlineTotalDay)
    {
        var reasons = new List<string>();
        if (!candidate.Available)
            reasons.Add("machine_input_support_candidate_not_ready_now");
        var machineQualifiedItemId = ReadStringParameter(
            candidate,
            "machine_qualified_item_id");
        var requiredQuantity = ReadPositiveIntParameter(
            candidate,
            "machine_input_required_count");
        var predictedMinutes = ReadNonNegativeIntParameter(
            candidate,
            "predicted_processing_minutes");
        var schedules = processing.Evaluations.Where(evaluation =>
                evaluation.TargetLocationId == candidate.LocationId &&
                evaluation.MachineScheduleBinding is not null &&
                evaluation.MachineScheduleBinding.TargetTileX ==
                    candidate.TileX &&
                evaluation.MachineScheduleBinding.TargetTileY ==
                    candidate.TileY &&
                evaluation.MachineScheduleBinding.MachineQualifiedItemId ==
                    machineQualifiedItemId)
            .Select(evaluation => evaluation.MachineScheduleBinding!)
            .ToArray();
        if (schedules.Length != 1)
            reasons.Add("machine_input_authoritative_schedule_not_unique");
        var schedule = schedules.Length == 1 ? schedules[0] : null;
        if (schedule is not null &&
            (schedule.ScheduleKind != "manual_input_processing" ||
             schedule.InitialCapacityState != "idle" ||
             schedule.RequiredAttemptCount <= 0 ||
             schedule.CreditedExistingOutputQuantity != 0))
        {
            reasons.Add("machine_input_authoritative_schedule_not_loadable");
        }
        var timeResolved = TryStateInt(
            snapshot,
            "time",
            "time_of_day",
            out var timeOfDay);
        int? authoritativeMinutes = null;
        if (schedule is not null && timeResolved)
        {
            authoritativeMinutes = AuthoritativeMachineMinutes(
                schedule,
                timeOfDay);
        }
        if (!currentDay.HasValue ||
            !timeResolved ||
            string.IsNullOrWhiteSpace(machineQualifiedItemId) ||
            !requiredQuantity.HasValue ||
            !predictedMinutes.HasValue ||
            !authoritativeMinutes.HasValue)
        {
            reasons.Add("machine_input_deadline_evidence_incomplete");
        }
        else if (predictedMinutes.Value != authoritativeMinutes.Value)
        {
            reasons.Add("machine_input_processing_duration_drifted");
        }

        int? expectedReadyDay = null;
        if (currentDay.HasValue && timeResolved && predictedMinutes.HasValue)
        {
            expectedReadyDay = MachineReadyTotalDay(
                currentDay.Value,
                timeOfDay,
                predictedMinutes.Value);
            if (!expectedReadyDay.HasValue)
                reasons.Add("machine_input_expected_ready_day_overflow");
        }
        var windowFits = expectedReadyDay.HasValue &&
            (requirement.MatchingWindows.Length == 0 ||
             requirement.MatchingWindows.Any(window =>
                 expectedReadyDay.Value >= window.FirstTotalDay &&
                 expectedReadyDay.Value <= window.LastTotalDay));
        var deadlineVerified = expectedReadyDay.HasValue &&
            currentDay.HasValue &&
            predictedMinutes == authoritativeMinutes &&
            supportDeadlineTotalDay > currentDay.Value &&
            expectedReadyDay.Value <= supportDeadlineTotalDay &&
            windowFits;
        if (!deadlineVerified)
            reasons.Add("machine_input_does_not_fit_support_deadline");
        var claimBound = CandidateCoveredByClaim(
            candidate,
            reservation.ClaimSet,
            "machine_input_load");
        if (!claimBound)
            reasons.Add("machine_input_candidate_material_claim_mismatch");
        return new SupportCandidateEvaluation(
            expectedReadyDay,
            null,
            null,
            candidate.QualifiedItemId,
            candidate.SlotIndex,
            requiredQuantity,
            machineQualifiedItemId,
            predictedMinutes,
            deadlineVerified,
            claimBound,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static int? AuthoritativeMachineMinutes(
        AcquisitionMachineProcessingScheduleBinding schedule,
        int timeOfDay)
    {
        if (schedule.AuthoritativeMinutesPerAttempt.HasValue ==
            schedule.AuthoritativeDaysPerAttempt.HasValue)
        {
            return null;
        }
        if (schedule.AuthoritativeMinutesPerAttempt.HasValue)
        {
            return schedule.AuthoritativeMinutesPerAttempt.Value >= 0
                ? schedule.AuthoritativeMinutesPerAttempt
                : null;
        }
        var days = schedule.AuthoritativeDaysPerAttempt!.Value;
        if (days < 1)
            return days == 0 ? 0 : null;
        if (!TryClockMinutes(timeOfDay, out var currentMinutes))
            return null;
        var result = 1560L - currentMinutes + 400L +
            (days - 1L) * 1600L;
        return result is >= 0 and <= int.MaxValue ? (int)result : null;
    }

    private static int? MachineReadyTotalDay(
        int currentTotalDay,
        int timeOfDay,
        int processingMinutes)
    {
        if (processingMinutes < 0 ||
            !TryClockMinutes(timeOfDay, out var currentMinutes))
        {
            return null;
        }
        var remainingPlayable = 1560 - currentMinutes;
        if (processingMinutes <= remainingPlayable)
            return currentTotalDay;
        var remainingAtNextMorning =
            (long)processingMinutes - remainingPlayable - 400L;
        var additionalDays = remainingAtNextMorning <= 1200L
            ? 0L
            : (remainingAtNextMorning - 1200L + 1599L) / 1600L;
        var result = (long)currentTotalDay + 1L + additionalDays;
        return result is >= int.MinValue and <= int.MaxValue
            ? (int)result
            : null;
    }

    private static bool TryClockMinutes(int timeOfDay, out int minutes)
    {
        var hour = timeOfDay / 100;
        var minute = timeOfDay % 100;
        minutes = hour * 60 + minute;
        return timeOfDay is >= 600 and <= 2600 &&
            minute is >= 0 and < 60;
    }

    private sealed record SupportCandidateEvaluation(
        int? ExpectedReadyTotalDay,
        int? AdjustedGrowDays,
        int? DaysRemainingInSeason,
        string InputQualifiedItemId,
        int? InputSlotIndex,
        int? InputRequiredQuantity,
        string MachineQualifiedItemId,
        int? PredictedProcessingMinutes,
        bool DeadlineProofVerified,
        bool ReservationClaimBoundToCandidate,
        string[] BlockingReasons)
    {
        public static SupportCandidateEvaluation Empty { get; } = new(
            null,
            null,
            null,
            string.Empty,
            null,
            null,
            string.Empty,
            null,
            false,
            false,
            Array.Empty<string>());

        public static SupportCandidateEvaluation Blocked(string reason) =>
            Empty with { BlockingReasons = new[] { reason } };
    }
}
