using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

// Shared read-only wave stock projection; it never commits a ledger or grants native execution.
public static class CropTeacherSaleStockInventory
{
    internal static MaterialReservation[] ReadActive(StrategyCommitmentLedger ledger,
        string commitmentId, string waveKey, string itemId) =>
        ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.NodeId == "player:" + ledger.PlayerId &&
            row.OwnerPlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == ledger.PlayerId &&
            row.QualifiedItemId == itemId && CropSaleStockReservationContract.TryRead(row, out var purpose) &&
            purpose!.CommitmentId == commitmentId && (string.IsNullOrWhiteSpace(waveKey) || purpose.OutputRetentionKey == waveKey)).ToArray();

    public static MaterialReservation[] ReadVerified(SnapshotEnvelope source, StrategyCommitmentLedger ledger,
        string commitmentId, string waveKey, string itemId)
    {
        Require(!string.IsNullOrWhiteSpace(waveKey) && ledger.SourceStateHash == source.StateHash,
            "Reserved terminal sale stock requires its original wave key and current material graph.");
        var claims = ReadActive(ledger, commitmentId, waveKey, itemId);
        ValidateInventory(source, ledger, claims);
        return claims;
    }

    internal static Func<SnapshotEnvelope, MoneyRevenueCropRetainedVariant[]> BindPreserved(SnapshotEnvelope source,
        StrategyCommitmentLedger ledger, string commitmentId, string waveKey, string itemId)
    {
        var claims = ReadVerified(source, ledger, commitmentId, waveKey, itemId);
        // The original native-source verifier binds this ledger; later frames are projections, not new ledger heads.
        return current =>
        {
            Require(current.GameVersion == source.GameVersion && current.GameTick > source.GameTick,
                "Preserved sale stock changed its original native version/clock.");
            ValidateInventory(current, ledger, claims);
            return claims.GroupBy(row =>
            {
                CropSaleStockReservationContract.TryRead(row, out var purpose);
                return purpose!.InventoryIdentitySha256;
            }, StringComparer.Ordinal).Select(group => new MoneyRevenueCropRetainedVariant(group.Key,
                checked(group.Sum(row => row.Quantity)))).ToArray();
        };
    }

    private static void ValidateInventory(SnapshotEnvelope source, StrategyCommitmentLedger ledger, MaterialReservation[] claims)
    {
        var readable = MaterialInventoryGraphReader.TryRead(source, out var graph);
        Require(readable &&
                source.SaveId.Status == FieldStatus.Available && source.PlayerId.Status == FieldStatus.Available &&
                ledger.SaveId == source.SaveId.Value && ledger.PlayerId == source.PlayerId.Value &&
                source.StateHash == SnapshotHash.ComputeStateHash(source.State) &&
                graph!.PlayerId.ToString(System.Globalization.CultureInfo.InvariantCulture) == ledger.PlayerId,
            "Reserved terminal sale stock requires its original wave key and current material graph.");
        var projection = new MaterialSupplyProjection().Project(graph!, ledger.MaterialReservations);
        Require(projection.Status == "available", "Reserved terminal sale stock has an unavailable material projection.");
        var inventory = source.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        foreach (var claim in claims)
        {
            var rows = inventory.Where(item => item.GetProperty("slot_index").GetInt32() == claim.SlotIndex).ToArray();
            Require(rows is [var row] && row.TryGetProperty("qualified_item_id", out var id) && id.GetString() == claim.QualifiedItemId &&
                CropSaleStockReservationContract.TryRead(claim, out var purpose) &&
                purpose!.InventoryIdentitySha256 == MoneyRevenueCropRetentionAccount.InventoryIdentity(row),
                "Reserved terminal sale stock changed its original slot variant.");
        }
    }

    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException(reason); }
}
