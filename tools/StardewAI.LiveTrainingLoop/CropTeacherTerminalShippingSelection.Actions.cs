using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherTerminalShippingSelection
{
    // completedDeposits must come from accepted owner outcomes, never model output.
    internal SmallModelActionEnvelope? BuildOriginalAction(SnapshotEnvelope source, int completedDeposits)
    {
        inputs.Validate();
        if (completedDeposits < 0 || completedDeposits > MinimumUnits || source.SaveId.Value != saveId ||
            source.PlayerId.Value != playerId || source.GameVersion != gameVersion ||
            source.StateHash != SnapshotHash.ComputeStateHash(source.State) || Number(Field(source, "time", "total_days")) != totalDay ||
            Field(source, "player", "location_id").GetString() != "Farm")
            throw new InvalidDataException("Selected crop terminal action changed its actor/day/source or accepted cursor.");
        if (nativeVariants is not null)
        {
            var target = CreateTarget(completedDeposits);
            target.VerifyNativeVariantSource(source, completedDeposits, MinimumGrossRevenue);
            return completedDeposits == MinimumUnits ? null : CropTeacherTerminalShipping.Preparation.BuildOriginalAction(source, target);
        }
        var rows = Field(source, "player", "inventory").EnumerateArray().Where(row => Integer(row, "slot_index") == SlotIndex).ToArray();
        if (rows is not [var slot]) throw new InvalidDataException("Selected crop terminal source slot is not unique.");
        var remaining = initialQuantity - completedDeposits;
        if (remaining == 0 ? !Boolean(slot, "is_empty") && String(slot, "qualified_item_id").Length != 0 :
            String(slot, "qualified_item_id") != QualifiedItemId || Integer(slot, "stack") != remaining ||
            Integer(slot, "quality") != Quality || Integer(slot, "sell_to_store_price") != UnitPrice || !Boolean(slot, "can_be_shipped"))
            throw new InvalidDataException("Selected crop terminal source quantity/variant differs from accepted deposits.");
        var bins = Field(source, "farm", "shipping_bins").EnumerateArray().Where(row => Integer(row, "tile_x") == BinX && Integer(row, "tile_y") == BinY).ToArray();
        if (bins is not [var bin] || String(bin, "bin_scope") != binScope || bin.GetProperty("player_id").ToString() != binPlayer ||
            !Boolean(bin, "pending_settlement_value_complete") || Long(bin, "pending_settlement_value") != checked((long)completedDeposits * UnitPrice))
            throw new InvalidDataException("Selected crop terminal source bin differs from accepted deposits.");
        if (completedDeposits == MinimumUnits) return null;
        return CropTeacherTerminalShipping.Preparation.BuildOriginalAction(source, CreateTarget(completedDeposits));
    }
}
