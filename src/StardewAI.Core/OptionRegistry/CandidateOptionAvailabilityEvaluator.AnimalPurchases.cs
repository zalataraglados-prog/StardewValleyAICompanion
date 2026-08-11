using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] AnimalPurchaseStageCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] boundParameters)
    {
        var targets = ReadAnimalPurchaseTargets(snapshot)
            .Where(target => AnimalPurchaseIdentityMatches(target, boundParameters))
            .ToArray();
        if (targets.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        var menuType = activeMenu.HasValue ? ReadString(activeMenu.Value, "type") : string.Empty;
        if (string.Equals(menuType, "PurchaseAnimalsMenu", StringComparison.Ordinal))
        {
            return AnimalPurchaseTerminalCandidates(snapshot, targets);
        }

        if (string.Equals(menuType, "DialogueBox", StringComparison.Ordinal))
        {
            var questionKey = ReadString(activeMenu!.Value, "last_question_key");
            if (string.Equals(questionKey, "Marnie", StringComparison.Ordinal))
            {
                return AnimalPurchaseDialogueCandidates(snapshot, targets, "Marnie", "Purchase", "animal_purchase_select_service");
            }

            if (string.Equals(questionKey, "pagedResponse", StringComparison.Ordinal))
            {
                return targets
                    .Select(target => AnimalPurchaseLocationDialogueCandidate(snapshot, target))
                    .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
            }

            return Array.Empty<EventCandidate>();
        }

        if (!string.IsNullOrWhiteSpace(menuType) && !string.Equals(menuType, "none", StringComparison.Ordinal))
        {
            return Array.Empty<EventCandidate>();
        }

        var previews = targets.Select(AnimalPurchasePreview).ToArray();
        return ShopObjectiveStageCandidates(
            snapshot,
            previews,
            "animal_purchase",
            (preview, shopLocation) => preview.Parameters
                .Concat(new[] { Parameter("continuation.shop_location_id", shopLocation) })
                .ToArray());
    }

    private static EconomicCandidate AnimalPurchasePreview(AnimalPurchaseTarget target)
    {
        var reasons = AnimalPurchaseBlockReasons(target);
        return new EconomicCandidate
        {
            CandidateId = "animal-purchase-preview:" + AnimalPurchaseTargetKey(target),
            Kind = "purchase_animal",
            Available = reasons.Length == 0,
            ItemId = target.AnimalTypeId,
            QualifiedItemId = "animal:" + target.AnimalTypeId + ":" + target.TargetLocationId + ":" +
                target.BuildingTileX.ToString(CultureInfo.InvariantCulture) + "," +
                target.BuildingTileY.ToString(CultureInfo.InvariantCulture),
            DisplayName = target.DisplayName,
            ShopId = "AnimalShop",
            Quantity = 1,
            UnitPrice = target.Price,
            TotalValue = target.Price,
            CurrencyBalance = target.PlayerMoney,
            Stock = target.AvailableSlots,
            InfiniteStock = false,
            BlockReasons = reasons,
            Parameters = AnimalPurchaseContinuation(target)
        };
    }

    private static EventCandidate[] AnimalPurchaseDialogueCandidates(
        SnapshotEnvelope snapshot,
        IEnumerable<AnimalPurchaseTarget> targets,
        string expectedQuestionKey,
        string responseKey,
        string kind)
    {
        var responseAvailable = DialogueResponseAvailable(snapshot, responseKey);
        return targets.Select(target =>
        {
            var reasons = AnimalPurchaseBlockReasons(target)
                .Concat(responseAvailable ? Array.Empty<string>() : new[] { "animal_purchase_dialogue_response_unavailable" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new EventCandidate
            {
                CandidateId = kind + ":" + AnimalPurchaseTargetKey(target),
                Kind = kind,
                Available = reasons.Length == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "dialogue_key=" + expectedQuestionKey + ";response_key=" + responseKey + ";fresh_snapshot_after_native_response=true",
                ItemId = target.AnimalTypeId,
                QualifiedItemId = "animal:" + target.AnimalTypeId,
                DisplayName = target.DisplayName,
                ShopId = "AnimalShop",
                UnitPrice = target.Price,
                TotalValue = target.Price,
                EstimatedTicks = 20,
                AvailabilityClass = "native_animal_purchase_dialogue",
                AllowedNow = reasons.Length == 0,
                AllowedToday = true,
                BlockReasons = reasons,
                Parameters = AnimalPurchaseContinuation(target)
                    .Concat(new[]
                    {
                        Parameter("expected_dialogue_key", expectedQuestionKey),
                        Parameter("dialogue_response_key", responseKey),
                        Parameter("expected_menu_type_after", "PurchaseAnimalsMenu|DialogueBox")
                    })
                    .ToArray()
            };
        }).ToArray();
    }

    private static EventCandidate AnimalPurchaseLocationDialogueCandidate(
        SnapshotEnvelope snapshot,
        AnimalPurchaseTarget target)
    {
        var responseKey = target.TargetLocationId;
        var kind = "animal_purchase_select_location";
        var expectedMenuType = "PurchaseAnimalsMenu";
        if (!DialogueResponseAvailable(snapshot, responseKey))
        {
            responseKey = AnimalPurchasePageResponse(snapshot, target);
            kind = "animal_purchase_navigate_location_page";
            expectedMenuType = "DialogueBox";
        }
        var responseAvailable = !string.IsNullOrWhiteSpace(responseKey) &&
            DialogueResponseAvailable(snapshot, responseKey);
        var reasons = AnimalPurchaseBlockReasons(target)
            .Concat(responseAvailable ? Array.Empty<string>() : new[] { "animal_purchase_target_location_response_unavailable" })
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new EventCandidate
        {
            CandidateId = kind + ":" + AnimalPurchaseTargetKey(target),
            Kind = kind,
            Available = reasons.Length == 0,
            LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
            ExpectedEffect = "dialogue_key=pagedResponse;response_key=" + responseKey +
                ";menus.active_menu.type=" + expectedMenuType + ";fresh_snapshot_after_native_response=true",
            ItemId = target.AnimalTypeId,
            QualifiedItemId = "animal:" + target.AnimalTypeId,
            DisplayName = target.DisplayName,
            ShopId = "AnimalShop",
            UnitPrice = target.Price,
            TotalValue = target.Price,
            EstimatedTicks = 20,
            AvailabilityClass = "native_animal_purchase_location_dialogue",
            AllowedNow = reasons.Length == 0,
            AllowedToday = true,
            BlockReasons = reasons,
            Parameters = AnimalPurchaseContinuation(target)
                .Concat(new[]
                {
                    Parameter("expected_dialogue_key", "pagedResponse"),
                    Parameter("dialogue_response_key", responseKey),
                    Parameter("expected_menu_type_after", expectedMenuType)
                })
                .ToArray()
        };
    }

    private static string AnimalPurchasePageResponse(SnapshotEnvelope snapshot, AnimalPurchaseTarget target)
    {
        var menu = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!menu.HasValue || !menu.Value.TryGetProperty("responses", out var responses) ||
            responses.ValueKind != JsonValueKind.Array)
            return string.Empty;
        var responseKeys = responses.EnumerateArray()
            .Select(response => ReadString(response, "response_key"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.Ordinal);
        var locationIndices = ReadAnimalPurchaseTargets(snapshot)
            .GroupBy(value => value.TargetLocationId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().NativeLocationChoiceIndex, StringComparer.Ordinal);
        var visibleIndices = responseKeys
            .Where(locationIndices.ContainsKey)
            .Select(key => locationIndices[key])
            .ToArray();
        if (visibleIndices.Length == 0) return string.Empty;
        if (target.NativeLocationChoiceIndex > visibleIndices.Max() && responseKeys.Contains("nextPage"))
            return "nextPage";
        if (target.NativeLocationChoiceIndex < visibleIndices.Min() && responseKeys.Contains("previousPage"))
            return "previousPage";
        return string.Empty;
    }

    private static EventCandidate[] AnimalPurchaseTerminalCandidates(
        SnapshotEnvelope snapshot,
        IEnumerable<AnimalPurchaseTarget> targets)
    {
        var menu = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        var targetLocation = menu.HasValue ? ReadString(menu.Value, "target_location_id") : string.Empty;
        return targets
            .Where(target => string.Equals(target.TargetLocationId, targetLocation, StringComparison.Ordinal))
            .Select(target =>
            {
                var exactStockPresent = menu.HasValue &&
                    menu.Value.TryGetProperty("stock", out var stock) &&
                    stock.ValueKind == JsonValueKind.Array &&
                    stock.EnumerateArray().Any(item =>
                        string.Equals(ReadString(item, "animal_type_id"), target.AnimalTypeId, StringComparison.Ordinal) &&
                        ReadInt(item, "price") == target.Price &&
                        ReadBool(item, "required_building_met") == true);
                var reasons = AnimalPurchaseBlockReasons(target)
                    .Concat(exactStockPresent ? Array.Empty<string>() : new[] { "animal_purchase_menu_stock_drifted" })
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new EventCandidate
                {
                    CandidateId = "purchase_animal:" + AnimalPurchaseTargetKey(target),
                    Kind = "purchase_animal",
                    Available = reasons.Length == 0,
                    LocationId = target.TargetLocationId,
                    TileX = target.BuildingTileX,
                    TileY = target.BuildingTileY,
                    ExpectedEffect = "animal_house_occupant_count=" + target.OccupantCount + "->" + (target.OccupantCount + 1) +
                        ";player.money=" + target.PlayerMoney + "->" + (target.PlayerMoney - target.Price) +
                        ";animal_name=" + target.GeneratedName,
                    ItemId = target.AnimalTypeId,
                    QualifiedItemId = "animal:" + target.AnimalTypeId,
                    DisplayName = target.DisplayName,
                    ShopId = "AnimalShop",
                    UnitPrice = target.Price,
                    TotalValue = target.Price,
                    EstimatedTicks = 180,
                    AvailabilityClass = "native_purchase_animals_menu_transaction",
                    AllowedNow = reasons.Length == 0,
                    AllowedToday = true,
                    BlockReasons = reasons,
                    Parameters = AnimalPurchaseContinuation(target)
                };
            })
            .ToArray();
    }

    private static bool DialogueResponseAvailable(SnapshotEnvelope snapshot, string responseKey)
    {
        var menu = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        return menu.HasValue &&
            menu.Value.TryGetProperty("responses", out var responses) &&
            responses.ValueKind == JsonValueKind.Array &&
            responses.EnumerateArray().Any(response =>
                string.Equals(ReadString(response, "response_key"), responseKey, StringComparison.Ordinal));
    }

    private static AnimalPurchaseTarget[] ReadAnimalPurchaseTargets(SnapshotEnvelope snapshot)
    {
        var catalog = ReadStateFieldValue(snapshot, "farm", "animal_purchase_catalog");
        if (!catalog.HasValue || catalog.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<AnimalPurchaseTarget>();
        }

        return catalog.Value.EnumerateArray()
            .Where(location => location.ValueKind == JsonValueKind.Object)
            .SelectMany(location =>
            {
                var locationId = ReadString(location, "target_location_id");
                var nativeLocationChoiceIndex = ReadInt(location, "native_location_choice_index");
                if (!location.TryGetProperty("stock", out var stock) || stock.ValueKind != JsonValueKind.Array)
                {
                    return Enumerable.Empty<AnimalPurchaseTarget>();
                }

                return stock.EnumerateArray().SelectMany(animal =>
                {
                    if (!animal.TryGetProperty("compatible_homes", out var homes) || homes.ValueKind != JsonValueKind.Array)
                    {
                        return Enumerable.Empty<AnimalPurchaseTarget>();
                    }

                    var possibleTypes = ReadStringArray(animal, "possible_actual_type_ids");
                    return homes.EnumerateArray().Select(home => new AnimalPurchaseTarget(
                        ReadString(animal, "candidate_identity_sha256"),
                        ReadString(animal, "animal_type_id"),
                        ReadString(animal, "display_name"),
                        possibleTypes,
                        ReadInt(animal, "price"),
                        ReadInt(animal, "player_money"),
                        ReadBool(animal, "required_building_met") == true,
                        ReadBool(animal, "can_afford") == true,
                        ReadString(animal, "generated_unique_name"),
                        locationId,
                        nativeLocationChoiceIndex,
                        ReadString(home, "building_type"),
                        ReadInt(home, "building_tile_x"),
                        ReadInt(home, "building_tile_y"),
                        ReadString(home, "indoor_location_id"),
                        ReadBool(home, "compatible_with_all_possible_types") == true,
                        ReadBool(home, "is_under_construction") == true,
                        ReadInt(home, "occupant_count"),
                        ReadInt(home, "capacity"),
                        ReadInt(home, "available_slots")));
                });
            })
            .ToArray();
    }

    private static string[] AnimalPurchaseBlockReasons(AnimalPurchaseTarget target)
    {
        var reasons = new List<string>();
        if (!target.RequiredBuildingMet) reasons.Add("animal_purchase_required_building_missing");
        if (!target.CanAfford) reasons.Add("animal_purchase_insufficient_money");
        if (!target.CompatibleWithAllPossibleTypes) reasons.Add("animal_purchase_home_incompatible");
        if (target.IsUnderConstruction) reasons.Add("animal_purchase_home_under_construction");
        if (target.AvailableSlots <= 0) reasons.Add("animal_purchase_home_full");
        if (target.PossibleActualTypeIds.Length == 0) reasons.Add("animal_purchase_possible_type_projection_empty");
        if (string.IsNullOrWhiteSpace(target.GeneratedName)) reasons.Add("animal_purchase_unique_name_missing");
        return reasons.ToArray();
    }

    private static bool AnimalPurchaseIdentityMatches(
        AnimalPurchaseTarget target,
        SmallModelActionParameter[] parameters)
    {
        var animalType = ReadParameter(parameters, "continuation.animal_type_id");
        if (string.IsNullOrWhiteSpace(animalType)) animalType = ReadParameter(parameters, "animal_type_id");
        var location = ReadParameter(parameters, "continuation.target_location_id");
        if (string.IsNullOrWhiteSpace(location)) location = ReadParameter(parameters, "target_location_id");
        var buildingX = ReadParameterInt(parameters, "continuation.home_building_tile_x") ?? ReadParameterInt(parameters, "home_building_tile_x");
        var buildingY = ReadParameterInt(parameters, "continuation.home_building_tile_y") ?? ReadParameterInt(parameters, "home_building_tile_y");
        return (string.IsNullOrWhiteSpace(animalType) || string.Equals(target.AnimalTypeId, animalType, StringComparison.Ordinal)) &&
            (string.IsNullOrWhiteSpace(location) || string.Equals(target.TargetLocationId, location, StringComparison.Ordinal)) &&
            (!buildingX.HasValue || target.BuildingTileX == buildingX.Value) &&
            (!buildingY.HasValue || target.BuildingTileY == buildingY.Value);
    }

    private static SmallModelActionParameter[] AnimalPurchaseContinuation(AnimalPurchaseTarget target) =>
        new[]
        {
            Parameter("continuation.option_id", "animals.purchase"),
            Parameter("continuation.animal_type_id", target.AnimalTypeId),
            Parameter("continuation.possible_actual_type_ids_json", JsonSerializer.Serialize(target.PossibleActualTypeIds)),
            Parameter("continuation.target_location_id", target.TargetLocationId),
            Parameter("continuation.home_building_type", target.BuildingType),
            Parameter("continuation.home_building_tile_x", target.BuildingTileX.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.home_building_tile_y", target.BuildingTileY.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.home_indoor_location_id", target.IndoorLocationId),
            Parameter("continuation.generated_animal_name", target.GeneratedName),
            Parameter("continuation.expected_price", target.Price.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.expected_money_before", target.PlayerMoney.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.expected_money_after", (target.PlayerMoney - target.Price).ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.expected_home_occupant_count_before", target.OccupantCount.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.expected_home_capacity", target.Capacity.ToString(CultureInfo.InvariantCulture)),
            Parameter("continuation.candidate_identity_sha256", target.CandidateIdentity)
        };

    private static string AnimalPurchaseTargetKey(AnimalPurchaseTarget target) =>
        target.CandidateIdentity + ":" + target.TargetLocationId + ":" +
        target.BuildingTileX.ToString(CultureInfo.InvariantCulture) + "," +
        target.BuildingTileY.ToString(CultureInfo.InvariantCulture);

    private sealed record AnimalPurchaseTarget(
        string CandidateIdentity,
        string AnimalTypeId,
        string DisplayName,
        string[] PossibleActualTypeIds,
        int Price,
        int PlayerMoney,
        bool RequiredBuildingMet,
        bool CanAfford,
        string GeneratedName,
        string TargetLocationId,
        int NativeLocationChoiceIndex,
        string BuildingType,
        int BuildingTileX,
        int BuildingTileY,
        string IndoorLocationId,
        bool CompatibleWithAllPossibleTypes,
        bool IsUnderConstruction,
        int OccupantCount,
        int Capacity,
        int AvailableSlots);
}
