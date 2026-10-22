using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.GameData.Buildings;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object[] ReadBuildingUpgradeDefinitions() =>
        Game1.buildingData
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.BuildingToUpgrade) &&
                pair.Value.Builder is "Robin" or "Wizard")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (object)new
            {
                target_building_type = pair.Key,
                upgrade_from_building_type = pair.Value.BuildingToUpgrade,
                builder = pair.Value.Builder,
                build_condition = pair.Value.BuildCondition ?? string.Empty,
                build_days = pair.Value.BuildDays,
                native_countdown_days = Math.Max(pair.Value.BuildDays, 1),
                build_cost = pair.Value.BuildCost,
                build_materials = pair.Value.BuildMaterials?.Select(material => (object)new
                    {
                        qualified_item_id = material.ItemId,
                        required_count = material.Amount
                    }).ToArray() ?? Array.Empty<object>(),
                native_contract = "CarpenterMenu.BlueprintEntry.IsUpgrade_from_BuildingToUpgrade"
            })
            .ToArray();

    private static void AddBuildingUpgradeRows(
        Farmer player,
        string targetBuildingType,
        BuildingData data,
        IReadOnlyList<GameLocation> locations,
        (GameLocation? Location, Point? ActionTile, string ActionRaw, bool OwnerReady) service,
        List<object> rows)
    {
        var sourceBuildingType = data.BuildingToUpgrade!;
        foreach (var location in locations)
        {
            var sourceBuildings = location.buildings
                .Where(building => string.Equals(building.buildingType.Value, sourceBuildingType, StringComparison.Ordinal))
                .OrderBy(building => building.tileY.Value)
                .ThenBy(building => building.tileX.Value)
                .ToArray();
            foreach (var building in sourceBuildings)
            {
                var conditionMet = GameStateQuery.CheckConditions(data.BuildCondition, location);
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
                var currentAtService = service.Location is not null && ReferenceEquals(Game1.currentLocation, service.Location);
                var materialReady = materials.All(row => row.satisfied);
                var status = !conditionMet
                    ? "native_build_condition_false"
                    : Game1.IsThereABuildingUnderConstruction()
                        ? "another_building_under_construction"
                        : player.Money < data.BuildCost
                            ? "insufficient_money"
                            : !materialReady
                                ? "insufficient_materials"
                                : service.Location is null || !service.ActionTile.HasValue
                                    ? "builder_service_action_missing"
                                    : !currentAtService
                                        ? "route_to_builder_service_required"
                                        : !service.OwnerReady
                                            ? "builder_not_present_at_service"
                                            : Game1.activeClickableMenu is not null || Game1.dialogueUp
                                                ? "builder_menu_or_dialogue_not_clear"
                                                : "ready_for_native_upgrade";

                rows.Add(new
                {
                    project_kind = "upgrade",
                    building_type = targetBuildingType,
                    upgrade_from_building_type = sourceBuildingType,
                    display_name = data.Name,
                    description = data.Description,
                    builder = data.Builder,
                    build_condition = data.BuildCondition ?? string.Empty,
                    build_condition_met = conditionMet,
                    native_location_valid = true,
                    build_days = data.BuildDays,
                    upgrade_days_after_native_start = Math.Max(data.BuildDays, 1),
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
                    existing_building_count = location.getNumberBuildingsConstructed(targetBuildingType),
                    matching_upgrade_source_count = sourceBuildings.Length,
                    expected_money_before = player.Money,
                    expected_money_after = player.Money - data.BuildCost,
                    service_location_id = service.Location?.NameOrUniqueName ?? string.Empty,
                    service_action_raw = service.ActionRaw,
                    service_action_tile_x = service.ActionTile?.X,
                    service_action_tile_y = service.ActionTile?.Y,
                    builder_present_at_service = service.OwnerReady,
                    placement_location_id = location.NameOrUniqueName,
                    placement_tile_x = building.tileX.Value,
                    placement_tile_y = building.tileY.Value,
                    placement_verification = "native_upgrade_source_type_and_tile_verified_runtime_recheck_required",
                    action_status = status,
                    native_contract = "GameLocation.checkAction->ShowConstructOptions->CarpenterMenu.Action.Upgrade->receiveLeftClick->ConsumeResources->Building.upgradeName_and_daysUntilUpgrade"
                });
            }
        }
    }
}
