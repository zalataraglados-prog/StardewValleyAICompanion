using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static class RainTotemMaterialReservationSettlementRequestBuilder
{
    public static RainTotemMaterialReservationSettlementBuildResult Build(
        ActionQueueItem item,
        string beforeStateHash,
        string afterStateHash,
        string executionReceiptSha256)
    {
        var errors = new List<string>();
        if (!string.Equals(
                item.OptionId,
                "executor.craft_rain_totem",
                StringComparison.Ordinal))
        {
            errors.Add("rain_totem_settlement_option_invalid");
        }

        var targetDay = IntParameter(
            item,
            "weather_supply_target_total_day");
        var affectedContext = Parameter(
            item,
            "weather_supply_affected_context_id");
        if (!targetDay.HasValue || string.IsNullOrWhiteSpace(affectedContext))
            errors.Add("rain_totem_settlement_target_invalid");

        var authorization = targetDay.HasValue &&
                            !string.IsNullOrWhiteSpace(affectedContext)
            ? RainTotemMaterialReservationContract.Authorization(
                targetDay.Value,
                affectedContext)
            : null;
        var portfolioId = targetDay.HasValue &&
                          !string.IsNullOrWhiteSpace(affectedContext)
            ? RainTotemMaterialReservationContract.PortfolioId(
                targetDay.Value,
                affectedContext)
            : string.Empty;
        if (authorization is not null &&
            (!string.Equals(
                 Parameter(item, "material_reservation_goal_id"),
                 authorization.GoalId,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 Parameter(item, "material_reservation_source_decision_id"),
                 authorization.SourceDecisionId,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 Parameter(item, "material_reservation_purpose"),
                 authorization.Purpose,
                 StringComparison.Ordinal) ||
             !string.Equals(
                 Parameter(item, "material_reservation_portfolio_id"),
                 portfolioId,
                 StringComparison.Ordinal)))
        {
            errors.Add("rain_totem_settlement_identity_invalid");
        }

        var ledgerRevision = IntParameter(
            item,
            "material_reservation_ledger_revision");
        if (!ledgerRevision.HasValue || ledgerRevision.Value <= 0)
            errors.Add("rain_totem_settlement_ledger_revision_invalid");

        var consumptions = ReadConsumptions(item, errors);
        if (authorization is not null && consumptions.Length > 0)
        {
            var expectedIds = consumptions.Select(row =>
                    RainTotemMaterialReservationContract.ReservationId(
                        authorization,
                        new Infrastructure.ExactCraftingMaterialConsumption(
                            row.NodeId,
                            row.SlotIndex,
                            row.QualifiedItemId,
                            row.ConsumedQuantity)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var actualIds = consumptions
                .Select(row => row.MaterialReservationId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal) ||
                !ReadStringArrayParameter(
                        item,
                        "material_reservation_ids_json")
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedIds, StringComparer.Ordinal))
            {
                errors.Add("rain_totem_settlement_consumption_identity_invalid");
            }
        }

        if (string.IsNullOrWhiteSpace(beforeStateHash) ||
            string.IsNullOrWhiteSpace(afterStateHash))
        {
            errors.Add("rain_totem_settlement_state_hash_missing");
        }
        if (!IsLowerSha256(executionReceiptSha256))
            errors.Add("rain_totem_settlement_execution_receipt_invalid");

        if (errors.Count > 0 || authorization is null ||
            !ledgerRevision.HasValue)
        {
            return new RainTotemMaterialReservationSettlementBuildResult(
                null,
                errors.Distinct(StringComparer.Ordinal).ToArray());
        }

        var receiptSha256 = ReceiptSha256(
            item,
            beforeStateHash,
            afterStateHash,
            executionReceiptSha256,
            portfolioId,
            authorization,
            consumptions);
        return new RainTotemMaterialReservationSettlementBuildResult(
            new ReservationPortfolioSupportingTransitionSettlementRequest
            {
                StateHash = afterStateHash,
                ExpectedLedgerRevision = ledgerRevision.Value,
                PortfolioId = portfolioId,
                GoalId = authorization.GoalId,
                RouteSourceDecisionId = authorization.SourceDecisionId,
                SupportingTransitionReceiptSha256 = receiptSha256,
                MaterialConsumptions = consumptions,
                Reason = "native_rain_totem_recipe_consumed_reserved_materials"
            },
            Array.Empty<string>());
    }

    private static ReservationPortfolioMaterialConsumption[] ReadConsumptions(
        ActionQueueItem item,
        ICollection<string> errors)
    {
        try
        {
            var rows = JsonSerializer.Deserialize<
                    ReservationPortfolioMaterialConsumption[]>(
                    Parameter(
                        item,
                        "material_reservation_consumptions_json")) ??
                Array.Empty<ReservationPortfolioMaterialConsumption>();
            if (rows.Length == 0 ||
                rows.Any(row =>
                    string.IsNullOrWhiteSpace(row.MaterialReservationId) ||
                    string.IsNullOrWhiteSpace(row.NodeId) ||
                    row.SlotIndex < 0 ||
                    string.IsNullOrWhiteSpace(row.QualifiedItemId) ||
                    row.ConsumedQuantity <= 0) ||
                rows.Select(row => row.MaterialReservationId)
                    .Distinct(StringComparer.Ordinal).Count() != rows.Length)
            {
                errors.Add("rain_totem_settlement_consumptions_invalid");
                return Array.Empty<ReservationPortfolioMaterialConsumption>();
            }

            return rows.OrderBy(
                    row => row.MaterialReservationId,
                    StringComparer.Ordinal)
                .ToArray();
        }
        catch (JsonException)
        {
            errors.Add("rain_totem_settlement_consumptions_invalid");
            return Array.Empty<ReservationPortfolioMaterialConsumption>();
        }
    }

    private static string ReceiptSha256(
        ActionQueueItem item,
        string beforeStateHash,
        string afterStateHash,
        string executionReceiptSha256,
        string portfolioId,
        Infrastructure.MaterialReservationConsumptionAuthorization authorization,
        IEnumerable<ReservationPortfolioMaterialConsumption> consumptions)
    {
        var canonical = new StringBuilder()
            .Append("rain_totem_material_settlement.v1\n")
            .Append(item.QueueItemId).Append('\n')
            .Append(item.SourceActionId).Append('\n')
            .Append(beforeStateHash).Append('\n')
            .Append(afterStateHash).Append('\n')
            .Append(executionReceiptSha256).Append('\n')
            .Append(portfolioId).Append('\n')
            .Append(authorization.GoalId).Append('\n')
            .Append(authorization.SourceDecisionId).Append('\n');
        foreach (var row in consumptions.OrderBy(
                     value => value.MaterialReservationId,
                     StringComparer.Ordinal))
        {
            canonical.Append(row.MaterialReservationId).Append('|')
                .Append(row.NodeId).Append('|')
                .Append(row.SlotIndex).Append('|')
                .Append(row.QualifiedItemId).Append('|')
                .Append(row.ConsumedQuantity).Append('\n');
        }

        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(
                Encoding.UTF8.GetBytes(canonical.ToString())))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static string Parameter(ActionQueueItem item, string name) =>
        item.NormalizedCommand.Parameters.FirstOrDefault(parameter =>
            string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal))?.Value ?? string.Empty;

    private static int? IntParameter(ActionQueueItem item, string name) =>
        int.TryParse(Parameter(item, name), out var value)
            ? value
            : null;

    private static string[] ReadStringArrayParameter(
        ActionQueueItem item,
        string name)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(
                    Parameter(item, name)) ??
                Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static bool IsLowerSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record RainTotemMaterialReservationSettlementBuildResult(
    ReservationPortfolioSupportingTransitionSettlementRequest? Request,
    string[] Errors)
{
    public bool Valid => Request is not null && Errors.Length == 0;
}
