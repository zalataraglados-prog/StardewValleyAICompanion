using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Contracts.State;

public static class PrizeTicketRewardIdentity
{
    public static string ComputeRewardFingerprint(PrizeTicketRewardItemRef reward) => Hash(reward);

    public static string ComputeProjectionFingerprint(PrizeTicketRewardProjectionRef projection) => Hash(new
    {
        projection.Stage,
        projection.TargetLocationId,
        projection.InventoryTicketCount,
        projection.PendingSpecialOrderTicketCount,
        projection.TicketPrizesClaimed,
        projection.CurrentRewardFingerprint,
        projection.PreviewTrack,
        projection.PrizeMachineActionTiles,
        projection.SpecialOrderTicketActionTiles,
        projection.InventoryMaxItems,
        projection.InventoryOccupiedSlots,
        projection.PendingTicketCapacitySufficient,
        projection.GameId,
        projection.PlayerId,
        projection.HouseUpgradeLevel,
        projection.Season,
        projection.DayOfMonth
    });

    private static string Hash<T>(T value)
    {
        var canonical = JsonSerializer.Serialize(value);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }
}
