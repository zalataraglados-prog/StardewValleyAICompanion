using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewAI.TransparentBridge.State;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const int NativeBuildingPaintSliderWidth = 284;

    private static object ReadBuildingPaintCatalog(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady)
            return new { projection_status = "unavailable_world", rows = Array.Empty<object>() };
        if (SnapshotProfileContext.Current is not "full")
            return new { projection_status = "blocked_requires_full_profile", rows = Array.Empty<object>() };

        var paintData = DataLoader.PaintData(Game1.content);
        var service = FindBuildingService("Robin");
        var currentAtService = service.Location is not null && ReferenceEquals(Game1.currentLocation, service.Location);
        var rows = new List<object>();
        foreach (var location in Game1.locations.Where(value => value.IsBuildableLocation()).OrderBy(value => value.NameOrUniqueName, StringComparer.Ordinal))
        {
            foreach (var building in location.buildings.OrderBy(value => value.tileY.Value).ThenBy(value => value.tileX.Value).ThenBy(value => value.buildingType.Value, StringComparer.Ordinal))
            {
                if (!building.CanBePainted() || !HasNativeBuildingPaintPermission(building, player))
                    continue;
                var key = building.GetPaintDataKey(paintData);
                if (key is null || !paintData.TryGetValue(key, out var raw))
                    continue;
                var regions = ParseBuildingPaintRegions(raw);
                var colors = building.netBuildingPaintColor.Value;
                for (var index = 0; index < regions.Count; index++)
                {
                    var region = regions[index];
                    var current = PaintRegionValues(colors, index);
                    var status = building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0
                        ? "building_construction_or_upgrade_active"
                        : service.Location is null || !service.ActionTile.HasValue
                            ? "carpenter_service_action_missing"
                            : !currentAtService
                                ? "route_to_carpenter_service_required"
                                : !service.OwnerReady
                                    ? "robin_not_present_at_service"
                                    : Game1.activeClickableMenu is not null || Game1.dialogueUp
                                        ? "carpenter_menu_or_dialogue_not_clear"
                                        : "ready_for_native_building_paint";
                    rows.Add(new
                    {
                        building_identity = location.NameOrUniqueName + ":" + building.buildingType.Value + ":" + building.tileX.Value + "," + building.tileY.Value,
                        building_location_id = location.NameOrUniqueName,
                        building_type = building.buildingType.Value,
                        building_tile_x = building.tileX.Value,
                        building_tile_y = building.tileY.Value,
                        building_owner_id = building.owner.Value,
                        permission_to_paint = true,
                        paint_data_key = key,
                        paint_region_count = regions.Count,
                        paint_region_index = index,
                        paint_region_id = region.Id,
                        paint_region_display_name = region.DisplayName,
                        hue_min = 0,
                        hue_max = 360,
                        saturation_min = 0,
                        saturation_max = 75,
                        lightness_min = region.MinBrightness,
                        lightness_max = region.MaxBrightness,
                        native_slider_logical_width = NativeBuildingPaintSliderWidth,
                        hue_mouse_reachable_values = NativeMouseReachableSliderValues(0, 360),
                        saturation_mouse_reachable_values = NativeMouseReachableSliderValues(0, 75),
                        lightness_mouse_reachable_values = NativeMouseReachableSliderValues(region.MinBrightness, region.MaxBrightness),
                        default_displayed_hue = 0,
                        default_displayed_saturation = 75,
                        default_displayed_lightness = (region.MinBrightness + region.MaxBrightness) / 2,
                        current_default = current.Default,
                        current_hue = current.Hue,
                        current_saturation = current.Saturation,
                        current_lightness = current.Lightness,
                        service_location_id = service.Location?.NameOrUniqueName ?? string.Empty,
                        service_action_raw = service.ActionRaw,
                        service_action_tile_x = service.ActionTile?.X,
                        service_action_tile_y = service.ActionTile?.Y,
                        robin_present_at_service = service.OwnerReady,
                        action_status = status,
                        native_contract = "GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.region_navigation->native_slider_or_default_clicks->BuildingPaintMenu.Ok"
                    });
                }
            }
        }
        return new { projection_status = "complete_live_native_building_paint_catalog", rows = rows.ToArray() };
    }

    private static List<(string Id, string DisplayName, int MinBrightness, int MaxBrightness)> ParseBuildingPaintRegions(string raw)
    {
        var parts = raw.Replace("\n", string.Empty).Replace("\t", string.Empty).Split('/');
        var result = new List<(string, string, int, int)>();
        for (var index = 0; index < parts.Length / 2; index++)
        {
            var id = parts[index * 2];
            if (string.IsNullOrWhiteSpace(id))
                continue;
            var bounds = ArgUtility.SplitBySpace(parts[index * 2 + 1]);
            var min = bounds.Length >= 2 && int.TryParse(bounds[0], out var parsedMin) ? parsedMin : -100;
            var max = bounds.Length >= 2 && int.TryParse(bounds[1], out var parsedMax) ? parsedMax : 100;
            var display = Game1.content.LoadStringReturnNullIfNotFound("Strings/Buildings:Paint_Region_" + id) ?? id;
            result.Add((id, display, min, max));
        }
        return result;
    }

    private static (bool Default, int Hue, int Saturation, int Lightness) PaintRegionValues(BuildingPaintColor colors, int index) => index switch
    {
        0 => (colors.Color1Default.Value, colors.Color1Hue.Value, colors.Color1Saturation.Value, colors.Color1Lightness.Value),
        1 => (colors.Color2Default.Value, colors.Color2Hue.Value, colors.Color2Saturation.Value, colors.Color2Lightness.Value),
        _ => (colors.Color3Default.Value, colors.Color3Hue.Value, colors.Color3Saturation.Value, colors.Color3Lightness.Value)
    };

    private static int[] NativeMouseReachableSliderValues(int min, int max)
    {
        var values = new SortedSet<int>();
        for (var offset = 0; offset < NativeBuildingPaintSliderWidth; offset++)
            values.Add(min + (int)((double)offset / NativeBuildingPaintSliderWidth * (max - min)));
        return values.ToArray();
    }
}
