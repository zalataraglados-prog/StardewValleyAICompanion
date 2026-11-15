using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherHarvest
{
    private static MaterialReservationUpsertRequest[] BuildSaleStockClaims(Preparation preparation,
        SnapshotEnvelope before, SnapshotEnvelope after, int appliedTileCount, string stepId)
    {
        var account = new MoneyRevenueCropRetentionAccount(before, preparation.HarvestItemQualifiedId);
        account.Observe(before, after, ownedHarvest: true);
        var remaining = checked(appliedTileCount * preparation.MinimumUnitsPerTile);
        Require(account.Quantity >= remaining, "Harvest cannot reserve unproved sale stock.");
        var credit = account.Variants.ToDictionary(row => row.InventoryIdentitySha256, row => row.RetainedQuantity, StringComparer.Ordinal);
        var ledger = preparation.BaseLedger;
        var claims = new List<MaterialReservationUpsertRequest>();
        foreach (var row in after.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray()
            .Where(row => row.TryGetProperty("qualified_item_id", out var id) && id.ValueKind == JsonValueKind.String &&
                id.GetString() == preparation.HarvestItemQualifiedId).OrderBy(row => row.GetProperty("slot_index").GetInt32()))
        {
            if (remaining == 0) break;
            var key = MoneyRevenueCropRetentionAccount.InventoryIdentity(row);
            var quantity = Math.Min(remaining, Math.Min(row.GetProperty("stack").GetInt32(), credit.GetValueOrDefault(key)));
            if (quantity == 0) continue;
            var slot = row.GetProperty("slot_index").GetInt32();
            claims.Add(new()
            {
                StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision,
                ReservationId = CropSaleStockReservationContract.ReservationId(stepId, slot), SourceDecisionId = stepId,
                GoalId = "earn_money", NodeId = "player:" + ledger.PlayerId, SlotIndex = slot,
                QualifiedItemId = preparation.HarvestItemQualifiedId, Quantity = quantity,
                Purpose = preparation.TransferableSaleStock ?
                    CropSaleStockReservationContract.TransferablePurpose(preparation.CommitmentId, preparation.OutputRetentionKey, key, slot) :
                    CropSaleStockReservationContract.Purpose(preparation.CommitmentId, preparation.OutputRetentionKey, key)
            });
            remaining -= quantity; credit[key] -= quantity;
        }
        Require(remaining == 0, "Harvest sale stock lost its exact owned variant allocation.");
        return claims.ToArray();
    }
}
