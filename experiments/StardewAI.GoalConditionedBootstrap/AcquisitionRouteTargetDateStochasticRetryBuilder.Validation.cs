namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    private static readonly IReadOnlyDictionary<string, string>
        UncertaintyModeByRouteKind = new Dictionary<string, string>(
            StringComparer.Ordinal)
        {
            ["creates_reward_item"] = DeterministicReceipt,
            ["harvests_as"] = SourceResolvedDownstream,
            ["machine_output"] = NativeRetryBound,
            ["native_bush_shake"] = DeterministicReceipt,
            ["native_crab_pot_output"] = NativeRetryBound,
            ["native_farm_animal_deluxe_produce"] = NativeRetryBound,
            ["native_farm_animal_produce"] = NativeRetryBound,
            ["native_fish_pond_output"] = NativeRetryBound,
            ["native_fruit_tree_produce"] = DeterministicReceipt,
            ["native_geode_default_drop"] = NativeRetryBound,
            ["native_geode_drop"] = NativeRetryBound,
            ["native_ginger_harvest"] = DeterministicReceipt,
            ["native_location_artifact_spot"] = NativeRetryBound,
            ["native_location_fish_spawn"] = NativeRetryBound,
            ["native_location_forage_spawn"] = NativeRetryBound,
            ["native_machine_flavored_output"] = NativeRetryBound,
            ["native_machine_item_query_output"] = NativeRetryBound,
            ["native_mine_buried_item"] = NativeRetryBound,
            ["native_mine_fishing_override"] = NativeRetryBound,
            ["native_money_payment"] = DeterministicReceipt,
            ["native_monster_drop_table"] = NativeRetryBound,
            ["native_object_artifact_spot_chance"] = NativeRetryBound,
            ["native_radioactive_ore_node"] = NativeRetryBound,
            ["native_solar_panel_output"] = DeterministicReceipt,
            ["native_spring_onion_harvest"] = DeterministicReceipt,
            ["native_tea_bush_harvest"] = DeterministicReceipt,
            ["native_tree_moss_harvest"] = DeterministicReceipt,
            ["native_wild_tree_chop_drop"] = NativeRetryBound,
            ["native_wild_tree_seed"] = NativeRetryBound,
            ["native_wild_tree_seed_drop"] = NativeRetryBound,
            ["native_wild_tree_tapper_output"] = NativeRetryBound,
            ["recipe_output"] = DeterministicReceipt,
            ["sells"] = DeterministicReceipt
        };

    private static void ValidateSource(
        AcquisitionRouteTargetDateProcessingReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_processing_lead_time.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date processing-lead-time metadata is incomplete.");
        Require(source.ProcessingLeadTimeAxisResolvedCount ==
                    source.Routes.Count(route =>
                        route.ProcessingLeadTimeAxisResolved) &&
                source.ProcessingLeadTimeMatchCount == source.Routes.Count(
                    route =>
                        route.ProcessingLeadTimeMatchesTargetDate == true) &&
                source.ProcessingLeadTimeMissCount == source.Routes.Count(
                    route =>
                        route.ProcessingLeadTimeMatchesTargetDate == false) &&
                source.ProcessingLeadTimeNotRequiredCount ==
                    source.Routes.Count(route =>
                        route.ProcessingLeadTimeRequirementKind ==
                            "no_deterministic_processing_wait") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(
                    route => route.ProcessingLeadTimeAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.ProcessingLeadTimeAxisStatus ==
                        "blocked_upstream_inventory_reservation_axis") &&
                source.BlockedProcessingEvidenceCount == source.Routes.Count(
                    route => route.ProcessingLeadTimeAxisStatus ==
                        "blocked_processing_lead_time_evidence"),
            "Target-date processing-lead-time counts drifted.");
        Require(UncertaintyModeByRouteKind.Count == 33,
            "Stochastic retry route-kind inventory drifted.");
        foreach (var route in source.Routes)
        {
            var requirement = RequirementRoute(route);
            Require(UncertaintyModeByRouteKind.TryGetValue(
                        requirement.RouteKind,
                        out var expectedMode) &&
                    requirement.UncertaintyMode == expectedMode,
                "A processing route has an invalid stochastic classification: " +
                route.RouteOccurrenceId);
        }
    }

    private static void ValidateStaticSource(
        AcquisitionRouteCalendarResolutionReport staticSource,
        AcquisitionRouteTargetDateProcessingReport processingSource)
    {
        Require(staticSource.SchemaVersion ==
                    "acquisition_route_calendar_resolution.v1" &&
                staticSource.RouteOccurrenceInventoryComplete &&
                !staticSource.TrainingLabelEligible &&
                staticSource.GoalId == processingSource.GoalId &&
                staticSource.GameVersion == processingSource.GameVersion &&
                staticSource.RouteOccurrenceCount == staticSource.Routes.Length,
            "Static calendar metadata is incomplete for stochastic retry.");
        var staticRoutes = staticSource.Routes.ToDictionary(
            route => route.RouteOccurrenceId,
            StringComparer.Ordinal);
        Require(staticRoutes.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
                processingSource.Routes.Select(route =>
                    route.RouteOccurrenceId)),
            "Static calendar and processing route inventories disagree.");
        foreach (var route in processingSource.Routes)
        {
            var requirement = RequirementRoute(route);
            var staticRoute = staticRoutes[route.RouteOccurrenceId];
            Require(staticRoute.RouteKind == requirement.RouteKind &&
                    staticRoute.UncertaintyMode ==
                        requirement.UncertaintyMode &&
                    staticRoute.RequiredAmount == requirement.RequiredAmount &&
                    staticRoute.MinimumQuality == requirement.MinimumQuality,
                "Static stochastic contract disagrees with processing route: " +
                route.RouteOccurrenceId);
        }
    }
}
