namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private const string NoDeterministicWait =
        "no_deterministic_processing_wait";
    private const string CropGrowth = "crop_growth_or_ready_crop";
    private const string CrabPotProduction = "crab_pot_daily_production";
    private const string DeferredProduction =
        "production_lead_time_binding_pending_upstream";

    private static readonly IReadOnlyDictionary<string, string>
        ProcessingClassByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = NoDeterministicWait,
            ["harvests_as"] = CropGrowth,
            ["machine_output"] = DeferredProduction,
            ["native_bush_shake"] = NoDeterministicWait,
            ["native_crab_pot_output"] = CrabPotProduction,
            ["native_farm_animal_deluxe_produce"] = DeferredProduction,
            ["native_farm_animal_produce"] = DeferredProduction,
            ["native_fish_pond_output"] = DeferredProduction,
            ["native_fruit_tree_produce"] = DeferredProduction,
            ["native_geode_default_drop"] = NoDeterministicWait,
            ["native_geode_drop"] = NoDeterministicWait,
            ["native_ginger_harvest"] = NoDeterministicWait,
            ["native_location_artifact_spot"] = NoDeterministicWait,
            ["native_location_fish_spawn"] = NoDeterministicWait,
            ["native_location_forage_spawn"] = NoDeterministicWait,
            ["native_machine_flavored_output"] = DeferredProduction,
            ["native_machine_item_query_output"] = DeferredProduction,
            ["native_mine_buried_item"] = NoDeterministicWait,
            ["native_mine_fishing_override"] = NoDeterministicWait,
            ["native_money_payment"] = NoDeterministicWait,
            ["native_monster_drop_table"] = NoDeterministicWait,
            ["native_object_artifact_spot_chance"] = NoDeterministicWait,
            ["native_radioactive_ore_node"] = NoDeterministicWait,
            ["native_solar_panel_output"] = DeferredProduction,
            ["native_spring_onion_harvest"] = NoDeterministicWait,
            ["native_tea_bush_harvest"] = NoDeterministicWait,
            ["native_tree_moss_harvest"] = NoDeterministicWait,
            ["native_wild_tree_chop_drop"] = NoDeterministicWait,
            ["native_wild_tree_seed"] = NoDeterministicWait,
            ["native_wild_tree_seed_drop"] = NoDeterministicWait,
            ["native_wild_tree_tapper_output"] = DeferredProduction,
            ["recipe_output"] = NoDeterministicWait,
            ["sells"] = NoDeterministicWait
        };

    private static AcquisitionRouteTargetDateProcessing Evaluate(
        AcquisitionRouteTargetDateReservation route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionProcessingLeadTimeSnapshotState state,
        int targetTotalDay)
    {
        if (!route.ReservationAxisResolved)
        {
            return Result(
                route,
                "blocked_upstream_inventory_reservation_axis",
                false,
                null,
                "upstream_inventory_reservation",
                Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[]
                    {
                        "upstream_inventory_reservation_axis_unresolved"
                    });
        }
        if (route.InventoryReservationMatchesTargetDate is null)
        {
            return NotApplicable(
                route,
                "not_applicable_upstream_inventory_reservation_axis");
        }
        if (route.InventoryReservationMatchesTargetDate == false)
        {
            return NotApplicable(
                route,
                "not_applicable_upstream_inventory_reservation_conflict");
        }

        var routeKind = RouteKind(route);
        return ProcessingClassByRouteKind[routeKind] switch
        {
            NoDeterministicWait => NotRequired(route),
            CropGrowth => EvaluateCrop(
                route,
                staticRoute,
                state,
                targetTotalDay),
            CrabPotProduction => EvaluateCrabPot(
                route,
                staticRoute.QualifiedItemId,
                state,
                targetTotalDay),
            _ => Blocked(
                route,
                DeferredProduction,
                "processing_lead_time_evaluator_pending_for_route_kind:" +
                routeKind)
        };
    }

    private static string RouteKind(
        AcquisitionRouteTargetDateReservation route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.RouteKind;

    private static string QualifiedItemId(
        AcquisitionRouteTargetDateReservation route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.QualifiedItemId;
}
