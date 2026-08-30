using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupGeodeProcessingFixture(TrainingExecutionRequest request)
    {
        if (!Context.IsWorldReady || !RuntimeLockedBaseGeodeIds.Contains(request.GeodeQualifiedItemId))
            return GeodeFixtureBlocked(request, "geode_processing_fixture_world_or_input_invalid");
        var blacksmith = Game1.getLocationFromName("Blacksmith");
        var target = blacksmith is null ? null : FindGeodeCounterTile(blacksmith);
        var stand = target.HasValue ? Neighbors(target.Value).FirstOrDefault(tile => IsTileOnMap(blacksmith!, tile) &&
            IsTileWalkable(blacksmith!, tile) && !IsTileOccupiedByCharacter(blacksmith!, tile)) : Point.Zero;
        if (blacksmith is null || !target.HasValue || stand == Point.Zero)
            return GeodeFixtureBlocked(request, "geode_processing_fixture_counter_endpoint_missing");
        var clint = Game1.getCharacterFromName("Clint", mustBeVillager: false);
        if (clint is null) return GeodeFixtureBlocked(request, "geode_processing_fixture_clint_missing");

        Game1.exitActiveMenu(); StopAllMovement(); Game1.dialogueUp = false; Game1.eventUp = false;
        Game1.player.controller = null; Game1.player.UsingTool = false; Game1.player.forceCanMove(); Game1.freezeControls = false;
        for (var index = 0; index < Game1.player.Items.Count; index++) Game1.player.Items[index] = null;
        var stack = Math.Max(1, request.GeodeStackBefore ?? 2);
        Game1.player.Items[0] = ItemRegistry.Create(request.GeodeQualifiedItemId, stack);
        Game1.player.CurrentToolIndex = 0;
        Game1.player.Money = Math.Max(1000, request.GeodeMoneyBefore ?? 1000);
        Game1.stats.GeodesCracked = (uint)Math.Max(0, request.GeodesCrackedBefore ?? 0);
        Game1.stats.Set("MysteryBoxesOpened", (uint)Math.Max(0, request.MysteryBoxesOpenedBefore ?? 0));
        Game1.netWorldState.Value.GoldenCoconutCracked = request.GoldenCoconutCrackedBefore ?? false;
        if (request.GeodeGotMysteryBookMailBefore == true) Game1.player.mailReceived.Add("GotMysteryBook");
        else Game1.player.mailReceived.Remove("GotMysteryBook");
        if (request.GeodeArtifactFoundMailBefore == true) Game1.player.mailReceived.Add("artifactFound");
        else Game1.player.mailReceived.Remove("artifactFound");
        Game1.player.toolBeingUpgraded.Value = null; Game1.player.daysLeftForToolUpgrade.Value = 0;
        clint.currentLocation?.characters.Remove(clint);
        if (!blacksmith.characters.Contains(clint)) blacksmith.characters.Add(clint);
        clint.currentLocation = blacksmith; clint.Position = new Vector2(target.Value.X, target.Value.Y - 1) * Game1.tileSize;
        clint.controller = null; clint.Halt(); clint.ignoreScheduleToday = true; clint.followSchedule = false; clint.IsInvisible = false;
        Game1.currentLocation = blacksmith; Game1.player.currentLocation = blacksmith;
        Game1.player.Position = stand.ToVector2() * Game1.tileSize; Game1.player.faceDirection(DirectionTo(stand, target.Value));
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId, Status = "applied", FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_geode_processing", PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "isolated_Blacksmith_Clint_counter_inventory_money_and_counter_fixture_ready" },
            RequestedEffect = "geode_processing_fixture=ready", ObservedEffect = GeodeObservedEffect()
        };
    }

    private static Point? FindGeodeCounterTile(GameLocation location)
    {
        var layer = location.map?.GetLayer("Buildings");
        if (layer is null) return null;
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
            if (location.doesTileHaveProperty(x, y, "Action", "Buildings")?.Split(' ')[0] == "Blacksmith")
                return new Point(x, y);
        return null;
    }

    private static TrainingExecutionResult GeodeFixtureBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "debug_setup_geode_processing", "geode_processing_fixture=ready", GeodeObservedEffect(), reason);
}
