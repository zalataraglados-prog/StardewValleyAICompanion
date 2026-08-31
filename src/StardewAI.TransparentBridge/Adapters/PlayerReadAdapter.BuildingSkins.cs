using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string DefaultBuildingSkinKey = "__default__";

    private static object ReadBuildingSkinCatalog(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
            return new { projection_status = "unavailable_world", rows = Array.Empty<object>() };
        if (SnapshotProfileContext.Current is not "full")
            return new { projection_status = "blocked_requires_full_profile", rows = Array.Empty<object>() };

        var service = FindBuildingService("Robin");
        var currentAtService = service.Location is not null && ReferenceEquals(Game1.currentLocation, service.Location);
        var rows = new List<object>();
        foreach (var location in Game1.locations
                     .Where(IsLoadedBuildableLocation)
                     .OrderBy(value => value.NameOrUniqueName, StringComparer.Ordinal))
        {
            foreach (var building in location.buildings
                         .OrderBy(value => value.tileY.Value)
                         .ThenBy(value => value.tileX.Value)
                         .ThenBy(value => value.buildingType.Value, StringComparer.Ordinal))
            {
                var data = building.GetData();
                if (data is null || data.Skins is null || data.Skins.Count == 0)
                    continue;

                var canBePainted = building.CanBePainted();
                var ignoreSeparateEntries = !canBePainted;
                var permission = HasNativeBuildingPaintPermission(building, player);
                var available = new List<(string Key, string? Id, string Name, string Description, string Condition, bool ConditionMet, bool Separate)>
                {
                    (DefaultBuildingSkinKey, null, TokenParser.ParseText(data.Name), TokenParser.ParseText(data.Description), string.Empty, true, false)
                };
                foreach (var skin in data.Skins)
                {
                    var isCurrent = string.Equals(skin.Id, building.skinId.Value, StringComparison.Ordinal);
                    var conditionMet = GameStateQuery.CheckConditions(skin.Condition, location);
                    if (!isCurrent && ((ignoreSeparateEntries && skin.ShowAsSeparateConstructionEntry) || !conditionMet))
                        continue;
                    available.Add((skin.Id, skin.Id, TokenParser.ParseText(skin.Name ?? data.Name),
                        TokenParser.ParseText(skin.Description ?? data.Description), skin.Condition ?? string.Empty,
                        conditionMet, skin.ShowAsSeparateConstructionEntry));
                }

                var currentIndex = Math.Max(0, available.FindIndex(value => value.Id == building.skinId.Value));
                for (var targetIndex = 0; targetIndex < available.Count; targetIndex++)
                {
                    if (targetIndex == currentIndex)
                        continue;
                    var target = available[targetIndex];
                    var nextClicks = (targetIndex - currentIndex + available.Count) % available.Count;
                    var previousClicks = (currentIndex - targetIndex + available.Count) % available.Count;
                    var direction = nextClicks <= previousClicks ? "next" : "previous";
                    var clickCount = Math.Min(nextClicks, previousClicks);
                    var status = building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0
                        ? "building_construction_or_upgrade_active"
                        : !permission
                            ? "building_paint_permission_denied"
                            : service.Location is null || !service.ActionTile.HasValue
                                ? "carpenter_service_action_missing"
                                : !currentAtService
                                    ? "route_to_carpenter_service_required"
                                    : !service.OwnerReady
                                        ? "robin_not_present_at_service"
                                        : Game1.activeClickableMenu is not null || Game1.dialogueUp
                                            ? "carpenter_menu_or_dialogue_not_clear"
                                            : "ready_for_native_skin_change";
                    var colors = building.netBuildingPaintColor.Value;
                    rows.Add(new
                    {
                        building_identity = location.NameOrUniqueName + ":" + building.buildingType.Value + ":" + building.tileX.Value + "," + building.tileY.Value,
                        building_location_id = location.NameOrUniqueName,
                        building_type = building.buildingType.Value,
                        building_tile_x = building.tileX.Value,
                        building_tile_y = building.tileY.Value,
                        building_tiles_wide = building.tilesWide.Value,
                        building_tiles_high = building.tilesHigh.Value,
                        building_owner_id = building.owner.Value,
                        permission_to_change_appearance = permission,
                        can_be_painted = canBePainted,
                        entry_route = canBePainted ? "building_paint_menu_then_appearance" : "direct_building_skin_menu",
                        ignore_separate_construction_entries = ignoreSeparateEntries,
                        current_skin_key = building.skinId.Value ?? DefaultBuildingSkinKey,
                        current_skin_id = building.skinId.Value ?? string.Empty,
                        current_skin_index = currentIndex,
                        target_skin_key = target.Key,
                        target_skin_id = target.Id ?? string.Empty,
                        target_skin_index = targetIndex,
                        target_skin_name = target.Name,
                        target_skin_description = target.Description,
                        target_skin_condition = target.Condition,
                        target_skin_condition_met = target.ConditionMet,
                        target_skin_separate_construction_entry = target.Separate,
                        available_skin_count = available.Count,
                        available_skin_keys = available.Select(value => value.Key).ToArray(),
                        shortest_click_direction = direction,
                        shortest_click_count = clickCount,
                        current_paint_color_1_default = colors.Color1Default.Value,
                        current_paint_color_2_default = colors.Color2Default.Value,
                        current_paint_color_3_default = colors.Color3Default.Value,
                        skin_change_resets_all_paint_colors_to_default = true,
                        service_location_id = service.Location?.NameOrUniqueName ?? string.Empty,
                        service_action_raw = service.ActionRaw,
                        service_action_tile_x = service.ActionTile?.X,
                        service_action_tile_y = service.ActionTile?.Y,
                        robin_present_at_service = service.OwnerReady,
                        action_status = status,
                        native_contract = "GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.appearance(optional)->BuildingSkinMenu.shortest_exact_clicks->BuildingSkinMenu.Ok"
                    });
                }
            }
        }
        return new
        {
            projection_status = "complete_live_native_building_skin_catalog",
            default_skin_key = DefaultBuildingSkinKey,
            rows = rows.ToArray()
        };
    }

    private static bool HasNativeBuildingPaintPermission(Building building, Farmer player)
    {
        if (!(building.isCabin || building.HasIndoorsName("Farmhouse")) || building.GetIndoors() is not FarmHouse house)
            return true;
        return house.IsOwnedByCurrentPlayer || house.OwnerId.ToString() == player.spouse;
    }
}
