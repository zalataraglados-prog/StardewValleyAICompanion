using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFieldOfficeDonationFixture(TrainingExecutionRequest request)
    {
        if (request.FieldOfficeTargetPieceIndex is not (>= 0 and < IslandFieldOffice.totalPieces) ||
            !request.FieldOfficeCompletesSet.HasValue || !request.FieldOfficeGoldenWalnutsFoundBefore.HasValue ||
            request.FieldOfficeGoldenWalnutsFoundBefore is < 0 or > 130)
            return FieldOfficeFixtureBlocked(request, "field_office_fixture_parameters_invalid");
        if (Game1.getLocationFromName("IslandFieldOffice") is not IslandFieldOffice office)
            return FieldOfficeFixtureBlocked(request, "field_office_fixture_location_missing");

        if (Game1.activeClickableMenu is not null)
            Game1.exitActiveMenu();
        if (office.safariGuyMutex.IsLockHeld())
            office.safariGuyMutex.ReleaseLock();
        while (office.piecesDonated.Count < IslandFieldOffice.totalPieces)
            office.piecesDonated.Add(false);
        for (var index = 0; index < IslandFieldOffice.totalPieces; index++)
            office.piecesDonated[index] = false;
        office.centerSkeletonRestored.Value = false;
        office.snakeRestored.Value = false;
        office.batRestored.Value = false;
        office.frogRestored.Value = false;
        office.plantsRestoredLeft.Value = false;
        office.plantsRestoredRight.Value = false;
        office.hasFailedSurveyToday.Value = false;
        office.uncollectedRewards.Clear();

        var target = request.FieldOfficeTargetPieceIndex.Value;
        if (request.FieldOfficeCompletesSet == true)
        {
            foreach (var index in FieldOfficeFixtureSetIndexes(target))
            {
                if (index != target)
                    office.piecesDonated[index] = true;
            }
        }
        else if (target == 2)
        {
            office.piecesDonated[0] = true;
        }
        else if (target == 6)
        {
            office.piecesDonated[7] = true;
        }
        foreach (var key in new[]
        {
            "IslandCenterSkeletonRestored", "IslandSnakeRestored", "IslandBatRestored", "IslandFrogRestored"
        })
            Game1.player.team.collectedNutTracker.Remove(key);
        Game1.netWorldState.Value.GoldenWalnutsFound = request.FieldOfficeGoldenWalnutsFoundBefore.Value;
        Game1.player.mailReceived.Add("islandNorthCaveOpened");
        Game1.player.mailReceived.Add("safariGuyIntro");
        Game1.player.mailReceived.Remove("fieldOfficeFinale");
        Game1.player.mailForTomorrow.Remove("fieldOfficeFinale");

        var slot = request.InventorySlotIndex ?? 11;
        if (slot < 0 || slot >= Game1.player.Items.Count)
            return FieldOfficeFixtureBlocked(request, "field_office_fixture_inventory_slot_invalid");
        var qualifiedItemId = FieldOfficeFixtureItemId(target);
        Game1.player.Items[slot] = ItemRegistry.Create(qualifiedItemId, 2);

        Game1.currentLocation = office;
        Game1.player.currentLocation = office;
        office.resetForPlayerEntry();
        var action = FieldOfficeFixtureDeskTile(office);
        var stand = action.HasValue ? FieldOfficeFixtureStandTile(office, action.Value) : null;
        if (!action.HasValue || !stand.HasValue || office.getSafariGuy() is null)
            return FieldOfficeFixtureBlocked(request, "field_office_fixture_native_endpoint_or_professor_missing");
        Game1.player.Position = stand.Value.ToVector2() * Game1.tileSize;
        Game1.player.forceCanMove();
        Game1.player.CurrentToolIndex = slot;

        var expectedBefore = office.piecesDonated.Count(value => value);
        var verified = office.piecesDonated.Count(value => value) == expectedBefore && !office.piecesDonated[target] &&
            office.uncollectedRewards.Count == 0 && Game1.player.Items[slot]?.QualifiedItemId == qualifiedItemId &&
            Game1.player.Items[slot]?.Stack == 2 && ReferenceEquals(Game1.currentLocation, office) &&
            Game1.player.TilePoint == stand.Value && !office.safariGuyMutex.IsLocked();
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_field_office_donation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_field_office_fixture_installed", "target_piece=" + target, "completes_set=" + request.FieldOfficeCompletesSet.Value.ToString().ToLowerInvariant() }
                : new[] { "field_office_fixture_projection_mismatch" },
            RequestedEffect = "field_office.fixture=ready",
            ObservedEffect = FieldOfficeObservedEffect(office, slot),
            TargetLocation = office.NameOrUniqueName,
            TargetTileX = action.Value.X,
            TargetTileY = action.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "field_office_fixture_projection_mismatch" }
        };
    }

    private static int[] FieldOfficeFixtureSetIndexes(int piece) => piece switch
    {
        >= 0 and <= 5 => new[] { 0, 1, 2, 3, 4, 5 },
        >= 6 and <= 8 => new[] { 6, 7, 8 },
        9 => new[] { 9 },
        10 => new[] { 10 },
        _ => Array.Empty<int>()
    };

    private static string FieldOfficeFixtureItemId(int piece) => piece switch
    {
        0 or 2 => "(O)823",
        1 => "(O)824",
        3 => "(O)822",
        4 => "(O)821",
        5 => "(O)820",
        6 or 7 => "(O)826",
        8 => "(O)825",
        9 => "(O)827",
        10 => "(O)828",
        _ => throw new ArgumentOutOfRangeException(nameof(piece))
    };

    private static Point? FieldOfficeFixtureDeskTile(IslandFieldOffice office)
    {
        var buildings = office.Map?.GetLayer("Buildings");
        if (buildings is null)
            return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                if (office.doesTileHaveProperty(x, y, "Action", "Buildings") == "FieldOfficeDesk")
                    return new Point(x, y);
            }
        }
        return null;
    }

    private static Point? FieldOfficeFixtureStandTile(IslandFieldOffice office, Point action)
    {
        foreach (var tile in new[]
        {
            new Point(action.X, action.Y + 1), new Point(action.X - 1, action.Y),
            new Point(action.X + 1, action.Y), new Point(action.X, action.Y - 1)
        })
        {
            if (IsTileOnMap(office, tile) && IsTileWalkable(office, tile) && !IsTileOccupiedByCharacter(office, tile))
                return tile;
        }
        return null;
    }

    private static TrainingExecutionResult FieldOfficeFixtureBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "debug_setup_field_office_donation", "field_office.fixture=ready",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none"), reason);
}
