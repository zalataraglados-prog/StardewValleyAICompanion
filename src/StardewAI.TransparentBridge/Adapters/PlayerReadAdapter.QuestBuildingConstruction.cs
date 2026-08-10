using Microsoft.Xna.Framework;
using System.Reflection;
using StardewAI.TransparentBridge.State;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Quests;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadQuestBuildingConstructionContext(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new { projection_status = "unavailable_world", active_target_count = 0, rows = Array.Empty<object>() };
        }

        var quests = player.questLog.OfType<HaveBuildingQuest>()
            .Where(quest => quest.accepted.Value && !quest.completed.Value)
            .OrderBy(quest => quest.id.Value, StringComparer.Ordinal)
            .ToArray();
        if (quests.Length == 0)
        {
            return new { projection_status = "not_applicable_no_active_building_quest", active_target_count = 0, rows = Array.Empty<object>() };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new
            {
                projection_status = "blocked_requires_full_profile",
                active_target_count = quests.Length,
                rows = quests.Select(quest => new
                {
                    quest_id = quest.id.Value,
                    quest_runtime_type = "HaveBuildingQuest",
                    target_building_type = quest.buildingType.Value,
                    action_status = "blocked_requires_full_profile"
                }).Cast<object>().ToArray()
            };
        }

        var farm = Game1.getFarm();
        var scienceHouse = Game1.getLocationFromName("ScienceHouse");
        var carpenterAction = scienceHouse is null ? null : FindCarpenterAction(scienceHouse);
        var robin = scienceHouse?.characters.FirstOrDefault(npc => npc.Name == "Robin");
        var robinReady = carpenterAction.HasValue && robin is not null &&
            Vector2.Distance(robin.Tile, new Vector2(carpenterAction.Value.X, carpenterAction.Value.Y)) <= 3f;
        var rows = quests.Select(quest => ReadQuestBuildingRow(
            player, farm, quest, scienceHouse, carpenterAction, robinReady)).ToArray();
        return new
        {
            projection_status = "complete_active_building_quest_projection",
            active_target_count = rows.Length,
            rows
        };
    }

    private static object ReadQuestBuildingRow(
        Farmer player,
        GameLocation farm,
        HaveBuildingQuest quest,
        GameLocation? scienceHouse,
        Point? carpenterAction,
        bool robinReady)
    {
        var type = quest.buildingType.Value ?? string.Empty;
        Game1.buildingData.TryGetValue(type, out var data);
        var matching = farm.buildings
            .Where(building => string.Equals(building.buildingType.Value, type, StringComparison.Ordinal))
            .OrderByDescending(building => building.daysOfConstructionLeft.Value)
            .ThenBy(building => building.tileY.Value)
            .ThenBy(building => building.tileX.Value)
            .ToArray();
        var underConstruction = matching.FirstOrDefault(building => building.daysOfConstructionLeft.Value > 0);
        var constructedMarker = player.team.constructedBuildings.Contains(type);
        var placementScan = data is null || data.BuildingToUpgrade is not null
            ? QuestBuildingPlacementScan.Unavailable
            : FindQuestBuildingPlacement(farm, type, data);
        var placement = placementScan.Placement;
        var materials = data?.BuildMaterials?.Select(material =>
        {
            var available = player.Items.CountId(material.ItemId);
            var consumption = QuestBuildingInventoryConsumptionPlan(
                player,
                material.ItemId,
                material.Amount);
            return new QuestBuildingMaterialProjection(
                material.ItemId,
                material.Amount,
                available,
                available >= material.Amount && consumption.Status == "exact_native_inventory_index_order",
                consumption.Status,
                consumption.Plan);
        }).ToArray() ?? Array.Empty<QuestBuildingMaterialProjection>();
        var materialReady = materials.All(row => row.satisfied);
        var currentAtCarpenter = ReferenceEquals(Game1.currentLocation, scienceHouse);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var actionStatus = constructedMarker
            ? "native_completion_marker_already_present"
            : underConstruction is not null
                ? "construction_in_progress"
                : data is null
                    ? "target_building_data_missing"
                    : data.Builder != "Robin" || data.BuildingToUpgrade is not null
                        ? "unsupported_non_robin_or_upgrade_blueprint"
                        : Game1.IsThereABuildingUnderConstruction()
                            ? "another_building_under_construction"
                            : player.Money < data.BuildCost
                                ? "insufficient_money"
                                : !materialReady
                                    ? "insufficient_materials"
                                    : placement is null
                                        ? "no_verified_static_placement"
                                        : !currentAtCarpenter
                                            ? "route_to_science_house_required"
                                            : !robinReady
                                                ? "robin_not_present_at_counter"
                                                : !menuClear
                                                    ? "carpenter_menu_or_dialogue_not_clear"
                                                    : "ready_for_native_carpenter_menu";

        return new
        {
            quest_id = quest.id.Value,
            quest_runtime_type = "HaveBuildingQuest",
            target_building_type = type,
            constructed_marker_present = constructedMarker,
            matching_building_count = matching.Length,
            matching_under_construction = underConstruction is not null,
            construction_days_left = underConstruction?.daysOfConstructionLeft.Value ?? 0,
            placed_tile_x = underConstruction?.tileX.Value,
            placed_tile_y = underConstruction?.tileY.Value,
            builder = data?.Builder ?? string.Empty,
            building_to_upgrade = data?.BuildingToUpgrade ?? string.Empty,
            build_condition = data?.BuildCondition ?? string.Empty,
            build_days = data?.BuildDays ?? 0,
            build_cost = data?.BuildCost ?? 0,
            size_x = data?.Size.X ?? 0,
            size_y = data?.Size.Y ?? 0,
            build_materials = materials,
            expected_money_before = player.Money,
            expected_money_after = data is null ? player.Money : player.Money - data.BuildCost,
            service_location_id = "ScienceHouse",
            service_is_current_location = currentAtCarpenter,
            carpenter_action_tile_x = carpenterAction?.X,
            carpenter_action_tile_y = carpenterAction?.Y,
            carpenter_action_raw = carpenterAction.HasValue ? "Carpenter" : string.Empty,
            robin_present_at_counter = robinReady,
            placement_location_id = farm.NameOrUniqueName,
            placement_tile_x = placement?.X,
            placement_tile_y = placement?.Y,
            placement_scan = placementScan,
            placement_verification = placement is null ? "unavailable" : "static_native_predicates_passed_runtime_recheck_required",
            action_status = actionStatus,
            native_completion_contract = "Building.FinishConstruction->FarmerTeam.constructedBuildings.OnValueAdded->HaveBuildingQuest.OnBuildingExists"
        };
    }

    private static Point? FindCarpenterAction(GameLocation location)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
        {
            return null;
        }
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            if (location.doesTileHaveProperty(x, y, "Action", "Buildings") == "Carpenter")
            {
                return new Point(x, y);
            }
        }
        return null;
    }

    private static QuestBuildingPlacementScan FindQuestBuildingPlacement(GameLocation farm, string type, BuildingData data)
    {
        var back = farm.Map?.GetLayer("Back");
        if (back is null || !farm.IsBuildableLocation())
        {
            return QuestBuildingPlacementScan.Unavailable;
        }
        var building = Building.CreateInstanceFromId(type, Vector2.Zero);
        if (building is null)
        {
            return QuestBuildingPlacementScan.Unavailable;
        }
        var scanned = 0;
        var footprintPassed = 0;
        var additionalPassed = 0;
        var doorPassed = 0;
        var preventionPassed = 0;
        for (var y = 0; y <= back.LayerHeight - data.Size.Y; y++)
        for (var x = 0; x <= back.LayerWidth - data.Size.X; x++)
        {
            scanned++;
            var origin = new Point(x, y);
            if (!QuestBuildingFootprintIsStaticallyBuildable(farm, building, origin, out var passedStage))
            {
                footprintPassed += passedStage >= 1 ? 1 : 0;
                additionalPassed += passedStage >= 2 ? 1 : 0;
                doorPassed += passedStage >= 3 ? 1 : 0;
                continue;
            }
            footprintPassed++;
            additionalPassed++;
            doorPassed++;
            preventionPassed++;
            return new QuestBuildingPlacementScan(
                origin, scanned, footprintPassed, additionalPassed, doorPassed, preventionPassed);
        }
        return new QuestBuildingPlacementScan(
            null, scanned, footprintPassed, additionalPassed, doorPassed, preventionPassed);
    }

    private static bool QuestBuildingFootprintIsStaticallyBuildable(
        GameLocation farm,
        Building building,
        Point origin,
        out int passedStage)
    {
        passedStage = 0;
        for (var y = 0; y < building.tilesHigh.Value; y++)
        for (var x = 0; x < building.tilesWide.Value; x++)
        {
            if (!QuestBuildingTileIsBuildable(farm, new Vector2(origin.X + x, origin.Y + y)))
            {
                return false;
            }
        }
        passedStage = 1;
        foreach (var extra in building.GetAdditionalPlacementTiles())
        for (var y = extra.TileArea.Top; y < extra.TileArea.Bottom; y++)
        for (var x = extra.TileArea.Left; x < extra.TileArea.Right; x++)
        {
            if (!QuestBuildingTileIsBuildable(
                    farm,
                    new Vector2(origin.X + x, origin.Y + y),
                    extra.OnlyNeedsToBePassable))
            {
                return false;
            }
        }
        passedStage = 2;
        if (building.humanDoor.Value != new Point(-1, -1))
        {
            var door = new Vector2(origin.X + building.humanDoor.X, origin.Y + building.humanDoor.Y + 1);
            if (!QuestBuildingTileIsBuildable(farm, door, onlyNeedsToBePassable: true) && !farm.isPath(door))
            {
                return false;
            }
        }
        passedStage = 3;
        if (building.isThereAnythingtoPreventConstruction(farm, origin.ToVector2()) is not null)
        {
            return false;
        }
        passedStage = 4;
        return true;
    }

    private static bool QuestBuildingTileIsBuildable(
        GameLocation location,
        Vector2 tile,
        bool onlyNeedsToBePassable = false)
    {
        var buildableRectangle = location.GetBuildableRectangle();
        if (buildableRectangle != Rectangle.Empty && !buildableRectangle.Contains((int)tile.X, (int)tile.Y))
        {
            return false;
        }
        if (onlyNeedsToBePassable)
        {
            return location.isTilePassable(tile) &&
                !location.IsTileOccupiedBy(tile, CollisionMask.All, CollisionMask.All);
        }
        var existingBuilding = location.getBuildingAt(tile);
        if (existingBuilding is not null && !existingBuilding.isMoving)
        {
            return false;
        }
        if (!location.CanItemBePlacedHere(
                tile,
                itemIsPassable: false,
                CollisionMask.All,
                ~CollisionMask.Objects,
                useFarmerTile: true) &&
            location.getObjectAtTile((int)tile.X, (int)tile.Y)?.QualifiedItemId != "(O)590")
        {
            return false;
        }
        var buildable = location.doesTileHavePropertyNoNull((int)tile.X, (int)tile.Y, "Buildable", "Back");
        if (buildable.Equals("t", StringComparison.OrdinalIgnoreCase) ||
            buildable.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return location.doesTileHaveProperty((int)tile.X, (int)tile.Y, "Diggable", "Back") is not null &&
            !buildable.Equals("f", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Status, QuestBuildingMaterialConsumption[] Plan) QuestBuildingInventoryConsumptionPlan(
        Farmer player,
        string qualifiedItemId,
        int requiredCount)
    {
        var indexField = player.Items.GetType().GetField(
            "ItemsById",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var inventoryIndex = indexField?.GetValue(player.Items);
        var valuesField = inventoryIndex?.GetType().GetField(
            "Index",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (valuesField?.GetValue(inventoryIndex) is not Dictionary<string, List<Item>> values ||
            !values.TryGetValue(qualifiedItemId, out var indexedItems))
        {
            return ("native_inventory_index_order_unavailable", Array.Empty<QuestBuildingMaterialConsumption>());
        }
        var remaining = requiredCount;
        var plan = new List<QuestBuildingMaterialConsumption>();
        foreach (var item in indexedItems)
        {
            if (remaining <= 0)
            {
                break;
            }
            var slot = player.Items.IndexOf(item);
            if (slot < 0 || !string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal))
            {
                return ("native_inventory_index_slot_resolution_failed", Array.Empty<QuestBuildingMaterialConsumption>());
            }
            var amount = Math.Min(remaining, item.Stack);
            plan.Add(new QuestBuildingMaterialConsumption(slot, qualifiedItemId, amount));
            remaining -= amount;
        }
        return remaining == 0
            ? ("exact_native_inventory_index_order", plan.ToArray())
            : ("native_inventory_index_quantity_incomplete", Array.Empty<QuestBuildingMaterialConsumption>());
    }

    private sealed record QuestBuildingMaterialProjection(
        string qualified_item_id,
        int required_count,
        int available_count,
        bool satisfied,
        string consumption_plan_status,
        QuestBuildingMaterialConsumption[] reverse_slot_consumption_plan);

    private sealed record QuestBuildingMaterialConsumption(
        int slot_index,
        string qualified_item_id,
        int amount);

    private sealed record QuestBuildingPlacementScan(
        Point? Placement,
        int scanned_origin_count,
        int footprint_pass_count,
        int additional_tiles_pass_count,
        int door_access_pass_count,
        int building_prevention_pass_count)
    {
        public static QuestBuildingPlacementScan Unavailable { get; } = new(null, 0, 0, 0, 0, 0);
    }
}
