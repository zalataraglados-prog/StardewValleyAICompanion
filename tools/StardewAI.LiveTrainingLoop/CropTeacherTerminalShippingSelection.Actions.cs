using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

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
        var approach = Number(Field(source, "player", "tile_x")) != StandX || Number(Field(source, "player", "tile_y")) != StandY;
        var root = $"ship:Farm:{BinX},{BinY}:{SlotIndex}:{QualifiedItemId}";
        var candidate = root + (approach ? ":approach" : ":deposit:" + (completedDeposits + 1).ToString(CultureInfo.InvariantCulture));
        var parameters = new List<SmallModelActionParameter>();
        void Add(string name, object value) => parameters.Add(new() { Name = name, Value = Convert.ToString(value, CultureInfo.InvariantCulture)! });
        if (approach) { Add("target_tile_x", StandX); Add("target_tile_y", StandY); }
        else
        {
            Add("target_location", "Farm"); Add("target_tile_x", BinX); Add("target_tile_y", BinY);
            Add("stand_tile_x", StandX); Add("stand_tile_y", StandY); Add("slot_index", SlotIndex);
            Add("qualified_item_id", QualifiedItemId); Add("quantity", 1); Add("expected_unit_price", UnitPrice);
        }
        Add("continuation.option_id", "executor.ship_inventory_item_to_bin"); Add("continuation.target_location", "Farm");
        Add("continuation.qualified_item_id", QualifiedItemId); Add("continuation.slot_index", SlotIndex);
        Add("continuation.quantity", 1); Add("continuation.expected_unit_price", UnitPrice); Add("precondition", "candidate_id:" + candidate);
        return new()
        {
            ModelOutputId = "model-output:" + candidate, SourceModel = "authoritative_crop_terminal_continuation.v1",
            StateHash = source.StateHash, GoalId = "daily.closed_loop", ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
            Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), Actions = new[] { new SmallModelAction
            {
                ActionId = candidate + ":action", OptionId = approach ? "executor.move_to_tile" : "executor.ship_inventory_item_to_bin",
                Rationale = "authoritative_crop_terminal_continuation", Parameters = parameters.ToArray()
            } }
        };
    }
}
