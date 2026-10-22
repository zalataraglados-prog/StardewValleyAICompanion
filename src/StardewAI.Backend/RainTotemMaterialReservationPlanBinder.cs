using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

public static class RainTotemMaterialReservationPlanBinder
{
    public static ReservationPortfolioCommitResult? Bind(
        SmallModelPlanEnvelope plan,
        SnapshotEnvelope snapshot,
        IStrategyCommitmentRepository repository)
    {
        var steps = plan.Steps.Where(step => string.Equals(
                step.Kind,
                "craft_rain_totem",
                StringComparison.Ordinal))
            .ToArray();
        if (steps.Length == 0)
            return null;
        if (steps.Length != 1)
        {
            return Rejected(
                "rain_totem_material_reservation_requires_single_craft_step");
        }

        var step = steps[0];
        if (!int.TryParse(
                Parameter(step, "weather_supply_target_total_day"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var targetTotalDay))
        {
            return Rejected(
                "rain_totem_material_reservation_target_day_invalid");
        }
        var affectedContext = Parameter(
            step,
            "weather_supply_affected_context_id");
        if (string.IsNullOrWhiteSpace(affectedContext))
        {
            return Rejected(
                "rain_totem_material_reservation_context_missing");
        }

        var authorization = RainTotemMaterialReservationContract.Authorization(
            targetTotalDay,
            affectedContext);
        var portfolioId = RainTotemMaterialReservationContract.PortfolioId(
            targetTotalDay,
            affectedContext);
        if (!MatchesAuthorization(step, authorization, portfolioId))
        {
            return Rejected(
                "rain_totem_material_reservation_identity_invalid");
        }

        if (!MaterialInventoryGraphReader.TryRead(
                snapshot,
                out var graph) ||
            graph is null)
        {
            return Rejected("material_inventory_graph_unavailable");
        }

        ExactCraftingMaterialConsumption[] consumptions;
        try
        {
            using var ingredients = JsonDocument.Parse(
                Parameter(step, "ingredient_rows_json"));
            consumptions = ExactCraftingMaterialConsumptionReader.Read(
                ingredients.RootElement,
                string.Equals(
                    Parameter(step, "crafting_source"),
                    "native_workbench_crafting_menu",
                    StringComparison.Ordinal),
                graph.PlayerId);
        }
        catch (JsonException)
        {
            return Rejected(
                "rain_totem_material_consumption_plan_invalid");
        }
        if (consumptions.Length == 0)
        {
            return Rejected(
                "rain_totem_material_consumption_plan_unavailable");
        }

        var ledger = repository.Get(snapshot);
        var claims = consumptions.Select(consumption =>
            new MaterialReservationUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = ledger.Revision,
                ReservationId =
                    RainTotemMaterialReservationContract.ReservationId(
                        authorization,
                        consumption),
                SourceDecisionId = authorization.SourceDecisionId,
                GoalId = authorization.GoalId,
                NodeId = consumption.NodeId,
                SlotIndex = consumption.SlotIndex,
                QualifiedItemId = consumption.QualifiedItemId,
                Quantity = consumption.Quantity,
                Purpose = authorization.Purpose
            })
            .ToArray();
        var desiredIds = claims.Select(row => row.ReservationId)
            .ToHashSet(StringComparer.Ordinal);
        var staleIds = ledger.MaterialReservations
            .Where(row =>
                string.Equals(
                    row.Status,
                    StrategyCommitmentStatuses.Active,
                    StringComparison.Ordinal) &&
                authorization.Matches(row) &&
                !desiredIds.Contains(row.ReservationId))
            .Select(row => row.ReservationId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var result = repository.CommitReservationPortfolio(
            snapshot,
            new ReservationPortfolioCommitRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = ledger.Revision,
                PortfolioId = portfolioId,
                GoalId = authorization.GoalId,
                SourceDecisionId = portfolioId,
                ReleaseReservationIds = staleIds,
                MaterialClaims = claims
            });
        if (result.Accepted && result.Ledger is not null)
        {
            var settlementConsumptions =
                RainTotemMaterialReservationContract.SettlementConsumptions(
                    authorization,
                    consumptions);
            RebindPlan(
                plan,
                step,
                result.Ledger,
                portfolioId,
                claims.Select(row => row.ReservationId).ToArray(),
                settlementConsumptions);
        }
        return result;
    }

    private static bool MatchesAuthorization(
        SmallModelPlanStep step,
        MaterialReservationConsumptionAuthorization authorization,
        string portfolioId) =>
        string.Equals(
            Parameter(step, "material_reservation_goal_id"),
            authorization.GoalId,
            StringComparison.Ordinal) &&
        string.Equals(
            Parameter(step, "material_reservation_source_decision_id"),
            authorization.SourceDecisionId,
            StringComparison.Ordinal) &&
        string.Equals(
            Parameter(step, "material_reservation_purpose"),
            authorization.Purpose,
            StringComparison.Ordinal) &&
        string.Equals(
            Parameter(step, "material_reservation_portfolio_id"),
            portfolioId,
            StringComparison.Ordinal);

    private static void RebindPlan(
        SmallModelPlanEnvelope plan,
        SmallModelPlanStep rainTotemStep,
        StrategyCommitmentLedger ledger,
        string portfolioId,
        string[] reservationIds,
        ReservationPortfolioMaterialConsumption[] settlementConsumptions)
    {
        foreach (var step in plan.Steps)
        {
            SetIfPresent(step, "commitment_ledger_id", ledger.LedgerId);
            SetIfPresent(
                step,
                "commitment_ledger_revision",
                ledger.Revision.ToString(CultureInfo.InvariantCulture));
            SetIfPresent(
                step,
                "material_reservation_ledger_id",
                ledger.LedgerId);
            SetIfPresent(
                step,
                "material_reservation_ledger_revision",
                ledger.Revision.ToString(CultureInfo.InvariantCulture));
        }

        Set(rainTotemStep, "material_reservation_guard_status", "ready");
        Set(
            rainTotemStep,
            "material_reservation_ids_json",
            JsonSerializer.Serialize(reservationIds.OrderBy(
                value => value,
                StringComparer.Ordinal)));
        Set(
            rainTotemStep,
            "material_reservation_consumptions_json",
            JsonSerializer.Serialize(settlementConsumptions));
        Set(
            rainTotemStep,
            "material_reservation_portfolio_id",
            portfolioId);
    }

    private static ReservationPortfolioCommitResult Rejected(
        string reason) => new()
        {
            Errors = [reason]
        };

    private static void SetIfPresent(
        SmallModelPlanStep step,
        string name,
        string value)
    {
        if (step.Parameters.Any(parameter => string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal)))
        {
            Set(step, name, value);
        }
    }

    private static void Set(
        SmallModelPlanStep step,
        string name,
        string value)
    {
        var existing = step.Parameters.FirstOrDefault(parameter =>
            string.Equals(parameter.Name, name, StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        step.Parameters = step.Parameters.Append(
            new SmallModelActionParameter
            {
                Name = name,
                Value = value
            }).ToArray();
    }

    private static string Parameter(
        SmallModelPlanStep step,
        string name) =>
        step.Parameters.FirstOrDefault(parameter => string.Equals(
            parameter.Name,
            name,
            StringComparison.Ordinal))?.Value ?? string.Empty;
}
