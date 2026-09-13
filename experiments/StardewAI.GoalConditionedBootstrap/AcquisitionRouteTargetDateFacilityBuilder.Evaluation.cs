namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateFacilityBuilder
{
    private const string NoFacility = "no_capacity_bearing_facility";
    private const string PreparedCultivation = "prepared_cultivation_slot";
    private const string ExistingCrabPot = "existing_crab_pot";
    private const string DeferredFacility = "facility_binding_pending_upstream";

    private static readonly IReadOnlyDictionary<string, string>
        FacilityClassByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = NoFacility,
            ["harvests_as"] = PreparedCultivation,
            ["machine_output"] = DeferredFacility,
            ["native_bush_shake"] = NoFacility,
            ["native_crab_pot_output"] = ExistingCrabPot,
            ["native_farm_animal_deluxe_produce"] = DeferredFacility,
            ["native_farm_animal_produce"] = DeferredFacility,
            ["native_fish_pond_output"] = DeferredFacility,
            ["native_fruit_tree_produce"] = DeferredFacility,
            ["native_geode_default_drop"] = NoFacility,
            ["native_geode_drop"] = NoFacility,
            ["native_ginger_harvest"] = NoFacility,
            ["native_location_artifact_spot"] = NoFacility,
            ["native_location_fish_spawn"] = NoFacility,
            ["native_location_forage_spawn"] = NoFacility,
            ["native_machine_flavored_output"] = DeferredFacility,
            ["native_machine_item_query_output"] = DeferredFacility,
            ["native_mine_buried_item"] = NoFacility,
            ["native_mine_fishing_override"] = NoFacility,
            ["native_money_payment"] = NoFacility,
            ["native_monster_drop_table"] = NoFacility,
            ["native_object_artifact_spot_chance"] = NoFacility,
            ["native_radioactive_ore_node"] = NoFacility,
            ["native_solar_panel_output"] = DeferredFacility,
            ["native_spring_onion_harvest"] = NoFacility,
            ["native_tea_bush_harvest"] = NoFacility,
            ["native_tree_moss_harvest"] = NoFacility,
            ["native_wild_tree_chop_drop"] = NoFacility,
            ["native_wild_tree_seed"] = NoFacility,
            ["native_wild_tree_seed_drop"] = NoFacility,
            ["native_wild_tree_tapper_output"] = DeferredFacility,
            ["recipe_output"] = DeferredFacility,
            ["sells"] = NoFacility
        };

    private static AcquisitionRouteTargetDateFacility Evaluate(
        AcquisitionRouteTargetDateLocation route,
        AcquisitionLocationRouteSnapshotState state)
    {
        if (!route.LocationRouteAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_location_route_axis",
                false,
                null,
                "upstream_location_route",
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_location_route_axis_unresolved" });
        }

        var upstreamNotApplicable = route.LocationRouteAxisStatus switch
        {
            "not_applicable_static_window_miss" => "static_window_miss",
            "not_applicable_unlock_state_miss" => "unlock_state_miss",
            "not_applicable_calendar_condition_miss" =>
                "calendar_condition_miss",
            "resolved_location_route_miss" => "location_route_miss",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(upstreamNotApplicable))
        {
            return Result(
                route,
                "not_applicable_upstream_" + upstreamNotApplicable,
                true,
                null,
                "not_applicable",
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        Require(route.LocationRouteMatchesTargetDate == true,
            "A facility-applicable route lacks a location-route match.");

        var routeKind = route.UpstreamRoute.UpstreamRoute.RouteKind;
        var facilityClass = FacilityClassByRouteKind[routeKind];
        return facilityClass switch
        {
            NoFacility => Result(
                route,
                "resolved_facility_capacity_not_required",
                true,
                true,
                NoFacility,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>()),
            ExistingCrabPot => EvaluateExistingCrabPot(route),
            PreparedCultivation => EvaluatePreparedCultivation(route, state),
            _ => Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                DeferredFacility,
                Array.Empty<AcquisitionFacilityTargetEvaluation>(),
                Array.Empty<string>(),
                new[]
                {
                    "facility_capacity_evaluator_pending_for_route_kind:" +
                    routeKind
                })
        };
    }

    private static AcquisitionRouteTargetDateFacility EvaluateExistingCrabPot(
        AcquisitionRouteTargetDateLocation route)
    {
        var targets = route.TargetEvaluations.Where(value =>
                value.Status == "resolved_location_route_match" &&
                value.BindingKind == "runtime_crab_pot_production_location")
            .ToArray();
        Require(targets.Length > 0,
            "A matched crab-pot route lacks its exact placed-pot target.");
        return Result(
            route,
            "resolved_existing_crab_pot_capacity_match",
            true,
            true,
            ExistingCrabPot,
            targets.Select(target => new AcquisitionFacilityTargetEvaluation(
                target.TargetLocationId,
                "resolved_existing_crab_pot_capacity_match",
                null,
                null,
                null,
                null,
                new[]
                {
                    "upstream_route.target_evaluations[].binding_kind",
                    "state.player.crab_pot_network.value.rows[]"
                },
                Array.Empty<string>())).ToArray(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateFacility
        EvaluatePreparedCultivation(
            AcquisitionRouteTargetDateLocation route,
            AcquisitionLocationRouteSnapshotState state)
    {
        var qualifiedItemId =
            route.UpstreamRoute.UpstreamRoute.QualifiedItemId;
        var evaluations = route.TargetEvaluations
            .Where(value => value.Status == "resolved_location_route_match")
            .Select(target => EvaluateCultivationTarget(
                target.TargetLocationId,
                qualifiedItemId,
                state))
            .ToArray();
        Require(evaluations.Length > 0,
            "A matched crop route lacks a matched target location.");

        if (evaluations.Any(value => value.Status ==
                "resolved_prepared_cultivation_capacity_match"))
        {
            return Result(
                route,
                "resolved_prepared_cultivation_capacity_match",
                true,
                true,
                PreparedCultivation,
                evaluations,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        var blocking = evaluations
            .SelectMany(value => value.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (blocking.Length > 0)
        {
            return Result(
                route,
                "blocked_facility_capacity_evidence",
                false,
                null,
                PreparedCultivation,
                evaluations,
                Array.Empty<string>(),
                blocking);
        }
        return Result(
            route,
            "resolved_prepared_cultivation_capacity_miss",
            true,
            false,
            PreparedCultivation,
            evaluations,
            new[] { "no_existing_target_crop_or_open_prepared_soil_slot" },
            Array.Empty<string>());
    }

    private static AcquisitionFacilityTargetEvaluation
        EvaluateCultivationTarget(
            string targetLocationId,
            string qualifiedItemId,
            AcquisitionLocationRouteSnapshotState state)
    {
        if (!state.TryGetLocation(targetLocationId, out var location) ||
            !location.CultivationCapacity.EvidenceAvailable)
        {
            var reasons = location?.CultivationCapacity.BlockingReasons ??
                new[] { "target_location_capacity_row_missing" };
            return new AcquisitionFacilityTargetEvaluation(
                targetLocationId,
                "blocked_prepared_cultivation_capacity_evidence",
                null,
                null,
                null,
                null,
                Array.Empty<string>(),
                reasons.Select(reason => targetLocationId + ":" + reason)
                    .ToArray());
        }

        var capacity = location.CultivationCapacity;
        var matching = capacity.OccupiedSlotsFor(qualifiedItemId);
        var matched = matching > 0 || capacity.OpenPreparedSoilSlotCount > 0;
        if (!matched && capacity.UnresolvedHarvestItemSlotCount > 0)
        {
            return new AcquisitionFacilityTargetEvaluation(
                targetLocationId,
                "blocked_prepared_cultivation_capacity_evidence",
                capacity.TotalPreparedSoilSlotCount,
                capacity.OpenPreparedSoilSlotCount,
                matching,
                capacity.UnresolvedHarvestItemSlotCount,
                new[]
                {
                    "state.locations.social_route_date_evidence.value.locations[].cultivation_capacity"
                },
                new[]
                {
                    targetLocationId +
                    ":prepared_crop_harvest_identity_unresolved"
                });
        }
        return new AcquisitionFacilityTargetEvaluation(
            targetLocationId,
            matched
                ? "resolved_prepared_cultivation_capacity_match"
                : "resolved_prepared_cultivation_capacity_miss",
            capacity.TotalPreparedSoilSlotCount,
            capacity.OpenPreparedSoilSlotCount,
            matching,
            capacity.UnresolvedHarvestItemSlotCount,
            new[]
            {
                "state.locations.social_route_date_evidence.value.locations[].cultivation_capacity"
            },
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateFacility Result(
        AcquisitionRouteTargetDateLocation route,
        string status,
        bool resolved,
        bool? matches,
        string requirementKind,
        AcquisitionFacilityTargetEvaluation[] evaluations,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            status,
            resolved,
            matches,
            requirementKind,
            evaluations,
            nonMatchingReasons,
            blockingReasons);
}
