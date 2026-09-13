namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private static AcquisitionRouteTargetDateProcessing EvaluateCrop(
        AcquisitionRouteTargetDateReservation route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        if (staticRoute.CropSource is null)
        {
            return Blocked(
                route,
                CropGrowth,
                "authoritative_crop_growth_evidence_missing");
        }
        if (!AcquisitionOutputProof.CanGuaranteeQuality(
                staticRoute.CropSource.HarvestMinQuality,
                staticRoute.MinimumQuality))
        {
            return Blocked(
                route,
                CropGrowth,
                "live_crop_exact_harvest_quality_projection_missing:" +
                staticRoute.MinimumQuality);
        }
        var facility = route.UpstreamRoute.UpstreamRoute.UpstreamRoute;
        var targets = facility.TargetEvaluations.Where(value =>
                value.MatchingExistingCropSlotCount.GetValueOrDefault() +
                    value.OpenPreparedSoilSlotCount.GetValueOrDefault() > 0)
            .ToArray();
        if (targets.Length == 0)
        {
            return Blocked(
                route,
                CropGrowth,
                "matched_crop_route_has_no_cultivation_target");
        }

        var evaluations = targets.Select(target =>
                EvaluateCropTarget(
                    target,
                    staticRoute.QualifiedItemId,
                    staticRoute.CropSource,
                    state,
                    targetTotalDay))
            .ToArray();
        var provenReadyQuantity = AcquisitionOutputProof.ReadyQuantity(
            evaluations,
            staticRoute.MinimumQuality);
        if (provenReadyQuantity >= staticRoute.RequiredAmount)
            return ResolvedMatch(route, CropGrowth, evaluations);
        var blocking = evaluations.SelectMany(value => value.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return blocking.Length > 0
            ? Blocked(route, CropGrowth, evaluations, blocking)
            : ResolvedMiss(
                route,
                CropGrowth,
                evaluations,
                provenReadyQuantity > 0
                    ? "crop_ready_output_quantity_shortfall:" +
                        provenReadyQuantity + ":" + staticRoute.RequiredAmount
                    : "crop_output_not_ready_on_target_date");
    }

    private static AcquisitionProcessingLeadTimeEvaluation EvaluateCropTarget(
        AcquisitionFacilityTargetEvaluation target,
        string qualifiedItemId,
        AcquisitionCropSourceEvidence cropSource,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        if (target.MatchingExistingCropSlotCount.GetValueOrDefault() > 0)
        {
            return EvaluateExistingCrop(
                target,
                qualifiedItemId,
                cropSource,
                state,
                targetTotalDay);
        }
        if (target.OpenPreparedSoilSlotCount.GetValueOrDefault() <= 0 ||
            cropSource.BaseGrowthDays <= 0)
        {
            return EvaluationBlocked(
                target.TargetLocationId,
                "new_crop_from_seed",
                "crop_capacity_or_growth_duration_invalid");
        }

        return new AcquisitionProcessingLeadTimeEvaluation(
            target.TargetLocationId,
            "new_crop_from_seed",
            "resolved_new_crop_requires_future_daily_growth",
            "strict_lower_bound_from_native_daily_growth",
            cropSource.BaseGrowthDays,
            1,
            checked(targetTotalDay + 1),
            null,
            null,
            false,
            new[]
            {
                "static_calendar_resolution.routes[].crop_source.base_growth_days",
                "state.locations.social_route_date_evidence.value.locations[].cultivation_capacity.open_prepared_soil_slot_count"
            },
            Array.Empty<string>());
    }

    private static AcquisitionProcessingLeadTimeEvaluation EvaluateExistingCrop(
        AcquisitionFacilityTargetEvaluation target,
        string qualifiedItemId,
        AcquisitionCropSourceEvidence cropSource,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        var lookup = state.FindCrops(target.TargetLocationId, qualifiedItemId);
        if (!lookup.EvidenceAvailable)
        {
            return EvaluationBlocked(
                target.TargetLocationId,
                "existing_crop",
                lookup.BlockingReasons);
        }
        var expected = target.MatchingExistingCropSlotCount.GetValueOrDefault();
        if (lookup.Rows.Length != expected)
        {
            return EvaluationBlocked(
                target.TargetLocationId,
                "existing_crop",
                "live_crop_count_disagrees_with_cultivation_capacity:" +
                expected + ":" + lookup.Rows.Length);
        }
        if (lookup.Rows.Any(crop =>
                !crop.ProjectionStatus.StartsWith(
                    "exact_",
                    StringComparison.Ordinal)))
        {
            return EvaluationBlocked(
                target.TargetLocationId,
                "existing_crop",
                "live_crop_harvest_projection_is_not_exact");
        }
        var evidencePaths = lookup.EvidencePaths.Concat(new[]
            {
                "state.locations.social_route_date_evidence.value.locations[].cultivation_capacity.occupied_harvest_items[]"
            })
            .ToArray();
        if (lookup.Rows.Any(crop => !crop.Dead && crop.ReadyForHarvest))
        {
            var readyCropCount = lookup.Rows.Count(crop =>
                !crop.Dead && crop.ReadyForHarvest);
            return new AcquisitionProcessingLeadTimeEvaluation(
                target.TargetLocationId,
                "existing_crop",
                "resolved_existing_crop_ready_on_target_date",
                "exact_live_state",
                null,
                0,
                targetTotalDay,
                AcquisitionQuantityMath.Multiply(
                    cropSource.HarvestMinStack,
                    readyCropCount),
                cropSource.HarvestMinQuality,
                true,
                evidencePaths,
                Array.Empty<string>());
        }
        var living = lookup.Rows.Where(crop => !crop.Dead).ToArray();
        if (living.Any(crop =>
                !crop.DaysUntilNextHarvestIfWatered.HasValue ||
                crop.DaysUntilNextHarvestIfWatered == 0))
        {
            return EvaluationBlocked(
                target.TargetLocationId,
                "existing_crop",
                "live_crop_growth_projection_inconsistent_or_unavailable");
        }
        if (living.Length == 0)
        {
            return new AcquisitionProcessingLeadTimeEvaluation(
                target.TargetLocationId,
                "existing_crop",
                "resolved_existing_crop_dead",
                "exact_live_state",
                null,
                null,
                null,
                0,
                null,
                false,
                evidencePaths,
                Array.Empty<string>());
        }

        var leadDays = living.Min(crop =>
            crop.DaysUntilNextHarvestIfWatered!.Value);
        return new AcquisitionProcessingLeadTimeEvaluation(
            target.TargetLocationId,
            "existing_crop",
            "resolved_existing_crop_completes_after_target_date",
            "conditional_live_growth_projection",
            null,
            leadDays,
            checked(targetTotalDay + leadDays),
            null,
            null,
            false,
            evidencePaths,
            Array.Empty<string>());
    }
}
