using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPlayerCustomizationFixture(TrainingExecutionRequest request)
    {
        if (!Context.IsWorldReady || request.CustomizationMode is not ("wizard_shrine" or "desert_makeover"))
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_world_or_mode_invalid");

        Game1.exitActiveMenu();
        StopAllMovement();
        Game1.dialogueUp = false;
        Game1.player.controller = null;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        Game1.freezeControls = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        return request.CustomizationMode == "wizard_shrine"
            ? SetupWizardCustomizationFixture(request)
            : SetupDesertMakeoverFixture(request);
    }

    private TrainingExecutionResult SetupWizardCustomizationFixture(TrainingExecutionRequest request)
    {
        var basement = Game1.getLocationFromName("WizardHouseBasement");
        var target = basement is null ? null : FindPlayerCustomizationTile(basement, "Action", "Buildings", "WizardShrine");
        var stand = target.HasValue
            ? Neighbors(target.Value).FirstOrDefault(tile => IsTileOnMap(basement!, tile) &&
                IsTileWalkable(basement!, tile) && !IsTileOccupiedByCharacter(basement!, tile))
            : Point.Zero;
        if (basement is null || !target.HasValue || stand == Point.Zero)
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_wizard_endpoint_missing");

        Game1.player.Money = Math.Max(Game1.player.Money, 1000);
        Game1.currentLocation = basement;
        Game1.player.currentLocation = basement;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.faceDirection(DirectionTo(stand, target.Value));
        Game1.player.forceCanMove();
        return PlayerCustomizationFixtureApplied(request,
            "isolated_WizardHouseBasement_native_shrine_endpoint_and_funds_ready");
    }

    private TrainingExecutionResult SetupDesertMakeoverFixture(TrainingExecutionRequest request)
    {
        Game1.MasterPlayer.mailReceived.Add("ccVault");
        Game1.currentSeason = "spring";
        Game1.dayOfMonth = 15;
        Game1.timeOfDay = 1200;
        Game1.UpdatePassiveFestivalStates();
        if (!Game1.netWorldState.Value.ActivePassiveFestivals.Contains("DesertFestival"))
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_desert_festival_not_activated");
        Game1.PerformPassiveFestivalSetup();

        var desert = Game1.getLocationFromName("DesertFestival") as DesertFestival;
        var target = desert is null ? null : FindPlayerCustomizationTile(desert, "TouchAction", "Back", "DesertMakeover");
        if (desert is null || !target.HasValue)
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_desert_location_or_touch_missing");

        var emily = Game1.getCharacterFromName("Emily", mustBeVillager: false);
        var sandy = Game1.getCharacterFromName("Sandy", mustBeVillager: false);
        if (emily is null || sandy is null)
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_desert_stylists_missing");
        MovePlayerCustomizationFixtureNpc(emily, desert, new Point(25, 52));
        MovePlayerCustomizationFixtureNpc(sandy, desert, new Point(22, 52));

        Game1.player.activeDialogueEvents.Remove("DesertMakeover");
        for (var index = 0; index < Game1.player.Items.Count; index++)
            Game1.player.Items[index] = null;
        Game1.player.CurrentToolIndex = 0;
        Game1.currentLocation = desert;
        Game1.player.currentLocation = desert;
        var start = new Point(target.Value.X, target.Value.Y - 1);
        if (!IsTileOnMap(desert, start) || !IsTileWalkable(desert, start) ||
            !IsTileOnMap(desert, target.Value) || !IsTileWalkable(desert, target.Value))
            return PlayerCustomizationFixtureBlocked(request, "player_customization_fixture_desert_start_or_touch_not_walkable");
        Game1.player.Position = start.ToVector2() * Game1.tileSize;
        Game1.player.faceDirection(DirectionTo(start, target.Value));
        Game1.player.forceCanMove();
        return PlayerCustomizationFixtureApplied(request,
            "isolated_DesertFestival_native_touch_stylists_inventory_and_daily_gate_ready");
    }

    private static void MovePlayerCustomizationFixtureNpc(NPC npc, GameLocation location, Point tile)
    {
        npc.currentLocation?.characters.Remove(npc);
        if (!location.characters.Contains(npc)) location.characters.Add(npc);
        npc.currentLocation = location;
        npc.Position = tile.ToVector2() * Game1.tileSize;
        npc.controller = null;
        npc.Halt();
        npc.ignoreScheduleToday = true;
        npc.followSchedule = false;
        npc.IsInvisible = false;
    }

    private static Point? FindPlayerCustomizationTile(
        GameLocation location, string property, string layerName, string token)
    {
        var layer = location.map?.GetLayer(layerName);
        if (layer is null) return null;
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var raw = location.doesTileHaveProperty(x, y, property, layerName);
            if (raw?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() == token)
                return new Point(x, y);
        }
        return null;
    }

    private static TrainingExecutionResult PlayerCustomizationFixtureApplied(
        TrainingExecutionRequest request, string verificationReason) => new()
    {
        RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
        BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
        Status = "applied", FeedbackAvailable = true,
        StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
        PrimitiveKind = "debug_setup_player_customization", PrimitiveVerificationStatus = "verified",
        PrimitiveVerificationReasons = new[] { verificationReason }, RequestedEffect = "player_customization_fixture=ready",
        ObservedEffect = PlayerCustomizationObservedEffect()
    };

    private static TrainingExecutionResult PlayerCustomizationFixtureBlocked(
        TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "debug_setup_player_customization", "player_customization_fixture=ready",
            PlayerCustomizationObservedEffect(), reason);
}
