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
    private static CompiledActionStep[] CompileChooseAnimalPurchaseResponseStep(
        SmallModelAction action)
    {
        var expectedKey = ReadParameter(action, "expected_dialogue_key");
        var responseKey = ReadParameter(action, "dialogue_response_key");
        if (string.IsNullOrWhiteSpace(expectedKey) || string.IsNullOrWhiteSpace(responseKey))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "choose_animal_purchase_response",
                expectedKey + ":" + responseKey,
                "native_animal_purchase_dialogue_advanced_then_fresh_snapshot",
                20)
        };
    }

    private static CompiledActionStep[] CompilePurchaseAnimalStep(
        SmallModelAction action)
    {
        var animalType = AnimalPurchaseParameter(action, "animal_type_id");
        var targetLocation = AnimalPurchaseParameter(action, "target_location_id");
        var homeX = AnimalPurchaseIntParameter(action, "home_building_tile_x");
        var homeY = AnimalPurchaseIntParameter(action, "home_building_tile_y");
        if (string.IsNullOrWhiteSpace(animalType) || string.IsNullOrWhiteSpace(targetLocation) ||
            !homeX.HasValue || !homeY.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "purchase_animal",
                animalType + "@" + targetLocation + "(" + homeX.Value + "," + homeY.Value + ")",
                "animal_house_count_increments;player_money_decrements;exact_new_animal_receipt",
                300)
        };
    }

    private static string[] ValidateAnimalPurchasePlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId == "executor.choose_animal_purchase_response")
        {
            return ValidateAnimalPurchaseResponsePlan(action, snapshot);
        }

        if (action.OptionId != "executor.purchase_animal")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var animalType = AnimalPurchaseParameter(action, "animal_type_id");
        var possibleTypesJson = AnimalPurchaseParameter(action, "possible_actual_type_ids_json");
        var targetLocation = AnimalPurchaseParameter(action, "target_location_id");
        var buildingType = AnimalPurchaseParameter(action, "home_building_type");
        var homeX = AnimalPurchaseIntParameter(action, "home_building_tile_x");
        var homeY = AnimalPurchaseIntParameter(action, "home_building_tile_y");
        var animalName = AnimalPurchaseParameter(action, "generated_animal_name");
        var price = AnimalPurchaseIntParameter(action, "expected_price");
        var moneyBefore = AnimalPurchaseIntParameter(action, "expected_money_before");
        var moneyAfter = AnimalPurchaseIntParameter(action, "expected_money_after");
        var occupantsBefore = AnimalPurchaseIntParameter(action, "expected_home_occupant_count_before");
        var capacity = AnimalPurchaseIntParameter(action, "expected_home_capacity");
        var identity = AnimalPurchaseParameter(action, "candidate_identity_sha256");
        if (string.IsNullOrWhiteSpace(animalType) || string.IsNullOrWhiteSpace(possibleTypesJson) ||
            string.IsNullOrWhiteSpace(targetLocation) || string.IsNullOrWhiteSpace(buildingType) ||
            !homeX.HasValue || !homeY.HasValue || string.IsNullOrWhiteSpace(animalName) ||
            !price.HasValue || price.Value < 0 || !moneyBefore.HasValue || !moneyAfter.HasValue ||
            !occupantsBefore.HasValue || !capacity.HasValue || string.IsNullOrWhiteSpace(identity))
        {
            return new[] { "animal_purchase_typed_projection_required" };
        }

        if (moneyAfter.Value != moneyBefore.Value - price.Value)
        {
            reasons.Add("animal_purchase_money_projection_invalid");
        }
        if (occupantsBefore.Value < 0 || capacity.Value <= occupantsBefore.Value)
        {
            reasons.Add("animal_purchase_home_capacity_projection_invalid");
        }
        try
        {
            var possibleTypes = JsonSerializer.Deserialize<string[]>(possibleTypesJson) ?? Array.Empty<string>();
            if (possibleTypes.Length == 0 || possibleTypes.Any(string.IsNullOrWhiteSpace))
            {
                reasons.Add("animal_purchase_possible_type_projection_invalid");
            }
        }
        catch (JsonException)
        {
            reasons.Add("animal_purchase_possible_type_projection_invalid");
        }

        if (!string.Equals(ActiveMenuType(snapshot), "PurchaseAnimalsMenu", StringComparison.Ordinal))
        {
            reasons.Add("animal_purchase_menu_not_open");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        var menu = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!menu.HasValue || menu.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(menu.Value, "target_location_id"), targetLocation, StringComparison.Ordinal))
        {
            reasons.Add("animal_purchase_menu_target_location_drifted");
        }

        var catalog = ReadStateFieldValue(snapshot, "farm", "animal_purchase_catalog");
        var exactProjection = catalog.HasValue && catalog.Value.ValueKind == JsonValueKind.Array &&
            catalog.Value.EnumerateArray().Any(location =>
                string.Equals(ReadString(location, "target_location_id"), targetLocation, StringComparison.Ordinal) &&
                location.TryGetProperty("stock", out var stock) && stock.ValueKind == JsonValueKind.Array &&
                stock.EnumerateArray().Any(animal =>
                    string.Equals(ReadString(animal, "animal_type_id"), animalType, StringComparison.Ordinal) &&
                    string.Equals(ReadString(animal, "candidate_identity_sha256"), identity, StringComparison.Ordinal) &&
                    ReadInt(animal, "price") == price.Value &&
                    ReadInt(animal, "player_money") == moneyBefore.Value &&
                    string.Equals(ReadString(animal, "generated_unique_name"), animalName, StringComparison.Ordinal) &&
                    animal.TryGetProperty("compatible_homes", out var homes) && homes.ValueKind == JsonValueKind.Array &&
                    homes.EnumerateArray().Any(home =>
                        string.Equals(ReadString(home, "building_type"), buildingType, StringComparison.Ordinal) &&
                        ReadInt(home, "building_tile_x") == homeX.Value &&
                        ReadInt(home, "building_tile_y") == homeY.Value &&
                        ReadInt(home, "occupant_count") == occupantsBefore.Value &&
                        ReadInt(home, "capacity") == capacity.Value &&
                        ReadInt(home, "available_slots") > 0 &&
                        ReadBool(home, "compatible_with_all_possible_types") == true &&
                        ReadBool(home, "is_under_construction") != true)));
        if (!exactProjection)
        {
            reasons.Add("animal_purchase_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateAnimalPurchaseResponsePlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var expectedKey = ReadParameter(action, "expected_dialogue_key");
        var responseKey = ReadParameter(action, "dialogue_response_key");
        var targetLocation = AnimalPurchaseParameter(action, "target_location_id");
        var expectedMenu = ReadParameter(action, "expected_menu_type_after");
        var allowed = string.Equals(expectedKey, "Marnie", StringComparison.Ordinal) &&
                string.Equals(responseKey, "Purchase", StringComparison.Ordinal) &&
                string.Equals(expectedMenu, "PurchaseAnimalsMenu|DialogueBox", StringComparison.Ordinal) ||
            string.Equals(expectedKey, "pagedResponse", StringComparison.Ordinal) &&
                string.Equals(responseKey, targetLocation, StringComparison.Ordinal) &&
                string.Equals(expectedMenu, "PurchaseAnimalsMenu", StringComparison.Ordinal) ||
            string.Equals(expectedKey, "pagedResponse", StringComparison.Ordinal) &&
                (string.Equals(responseKey, "nextPage", StringComparison.Ordinal) ||
                 string.Equals(responseKey, "previousPage", StringComparison.Ordinal)) &&
                string.Equals(expectedMenu, "DialogueBox", StringComparison.Ordinal);
        if (!allowed)
        {
            return new[] { "animal_purchase_dialogue_response_not_whitelisted" };
        }
        if (!ActionSeesDialogueBoxOpen(action, snapshot))
        {
            return new[] { "animal_purchase_dialogue_box_not_open" };
        }

        var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        var actualKey = activeMenu.HasValue ? ReadString(activeMenu.Value, "last_question_key") : string.Empty;
        var menu = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        var responsePresent = menu.HasValue && menu.Value.ValueKind == JsonValueKind.Object &&
            menu.Value.TryGetProperty("responses", out var responses) && responses.ValueKind == JsonValueKind.Array &&
            responses.EnumerateArray().Any(response =>
                string.Equals(ReadString(response, "response_key"), responseKey, StringComparison.Ordinal));
        var reasons = new List<string>();
        if (!string.Equals(actualKey, expectedKey, StringComparison.Ordinal)) reasons.Add("animal_purchase_dialogue_key_drifted");
        if (!responsePresent) reasons.Add("animal_purchase_dialogue_response_unavailable");
        return reasons.ToArray();
    }

    private static string AnimalPurchaseParameter(SmallModelAction action, string name) =>
        ReadParameter(action, name) ?? ReadParameter(action, "continuation." + name) ?? string.Empty;

    private static int? AnimalPurchaseIntParameter(SmallModelAction action, string name)
    {
        var value = AnimalPurchaseParameter(action, name);
        return int.TryParse(value, out var parsed) ? parsed : null;
    }
}
