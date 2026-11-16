using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherTerminalShippingSelection
{
    private static CropTeacherObservedShippingPrefix? ReadObservedPrefix(JsonObject manifest, string[] proofs, string head)
    {
        if (manifest["observed_shipping_prefixes"] is null) return null;
        var all = manifest["observed_shipping_prefixes"]!.Deserialize<CropTeacherObservedShippingPrefix[]>(CropTeacherManagedSeedOwner.JsonOptions);
        var matches = all?.Where(prefix => prefix.SchemaVersion == "crop_teacher_observed_shipping_prefix.v1" &&
            proofs.Any(path => SamePath(path, prefix.OutputRetentionProofPath)) && prefix.Transitions is { Length: > 0 } &&
            SamePath(prefix.Transitions[0].BeforeSnapshotPath, prefix.RetentionSnapshotPath) &&
            SamePath(prefix.Transitions[^1].AfterSnapshotPath, head)).ToArray();
        return matches is [var selected] ? selected :
            throw new InvalidDataException("Selected terminal lacks one explicit original shipping prefix at its accepted head.");
    }

    private CropTeacherTerminalVariantPlan ReadObservedOrder(CropTeacherObservedShippingPrefix prefix,
        CropTeacherTerminalShippingTarget grouped)
    {
        var variants = grouped.ReadNativeVariants().ToArray();
        var remaining = variants.ToDictionary(row => row.SlotIndex, row => row.PlannedQuantity);
        var order = new List<int>();
        foreach (var transition in prefix.Transitions)
        {
            var queue = inputs.Read<ActionQueueEnvelope>(transition.QueuePath);
            foreach (var item in queue.Items.Where(item => item.OptionId == "executor.ship_inventory_item_to_bin"))
            {
                string Parameter(string name)
                {
                    var values = item.NormalizedCommand.Parameters.Where(row => row.Name == name).ToArray();
                    return values is [var value] ? value.Value : throw new InvalidDataException("Observed shipping order has an ambiguous parameter.");
                }
                if (Parameter("qualified_item_id") != QualifiedItemId) continue;
                if (!int.TryParse(Parameter("quantity"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity) || quantity != 1 ||
                    !int.TryParse(Parameter("slot_index"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var slot) || !remaining.TryGetValue(slot, out var units) || units <= 0)
                    throw new InvalidDataException("Observed shipping order changed or exceeded its original retained units.");
                remaining[slot] = units - 1; order.Add(slot);
            }
        }
        foreach (var variant in variants)
            for (var index = 0; index < remaining[variant.SlotIndex]; index++) order.Add(variant.SlotIndex);
        CropTeacherTerminalDepositOrder? linked = null;
        for (var index = order.Count - 1; index >= 0; index--) linked = new(order[index], linked);
        // These are pinned original queue intents, not credited outcomes. The native prefix verifier
        // immediately binds this order to the original wire/results and actual resource effects.
        return grouped.NativeVariants! with { SchemaVersion = "crop_teacher_native_terminal_variants.v2", DepositOrder = linked };
    }
}
