using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.FishPonds;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object ReadFishPondProjection(Building building, Farm farm, Farmer player)
    {
        if (building is not FishPond pond)
        {
            return new { status = "not_applicable" };
        }

        var exactRuntimeType = pond.GetType() == typeof(FishPond);
        var completed = pond.daysOfConstructionLeft.Value <= 0 && !pond.isUnderConstruction();
        var interactionPoints = ReadFishPondInteractionPoints(farm, pond, player);
        var preferred = interactionPoints.FirstOrDefault();
        var safeSlot = FindSafeToolbarSlot(player);
        var output = pond.output.Value;
        ClearanceOutputItemProjection? outputProjection = null;
        var outputItemsJson = string.Empty;
        var outputExperience = 0;
        var inventoryAcceptsOutput = false;
        if (output is not null)
        {
            outputProjection = ProjectFishPondInventoryOutput(output);
            outputItemsJson = System.Text.Json.JsonSerializer.Serialize(new[] { outputProjection });
            inventoryAcceptsOutput = player.couldInventoryAcceptThisItem(output);
            var priceContribution = output is StardewValley.Object obj
                ? (int)(obj.sellToStorePrice(-1L) * FishPond.HARVEST_OUTPUT_EXP_MULTIPLIER)
                : 0;
            outputExperience = FishPond.HARVEST_BASE_EXP + priceContribution;
        }

        var neededItem = pond.neededItem.Value;
        var pondData = FishPond.GetRawData(pond.fishType.Value);
        var unresolvedRequest = neededItem is not null && !pond.hasCompletedRequest.Value &&
            pond.currentOccupants.Value >= pond.maxOccupants.Value &&
            pond.maxOccupants.Value + 1 > pond.lastUnlockedPopulationGate.Value &&
            (pondData?.PopulationGates?.ContainsKey(pond.maxOccupants.Value + 1) ?? false);
        var neededCount = unresolvedRequest ? pond.neededItemCount.Value : 0;
        var matchingSlots = neededItem is null
            ? Array.Empty<FishPondRequestInventorySlot>()
            : player.Items
                .Take(Math.Min(12, player.Items.Count))
                .Select((item, index) => new { item, index })
                .Where(entry => entry.item is not null &&
                    string.Equals(entry.item.QualifiedItemId, neededItem.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new FishPondRequestInventorySlot(
                    entry.index,
                    entry.item!.Stack,
                    entry.item.GetType().FullName ?? entry.item.GetType().Name,
                    entry.item.QualifiedItemId))
                .ToArray();
        var matchingToolbarCount = matchingSlots.Sum(row => row.stack);
        var matchingInventoryCount = neededItem is null
            ? 0
            : player.Items.Where(item => item is not null &&
                    string.Equals(item.QualifiedItemId, neededItem.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
                .Sum(item => item!.Stack);
        var signInterception = neededItem is not null &&
            pond.IsValidSignItem(neededItem) &&
            !string.Equals(pond.sign.Value?.QualifiedItemId, neededItem.QualifiedItemId, StringComparison.OrdinalIgnoreCase);
        var crackerInterception = neededItem?.QualifiedItemId == "(O)GoldenAnimalCracker" &&
            !pond.goldenAnimalCracker.Value && pond.FishCount > 0;
        var spawnTime = ResolveFishPondSpawnTime(pondData, pond.fishType.Value);
        var requestExperience = !spawnTime.HasValue
            ? 0
            : FishPond.QUEST_BASE_EXP + (int)(spawnTime.Value * FishPond.QUEST_SPAWNRATE_EXP_MULTIPIER);
        var expectedUnlockedGate = pond.maxOccupants.Value + 1;
        var expectedMaxOccupants = pondData is null
            ? pond.maxOccupants.Value
            : ProjectMaximumOccupants(pondData, expectedUnlockedGate, pond.maxOccupants.Value);

        var outputStatus = !exactRuntimeType
            ? "unsupported_fish_pond_runtime_type"
            : !completed
                ? "fish_pond_under_construction"
                : output is null
                    ? "fish_pond_output_not_ready"
                    : outputProjection is null
                        ? "fish_pond_output_projection_unavailable"
                        : !inventoryAcceptsOutput
                            ? "fish_pond_inventory_cannot_accept_output"
                            : !safeSlot.HasValue
                                ? "fish_pond_safe_toolbar_slot_unavailable"
                                : preferred is null
                                    ? "fish_pond_interaction_point_unavailable"
                                    : "ready";
        var requestStatus = !exactRuntimeType
            ? "unsupported_fish_pond_runtime_type"
            : !completed
                ? "fish_pond_under_construction"
                : !unresolvedRequest || neededItem is null || neededCount <= 0
                    ? "fish_pond_request_not_ready"
                    : output is not null
                        ? "fish_pond_output_precedes_request"
                        : signInterception
                            ? "fish_pond_request_item_intercepted_by_sign"
                            : crackerInterception
                                ? "fish_pond_request_item_intercepted_by_golden_cracker"
                                : matchingToolbarCount < neededCount
                                    ? (matchingInventoryCount >= neededCount
                                        ? "fish_pond_request_item_not_in_toolbar"
                                        : "fish_pond_request_item_insufficient")
                                    : preferred is null
                                        ? "fish_pond_interaction_point_unavailable"
                                        : "ready";

        return new
        {
            status = "exact",
            runtime_type = pond.GetType().FullName,
            fish_type_item_id = pond.fishType.Value ?? string.Empty,
            fish_count = pond.FishCount,
            maximum_occupants = pond.maxOccupants.Value,
            last_unlocked_population_gate = pond.lastUnlockedPopulationGate.Value,
            days_since_spawn = pond.daysSinceSpawn.Value,
            seed_offset = pond.seedOffset.Value,
            has_completed_request = pond.hasCompletedRequest.Value,
            golden_animal_cracker = pond.goldenAnimalCracker.Value,
            sign_qualified_item_id = pond.sign.Value?.QualifiedItemId ?? string.Empty,
            interaction_points = interactionPoints,
            preferred_target_tile_x = preferred?.target_tile_x,
            preferred_target_tile_y = preferred?.target_tile_y,
            preferred_stand_tile_x = preferred?.stand_tile_x,
            preferred_stand_tile_y = preferred?.stand_tile_y,
            output_status = outputStatus,
            output_runtime_type = outputProjection?.RuntimeType ?? string.Empty,
            output_qualified_item_id = outputProjection?.QualifiedItemId ?? string.Empty,
            output_quality = outputProjection?.Quality ?? 0,
            output_stack = outputProjection?.Quantity ?? 0,
            output_unit_state_sha256 = outputProjection?.UnitStateSha256 ?? string.Empty,
            output_items_json = outputItemsJson,
            output_state_context = outputProjection is null ? "not_applicable" : "post_inventory_receive",
            output_inventory_accepts = inventoryAcceptsOutput,
            output_safe_slot_index = safeSlot,
            output_fishing_experience_delta = outputExperience,
            output_receipt_callbacks_status = outputProjection is null ? "not_applicable" : "runtime_observed",
            request_status = requestStatus,
            request_unresolved = unresolvedRequest,
            request_item_runtime_type = neededItem?.GetType().FullName ?? string.Empty,
            request_item_qualified_item_id = neededItem?.QualifiedItemId ?? string.Empty,
            request_item_count_remaining = neededCount,
            request_item_inventory_count = matchingInventoryCount,
            request_item_toolbar_count = matchingToolbarCount,
            request_item_toolbar_slots = matchingSlots,
            request_item_toolbar_slots_json = System.Text.Json.JsonSerializer.Serialize(matchingSlots),
            request_item_sign_interception = signInterception,
            request_item_golden_cracker_interception = crackerInterception,
            request_fishing_experience_delta = requestExperience,
            request_spawn_time_days = spawnTime,
            request_expected_maximum_occupants_after = expectedMaxOccupants,
            request_expected_last_unlocked_population_gate_after = expectedUnlockedGate,
            request_expected_days_since_spawn_after = 0,
            request_expected_needed_item_count_after = -1,
            request_expected_has_completed_request_after = true
        };
    }

    private static FishPondInteractionPoint[] ReadFishPondInteractionPoints(Farm farm, FishPond pond, Farmer player)
    {
        var rows = new List<FishPondInteractionPoint>();
        var left = pond.tileX.Value;
        var top = pond.tileY.Value;
        var right = left + pond.tilesWide.Value - 1;
        var bottom = top + pond.tilesHigh.Value - 1;
        var directions = new[] { new Point(0, -1), new Point(1, 0), new Point(0, 1), new Point(-1, 0) };
        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (x != left && x != right && y != top && y != bottom)
                {
                    continue;
                }
                foreach (var direction in directions)
                {
                    var standX = x + direction.X;
                    var standY = y + direction.Y;
                    if (IsTileInBuildingFootprint(standX, standY, left, top, pond.tilesWide.Value, pond.tilesHigh.Value))
                    {
                        continue;
                    }
                    var mapPassable = IsTilePassableForInteraction(farm, standX, standY);
                    var dynamicBlocked = mapPassable && IsTileDynamicallyBlocked(farm, standX, standY);
                    if (mapPassable && !dynamicBlocked)
                    {
                        rows.Add(new FishPondInteractionPoint(x, y, standX, standY));
                    }
                }
            }
        }

        var playerOnFarm = string.Equals(
            player.currentLocation?.NameOrUniqueName,
            farm.NameOrUniqueName,
            StringComparison.OrdinalIgnoreCase);
        return rows
            .OrderBy(row => playerOnFarm
                ? Math.Abs(player.TilePoint.X - row.stand_tile_x) + Math.Abs(player.TilePoint.Y - row.stand_tile_y)
                : 0)
            .ThenBy(row => row.stand_tile_y)
            .ThenBy(row => row.stand_tile_x)
            .ThenBy(row => row.target_tile_y)
            .ThenBy(row => row.target_tile_x)
            .ToArray();
    }

    private static int? FindSafeToolbarSlot(Farmer player)
    {
        var toolbarCount = Math.Min(12, player.Items.Count);
        for (var index = 0; index < toolbarCount; index++)
        {
            if (player.Items[index] is null)
            {
                return index;
            }
        }
        for (var index = 0; index < toolbarCount; index++)
        {
            if (player.Items[index] is Tool)
            {
                return index;
            }
        }
        return null;
    }

    private static ClearanceOutputItemProjection ProjectFishPondInventoryOutput(Item output)
    {
        var liveRandom = Game1.random;
        try
        {
            Game1.random = new Random(0);
            var inventoryUnit = output.getOne();
            inventoryUnit.Stack = 1;
            inventoryUnit.HasBeenInInventory = true;
            return ClearanceOutputItemProjection.From(inventoryUnit) with { Quantity = output.Stack };
        }
        finally
        {
            Game1.random = liveRandom;
        }
    }

    private static int? ResolveFishPondSpawnTime(FishPondData? data, string? fishItemId)
    {
        if (data is null || string.IsNullOrWhiteSpace(fishItemId))
        {
            return null;
        }
        if (data.SpawnTime >= 0)
        {
            return data.SpawnTime;
        }
        if (!Game1.objectData.TryGetValue(fishItemId, out var objectData))
        {
            return null;
        }
        return objectData.Price <= 30 ? 1
            : objectData.Price <= 80 ? 2
            : objectData.Price <= 120 ? 3
            : objectData.Price <= 250 ? 4
            : 5;
    }

    private static int ProjectMaximumOccupants(FishPondData data, int lastUnlockedGate, int currentMaximum)
    {
        if (data.MaxPopulation > 0)
        {
            return data.MaxPopulation;
        }
        var maximum = currentMaximum;
        for (var population = 1; population <= FishPond.MAXIMUM_OCCUPANCY; population++)
        {
            if (population <= lastUnlockedGate || !(data.PopulationGates?.ContainsKey(population) ?? false))
            {
                maximum = population;
                continue;
            }
            break;
        }
        return maximum;
    }

    private sealed record FishPondInteractionPoint(
        int target_tile_x,
        int target_tile_y,
        int stand_tile_x,
        int stand_tile_y);

    private sealed record FishPondRequestInventorySlot(
        int slot_index,
        int stack,
        string runtime_type,
        string qualified_item_id);
}
