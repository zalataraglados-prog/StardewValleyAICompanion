using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateOpportunityCostBuilder
{
    private static AcquisitionRouteTargetDateOpportunityCost Evaluate(
        AcquisitionRouteTargetDateDailyTimeEnergy route,
        string snapshotStateHash,
        AcquisitionOpportunityCostSnapshotState state)
    {
        var groupKey = ComparisonGroupKey(route);
        if (!route.DailyTimeEnergyAxisResolved)
        {
            return Result(
                route,
                groupKey,
                "blocked_upstream_daily_time_energy_axis",
                false,
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                route.BlockingReasons.Length > 0
                    ? route.BlockingReasons
                    : new[] { "upstream_daily_time_energy_axis_unresolved" });
        }
        if (route.DailyTimeEnergyMatchesTargetDate is null)
            return NotApplicable(route, groupKey,
                "not_applicable_upstream_daily_time_energy_axis");
        if (route.DailyTimeEnergyMatchesTargetDate == false)
            return NotApplicable(route, groupKey,
                "not_applicable_upstream_daily_time_energy_miss");

        var reservation = ReservationRoute(route);
        if (!reservation.ReservationAxisResolved ||
            reservation.InventoryReservationMatchesTargetDate != true)
        {
            return Blocked(
                route,
                groupKey,
                reservation.BlockingReasons.Length > 0
                    ? reservation.BlockingReasons
                    : new[] { "matched_daily_route_reservation_not_proven" });
        }
        if (!TryBuildVector(
                route,
                reservation,
                snapshotStateHash,
                state,
                out var vector,
                out var blockingReasons))
        {
            return Blocked(route, groupKey, blockingReasons);
        }
        return Result(
            route,
            groupKey,
            "resolved_opportunity_cost_vector_pending_pareto",
            true,
            true,
            vector,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static bool TryBuildVector(
        AcquisitionRouteTargetDateDailyTimeEnergy route,
        AcquisitionRouteTargetDateReservation reservation,
        string snapshotStateHash,
        AcquisitionOpportunityCostSnapshotState state,
        out AcquisitionOpportunityCostVector? vector,
        out string[] blockingReasons)
    {
        vector = null;
        var reasons = new List<string>();
        var evaluation = route.Evaluation;
        if (evaluation is null ||
            !evaluation.GuaranteedCompletionByTime.HasValue ||
            evaluation.TimeBudgetMatches != true ||
            evaluation.EnergyBudgetMatches != true ||
            evaluation.RequiredEnergy is < 0d ||
            evaluation.RequiredEnergy.HasValue &&
                !double.IsFinite(evaluation.RequiredEnergy.Value))
        {
            blockingReasons = new[]
            {
                "matched_daily_route_cost_evaluation_incomplete"
            };
            return false;
        }
        var elapsed = GameClockBudgetPolicy.ClockMinutesBetween(
            evaluation.SnapshotStartTime,
            evaluation.GuaranteedCompletionByTime.Value);
        if (elapsed < 0)
            reasons.Add("guaranteed_elapsed_game_minutes_invalid");

        var claimSet = reservation.ClaimSet;
        if (reservation.ClaimDisposition == "not_required")
        {
            if (claimSet is not null)
                reasons.Add("not_required_reservation_has_claim_set");
        }
        else if (reservation.ClaimDisposition is
                 "claim_proposed" or
                 "claim_already_committed" or
                 "claim_replacement_required")
        {
            if (claimSet is null ||
                !claimSet.AtomicCommitRequired ||
                claimSet.SourceStateHash != snapshotStateHash)
            {
                reasons.Add("reservation_claim_set_identity_invalid");
            }
        }
        else
        {
            reasons.Add("reservation_claim_disposition_invalid");
        }

        var materialClaims = claimSet?.MaterialClaims ??
            Array.Empty<MaterialReservationUpsertRequest>();
        var currencyClaims = claimSet?.CurrencyClaims ??
            Array.Empty<CurrencyReservationUpsertRequest>();
        if (materialClaims.Length > 0 && !state.MaterialEvidenceAvailable)
            reasons.AddRange(state.BlockingReasons);

        var materialRows = new List<AcquisitionOpportunityMaterialCost>();
        if (reasons.Count == 0)
        {
            var materialParts = new List<MaterialCostPart>();
            foreach (var claim in materialClaims)
            {
                var key = AcquisitionOpportunityCostSnapshotState.SlotKey(
                    claim.NodeId,
                    claim.SlotIndex);
                if (claim.Quantity <= 0 ||
                    claim.StateHash != snapshotStateHash ||
                    !state.Slots.TryGetValue(key, out var slot) ||
                    !slot.ActorUseAuthorized ||
                    slot.SupplyState != "available" ||
                    slot.QualifiedItemId != claim.QualifiedItemId ||
                    claim.Quantity > slot.Stack)
                {
                    reasons.Add("material_claim_cost_identity_invalid:" + key);
                    continue;
                }
                materialParts.Add(new MaterialCostPart(
                    slot.QualifiedItemId,
                    slot.Quality,
                    slot.SalePrice,
                    claim.Quantity));
            }
            try
            {
                materialRows.AddRange(materialParts
                    .GroupBy(part => new
                    {
                        part.QualifiedItemId,
                        part.Quality,
                        part.UnitSalePrice
                    })
                    .Select(group =>
                    {
                        var quantity = checked(group.Sum(part => part.Quantity));
                        return new AcquisitionOpportunityMaterialCost(
                            group.Key.QualifiedItemId,
                            group.Key.Quality,
                            group.Key.UnitSalePrice,
                            quantity,
                            checked(quantity * group.Key.UnitSalePrice));
                    })
                    .OrderBy(row => row.QualifiedItemId, StringComparer.Ordinal)
                    .ThenBy(row => row.Quality)
                    .ThenBy(row => row.UnitSalePrice));
            }
            catch (OverflowException)
            {
                reasons.Add("material_claim_cost_overflow");
            }
        }

        var currencyRows = new List<AcquisitionOpportunityCurrencyCost>();
        if (reasons.Count == 0)
        {
            try
            {
                foreach (var group in currencyClaims.GroupBy(claim =>
                             claim.CurrencyId).OrderBy(group => group.Key))
                {
                    if (!NativeShopCurrencies.TryGetKey(
                            group.Key,
                            out var currencyKey) ||
                        group.Any(claim => claim.Amount <= 0 ||
                            claim.StateHash != snapshotStateHash))
                    {
                        reasons.Add(
                            "currency_claim_cost_identity_invalid:" +
                            group.Key);
                        continue;
                    }
                    currencyRows.Add(new AcquisitionOpportunityCurrencyCost(
                        group.Key,
                        currencyKey,
                        checked(group.Sum(claim => claim.Amount))));
                }
            }
            catch (OverflowException)
            {
                reasons.Add("currency_claim_cost_overflow");
            }
        }

        if (reasons.Count > 0)
        {
            blockingReasons = reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return false;
        }
        try
        {
            vector = new AcquisitionOpportunityCostVector(
                elapsed,
                evaluation.RequiredEnergy ?? 0d,
                checked(materialRows.Sum(row => row.TotalSaleValue)),
                materialRows.ToArray(),
                currencyRows.ToArray(),
                new[]
                {
                    "target_date_daily_time_energy_budget.routes[].evaluation",
                    "target_date_inventory_reservation.routes[].claim_set",
                    "state.farm.material_inventory_graph.value.inventory_nodes[].slots[]"
                });
        }
        catch (OverflowException)
        {
            blockingReasons = new[] { "material_total_sale_value_overflow" };
            return false;
        }
        blockingReasons = Array.Empty<string>();
        return true;
    }

    private static AcquisitionRouteTargetDateOpportunityCost NotApplicable(
        AcquisitionRouteTargetDateDailyTimeEnergy route,
        string groupKey,
        string status) => Result(
        route,
        groupKey,
        status,
        true,
        null,
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        Array.Empty<string>());

    private static AcquisitionRouteTargetDateOpportunityCost Blocked(
        AcquisitionRouteTargetDateDailyTimeEnergy route,
        string groupKey,
        string[] blockingReasons) => Result(
        route,
        groupKey,
        "blocked_opportunity_cost_evidence",
        false,
        null,
        null,
        Array.Empty<string>(),
        Array.Empty<string>(),
        blockingReasons);

    private static AcquisitionRouteTargetDateOpportunityCost Result(
        AcquisitionRouteTargetDateDailyTimeEnergy route,
        string groupKey,
        string status,
        bool resolved,
        bool? matches,
        AcquisitionOpportunityCostVector? vector,
        string[] dominatedBy,
        string[] nonMatchingReasons,
        string[] blockingReasons) => new(
        route.RouteOccurrenceId,
        route,
        groupKey,
        status,
        resolved,
        matches,
        vector,
        dominatedBy,
        nonMatchingReasons,
        blockingReasons);

    private sealed record MaterialCostPart(
        string QualifiedItemId,
        int Quality,
        int UnitSalePrice,
        int Quantity);

}
