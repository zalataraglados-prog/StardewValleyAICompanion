using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupBuildingSkinFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        var house = Game1.getLocationFromName("ScienceHouse");
        var action = house is null ? null : FarmhouseFixtureActionTile(house);
        var stand = house is null || !action.HasValue ? null : FarmhouseFixtureStandTile(house, action.Value);
        var robinTile = house is null || !action.HasValue || !stand.HasValue ? null : FarmhouseFixtureRobinTile(house, action.Value, stand.Value);
        var robin = Game1.getCharacterFromName("Robin");
        var farm = Game1.getLocationFromName("Farm");
        var bowl = farm?.buildings.FirstOrDefault(value => value.buildingType.Value == "Pet Bowl" && value.CanBeReskinned(ignoreSeparateConstructionEntries: true));
        if (house is null || !action.HasValue || !stand.HasValue || !robinTile.HasValue || robin is null || farm is null || bowl is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_building_skin", "building_skin_fixture=ready",
                "location=" + (house?.NameOrUniqueName ?? "none") + ";pet_bowl=" + (bowl is null ? "missing" : "present"),
                "building_skin_fixture_service_robin_or_pet_bowl_missing");
        }

        StopAllMovement();
        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.timeOfDay = 1200;
        bowl.skinId.Value = null;
        bowl.daysOfConstructionLeft.Value = 0;
        bowl.daysUntilUpgrade.Value = 0;
        var colors = bowl.netBuildingPaintColor.Value;
        colors.Color1Default.Value = false;
        colors.Color2Default.Value = false;
        colors.Color3Default.Value = false;
        robin.currentLocation?.characters.Remove(robin);
        house.characters.Remove(robin);
        robin.Position = new Vector2(robinTile.Value.X * Game1.tileSize, robinTile.Value.Y * Game1.tileSize);
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

        var verified = ReferenceEquals(Game1.currentLocation, house) && Game1.player.TilePoint == stand.Value &&
            house.doesTileHaveProperty(action.Value.X, action.Value.Y, "Action", "Buildings") == "Carpenter" &&
            house.characters.Contains(robin) && bowl.skinId.Value is null &&
            !colors.Color1Default.Value && !colors.Color2Default.Value && !colors.Color3Default.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_building_skin",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_save_fixture_ready", "native_Carpenter_action_and_Robin_ready", "Pet_Bowl_default_skin_with_nondefault_paint_ready" }
                : new[] { "building_skin_fixture_post_state_mismatch" },
            RequestedEffect = "building_skin_fixture=ready",
            ObservedEffect = "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";building=Farm:Pet Bowl:" + bowl.tileX.Value + "," + bowl.tileY.Value + ";skin=__default__;paint_colors_default=false",
            TargetLocation = "ScienceHouse", TargetTileX = action.Value.X, TargetTileY = action.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "building_skin_fixture_post_state_mismatch" }
        };
    }
}
