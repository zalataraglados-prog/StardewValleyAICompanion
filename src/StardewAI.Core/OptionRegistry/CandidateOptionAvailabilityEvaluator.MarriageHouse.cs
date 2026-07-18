using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] FarmhouseUpgradeCandidates(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "marriage_house");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            !progress.Value.TryGetProperty("house_upgrade", out var upgrade) || upgrade.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var row = progress.Value;
        var levelBefore = ReadInt(upgrade, "level_before");
        var levelAfter = ReadInt(upgrade, "level_after");
        var price = ReadInt(upgrade, "price");
        var requiredItemId = ReadString(upgrade, "required_item_id");
        var requiredCount = ReadInt(upgrade, "required_item_count");
        var inventoryCount = ReadInt(upgrade, "inventory_item_count");
        var constructionDays = ReadInt(upgrade, "construction_days");
        var meetsGrandpaHouseLevel = ReadBool(upgrade, "meets_grandpa_house_level_after_construction") == true;
        var grandpaFactorAfter = ReadBool(upgrade, "grandpa_factor_satisfied_after_construction") == true;
        var directGrandpaScoreDelta = ReadInt(upgrade, "direct_grandpa_score_delta_after_construction");
        var unlocksCellar = ReadBool(upgrade, "unlocks_cellar") == true;
        var unlocksCaskRecipe = ReadBool(upgrade, "unlocks_cask_recipe") == true;
        var addsIndoorMachineLocation = ReadBool(upgrade, "adds_indoor_machine_placement_location") == true;
        var capacityProjectionStatus = ReadString(upgrade, "machine_capacity_projection_status");
        var cellarInfrastructure = row.TryGetProperty("cellar_infrastructure", out var infrastructure) && infrastructure.ValueKind == JsonValueKind.Object
            ? infrastructure
            : default;
        var cellarLocationId = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadString(cellarInfrastructure, "location_id") : string.Empty;
        var cellarMapWidth = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadInt(cellarInfrastructure, "map_width") : 0;
        var cellarMapHeight = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadInt(cellarInfrastructure, "map_height") : 0;
        var cellarPlaceableTiles = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadInt(cellarInfrastructure, "static_placeable_tile_count") : 0;
        var cellarObjectCount = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadInt(cellarInfrastructure, "occupied_object_count") : 0;
        var cellarMachineCount = cellarInfrastructure.ValueKind == JsonValueKind.Object ? ReadInt(cellarInfrastructure, "machine_count") : 0;
        var cellarMachineCountsJson = cellarInfrastructure.ValueKind == JsonValueKind.Object &&
            cellarInfrastructure.TryGetProperty("machine_counts_by_qualified_id", out var machineCounts) && machineCounts.ValueKind == JsonValueKind.Object
                ? machineCounts.GetRawText()
                : "{}";
        var money = ReadInt(row, "money");
        var actionX = NullableReadInt(row, "carpenter_action_tile_x");
        var actionY = NullableReadInt(row, "carpenter_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue ? FindBestStandTile(snapshot, actionX.Value, actionY.Value) : null;
        var reasons = new List<string>();
        var status = ReadString(upgrade, "action_status");
        if (status != "ready")
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "farmhouse_upgrade_projection_unavailable" : status);
        }
        if (ReadBool(row, "location_accessible") != true)
        {
            reasons.Add("science_house_not_accessible");
        }
        if (!actionX.HasValue || !actionY.HasValue || ReadString(row, "carpenter_action_raw") != "Carpenter")
        {
            reasons.Add("carpenter_action_tile_unavailable");
        }
        if (stand is null)
        {
            reasons.Add("carpenter_no_reachable_counter_stand_tile");
        }
        if (!FarmhouseUpgradeTupleExact(levelBefore, levelAfter, price, requiredItemId, requiredCount, constructionDays))
        {
            reasons.Add("farmhouse_upgrade_native_tuple_invalid");
        }
        if (!HasCompleteBenefitProjection(upgrade) ||
            !FarmhouseUpgradeBenefitsExact(
                row,
                levelBefore,
                levelAfter,
                meetsGrandpaHouseLevel,
                grandpaFactorAfter,
                directGrandpaScoreDelta,
                unlocksCellar,
                unlocksCaskRecipe,
                addsIndoorMachineLocation,
                capacityProjectionStatus))
        {
            reasons.Add("farmhouse_upgrade_benefit_projection_invalid");
        }
        if (unlocksCellar &&
            (capacityProjectionStatus != "cellar_static_map_capacity_available" ||
             cellarInfrastructure.ValueKind != JsonValueKind.Object ||
             ReadString(cellarInfrastructure, "projection_status") != capacityProjectionStatus ||
             string.IsNullOrWhiteSpace(cellarLocationId) || cellarMapWidth <= 0 || cellarMapHeight <= 0 || cellarPlaceableTiles <= 0))
        {
            reasons.Add("farmhouse_upgrade_cellar_capacity_projection_unavailable");
        }

        var parameters = stand is null || !actionX.HasValue || !actionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                Parameter("target_location", "ScienceHouse"),
                Parameter("stand_tile_x", stand.X.ToString()),
                Parameter("stand_tile_y", stand.Y.ToString()),
                Parameter("carpenter_action_tile_x", actionX.Value.ToString()),
                Parameter("carpenter_action_tile_y", actionY.Value.ToString()),
                Parameter("carpenter_action_raw", "Carpenter"),
                Parameter("purchase_kind", "farmhouse_upgrade"),
                Parameter("project_id", ReadString(upgrade, "upgrade_id")),
                Parameter("expected_house_upgrade_level_before", levelBefore.ToString()),
                Parameter("expected_house_upgrade_level_after_construction", levelAfter.ToString()),
                Parameter("expected_days_until_house_upgrade_before", ReadInt(row, "days_until_farmhouse_upgrade").ToString()),
                Parameter("expected_days_until_house_upgrade_after", constructionDays.ToString()),
                Parameter("expected_money_before", money.ToString()),
                Parameter("price", price.ToString()),
                Parameter("expected_money_after", (money - price).ToString()),
                Parameter("qualified_item_id", requiredItemId),
                Parameter("required_stack", requiredCount.ToString()),
                Parameter("inventory_item_total_before", inventoryCount.ToString()),
                Parameter("inventory_item_total_after", (inventoryCount - requiredCount).ToString()),
                Parameter("meets_grandpa_house_level_after_construction", meetsGrandpaHouseLevel.ToString().ToLowerInvariant()),
                Parameter("grandpa_factor_satisfied_after_construction", grandpaFactorAfter.ToString().ToLowerInvariant()),
                Parameter("direct_grandpa_score_delta_after_construction", directGrandpaScoreDelta.ToString()),
                Parameter("unlocks_cellar", unlocksCellar.ToString().ToLowerInvariant()),
                Parameter("unlocks_cask_recipe", unlocksCaskRecipe.ToString().ToLowerInvariant()),
                Parameter("adds_indoor_machine_placement_location", addsIndoorMachineLocation.ToString().ToLowerInvariant()),
                Parameter("machine_capacity_projection_status", capacityProjectionStatus),
                Parameter("projected_cellar_location_id", unlocksCellar ? cellarLocationId : string.Empty),
                Parameter("projected_cellar_map_width", (unlocksCellar ? cellarMapWidth : 0).ToString()),
                Parameter("projected_cellar_map_height", (unlocksCellar ? cellarMapHeight : 0).ToString()),
                Parameter("projected_cellar_static_placeable_tiles", (unlocksCellar ? cellarPlaceableTiles : 0).ToString()),
                Parameter("projected_cellar_existing_object_count", (unlocksCellar ? cellarObjectCount : 0).ToString()),
                Parameter("projected_cellar_existing_machine_count", (unlocksCellar ? cellarMachineCount : 0).ToString()),
                Parameter("projected_cellar_machine_counts_by_qualified_id_json", unlocksCellar ? cellarMachineCountsJson : "{}"),
                Parameter("native_contract", "GameLocation.checkAction_Carpenter_then_answerDialogue_carpenter_Upgrade_then_upgrade_Yes")
            };
        var expectedEffect = "player.money=" + (money - price) +
            ";player.days_until_farmhouse_upgrade=" + constructionDays +
            (requiredCount > 0 ? ";inventory." + requiredItemId + "=" + (inventoryCount - requiredCount) : string.Empty) +
            ";eventual.player.farmhouse_upgrade_level=" + levelAfter +
            ";eventual.grandpa_score_delta=" + directGrandpaScoreDelta +
            ";eventual.capability.cellar=" + unlocksCellar.ToString().ToLowerInvariant() +
            ";eventual.capability.cask_recipe=" + unlocksCaskRecipe.ToString().ToLowerInvariant() +
            ";eventual.capability.indoor_machine_placement_location=" + addsIndoorMachineLocation.ToString().ToLowerInvariant() +
            ";machine_capacity_projection_status=" + capacityProjectionStatus +
            ";projected_cellar_static_placeable_tiles=" + (unlocksCellar ? cellarPlaceableTiles : 0) +
            ";projected_cellar_existing_machine_count=" + (unlocksCellar ? cellarMachineCount : 0);
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        var candidateKind = levelAfter <= 2 ? "purchase_farmhouse_upgrade" : "purchase_farmhouse_expansion";
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "farmhouse-upgrade:" + ReadString(upgrade, "upgrade_id"),
                Kind = candidateKind,
                Available = reasons.Count == 0,
                LocationId = "ScienceHouse",
                TileX = actionX,
                TileY = actionY,
                ExpectedEffect = expectedEffect,
                Quantity = 1,
                EstimatedTicks = Math.Max(300, distance * 60 + 300),
                AvailabilityClass = unlocksCellar
                    ? "transparent_native_farmhouse_upgrade_indirect_infrastructure"
                    : "transparent_native_farmhouse_upgrade",
                AllowedNow = reasons.Count == 0,
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static bool FarmhouseUpgradeTupleExact(int before, int after, int price, string itemId, int count, int days) =>
        days == 3 && (before, after, price, itemId, count) switch
        {
            (0, 1, 10000, "(O)388", 450) => true,
            (1, 2, 65000, "(O)709", 100) => true,
            (2, 3, 100000, "", 0) => true,
            _ => false
        };

    private static bool HasCompleteBenefitProjection(JsonElement upgrade) =>
        HasBooleanProperty(upgrade, "meets_grandpa_house_level_after_construction") &&
        HasBooleanProperty(upgrade, "grandpa_factor_satisfied_after_construction") &&
        upgrade.TryGetProperty("direct_grandpa_score_delta_after_construction", out var delta) && delta.TryGetInt32(out _) &&
        HasBooleanProperty(upgrade, "unlocks_cellar") &&
        HasBooleanProperty(upgrade, "unlocks_cask_recipe") &&
        HasBooleanProperty(upgrade, "adds_indoor_machine_placement_location") &&
        upgrade.TryGetProperty("machine_capacity_projection_status", out var status) && status.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(status.GetString());

    private static bool HasBooleanProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False);

    private static bool FarmhouseUpgradeBenefitsExact(
        JsonElement row,
        int levelBefore,
        int levelAfter,
        bool meetsGrandpaHouseLevel,
        bool grandpaFactorAfter,
        int directGrandpaScoreDelta,
        bool unlocksCellar,
        bool unlocksCaskRecipe,
        bool addsIndoorMachineLocation,
        string capacityProjectionStatus)
    {
        var currentGrandpaFactor = ReadBool(row, "grandpa_factor_satisfied");
        var expectedMeetsLevel = levelAfter >= 2;
        var expectedGrandpaFactorAfter = ReadBool(row, "married_or_roommate") && expectedMeetsLevel;
        var expectedDelta = !currentGrandpaFactor && expectedGrandpaFactorAfter ? 1 : 0;
        var expectedCellarUnlock = levelBefore == 2 && levelAfter == 3;
        var expectedCapacityStatus = expectedCellarUnlock
            ? "cellar_static_map_capacity_available"
            : "no_new_machine_location_from_this_upgrade";

        return meetsGrandpaHouseLevel == expectedMeetsLevel &&
            grandpaFactorAfter == expectedGrandpaFactorAfter &&
            directGrandpaScoreDelta == expectedDelta &&
            unlocksCellar == expectedCellarUnlock &&
            unlocksCaskRecipe == expectedCellarUnlock &&
            addsIndoorMachineLocation == expectedCellarUnlock &&
            string.Equals(capacityProjectionStatus, expectedCapacityStatus, StringComparison.Ordinal);
    }
}
