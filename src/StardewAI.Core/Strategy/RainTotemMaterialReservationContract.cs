using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Strategy;

public static class RainTotemMaterialReservationContract
{
    public const string GoalId = "goal.fishing.complete_master_angler";

    public static string PortfolioId(
        int targetTotalDay,
        string affectedLocationContextId) =>
        "portfolio:master-angler:rain-totem:" +
        IdentitySuffix(targetTotalDay, affectedLocationContextId);

    public static string SourceDecisionId(
        int targetTotalDay,
        string affectedLocationContextId) =>
        "decision:master-angler:rain-totem:" +
        IdentitySuffix(targetTotalDay, affectedLocationContextId);

    public static string Purpose(
        int targetTotalDay,
        string affectedLocationContextId) =>
        "consume_native_rain_totem_recipe:" +
        IdentitySuffix(targetTotalDay, affectedLocationContextId);

    public static string ReservationId(
        MaterialReservationConsumptionAuthorization authorization,
        ExactCraftingMaterialConsumption consumption)
    {
        var canonical = authorization.GoalId + "\n" +
            authorization.SourceDecisionId + "\n" +
            authorization.Purpose + "\n" +
            consumption.NodeId + "\n" +
            consumption.SlotIndex + "\n" +
            consumption.QualifiedItemId;
        using var sha256 = SHA256.Create();
        var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return "material:rain-totem:" + BitConverter.ToString(digest)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    public static MaterialReservationConsumptionAuthorization Authorization(
        int targetTotalDay,
        string affectedLocationContextId) => new(
            GoalId,
            SourceDecisionId(targetTotalDay, affectedLocationContextId),
            Purpose(targetTotalDay, affectedLocationContextId));

    public static ReservationPortfolioMaterialConsumption[]
        SettlementConsumptions(
            MaterialReservationConsumptionAuthorization authorization,
            IEnumerable<ExactCraftingMaterialConsumption> consumptions) =>
        consumptions
            .Select(consumption =>
                new ReservationPortfolioMaterialConsumption
                {
                    MaterialReservationId = ReservationId(
                        authorization,
                        consumption),
                    NodeId = consumption.NodeId,
                    SlotIndex = consumption.SlotIndex,
                    QualifiedItemId = consumption.QualifiedItemId,
                    ConsumedQuantity = consumption.Quantity
                })
            .OrderBy(row => row.MaterialReservationId, StringComparer.Ordinal)
            .ToArray();

    private static string IdentitySuffix(
        int targetTotalDay,
        string affectedLocationContextId) =>
        targetTotalDay + ":" + affectedLocationContextId.Trim();
}
