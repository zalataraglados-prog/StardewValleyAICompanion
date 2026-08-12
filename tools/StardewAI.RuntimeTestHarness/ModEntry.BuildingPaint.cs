using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool BuildingPaintRequestExact(TrainingExecutionRequest request, Building? building, out string reason)
    {
        reason = "paint_building_region_typed_request_invalid";
        if (building is null || request.NativeContract != BuildingPaintNativeContract || request.BuilderActionRaw != "Carpenter" ||
            string.IsNullOrWhiteSpace(request.AppearanceReason) || request.BuildingIdentity != request.BuildingLocationId + ":" + request.BuildingType + ":" + request.BuildingTileX + "," + request.BuildingTileY ||
            !building.CanBePainted() || !HasRuntimeBuildingPaintPermission(building) || building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0 ||
            request.PaintTargetMode is not ("custom" or "default") || !request.PaintRegionIndex.HasValue || !request.PaintRegionCount.HasValue || request.NativePaintSliderLogicalWidth != 284)
            return false;
        var paintData = DataLoader.PaintData(Game1.content);
        var key = building.GetPaintDataKey(paintData);
        if (key != request.PaintDataKey || key is null || !paintData.TryGetValue(key, out var raw))
            return false;
        var regions = ParseRuntimeBuildingPaintRegions(raw);
        if (request.PaintRegionCount != regions.Count || request.PaintRegionIndex < 0 || request.PaintRegionIndex >= regions.Count)
            return false;
        var region = regions[request.PaintRegionIndex.Value];
        var current = CaptureBuildingPaint(building)[request.PaintRegionIndex.Value];
        if (region.Id != request.PaintRegionId || request.PaintHueMin != 0 || request.PaintHueMax != 360 ||
            request.PaintSaturationMin != 0 || request.PaintSaturationMax != 75 || request.PaintLightnessMin != region.Min || request.PaintLightnessMax != region.Max ||
            request.CurrentPaintDefault != current.Default || request.CurrentPaintHue != current.Hue || request.CurrentPaintSaturation != current.Saturation || request.CurrentPaintLightness != current.Lightness)
            return false;
        if (request.PaintTargetMode == "default")
        {
            if (current.Default) return false;
        }
        else if (!request.TargetPaintHue.HasValue || !request.TargetPaintSaturation.HasValue || !request.TargetPaintLightness.HasValue ||
                 !NativePaintSliderValueReachable(0, 360, request.TargetPaintHue.Value, 284) ||
                 !NativePaintSliderValueReachable(0, 75, request.TargetPaintSaturation.Value, 284) ||
                 !NativePaintSliderValueReachable(region.Min, region.Max, request.TargetPaintLightness.Value, 284) ||
                 current.Default && request.TargetPaintHue == 0 && request.TargetPaintSaturation == 75 && request.TargetPaintLightness == (region.Min + region.Max) / 2 ||
                 !current.Default && request.TargetPaintHue == current.Hue && request.TargetPaintSaturation == current.Saturation && request.TargetPaintLightness == current.Lightness)
            return false;
        reason = string.Empty;
        return true;
    }

    private static bool HasRuntimeBuildingPaintPermission(Building building)
    {
        if (!(building.isCabin || building.HasIndoorsName("Farmhouse")) || building.GetIndoors() is not StardewValley.Locations.FarmHouse house)
            return true;
        return house.IsOwnedByCurrentPlayer || house.OwnerId.ToString() == Game1.player.spouse;
    }

    private static List<(string Id, int Min, int Max)> ParseRuntimeBuildingPaintRegions(string raw)
    {
        var parts = raw.Replace("\n", string.Empty).Replace("\t", string.Empty).Split('/');
        var result = new List<(string, int, int)>();
        for (var index = 0; index < parts.Length / 2; index++)
        {
            var id = parts[index * 2];
            if (string.IsNullOrWhiteSpace(id)) continue;
            var bounds = ArgUtility.SplitBySpace(parts[index * 2 + 1]);
            var min = bounds.Length >= 2 && int.TryParse(bounds[0], out var parsedMin) ? parsedMin : -100;
            var max = bounds.Length >= 2 && int.TryParse(bounds[1], out var parsedMax) ? parsedMax : 100;
            result.Add((id, min, max));
        }
        return result;
    }

    private static bool NativePaintSliderValueReachable(int min, int max, int target, int width)
    {
        for (var offset = 0; offset < width; offset++)
            if (min + (int)((float)offset / width * (max - min)) == target) return true;
        return false;
    }

    private static (bool Default, int Hue, int Saturation, int Lightness)[] CaptureBuildingPaint(Building building)
    {
        var colors = building.netBuildingPaintColor.Value;
        return new[]
        {
            (colors.Color1Default.Value, colors.Color1Hue.Value, colors.Color1Saturation.Value, colors.Color1Lightness.Value),
            (colors.Color2Default.Value, colors.Color2Hue.Value, colors.Color2Saturation.Value, colors.Color2Lightness.Value),
            (colors.Color3Default.Value, colors.Color3Hue.Value, colors.Color3Saturation.Value, colors.Color3Lightness.Value)
        };
    }

    private static bool AdvanceBuildingPaintMenu(ActiveBuildingAppearanceChange active, BuildingPaintMenu menu, out string reason)
    {
        var request = active.Pending.Request;
        reason = string.Empty;
        var regionIndex = request.PaintRegionIndex;
        if (!ReferenceEquals(menu.building, active.Building) || menu.regions.Count != request.PaintRegionCount || !regionIndex.HasValue ||
            regionIndex < 0 || regionIndex >= menu.regions.Count || menu.regions[regionIndex.Value].Id != request.PaintRegionId)
        {
            reason = "paint_building_region_live_menu_projection_drifted";
            return false;
        }
        if (menu.currentPaintRegion != regionIndex.Value)
        {
            var count = menu.regions.Count;
            var next = (regionIndex.Value - menu.currentPaintRegion + count) % count;
            var previous = (menu.currentPaintRegion - regionIndex.Value + count) % count;
            var button = next <= previous ? menu.nextRegionButton : menu.previousRegionButton;
            menu.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y);
            active.PaintRegionClicks++;
            active.Cooldown = 2;
            return false;
        }
        if (menu.colorSliderPanel.hueSlider.bounds.Width != request.NativePaintSliderLogicalWidth ||
            menu.colorSliderPanel.hueSlider.min != request.PaintHueMin || menu.colorSliderPanel.hueSlider.max != request.PaintHueMax ||
            menu.colorSliderPanel.saturationSlider.min != request.PaintSaturationMin || menu.colorSliderPanel.saturationSlider.max != request.PaintSaturationMax ||
            menu.colorSliderPanel.lightnessSlider.min != request.PaintLightnessMin || menu.colorSliderPanel.lightnessSlider.max != request.PaintLightnessMax)
        {
            reason = "paint_building_region_live_slider_bounds_drifted";
            return false;
        }
        if (request.PaintTargetMode == "default")
        {
            if (active.PaintControlStage == 0)
            {
                menu.receiveLeftClick(menu.defaultColorButton.bounds.Center.X, menu.defaultColorButton.bounds.Center.Y);
                active.PaintControlStage++;
                active.Cooldown = 2;
                return false;
            }
        }
        else if (active.PaintControlStage < 3)
        {
            var slider = active.PaintControlStage switch { 0 => menu.colorSliderPanel.hueSlider, 1 => menu.colorSliderPanel.saturationSlider, _ => menu.colorSliderPanel.lightnessSlider };
            var target = active.PaintControlStage switch { 0 => request.TargetPaintHue!.Value, 1 => request.TargetPaintSaturation!.Value, _ => request.TargetPaintLightness!.Value };
            var x = NativePaintSliderClickX(slider, target);
            if (!x.HasValue)
            {
                reason = "paint_building_region_target_not_mouse_reachable_in_live_menu";
                return false;
            }
            menu.receiveLeftClick(x.Value, slider.bounds.Center.Y);
            menu.releaseLeftClick(x.Value, slider.bounds.Center.Y);
            if (slider.GetValue() != target)
            {
                reason = "paint_building_region_native_slider_click_mismatch";
                return false;
            }
            active.PaintControlStage++;
            active.Cooldown = 2;
            return false;
        }
        if (!BuildingPaintTargetMatches(active.Building, request))
        {
            reason = "paint_building_region_target_not_reached_before_ok";
            return false;
        }
        menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
        return true;
    }

    private static int? NativePaintSliderClickX(BuildingPaintMenu.BuildingColorSlider slider, int target)
    {
        for (var x = slider.bounds.Left; x < slider.bounds.Right; x++)
            if (slider.min + (int)((float)(x - slider.bounds.Left) / slider.bounds.Width * (slider.max - slider.min)) == target) return x;
        return null;
    }

    private static bool BuildingPaintTargetMatches(Building building, TrainingExecutionRequest request)
    {
        var actual = CaptureBuildingPaint(building)[request.PaintRegionIndex!.Value];
        return request.PaintTargetMode == "default" ? actual.Default :
            !actual.Default && actual.Hue == request.TargetPaintHue && actual.Saturation == request.TargetPaintSaturation && actual.Lightness == request.TargetPaintLightness;
    }

    private void VerifyBuildingPaintSettlement(ActiveBuildingAppearanceChange active)
    {
        var request = active.Pending.Request;
        var actual = CaptureBuildingPaint(active.Building);
        var siblingsUnchanged = actual.Select((value, index) => index == request.PaintRegionIndex || value == active.InitialPaint[index]).All(value => value);
        if (!BuildingPaintTargetMatches(active.Building, request) || !siblingsUnchanged)
        {
            CompleteBuildingSkinBlocked(active, "paint_building_region_native_postconditions_mismatch");
            return;
        }
        activeBuildingAppearanceChange = null;
        var target = request.PaintTargetMode == "default" ? "default" : request.TargetPaintHue + "," + request.TargetPaintSaturation + "," + request.TargetPaintLightness;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId, Status = "applied", FeedbackAvailable = true, StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"), PrimitiveKind = "paint_building_region", PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "shared_native_Carpenter_Paint_target_flow", "exact_native_region_and_controls_used", "target_region_and_unchanged_sibling_regions_verified" },
            RequestedEffect = "building=" + request.BuildingIdentity + ";region=" + request.PaintRegionId + ";target=" + target,
            ObservedEffect = "region=" + request.PaintRegionId + ";target=" + target + ";sibling_regions_unchanged=true;region_clicks=" + active.PaintRegionClicks,
            ActualTicks = active.ElapsedTicks, TargetLocation = request.BuildingLocationId, TargetTileX = request.BuildingTileX, TargetTileY = request.BuildingTileY
        });
    }
}
