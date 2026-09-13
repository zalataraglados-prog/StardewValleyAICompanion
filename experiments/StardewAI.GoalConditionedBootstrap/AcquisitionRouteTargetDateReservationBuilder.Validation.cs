using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateReservationBuilder
{
    private static void ValidateSource(
        AcquisitionRouteTargetDateCurrencyReport source)
    {
        Require(source.SchemaVersion ==
                    "acquisition_route_target_date_currency_budget.v1" &&
                source.RouteOccurrenceInventoryComplete &&
                !source.TrainingLabelEligible &&
                source.RouteOccurrenceCount == source.Routes.Length &&
                source.Routes.Select(route => route.RouteOccurrenceId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    source.Routes.Length,
            "Target-date currency-budget metadata is incomplete.");
        Require(source.CurrencyAxisResolvedCount == source.Routes.Count(route =>
                    route.CurrencyAxisResolved) &&
                source.CurrencyBudgetMatchCount == source.Routes.Count(route =>
                    route.CurrencyBudgetMatchesTargetDate == true) &&
                source.CurrencyBudgetMissCount == source.Routes.Count(route =>
                    route.CurrencyBudgetMatchesTargetDate == false) &&
                source.CurrencyNotRequiredCount == source.Routes.Count(route =>
                    route.CurrencyAxisStatus ==
                        "resolved_currency_not_required") &&
                source.NotApplicableUpstreamCount == source.Routes.Count(route =>
                    route.CurrencyAxisStatus.StartsWith(
                        "not_applicable_upstream_",
                        StringComparison.Ordinal)) &&
                source.BlockedUpstreamCount == source.Routes.Count(route =>
                    route.CurrencyAxisStatus ==
                        "blocked_upstream_resource_input_axis") &&
                source.BlockedCurrencyEvidenceCount == source.Routes.Count(route =>
                    route.CurrencyAxisStatus ==
                        "blocked_currency_budget_evidence"),
            "Target-date currency-budget counts drifted.");
        Require(source.Routes.All(ValidMatchedRouteContract),
            "A matched currency route has an invalid reservation input contract.");
    }

    private static bool ValidMatchedRouteContract(
        AcquisitionRouteTargetDateCurrency route)
    {
        if (route.CurrencyBudgetMatchesTargetDate != true)
            return true;
        if (route.UpstreamRoute.ResourceInputsMatchTargetDate != true ||
            route.UpstreamRoute.InputEvaluations.Any(row =>
                row.RequiredQuantity < 0 ||
                row.AvailableQuantity < row.RequiredQuantity ||
                row.Status != "resolved_resource_input_match" &&
                row.Status !=
                    "resolved_existing_target_crop_requires_no_new_seed"))
        {
            return false;
        }
        if (route.CurrencyRequirementKind == "no_direct_currency_cost")
            return route.CurrencyEvaluation is null;
        return route.CurrencyEvaluation is not null &&
            route.CurrencyEvaluation.RequiredAmount.HasValue &&
            route.CurrencyEvaluation.RequiredAmount >= 0 &&
            route.CurrencyEvaluation.AvailableAmount >=
                route.CurrencyEvaluation.RequiredAmount;
    }

    private static JsonElement RequiredObject(JsonElement value, string name)
    {
        Require(value.TryGetProperty(name, out var result) &&
                result.ValueKind == JsonValueKind.Object,
            "Snapshot " + name + " is missing.");
        return result;
    }

    private static bool EqualJson<T>(T left, T right)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        return JsonSerializer.Serialize(left, options) ==
            JsonSerializer.Serialize(right, options);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
