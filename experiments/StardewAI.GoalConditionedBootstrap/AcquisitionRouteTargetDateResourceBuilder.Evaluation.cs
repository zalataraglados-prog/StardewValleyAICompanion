namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private const string NoInput = "no_consumed_resource_input";
    private const string CropSeed = "crop_seed_or_existing_crop";
    private const string ShopTrade = "shop_trade_item_or_currency_only";
    private const string FishingBait = "conditional_magic_bait";
    private const string CrabPotService = "existing_crab_pot_service";
    private const string DeferredInput = "resource_binding_pending_upstream";
    private const string MagicBaitQualifiedItemId = "(O)908";

    private static readonly IReadOnlyDictionary<string, string>
        ResourceClassByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = DeferredInput,
            ["harvests_as"] = CropSeed,
            ["machine_output"] = DeferredInput,
            ["native_bush_shake"] = NoInput,
            ["native_crab_pot_output"] = CrabPotService,
            ["native_farm_animal_deluxe_produce"] = DeferredInput,
            ["native_farm_animal_produce"] = DeferredInput,
            ["native_fish_pond_output"] = DeferredInput,
            ["native_fruit_tree_produce"] = NoInput,
            ["native_geode_default_drop"] = DeferredInput,
            ["native_geode_drop"] = DeferredInput,
            ["native_ginger_harvest"] = NoInput,
            ["native_location_artifact_spot"] = NoInput,
            ["native_location_fish_spawn"] = FishingBait,
            ["native_location_forage_spawn"] = NoInput,
            ["native_machine_flavored_output"] = DeferredInput,
            ["native_machine_item_query_output"] = DeferredInput,
            ["native_mine_buried_item"] = NoInput,
            ["native_mine_fishing_override"] = FishingBait,
            ["native_money_payment"] = NoInput,
            ["native_monster_drop_table"] = NoInput,
            ["native_object_artifact_spot_chance"] = NoInput,
            ["native_radioactive_ore_node"] = NoInput,
            ["native_solar_panel_output"] = NoInput,
            ["native_spring_onion_harvest"] = NoInput,
            ["native_tea_bush_harvest"] = NoInput,
            ["native_tree_moss_harvest"] = NoInput,
            ["native_wild_tree_chop_drop"] = NoInput,
            ["native_wild_tree_seed"] = NoInput,
            ["native_wild_tree_seed_drop"] = NoInput,
            ["native_wild_tree_tapper_output"] = NoInput,
            ["recipe_output"] = DeferredInput,
            ["sells"] = ShopTrade
        };

    private static AcquisitionRouteTargetDateResource Evaluate(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionResourceInputSnapshotState state)
    {
        if (!route.FacilityCapacityAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_facility_capacity_axis",
                false,
                null,
                "upstream_facility_capacity",
                Array.Empty<AcquisitionResourceInputEvaluation>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_facility_capacity_axis_unresolved" });
        }
        if (route.FacilityCapacityMatchesTargetDate is null)
        {
            return Result(
                route,
                "not_applicable_upstream_facility_capacity_axis",
                true,
                null,
                "not_applicable",
                Array.Empty<AcquisitionResourceInputEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }
        if (route.FacilityCapacityMatchesTargetDate == false)
        {
            return Result(
                route,
                "not_applicable_upstream_facility_capacity_miss",
                true,
                null,
                "not_applicable",
                Array.Empty<AcquisitionResourceInputEvaluation>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        var routeKind = RouteKind(route);
        return ResourceClassByRouteKind[routeKind] switch
        {
            NoInput => NotRequired(route),
            CropSeed => EvaluateCrop(route, staticRoute, state),
            ShopTrade => EvaluateShop(route, staticRoute, state),
            FishingBait => EvaluateFishing(route, state),
            CrabPotService => EvaluateCrabPot(route, state),
            _ => Blocked(
                route,
                DeferredInput,
                "resource_input_evaluator_pending_for_route_kind:" +
                routeKind)
        };
    }

    private static AcquisitionRouteTargetDateResource NotRequired(
        AcquisitionRouteTargetDateFacility route,
        string requirementKind = NoInput) => Result(
            route,
            "resolved_resource_inputs_not_required",
            true,
            true,
            requirementKind,
            Array.Empty<AcquisitionResourceInputEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateResource ResolvedMatch(
        AcquisitionRouteTargetDateFacility route,
        string requirementKind,
        AcquisitionResourceInputEvaluation evaluation) => Result(
            route,
            "resolved_resource_inputs_match",
            true,
            true,
            requirementKind,
            new[] { evaluation },
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateResource ResolvedMiss(
        AcquisitionRouteTargetDateFacility route,
        string requirementKind,
        AcquisitionResourceInputEvaluation evaluation,
        string reason) => Result(
            route,
            "resolved_resource_inputs_miss",
            true,
            false,
            requirementKind,
            new[] { evaluation },
            new[] { reason },
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateResource Blocked(
        AcquisitionRouteTargetDateFacility route,
        string requirementKind,
        params string[] reasons) => Result(
            route,
            "blocked_resource_input_evidence",
            false,
            null,
            requirementKind,
            Array.Empty<AcquisitionResourceInputEvaluation>(),
            Array.Empty<string>(),
            reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray());

    private static AcquisitionRouteTargetDateResource Result(
        AcquisitionRouteTargetDateFacility route,
        string status,
        bool resolved,
        bool? matches,
        string requirementKind,
        AcquisitionResourceInputEvaluation[] evaluations,
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
