using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadBuildingConstructionCatalog(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
        {
            return new { projection_status = "unavailable_world", rows = Array.Empty<object>() };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new { projection_status = "blocked_requires_full_profile", rows = Array.Empty<object>() };
        }

        var locations = Game1.locations
            .Where(location => location.IsBuildableLocation())
            .OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .ToArray();
        var services = new Dictionary<string, (GameLocation? Location, Point? ActionTile, string ActionRaw, bool OwnerReady)>(StringComparer.Ordinal)
        {
            ["Robin"] = FindBuildingService("Robin"),
            ["Wizard"] = FindBuildingService("Wizard")
        };
        var rows = new List<object>();
        foreach (var pair in Game1.buildingData.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var data = pair.Value;
            if (data.BuildingToUpgrade is not null || data.Builder is not ("Robin" or "Wizard"))
            {
                continue;
            }

            foreach (var location in locations)
            {
                var matchingUnderConstruction = location.buildings
                    .Where(building =>
                        string.Equals(building.buildingType.Value, pair.Key, StringComparison.Ordinal) &&
                        building.daysOfConstructionLeft.Value > 0)
                    .OrderBy(building => building.tileY.Value)
                    .ThenBy(building => building.tileX.Value)
                    .Select(building => new
                    {
                        tile_x = building.tileX.Value,
                        tile_y = building.tileY.Value,
                        days_of_construction_left = building.daysOfConstructionLeft.Value
                    })
                    .ToArray();
                var locationAcceptedByNativeMenu = pair.Key != "Cabin" || location.NameOrUniqueName == "Farm";
                var conditionMet = GameStateQuery.CheckConditions(data.BuildCondition, location);
                var placementScan = conditionMet && locationAcceptedByNativeMenu
                    ? FindQuestBuildingPlacement(location, pair.Key, data)
                    : QuestBuildingPlacementScan.Unavailable;
                var placement = placementScan.Placement;
                var materials = data.BuildMaterials?.Select(material =>
                {
                    var available = player.Items.CountId(material.ItemId);
                    var consumption = QuestBuildingInventoryConsumptionPlan(player, material.ItemId, material.Amount);
                    return new QuestBuildingMaterialProjection(
                        material.ItemId,
                        material.Amount,
                        available,
                        available >= material.Amount && consumption.Status == "exact_native_inventory_index_order",
                        consumption.Status,
                        consumption.Plan);
                }).ToArray() ?? Array.Empty<QuestBuildingMaterialProjection>();
                var service = services[data.Builder];
                var currentAtService = service.Location is not null && ReferenceEquals(Game1.currentLocation, service.Location);
                var serviceReady = service.OwnerReady;
                var materialReady = materials.All(row => row.satisfied);
                var status = !locationAcceptedByNativeMenu
                    ? "native_building_location_invalid"
                    : !conditionMet
                    ? "native_build_condition_false"
                    : Game1.IsThereABuildingUnderConstruction()
                        ? "another_building_under_construction"
                        : player.Money < data.BuildCost
                            ? "insufficient_money"
                            : !materialReady
                                ? "insufficient_materials"
                                : placement is null
                                    ? "no_verified_static_placement"
                                    : service.Location is null || !service.ActionTile.HasValue
                                        ? "builder_service_action_missing"
                                        : !currentAtService
                                            ? "route_to_builder_service_required"
                                            : !serviceReady
                                                ? "builder_not_present_at_service"
                                                : Game1.activeClickableMenu is not null || Game1.dialogueUp
                                                    ? "builder_menu_or_dialogue_not_clear"
                                                    : "ready_for_native_construction";

                rows.Add(new
                {
                    building_type = pair.Key,
                    display_name = data.Name,
                    description = data.Description,
                    builder = data.Builder,
                    build_condition = data.BuildCondition ?? string.Empty,
                    build_condition_met = conditionMet,
                    native_location_valid = locationAcceptedByNativeMenu,
                    build_days = data.BuildDays,
                    build_cost = data.BuildCost,
                    build_materials = materials,
                    size_x = data.Size.X,
                    size_y = data.Size.Y,
                    human_door_x = data.HumanDoor.X,
                    human_door_y = data.HumanDoor.Y,
                    indoor_map = data.IndoorMap ?? string.Empty,
                    max_occupants = data.MaxOccupants,
                    valid_occupant_types = data.ValidOccupantTypes?.OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? Array.Empty<string>(),
                    hay_capacity = data.HayCapacity,
                    existing_building_count = location.getNumberBuildingsConstructed(pair.Key),
                    matching_under_construction_count = matchingUnderConstruction.Length,
                    matching_under_construction = matchingUnderConstruction,
                    expected_money_before = player.Money,
                    expected_money_after = player.Money - data.BuildCost,
                    service_location_id = service.Location?.NameOrUniqueName ?? string.Empty,
                    service_action_raw = service.ActionRaw,
                    service_action_tile_x = service.ActionTile?.X,
                    service_action_tile_y = service.ActionTile?.Y,
                    builder_present_at_service = serviceReady,
                    placement_location_id = location.NameOrUniqueName,
                    placement_tile_x = placement?.X,
                    placement_tile_y = placement?.Y,
                    placement_scan = placementScan,
                    placement_verification = placement is null ? "unavailable" : "static_native_predicates_passed_runtime_recheck_required",
                    action_status = status,
                    native_contract = "GameLocation.checkAction->ShowConstructOptions->CarpenterMenu.receiveLeftClick->tryToBuild->ConsumeResources->Building.FinishConstruction"
                });
            }
        }

        return new
        {
            projection_status = "complete_live_native_building_catalog",
            buildable_location_count = locations.Length,
            rows = rows.ToArray()
        };
    }

    private static (GameLocation? Location, Point? ActionTile, string ActionRaw, bool OwnerReady) FindBuildingService(string builder)
    {
        var expectedAction = builder == "Wizard" ? "WizardBook" : "Carpenter";
        foreach (var location in Game1.locations.OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal))
        {
            var layer = location.Map?.GetLayer("Buildings");
            if (layer is null)
            {
                continue;
            }
            for (var y = 0; y < layer.LayerHeight; y++)
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (location.doesTileHaveProperty(x, y, "Action", "Buildings") != expectedAction)
                {
                    continue;
                }
                var ownerReady = builder == "Wizard"
                    ? Game1.player.hasMagicInk || Game1.player.mailReceived.Contains("hasPickedUpMagicInk")
                    : location.characters.Any(npc => npc.Name == "Robin" && Vector2.Distance(npc.Tile, new Vector2(x, y)) <= 3f);
                return (location, new Point(x, y), expectedAction, ownerReady);
            }
        }
        return (null, null, expectedAction, false);
    }
}
