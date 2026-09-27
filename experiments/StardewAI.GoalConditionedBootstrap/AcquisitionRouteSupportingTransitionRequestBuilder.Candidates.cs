using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

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
            "machine_input_material_transfer" =>
                EvaluateMachineMaterialTransferCandidate(
                    reservation,
                    snapshot,
                    candidate,
                    currentDay,
                    supportDeadlineTotalDay),
            "machine_input_purchase" => EvaluatePurchaseCandidate(
                reservation,
                candidate,
                currentDay,
                supportDeadlineTotalDay),
            _ => SupportCandidateEvaluation.Blocked(
                "support_transition_kind_not_bound")
        };
    }

    private static SupportCandidateEvaluation EvaluatePurchaseCandidate(
        AcquisitionRouteTargetDateReservation reservation,
        PolicyEventCandidatePrediction candidate,
        int? currentDay,
        int supportDeadlineTotalDay)
    {
        var reasons = new List<string>();
        var purchase = reservation.UpstreamRoute.CurrencyEvaluation?
            .PurchasePrerequisite;
        if (!candidate.Available)
            reasons.Add("machine_input_purchase_candidate_not_ready_now");
        if (purchase is null ||
            !PurchaseCandidateMatchesBinding(candidate, purchase))
        {
            reasons.Add("machine_input_purchase_candidate_binding_mismatch");
        }

        var currencyClaims = reservation.ClaimSet?.CurrencyClaims ??
            Array.Empty<CurrencyReservationUpsertRequest>();
        var matchingClaims = purchase is null
            ? Array.Empty<CurrencyReservationUpsertRequest>()
            : currencyClaims.Where(claim =>
                    claim.CurrencyId == purchase.CurrencyId &&
                    claim.Amount >= purchase.UnitPrice)
                .ToArray();
        var claimBound = purchase is not null &&
            matchingClaims.Length == 1 &&
            currencyClaims.Length == 1;
        if (!claimBound)
            reasons.Add("machine_input_purchase_currency_claim_mismatch");

        var deadlineVerified = currentDay.HasValue &&
            supportDeadlineTotalDay > currentDay.Value;
        if (!deadlineVerified)
        {
            reasons.Add(
                "machine_input_purchase_does_not_fit_support_deadline");
        }
        var stage = PurchaseStage(candidate);
        var currencyConsumptions = claimBound && stage == "purchase"
            ? new[]
            {
                new AcquisitionSupportCurrencyConsumption
                {
                    ReservationId = matchingClaims[0].ReservationId,
                    CurrencyId = purchase!.CurrencyId,
                    ConsumedAmount = purchase.UnitPrice
                }
            }
            : Array.Empty<AcquisitionSupportCurrencyConsumption>();
        return new SupportCandidateEvaluation(
            deadlineVerified ? currentDay : null,
            null,
            null,
            purchase?.QualifiedItemId ?? string.Empty,
            candidate.SlotIndex,
            purchase?.OutputStackPerPurchase,
            string.Empty,
            null,
            deadlineVerified,
            claimBound,
            Array.Empty<AcquisitionSupportMaterialConsumption>(),
            null,
            Array.Empty<AcquisitionSupportMaterialRelocation>(),
            reasons.Distinct(StringComparer.Ordinal).ToArray())
        {
            PurchaseStage = stage,
            PurchasePrerequisite = purchase,
            CurrencyConsumptions = currencyConsumptions
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
        var cropConsumptions = claimBound
            ? reservation.ClaimSet!.MaterialClaims
                .Where(claim =>
                    claim.SlotIndex == candidate.SlotIndex &&
                    claim.QualifiedItemId == candidate.QualifiedItemId)
                .Take(1)
                .Select(claim => Consumption(
                    claim,
                    1,
                    "primary_input"))
                .ToArray()
            : Array.Empty<AcquisitionSupportMaterialConsumption>();
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
            cropConsumptions,
            null,
            Array.Empty<AcquisitionSupportMaterialRelocation>(),
            reasons.ToArray());
    }

    private static SupportCandidateEvaluation
        EvaluateMachineMaterialTransferCandidate(
            AcquisitionRouteTargetDateReservation reservation,
            SnapshotEnvelope snapshot,
            PolicyEventCandidatePrediction candidate,
            int? currentDay,
            int supportDeadlineTotalDay)
    {
        var reasons = new List<string>();
        if (!candidate.Available)
            reasons.Add("machine_input_staging_candidate_not_ready_now");
        var sourceNodeId = ReadStringParameter(candidate, "source_node_id");
        var destinationNodeId = ReadStringParameter(
            candidate,
            "destination_node_id");
        var sourceSlot = ReadNonNegativeIntParameter(
            candidate,
            "source_slot_index");
        var quality = ReadNonNegativeIntParameter(candidate, "quality");
        var quantity = ReadPositiveIntParameter(candidate, "quantity");
        var expectedSourceStack = ReadPositiveIntParameter(
            candidate,
            "expected_source_stack");
        var qualifiedItemId = ReadStringParameter(
            candidate,
            "qualified_item_id");
        MaterialTransferIntent? intent = null;
        if (string.IsNullOrWhiteSpace(sourceNodeId) ||
            string.IsNullOrWhiteSpace(destinationNodeId) ||
            !sourceSlot.HasValue || !quality.HasValue ||
            !quantity.HasValue || !expectedSourceStack.HasValue ||
            string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            reasons.Add("machine_input_staging_intent_incomplete");
        }
        else
        {
            intent = new MaterialTransferIntent
            {
                SourceNodeId = sourceNodeId,
                DestinationNodeId = destinationNodeId,
                SourceSlotIndex = sourceSlot.Value,
                QualifiedItemId = qualifiedItemId,
                Quality = quality.Value,
                Quantity = quantity.Value,
                ExpectedSourceStack = expectedSourceStack.Value
            };
        }

        MaterialTransferProjection? projection = null;
        if (intent is not null &&
            AcquisitionMachineInputMaterialStaging.TryReadGraph(
                snapshot,
                out var graph))
        {
            projection = new MaterialTransferProjector().Project(
                graph!,
                intent);
            if (projection.Status != "projected")
                reasons.AddRange(projection.BlockingReasons);
        }
        else if (intent is not null)
        {
            reasons.Add("machine_input_staging_material_graph_unavailable");
        }
        if (projection is not null &&
            projection.DestinationSlotChanges.Length != 1)
        {
            reasons.Add(
                "machine_input_staging_requires_single_destination_slot");
        }

        var claims = intent is null
            ? Array.Empty<MaterialReservationUpsertRequest>()
            : (reservation.ClaimSet?.MaterialClaims ??
                Array.Empty<MaterialReservationUpsertRequest>())
                .Where(claim =>
                    claim.NodeId == intent.SourceNodeId &&
                    claim.SlotIndex == intent.SourceSlotIndex &&
                    claim.QualifiedItemId == intent.QualifiedItemId &&
                    claim.Quantity == intent.Quantity)
                .ToArray();
        var claimBound = claims.Length == 1 &&
            (reservation.ClaimSet?.CurrencyClaims.Length ?? 0) == 0;
        if (!claimBound)
            reasons.Add("machine_input_staging_claim_mismatch");
        var deadlineVerified = currentDay.HasValue &&
            supportDeadlineTotalDay > currentDay.Value;
        if (!deadlineVerified)
            reasons.Add("machine_input_staging_does_not_fit_support_deadline");
        var relocations = claimBound &&
            projection?.DestinationSlotChanges is [var destination]
                ? new[]
                {
                    new AcquisitionSupportMaterialRelocation
                    {
                        ReservationId = claims[0].ReservationId,
                        SourceNodeId = intent!.SourceNodeId,
                        SourceSlotIndex = intent.SourceSlotIndex,
                        DestinationNodeId = intent.DestinationNodeId,
                        DestinationSlotIndex = destination.SlotIndex,
                        QualifiedItemId = intent.QualifiedItemId,
                        Quantity = intent.Quantity
                    }
                }
                : Array.Empty<AcquisitionSupportMaterialRelocation>();
        return new SupportCandidateEvaluation(
            deadlineVerified ? currentDay : null,
            null,
            null,
            qualifiedItemId,
            sourceSlot,
            quantity,
            string.Empty,
            null,
            deadlineVerified,
            claimBound,
            Array.Empty<AcquisitionSupportMaterialConsumption>(),
            intent,
            relocations,
            reasons.Distinct(StringComparer.Ordinal).ToArray());
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
        var consumptions = schedule is not null &&
            requiredQuantity.HasValue && claimBound
                ? BuildMachineConsumptions(
                    candidate,
                    reservation.ClaimSet!,
                    schedule.RequiredAttemptCount,
                    requiredQuantity.Value,
                    reasons)
                : Array.Empty<AcquisitionSupportMaterialConsumption>();
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
            consumptions,
            null,
            Array.Empty<AcquisitionSupportMaterialRelocation>(),
            reasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static AcquisitionSupportMaterialConsumption[]
        BuildMachineConsumptions(
            PolicyEventCandidatePrediction candidate,
            AcquisitionRouteReservationClaimSet claimSet,
            int requiredAttemptCount,
            int primaryRequiredQuantity,
            ICollection<string> reasons)
    {
        if (requiredAttemptCount <= 0)
        {
            reasons.Add("machine_input_consumption_attempt_count_invalid");
            return Array.Empty<AcquisitionSupportMaterialConsumption>();
        }
        var primaryClaims = claimSet.MaterialClaims.Where(claim =>
                claim.SlotIndex == candidate.SlotIndex &&
                claim.QualifiedItemId == candidate.QualifiedItemId)
            .ToArray();
        if (primaryClaims.Length != 1 ||
            primaryClaims[0].Quantity < primaryRequiredQuantity)
        {
            reasons.Add("machine_input_primary_consumption_claim_not_unique");
            return Array.Empty<AcquisitionSupportMaterialConsumption>();
        }
        var consumptions = new List<AcquisitionSupportMaterialConsumption>
        {
            Consumption(
                primaryClaims[0],
                primaryRequiredQuantity,
                "primary_input")
        };
        foreach (var group in claimSet.MaterialClaims
                     .GroupBy(claim => claim.QualifiedItemId,
                         StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var total = group.Sum(claim => (long)claim.Quantity);
            if (total <= 0 || total % requiredAttemptCount != 0 ||
                total / requiredAttemptCount > int.MaxValue)
            {
                reasons.Add(
                    "machine_input_per_attempt_consumption_not_integral:" +
                    group.Key);
                continue;
            }
            var perAttempt = (int)(total / requiredAttemptCount);
            if (group.Key == candidate.QualifiedItemId)
            {
                if (perAttempt != primaryRequiredQuantity)
                {
                    reasons.Add(
                        "machine_input_primary_and_additional_identity_overlap");
                }
                continue;
            }
            var remaining = perAttempt;
            foreach (var claim in group
                         .OrderBy(value => value.NodeId, StringComparer.Ordinal)
                         .ThenBy(value => value.SlotIndex))
            {
                var quantity = Math.Min(remaining, claim.Quantity);
                if (quantity <= 0)
                    continue;
                consumptions.Add(Consumption(
                    claim,
                    quantity,
                    "additional_input"));
                remaining -= quantity;
                if (remaining == 0)
                    break;
            }
            if (remaining != 0)
            {
                reasons.Add(
                    "machine_input_additional_consumption_claim_incomplete:" +
                    group.Key);
            }
        }
        return reasons.Any(reason =>
                (reason.StartsWith(
                     "machine_input_",
                     StringComparison.Ordinal) &&
                 reason.Contains("consumption", StringComparison.Ordinal)) ||
                reason ==
                    "machine_input_primary_and_additional_identity_overlap")
                ? Array.Empty<AcquisitionSupportMaterialConsumption>()
                : consumptions
                    .OrderBy(value => value.InputRole, StringComparer.Ordinal)
                    .ThenBy(value => value.NodeId, StringComparer.Ordinal)
                    .ThenBy(value => value.SlotIndex)
                    .ToArray();
    }

    private static AcquisitionSupportMaterialConsumption Consumption(
        MaterialReservationUpsertRequest claim,
        int quantity,
        string role) => new()
        {
            ReservationId = claim.ReservationId,
            NodeId = claim.NodeId,
            SlotIndex = claim.SlotIndex,
            QualifiedItemId = claim.QualifiedItemId,
            ConsumedQuantity = quantity,
            InputRole = role
        };

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
        AcquisitionSupportMaterialConsumption[] MaterialConsumptions,
        MaterialTransferIntent? MaterialTransferIntent,
        AcquisitionSupportMaterialRelocation[] MaterialRelocations,
        string[] BlockingReasons)
    {
        public string PurchaseStage { get; init; } = string.Empty;

        public AcquisitionPurchasePrerequisiteBinding? PurchasePrerequisite
        { get; init; }

        public AcquisitionSupportCurrencyConsumption[] CurrencyConsumptions
        { get; init; } = Array.Empty<AcquisitionSupportCurrencyConsumption>();

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
            Array.Empty<AcquisitionSupportMaterialConsumption>(),
            null,
            Array.Empty<AcquisitionSupportMaterialRelocation>(),
            Array.Empty<string>());

        public static SupportCandidateEvaluation Blocked(string reason) =>
            Empty with { BlockingReasons = new[] { reason } };
    }
}
