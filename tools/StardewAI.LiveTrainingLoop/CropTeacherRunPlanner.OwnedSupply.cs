using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherRunPlanner
{
    private static CropTeacherOwnedSeedSupply ReadOwnedSupply(MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request, SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger, MoneyRevenueCropNativeProductionActionBatch[] pending)
    {
        var ids = pending.Select(row => row.CommitmentId).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Any(id => !plan.CoveredCommitmentIds.Contains(id, StringComparer.Ordinal)))
            throw new InvalidDataException("crop_teacher_owned_seed_commitment_outside_plan");
        var supply = CropTeacherOwnedSeedSupply.Read(snapshot, ledger, ids);
        foreach (var row in supply.Commitments)
        {
            var layouts = request.LayoutIntents.Where(value => value.CommitmentId == row.CommitmentId).ToArray();
            if (layouts.Length != 1 || layouts[0].Tiles.Length != row.TileCount ||
                pending.Any(batch => batch.CommitmentId == row.CommitmentId && batch.SeedQualifiedItemId != row.SeedQualifiedItemId))
                throw new InvalidDataException("crop_teacher_owned_seed_commitment_layout_or_seed_drift");
            var plantingDay = plan.Days.Where(day => day.ActionBatches.Any(batch =>
                batch.CommitmentId == row.CommitmentId && batch.OptionId == PlantOptionId)).Min(day => day.TotalDay);
            if (plantingDay != row.PlantingTotalDay)
                throw new InvalidDataException("crop_teacher_owned_seed_commitment_calendar_drift");
        }
        return supply;
    }

    private static CropTeacherRunStep OwnedSupplyStep(CropTeacherOwnedSeedSupply supply, string commitmentId,
        CropTeacherOwnedSeedSlot slot, string currentLocation, int totalDay, int remainingQuantity)
    {
        var transfer = currentLocation == slot.LocationId;
        return new CropTeacherRunStep(transfer ? CropTeacherRunStageKinds.TransferSeedSupply :
                CropTeacherRunStageKinds.RouteToSeedSupply, totalDay, commitmentId, slot.LocationId,
            string.Empty, slot.QualifiedItemId, string.Empty, 0, remainingQuantity, null,
            transfer ? "crop_teacher_owned_chest_seed_requires_transfer" : "crop_teacher_owned_seed_supply_requires_route",
            transfer ? supply.TransferIntent(slot) : null, slot.ReservationId);
    }
}
