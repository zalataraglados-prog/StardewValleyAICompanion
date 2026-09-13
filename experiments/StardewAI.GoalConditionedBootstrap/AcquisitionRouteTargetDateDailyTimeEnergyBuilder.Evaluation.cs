using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateDailyTimeEnergyBuilder
{
    private const double FishingEnergyReserve = 1d;

    private static AcquisitionRouteTargetDateDailyTimeEnergy Evaluate(
        AcquisitionRouteTargetDateStochasticRetry route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRouteTargetDateFishingProbability fishingRoute,
        AcquisitionDailyTimeEnergySnapshotState state)
    {
        if (!route.StochasticRetryAxisResolved)
        {
            return Result(
                route,
                "upstream_stochastic_retry",
                "blocked_upstream_stochastic_retry_axis",
                false,
                null,
                null,
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_stochastic_retry_axis_unresolved" });
        }
        if (route.StochasticRetryBudgetMatchesTargetDate is null)
        {
            return NotApplicable(
                route,
                "not_applicable_upstream_stochastic_retry_axis");
        }
        if (route.StochasticRetryBudgetMatchesTargetDate == false)
        {
            return NotApplicable(
                route,
                "not_applicable_upstream_stochastic_retry_miss");
        }

        return staticRoute.RouteKind switch
        {
            "native_location_fish_spawn" => EvaluateFishing(
                route,
                staticRoute,
                fishingRoute,
                state),
            "sells" => EvaluateShop(route, staticRoute, state),
            _ => Result(
                route,
                "terminal_budget_not_implemented",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "daily_terminal_budget_kind_not_implemented:" +
                    staticRoute.RouteKind
                })
        };
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy EvaluateFishing(
        AcquisitionRouteTargetDateStochasticRetry route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionRouteTargetDateFishingProbability fishingRoute,
        AcquisitionDailyTimeEnergySnapshotState state)
    {
        var projection = fishingRoute.SelectedProjection;
        if (projection is null ||
            !projection.Resolved ||
            projection.Probability is null ||
            !projection.Probability.Resolved ||
            fishingRoute.IndependentRetryLowerBoundProven != true ||
            !route.RequiredAttemptCount.HasValue ||
            route.RequiredAttemptCount.Value <= 0 ||
            projection.EffectiveFishingLevel < 0 ||
            projection.BaseFishingLevel != projection.EffectiveFishingLevel)
        {
            return Result(
                route,
                "native_fishing_retry_attempts",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[]
                {
                    "fishing_selected_projection_or_retry_budget_incomplete"
                });
        }
        if (!state.AvailableEnergy.HasValue)
        {
            return Result(
                route,
                "native_fishing_retry_attempts",
                "blocked_daily_energy_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                state.EnergyBlockingReasons);
        }
        if (!LocationRoute(route).TargetEvaluations.Any(value =>
                value.Status == "resolved_location_route_match" &&
                string.Equals(
                    value.TargetLocationId,
                    projection.TargetLocationId,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return Result(
                route,
                "native_fishing_retry_attempts",
                "blocked_daily_route_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "selected_fishing_location_not_proven_upstream" });
        }

        var attempts = route.RequiredAttemptCount.Value;
        var terminalMinutes =
            FishingAttemptBudgetPolicy.ConservativeGameMinutesForAttempts(
                attempts,
                challengeBait: false);
        var energyPerAttempt = FishingAttemptBudgetPolicy.EnergyPerAttempt(
            projection.EffectiveFishingLevel,
            efficientEnchantment: false);
        var requiredEnergy = attempts * energyPerAttempt;
        return EvaluateTerminal(
            route,
            staticRoute,
            state,
            "native_fishing_retry_attempts",
            projection.TargetLocationId,
            projection.StandTileX,
            projection.StandTileY,
            requireExactTargetTile: true,
            terminalMinutes,
            attempts,
            projection.EffectiveFishingLevel,
            state.AvailableEnergy.Value,
            energyPerAttempt,
            requiredEnergy,
            FishingEnergyReserve,
            "perfect_lock_input_profile",
            new[]
            {
                "target_date_fishing_probability.routes[].selected_projection",
                "target_date_stochastic_retry_budget.routes[].required_attempt_count",
                "state.player.energy.value",
                "decompile:FishingRod.calculateTimeUntilFishingBite",
                "decompile:FishingRod.tickUpdate",
                "decompile:BobberBar.update"
            });
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy EvaluateShop(
        AcquisitionRouteTargetDateStochasticRetry route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionDailyTimeEnergySnapshotState state)
    {
        var shop = staticRoute.ShopSource;
        var purchaseCount = CurrencyRoute(route).CurrencyEvaluation?
            .RequiredPurchaseCount;
        if (shop is null ||
            string.IsNullOrWhiteSpace(shop.ShopId) ||
            !purchaseCount.HasValue ||
            purchaseCount.Value <= 0)
        {
            return Result(
                route,
                "native_shop_rolling_purchase",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "shop_endpoint_or_purchase_count_missing" });
        }

        var matchedLocations = LocationRoute(route).TargetEvaluations
            .Where(value => value.Status == "resolved_location_route_match")
            .Select(value => value.TargetLocationId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var endpoints = shop.InteractionEndpoints
            .Where(endpoint => !string.IsNullOrWhiteSpace(endpoint.Resolution) &&
                matchedLocations.Contains(LocationId(endpoint.MapAsset)))
            .OrderBy(endpoint => endpoint.MapAsset, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.X)
            .ThenBy(endpoint => endpoint.Y)
            .ToArray();
        if (endpoints.Length == 0)
        {
            return Result(
                route,
                "native_shop_rolling_purchase",
                "blocked_daily_terminal_budget_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                new[] { "resolved_shop_interaction_endpoint_missing" });
        }

        var terminalMinutes =
            ShopPurchaseBudgetPolicy.ConservativeGameMinutesForPurchases(
                purchaseCount.Value);
        var evaluations = endpoints.Select(endpoint => EvaluateTerminal(
                route,
                staticRoute,
                state,
                "native_shop_rolling_purchase",
                LocationId(endpoint.MapAsset),
                endpoint.X,
                endpoint.Y,
                requireExactTargetTile: false,
                terminalMinutes,
                purchaseCount.Value,
                null,
                null,
                null,
                null,
                0d,
                "rolling_exact_shop_menu_input_profile",
                new[]
                {
                    "static_calendar_resolution.routes[].shop_source.interaction_endpoints[]",
                    "target_date_currency_budget.routes[].currency_evaluation.required_purchase_count",
                    "compiler:DailyPlanCompiler.InteractEndpointSteps",
                    "compiler:DailyPlanCompiler.BuyShopItemSteps"
                }))
            .ToArray();
        return evaluations
            .OrderByDescending(value =>
                value.DailyTimeEnergyMatchesTargetDate == true)
            .ThenByDescending(value => value.DailyTimeEnergyAxisResolved)
            .ThenBy(value =>
                value.Evaluation?.GuaranteedCompletionByTime ?? int.MaxValue)
            .First();
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy EvaluateTerminal(
        AcquisitionRouteTargetDateStochasticRetry route,
        AcquisitionRouteCalendarResolution staticRoute,
        AcquisitionDailyTimeEnergySnapshotState state,
        string budgetKind,
        string targetLocation,
        int targetTileX,
        int targetTileY,
        bool requireExactTargetTile,
        int terminalMinutes,
        int attemptCount,
        int? effectiveFishingLevel,
        double? availableEnergy,
        double? energyPerAttempt,
        double? requiredEnergy,
        double energyReserve,
        string executionAssumption,
        string[] terminalEvidencePaths)
    {
        var routeState = state.RouteState;
        if (!routeState.RouteEvidenceAvailable || routeState.Timing is null)
        {
            return Result(
                route,
                budgetKind,
                "blocked_daily_route_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                routeState.RouteEvidenceBlockingReasons);
        }
        var production = new FutureRouteDateEvidenceProducer().Produce(
            routeState.RouteGraph,
            routeState.SocialRouteDateEvidence,
            new FutureRouteDateEvidenceRequest
            {
                TotalDays = state.TargetTotalDay,
                StartLocation = routeState.CurrentLocationId,
                StartTileX = routeState.CurrentTileX,
                StartTileY = routeState.CurrentTileY,
                EarliestDepartureTime = routeState.CurrentTime,
                TargetLocation = targetLocation,
                TargetTileX = targetTileX,
                TargetTileY = targetTileY,
                RequireExactTargetTile = requireExactTargetTile
            },
            routeState.Timing);
        var scenario = production.Scenario;
        var approaches = scenario?.ApproachEvidence;
        if (production.Status != FutureRouteDateEvidenceProductionStatus.Produced ||
            !production.GuaranteedArrivalByTime.HasValue ||
            scenario is null ||
            approaches is null ||
            approaches.Length != 1)
        {
            return Result(
                route,
                budgetKind,
                "blocked_daily_route_evidence",
                false,
                null,
                null,
                Array.Empty<string>(),
                production.BlockingReasons.Length > 0
                    ? production.BlockingReasons
                    : new[] { "terminal_route_production_incomplete" });
        }

        var arrival = production.GuaranteedArrivalByTime.Value;
        var approach = approaches[0];
        var window = RequirementRoute(route).MatchingWindows
            .Where(value => string.IsNullOrWhiteSpace(value.LocationId) ||
                string.Equals(
                    value.LocationId,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            .SelectMany(value => value.TimeWindows)
            .Select(value => new
            {
                Window = value,
                Start = Math.Max(arrival, value.StartTime),
                AvailableMinutes = GameClockBudgetPolicy.ClockMinutesBetween(
                    Math.Max(arrival, value.StartTime),
                    value.EndTime)
            })
            .Where(value => value.AvailableMinutes >= terminalMinutes)
            .OrderBy(value => value.Start)
            .ThenBy(value => value.Window.EndTime)
            .FirstOrDefault();
        var timeMatches = window is not null;
        var energyMatches = !requiredEnergy.HasValue ||
            availableEnergy.HasValue &&
            availableEnergy.Value >= requiredEnergy.Value + energyReserve;
        var evaluation = new AcquisitionDailyTimeEnergyEvaluation(
            targetLocation,
            targetTileX,
            targetTileY,
            approach.StandTileX,
            approach.StandTileY,
            routeState.CurrentTime,
            arrival,
            window?.Window.StartTime,
            window?.Window.EndTime,
            terminalMinutes,
            window is null
                ? null
                : GameClockBudgetPolicy.AddClockMinutes(
                    window.Start,
                    terminalMinutes),
            attemptCount,
            effectiveFishingLevel,
            availableEnergy,
            energyPerAttempt,
            requiredEnergy,
            energyReserve,
            timeMatches,
            energyMatches,
            executionAssumption,
            approach.TimingEvidenceId,
            terminalEvidencePaths.Concat(new[]
                {
                    "state.locations.route_graph.value",
                    "state.locations.social_route_date_evidence.value",
                    "route_timing_calibration",
                    "static_calendar_resolution.routes[].matching_windows[]"
                })
                .Distinct(StringComparer.Ordinal)
                .ToArray());
        if (!timeMatches)
        {
            return Result(
                route,
                budgetKind,
                "resolved_daily_time_budget_miss",
                true,
                false,
                evaluation,
                new[] { "terminal_duration_does_not_fit_source_window" },
                Array.Empty<string>());
        }
        if (!energyMatches)
        {
            return Result(
                route,
                budgetKind,
                "resolved_daily_energy_budget_miss",
                true,
                false,
                evaluation,
                new[] { "available_energy_below_required_cost_and_reserve" },
                Array.Empty<string>());
        }
        return Result(
            route,
            budgetKind,
            "resolved_daily_time_energy_budget_match",
            true,
            true,
            evaluation,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static string LocationId(string mapAsset)
    {
        var normalized = mapAsset.Replace('\\', '/');
        return normalized[(normalized.LastIndexOf('/') + 1)..];
    }

    private static AcquisitionRouteTargetDateDailyTimeEnergy NotApplicable(
        AcquisitionRouteTargetDateStochasticRetry route,
        string status) => Result(
            route,
            "not_applicable",
            status,
            true,
            null,
            null,
            Array.Empty<string>(),
            Array.Empty<string>());

    private static AcquisitionRouteTargetDateDailyTimeEnergy Result(
        AcquisitionRouteTargetDateStochasticRetry route,
        string budgetKind,
        string status,
        bool resolved,
        bool? matches,
        AcquisitionDailyTimeEnergyEvaluation? evaluation,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
            route.RouteOccurrenceId,
            route,
            budgetKind,
            status,
            resolved,
            matches,
            evaluation,
            nonMatchingReasons,
            blockingReasons);
}
