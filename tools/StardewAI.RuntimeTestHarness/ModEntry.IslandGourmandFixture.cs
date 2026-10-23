using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupIslandGourmandFixture(
        TrainingExecutionRequest request)
    {
        if (request.GourmandRequestIndexBefore is < 0 or > 2 ||
            !request.GourmandGoldenWalnutsFoundBefore.HasValue ||
            request.GourmandGoldenWalnutsFoundBefore is < 0 or > 125 ||
            !request.GourmandTalkedBefore.HasValue)
        {
            return GourmandFixtureBlocked(
                request,
                "gourmand_fixture_parameters_invalid");
        }
        if (Game1.getLocationFromName("IslandFarmCave") is not IslandFarmCave cave ||
            Game1.getLocationFromName("IslandWest") is not IslandWest west)
        {
            return GourmandFixtureBlocked(
                request,
                "gourmand_fixture_location_missing");
        }
        if (Game1.eventUp || cave.currentEvent is not null)
        {
            return GourmandFixtureBlocked(
                request,
                "gourmand_fixture_event_active");
        }

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        var mutex = GourmandMutex(cave);
        if (mutex?.IsLockHeld() == true)
            mutex.ReleaseLock();
        if (mutex is null || mutex.IsLocked())
            return GourmandFixtureBlocked(request, "gourmand_fixture_mutex_unavailable");

        var requestIndex = request.GourmandRequestIndexBefore.GetValueOrDefault();
        var cropItemId = GourmandFixtureCropItemId(requestIndex);
        var seedItemId = GourmandFixtureSeedItemId(requestIndex);
        ClearGourmandFixtureCrops(west);
        var cropTile = FindGourmandFixtureCropTile(west);
        if (!cropTile.HasValue)
            return GourmandFixtureBlocked(request, "gourmand_fixture_crop_tile_missing");

        var crop = new Crop(seedItemId, cropTile.Value.X, cropTile.Value.Y, west);
        crop.growCompletely();
        west.terrainFeatures[cropTile.Value.ToVector2()] = new HoeDirt(0, west)
        {
            crop = crop
        };

        Game1.MasterPlayer.mailReceived.Add("Island_UpgradeHouse");
        SetGourmandFixtureMail(
            Game1.MasterPlayer,
            "talkedToGourmand",
            request.GourmandTalkedBefore.Value);
        cave.gourmandRequestsFulfilled.Value = requestIndex;
        cave.triggeredGourmand = false;
        foreach (var key in new[] { "IslandGourmand1", "IslandGourmand2", "IslandGourmand3" })
            Game1.player.team.collectedNutTracker.Remove(key);
        for (var index = cave.debris.Count - 1; index >= 0; index--)
        {
            if (DebrisQualifiedItemId(cave.debris[index]) == "(O)73")
                cave.debris.RemoveAt(index);
        }
        Game1.netWorldState.Value.GoldenWalnutsFound =
            request.GourmandGoldenWalnutsFoundBefore.Value;
        Game1.netWorldState.Value.GoldenWalnuts = 0;

        Game1.currentLocation = cave;
        Game1.player.currentLocation = cave;
        cave.resetForPlayerEntry();
        var action = FindGourmandFixtureActionTile(cave);
        var stand = action.HasValue
            ? Neighbors(action.Value).FirstOrDefault(tile =>
                IsTileOnMap(cave, tile) &&
                IsTileWalkable(cave, tile) &&
                !IsTileOccupiedByCharacter(cave, tile))
            : Point.Zero;
        if (!action.HasValue || stand == Point.Zero || cave.gourmand is null)
        {
            return GourmandFixtureBlocked(
                request,
                "gourmand_fixture_endpoint_stand_or_npc_missing");
        }

        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.forceCanMove();
        var readyCount = CountReadyGourmandCrops(cropItemId);
        var verified = ReferenceEquals(Game1.currentLocation, cave) &&
            Game1.player.TilePoint == stand &&
            cave.gourmandRequestsFulfilled.Value == requestIndex &&
            Game1.MasterPlayer.hasOrWillReceiveMail("talkedToGourmand") ==
                request.GourmandTalkedBefore.Value &&
            Game1.netWorldState.Value.GoldenWalnutsFound ==
                request.GourmandGoldenWalnutsFoundBefore.Value &&
            readyCount == 1 &&
            crop.indexOfHarvest.Value == cropItemId &&
            CountGourmandWalnutDebris(cave) == 0 &&
            !mutex.IsLocked();

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_island_gourmand",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_gourmand_fixture_installed",
                    "request_index=" + requestIndex,
                    "crop_item_id=" + cropItemId,
                    "talked_before=" + request.GourmandTalkedBefore.Value.ToString().ToLowerInvariant()
                }
                : new[] { "gourmand_fixture_projection_mismatch" },
            RequestedEffect = "island_gourmand.fixture=ready",
            ObservedEffect = GourmandObservedEffect(cave) +
                ";ready_matching_crop_count=" + readyCount,
            TargetLocation = cave.NameOrUniqueName,
            TargetTileX = action.Value.X,
            TargetTileY = action.Value.Y,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "gourmand_fixture_projection_mismatch" }
        };
    }

    private static Point? FindGourmandFixtureCropTile(IslandWest west)
    {
        var back = west.Map?.GetLayer("Back");
        if (back is null)
            return null;
        for (var y = 0; y < back.LayerHeight; y++)
        for (var x = 0; x < back.LayerWidth; x++)
        {
            var tile = new Point(x, y);
            var vector = tile.ToVector2();
            if (west.objects.ContainsKey(vector) || west.terrainFeatures.ContainsKey(vector) ||
                west.doesTileHaveProperty(x, y, "Diggable", "Back") != "T" ||
                !IsTileOnMap(west, tile) || !IsTileWalkable(west, tile))
                continue;
            return tile;
        }
        return null;
    }

    private static Point? FindGourmandFixtureActionTile(IslandFarmCave cave)
    {
        var buildings = cave.Map?.GetLayer("Buildings");
        if (buildings is null)
            return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            if (cave.doesTileHaveProperty(x, y, "Action", "Buildings") == "Gourmand")
                return new Point(x, y);
        }
        return null;
    }

    private static void ClearGourmandFixtureCrops(IslandWest west)
    {
        var itemIds = new HashSet<string>(StringComparer.Ordinal) { "254", "262", "248" };
        foreach (var pair in west.terrainFeatures.Pairs.Where(pair =>
                     pair.Value is HoeDirt { crop: not null } dirt &&
                     itemIds.Contains(dirt.crop.indexOfHarvest.Value)).ToArray())
        {
            west.terrainFeatures.Remove(pair.Key);
        }
    }

    private static void SetGourmandFixtureMail(Farmer farmer, string mailId, bool present)
    {
        while (farmer.mailReceived.Remove(mailId)) { }
        farmer.mailForTomorrow.RemoveWhere(value =>
            value.Split(new[] { "%&NL&%" }, StringSplitOptions.None)[0] == mailId);
        if (present)
            farmer.mailReceived.Add(mailId);
    }

    private static string GourmandFixtureCropItemId(int requestIndex) => requestIndex switch
    {
        0 => "254",
        1 => "262",
        2 => "248",
        _ => string.Empty
    };

    private static string GourmandFixtureSeedItemId(int requestIndex) => requestIndex switch
    {
        0 => "479",
        1 => "483",
        2 => "476",
        _ => string.Empty
    };

    private static TrainingExecutionResult GourmandFixtureBlocked(
        TrainingExecutionRequest request,
        string reason) =>
        BlockedWithPrimitive(
            request,
            "debug_setup_island_gourmand",
            "island_gourmand.fixture=ready",
            GourmandObservedEffect(
                Game1.getLocationFromName("IslandFarmCave") as IslandFarmCave),
            reason);
}
