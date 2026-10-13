namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateProcessingBuilder
{
    private const string NoDeterministicWait =
        "no_deterministic_processing_wait";
    private const string CropGrowth = "crop_growth_or_ready_crop";
    private const string CrabPotProduction = "crab_pot_daily_production";
    private const string MachineProduction =
        "native_machine_processing_schedule";
    private const string MachineInputPurchase =
        "upstream_machine_input_purchase";
    private const string CurrentMachineOutput =
        "current_machine_output_materialized";
    private const string CurrentFruitTreeOutput =
        "current_fruit_tree_output_materialized";
    private const string CurrentAnimalProduct =
        "current_animal_product_materialized";
    private const string CurrentFishPondOutput =
        "current_fish_pond_output_materialized";
    private const string DeferredProduction =
        "production_lead_time_binding_pending_upstream";

    private static readonly IReadOnlyDictionary<string, string>
        ProcessingClassByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = NoDeterministicWait,
            ["harvests_as"] = CropGrowth,
            ["machine_output"] = MachineProduction,
            ["native_bush_shake"] = NoDeterministicWait,
            ["native_crab_pot_output"] = CrabPotProduction,
            ["native_farm_animal_deluxe_produce"] = CurrentAnimalProduct,
            ["native_farm_animal_produce"] = CurrentAnimalProduct,
            ["native_fish_pond_output"] = CurrentFishPondOutput,
            ["native_fruit_tree_produce"] = CurrentFruitTreeOutput,
            ["native_geode_default_drop"] = NoDeterministicWait,
            ["native_geode_drop"] = NoDeterministicWait,
            ["native_ginger_harvest"] = NoDeterministicWait,
            ["native_location_artifact_spot"] = NoDeterministicWait,
            ["native_location_fish_spawn"] = NoDeterministicWait,
            ["native_location_forage_spawn"] = NoDeterministicWait,
            ["native_machine_flavored_output"] = MachineProduction,
            ["native_machine_item_query_output"] = MachineProduction,
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
            ["native_wild_tree_tapper_output"] = CurrentMachineOutput,
            ["recipe_output"] = NoDeterministicWait,
            ["sells"] = NoDeterministicWait
        };

    internal static AcquisitionRouteTargetDateProcessing Evaluate(
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

        if (route.UpstreamRoute.CurrencyEvaluation?.PurchasePrerequisite
            is not null)
        {
            return ResolvedMiss(
                route,
                MachineInputPurchase,
                Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
                "machine_input_purchase_required_before_processing");
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
                staticRoute,
                state,
                targetTotalDay),
            MachineProduction => EvaluateMachine(
                route,
                staticRoute,
                state,
                targetTotalDay),
            CurrentMachineOutput => EvaluateCurrentMachineOutput(
                route,
                staticRoute,
                targetTotalDay),
            CurrentFruitTreeOutput => EvaluateCurrentFruitTreeOutput(
                route,
                staticRoute),
            CurrentAnimalProduct => EvaluateCurrentAnimalProduct(
                route,
                staticRoute),
            CurrentFishPondOutput => EvaluateCurrentFishPondOutput(
                route,
                staticRoute),
            _ => Blocked(
                route,
                DeferredProduction,
                "processing_lead_time_evaluator_pending_for_route_kind:" +
                routeKind)
        };
    }

    private static AcquisitionRouteTargetDateUnlock RequirementRoute(
        AcquisitionRouteTargetDateReservation route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;

    private static string RouteKind(
        AcquisitionRouteTargetDateReservation route) =>
        RequirementRoute(route).RouteKind;

    private static string QualifiedItemId(
        AcquisitionRouteTargetDateReservation route) =>
        RequirementRoute(route).QualifiedItemId;

    private static AcquisitionRouteTargetDateProcessing
        EvaluateCurrentFruitTreeOutput(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute)
    {
        if (staticRoute.EvidenceClass !=
                "current_native_fruit_tree_candidate_calendar" ||
            staticRoute.CalendarWindows.Length == 0 ||
            staticRoute.CalendarWindows.Any(window =>
                !window.RequiresExistingLiveCandidateMatch))
        {
            return Blocked(
                route,
                CurrentFruitTreeOutput,
                "current_fruit_tree_materialized_output_evidence_missing");
        }

        return Result(
            route,
            "resolved_current_fruit_tree_output_materialized",
            true,
            true,
            CurrentFruitTreeOutput,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateProcessing
        EvaluateCurrentAnimalProduct(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute)
    {
        if (staticRoute.EvidenceClass !=
                "current_native_animal_product_candidate_calendar" ||
            staticRoute.CalendarWindows.Length == 0 ||
            staticRoute.CalendarWindows.Any(window =>
                !window.RequiresExistingLiveCandidateMatch))
        {
            return Blocked(
                route,
                CurrentAnimalProduct,
                "current_animal_product_materialized_output_evidence_missing");
        }

        return Result(
            route,
            "resolved_current_animal_product_materialized",
            true,
            true,
            CurrentAnimalProduct,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AcquisitionRouteTargetDateProcessing
        EvaluateCurrentFishPondOutput(
            AcquisitionRouteTargetDateReservation route,
            AcquisitionRouteCalendarResolution staticRoute)
    {
        if (staticRoute.EvidenceClass !=
                "current_native_fish_pond_output_candidate_calendar" ||
            staticRoute.CalendarWindows.Length == 0 ||
            staticRoute.CalendarWindows.Any(window =>
                !window.RequiresExistingLiveCandidateMatch))
        {
            return Blocked(
                route,
                CurrentFishPondOutput,
                "current_fish_pond_materialized_output_evidence_missing");
        }

        return Result(
            route,
            "resolved_current_fish_pond_output_materialized",
            true,
            true,
            CurrentFishPondOutput,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
