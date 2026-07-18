using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static string[] ValidateCollectAnimalProductPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.collect_animal_product")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var animalId = ReadParameter(action, "target_runtime_identity");
        var tool = ReadParameter(action, "required_tool_kind");
        var toolSlot = ReadIntParameter(action, "tool_slot_index");
        var outputId = ReadParameter(action, "qualified_item_id");
        var quantity = ReadIntParameter(action, "quantity");
        var outputQuality = ReadIntParameter(action, "expected_output_quality");
        var outputItemsJson = ReadParameter(action, "expected_output_items_json");
        var statIncrementsJson = ReadParameter(action, "expected_stat_increments_json");
        var expectedXp = ReadIntParameter(action, "expected_skill_experience_delta");
        var expectedEnergy = ReadIntParameter(action, "expected_energy_delta");
        var friendshipBefore = ReadIntParameter(action, "expected_friendship_before");
        var friendshipAfter = ReadIntParameter(action, "expected_friendship_after");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            string.IsNullOrWhiteSpace(animalId) || tool is not ("Milk Pail" or "Shears") || !toolSlot.HasValue || toolSlot.Value < 0 ||
            string.IsNullOrWhiteSpace(outputId) || !quantity.HasValue || quantity.Value is < 1 or > 2 ||
            !outputQuality.HasValue || string.IsNullOrWhiteSpace(outputItemsJson) || string.IsNullOrWhiteSpace(statIncrementsJson) ||
            ReadParameter(action, "expected_skill_id") != "farming" || expectedXp != 5 || expectedEnergy != -4 ||
            !friendshipBefore.HasValue || !friendshipAfter.HasValue)
        {
            return new[] { "collect_animal_product_typed_projection_required" };
        }
        if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("collect_animal_product_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("collect_animal_product_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("collect_animal_product_target_location_mismatch");
        }

        var animals = ReadStateFieldValue(snapshot, "farm", "animals");
        var target = animals.HasValue && animals.Value.ValueKind == JsonValueKind.Array
            ? animals.Value.EnumerateArray().FirstOrDefault(animal => AnimalIdText(animal) == animalId)
            : default;
        if (target.ValueKind != JsonValueKind.Object ||
            ReadString(target, "harvest_status") != "ready" ||
            ReadInt(target, "tile_x") != targetX.Value || ReadInt(target, "tile_y") != targetY.Value ||
            !string.Equals(ReadString(target, "location_id"), targetLocation, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(ReadString(target, "runtime_type"), ReadParameter(action, "target_runtime_type"), StringComparison.Ordinal))
        {
            reasons.Add("collect_animal_product_target_not_ready_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (ReadString(target, "harvest_tool") != tool || ReadInt(target, "harvest_tool_slot_index") != toolSlot.Value)
        {
            reasons.Add("collect_animal_product_tool_projection_drifted");
        }
        if (!string.Equals(ReadString(target, "harvest_output_qualified_item_id"), outputId, StringComparison.OrdinalIgnoreCase) ||
            ReadInt(target, "harvest_output_quantity") != quantity.Value ||
            ReadInt(target, "harvest_output_quality") != outputQuality.Value ||
            ReadString(target, "harvest_expected_output_items_json") != outputItemsJson ||
            ReadString(target, "harvest_stat_increments_json") != statIncrementsJson ||
            ReadIntParameter(action, "expected_animal_cracker_multiplier") != (ReadBool(target, "has_eaten_animal_cracker") == true ? 2 : 1))
        {
            reasons.Add("collect_animal_product_output_projection_drifted");
        }
        if (ReadString(target, "harvest_projection_status") != "exact" ||
            ReadInt(target, "harvest_farming_experience_delta") != expectedXp.Value ||
            -ReadInt(target, "harvest_energy_cost") != expectedEnergy.Value ||
            ReadInt(target, "friendship_toward_farmer") != friendshipBefore.Value ||
            ReadInt(target, "friendship_after_harvest") != friendshipAfter.Value)
        {
            reasons.Add("collect_animal_product_side_effect_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string AnimalIdText(JsonElement animal)
    {
        if (!animal.TryGetProperty("animal_id", out var id))
        {
            return string.Empty;
        }
        return id.ValueKind == JsonValueKind.String ? id.GetString() ?? string.Empty : id.GetRawText();
    }
}
