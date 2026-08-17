using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileAnimalManagementStep(SmallModelAction action)
    {
        var intent = ReadParameter(action, "management_intent");
        var animalId = ReadParameter(action, "animal_id");
        return string.IsNullOrWhiteSpace(intent) || string.IsNullOrWhiteSpace(animalId)
            ? Array.Empty<CompiledActionStep>()
            : new[]
            {
                Step(
                    "manage_animal",
                    animalId + ":" + intent,
                    "native_AnimalQueryMenu_receipt_verified",
                    intent == "move_home" ? 480 : 240)
            };
    }

    private static string[] ValidateAnimalManagementPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.manage_animal")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var animalId = ReadParameter(action, "animal_id");
        var intent = ReadParameter(action, "management_intent");
        var managementReason = ReadParameter(action, "management_reason");
        var locationId = ReadParameter(action, "location_id");
        var x = AnimalManagementIntParameter(action, "target_tile_x");
        var y = AnimalManagementIntParameter(action, "target_tile_y");
        var standX = AnimalManagementIntParameter(action, "stand_tile_x");
        var standY = AnimalManagementIntParameter(action, "stand_tile_y");
        var safeSlot = AnimalManagementIntParameter(action, "safe_slot_index");
        if (string.IsNullOrWhiteSpace(animalId) ||
            intent is not ("rename" or "toggle_reproduction" or "move_home" or "sell") ||
            string.IsNullOrWhiteSpace(managementReason) || string.IsNullOrWhiteSpace(locationId) ||
            !x.HasValue || !y.HasValue || !standX.HasValue || !standY.HasValue || !safeSlot.HasValue ||
            ReadParameter(action, "target_runtime_type") != "StardewValley.FarmAnimal")
        {
            return new[] { "animal_management_typed_projection_required" };
        }
        if (!string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), locationId, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("animal_management_location_drifted");
        }
        if (ActiveMenuOpen(snapshot))
        {
            reasons.Add("animal_management_menu_must_be_clear");
        }

        var animals = ReadStateFieldValue(snapshot, "farm", "animals");
        var animal = animals.HasValue && animals.Value.ValueKind == JsonValueKind.Array
            ? animals.Value.EnumerateArray().FirstOrDefault(row =>
                AnimalManagementReadId(row) == animalId)
            : default;
        if (animal.ValueKind != JsonValueKind.Object ||
            ReadString(animal, "location_id") != locationId ||
            ReadInt(animal, "tile_x") != x.Value || ReadInt(animal, "tile_y") != y.Value ||
            ReadString(animal, "management_query_status") != "ready" ||
            ReadInt(animal, "management_safe_slot_index") != safeSlot.Value)
        {
            reasons.Add("animal_management_projection_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        if (intent == "rename")
        {
            var targetName = ReadParameter(action, "target_name");
            if (string.IsNullOrWhiteSpace(targetName) || targetName == ReadString(animal, "display_name") ||
                animals!.Value.EnumerateArray().Any(row =>
                    AnimalManagementReadId(row) != animalId && ReadString(row, "display_name") == targetName))
            {
                reasons.Add("animal_management_rename_target_invalid");
            }
        }
        else if (intent == "toggle_reproduction")
        {
            var target = ReadParameter(action, "target_allow_reproduction");
            if (ReadBool(animal, "management_can_toggle_reproduction") != true ||
                !bool.TryParse(target, out var parsed) || parsed == ReadBool(animal, "management_allow_reproduction"))
            {
                reasons.Add("animal_management_reproduction_target_invalid");
            }
        }
        else if (intent == "sell")
        {
            var price = AnimalManagementIntParameter(action, "expected_sell_price");
            var money = AnimalManagementIntParameter(action, "expected_money_before");
            if (ReadParameter(action, "confirm_irreversible_sale") != "true" ||
                price != ReadInt(animal, "management_sell_price") ||
                money != ReadStateFieldInt(snapshot, "player", "money"))
            {
                reasons.Add("animal_management_sale_projection_or_confirmation_invalid");
            }
        }
        else
        {
            var type = ReadParameter(action, "target_home_building_type");
            var homeX = AnimalManagementIntParameter(action, "target_home_building_tile_x");
            var homeY = AnimalManagementIntParameter(action, "target_home_building_tile_y");
            var homesMatch = animal.TryGetProperty("management_compatible_move_homes", out var homes) &&
                homes.ValueKind == JsonValueKind.Array && homeX.HasValue && homeY.HasValue &&
                homes.EnumerateArray().Any(home =>
                    ReadString(home, "building_type") == type &&
                    ReadInt(home, "building_tile_x") == homeX.Value &&
                    ReadInt(home, "building_tile_y") == homeY.Value &&
                    ReadInt(home, "available_slots") > 0 && ReadBool(home, "is_under_construction") != true);
            if (!homesMatch)
            {
                reasons.Add("animal_management_target_home_invalid");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string AnimalManagementReadId(JsonElement row)
    {
        if (!row.TryGetProperty("animal_id", out var value))
        {
            return string.Empty;
        }
        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : value.GetRawText();
    }

    private static int? AnimalManagementIntParameter(SmallModelAction action, string name)
    {
        var value = ReadParameter(action, name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
