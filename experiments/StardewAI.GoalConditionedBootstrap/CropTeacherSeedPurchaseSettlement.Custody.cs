using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSeedPurchaseSettlement
{
    private ReservationPortfolioCommitRequest AcquiredSeedRequest(StrategyCommitmentLedger settled, CropTeacherOwnedSeedSupply supply)
    {
        Require(proof.Stage == "purchase_slice" && proof.InventoryQuantityIncrease > 0,
            "Crop seed support has no verified acquired-seed delta.");
        var owned = supply.Commitments.Single();
        var beforePlayer = beforeSupply.CopyPlayerInventory();
        var afterPlayer = supply.CopyPlayerInventory();
        Require(beforePlayer.NodeId == afterPlayer.NodeId, "Crop seed support player inventory node changed.");
        var oldSlots = beforePlayer.Slots.Where(row => row.QualifiedItemId == owned.SeedQualifiedItemId).ToArray();
        var newSlots = afterPlayer.Slots.Where(row => row.QualifiedItemId == owned.SeedQualifiedItemId).ToArray();
        Require(oldSlots.All(old => newSlots.Count(row => row.SlotIndex == old.SlotIndex && row.Quality == old.Quality &&
                row.Stack >= old.Stack) == 1),
            "Crop seed acquisition moved or removed an existing seed slot.");
        var deltas = newSlots.Select(slot => (Slot: slot, Delta: slot.Stack -
                (oldSlots.SingleOrDefault(old => old.SlotIndex == slot.SlotIndex && old.Quality == slot.Quality)?.Stack ?? 0)))
            .Where(row => row.Delta > 0).OrderBy(row => row.Slot.SlotIndex).ToArray();
        Require(deltas.Sum(row => (long)row.Delta) == proof.InventoryQuantityIncrease &&
                deltas.All(row => row.Slot.Quality == proof.OutputQuality),
            "Crop seed acquisition slot deltas differ from the verified native output.");
        var remaining = checked(owned.TileCount - owned.PlayerQuantity - owned.ExternalQuantity);
        Require(remaining > 0, "Crop seed support has no remaining owned seed shortfall.");
        var claims = new List<MaterialReservationUpsertRequest>();
        var crop = settled.CropPlantingCommitments.Single(row => row.CommitmentId == commitmentId);
        foreach (var (slot, delta) in deltas)
        {
            if (remaining == 0) break;
            var quantity = Math.Min(remaining, delta);
            var existing = owned.Slots.Where(row => row.NodeId == afterPlayer.NodeId && row.SlotIndex == slot.SlotIndex)
                .OrderBy(row => row.ReservationId, StringComparer.Ordinal).FirstOrDefault();
            var id = existing?.ReservationId ?? supportStepId + ":acquired-seed:" + slot.SlotIndex;
            Require(existing is not null || !settled.MaterialReservations.Any(row => row.ReservationId == id) &&
                    !settled.CurrencyReservations.Any(row => row.ReservationId == id),
                "Crop acquired-seed reservation ID already exists.");
            claims.Add(new MaterialReservationUpsertRequest
            {
                StateHash = after.StateHash, ExpectedLedgerRevision = settled.Revision, ReservationId = id,
                SourceDecisionId = crop.SourceDecisionId, GoalId = "earn_money", NodeId = afterPlayer.NodeId,
                SlotIndex = slot.SlotIndex, QualifiedItemId = owned.SeedQualifiedItemId,
                Quantity = checked((existing?.Quantity ?? 0) + quantity), Purpose = "crop_seed_supply:" + commitmentId
            });
            remaining -= quantity;
        }
        var cashId = commitmentId + ":seed-purchase:money";
        return new ReservationPortfolioCommitRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = settled.Revision,
            PortfolioId = supportStepId + ":acquired-seed", SourceDecisionId = crop.SourceDecisionId, GoalId = "earn_money",
            MaterialClaims = claims.ToArray(),
            ReleaseReservationIds = remaining == 0 && settled.CurrencyReservations.Any(row =>
                row.ReservationId == cashId && row.Status == StrategyCommitmentStatuses.Active) ? new[] { cashId } : Array.Empty<string>()
        };
    }
}
