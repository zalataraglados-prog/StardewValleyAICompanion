using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupBuildingPaintFixture(TrainingExecutionRequest request)
    {
        var house = Game1.getLocationFromName("ScienceHouse");
        var action = house is null ? null : FarmhouseFixtureActionTile(house);
        var stand = house is null || !action.HasValue ? null : FarmhouseFixtureStandTile(house, action.Value);
        var robinTile = house is null || !action.HasValue || !stand.HasValue ? null : FarmhouseFixtureRobinTile(house, action.Value, stand.Value);
        var robin = Game1.getCharacterFromName("Robin");
        var farm = Game1.getFarm();
        var building = farm.buildings.FirstOrDefault(value => value.buildingType.Value == "Farmhouse" && value.CanBePainted());
        if (house is null || !action.HasValue || !stand.HasValue || !robinTile.HasValue || robin is null || building is null || !building.CanBePainted())
            return BlockedWithPrimitive(request, "debug_setup_building_paint", "building_paint_fixture=ready", "building=" + (building?.buildingType.Value ?? "missing"), "building_paint_fixture_unavailable");

        StopAllMovement();
        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.timeOfDay = 1200;
        building.daysOfConstructionLeft.Value = 0;
        building.daysUntilUpgrade.Value = 0;
        var colors = building.netBuildingPaintColor.Value;
        colors.Color1Default.Value = true;
        colors.Color2Default.Value = true;
        colors.Color3Default.Value = true;
        robin.currentLocation?.characters.Remove(robin);
        house.characters.Remove(robin);
        robin.Position = robinTile.Value.ToVector2() * Game1.tileSize;
        house.characters.Add(robin);
        Game1.currentLocation = house;
        Game1.player.currentLocation = house;
        Game1.player.Position = stand.Value.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.faceDirection(DirectionTo(stand.Value, action.Value));
        robin.currentLocation = house;
        robin.controller = null;
        robin.Halt();
        robin.ignoreScheduleToday = true;
        robin.followSchedule = false;
        robin.isSleeping.Value = false;
        robin.IsInvisible = false;

        var verified = Game1.player.TilePoint == stand.Value && house.characters.Contains(robin) && building.CanBePainted() &&
            colors.Color1Default.Value && colors.Color2Default.Value && colors.Color3Default.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId, Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_building_paint", PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_save_fixture_ready", "native_Carpenter_action_and_Robin_ready", "paintable_Farmhouse_all_regions_default_ready" } : new[] { "building_paint_fixture_post_state_mismatch" },
            RequestedEffect = "building_paint_fixture=ready",
            ObservedEffect = "building=Farm:Farmhouse:" + building.tileX.Value + "," + building.tileY.Value + ";paint_colors_default=true",
            TargetLocation = "ScienceHouse", TargetTileX = action.Value.X, TargetTileY = action.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "building_paint_fixture_post_state_mismatch" }
        };
    }
}
