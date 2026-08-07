using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMuseumDonationFixture(TrainingExecutionRequest request)
    {
        if (!request.ExpectedDonatedCountBefore.HasValue || request.ExpectedDonatedCountBefore.Value < 0 ||
            request.ExpectedDonatedCountBefore.Value >= LibraryMuseum.totalArtifacts)
        {
            return BlockedWithPrimitive(request, "debug_setup_museum_donation", "museum.fixture=ready", "donated_count=invalid", "museum_fixture_donated_count_invalid");
        }
        if (Game1.getLocationFromName("ArchaeologyHouse") is not LibraryMuseum museum)
        {
            return BlockedWithPrimitive(request, "debug_setup_museum_donation", "museum.fixture=ready", "museum=missing", "museum_fixture_location_missing");
        }

        if (Game1.activeClickableMenu is not null)
        {
            Game1.exitActiveMenu();
        }
        var mutex = MuseumMutex(museum);
        if (mutex?.IsLockHeld() == true)
        {
            mutex.ReleaseLock();
        }

        const string targetQualifiedId = "(O)96";
        museum.museumPieces.Clear();
        var donationTiles = MuseumDonationTiles(museum);
        var donatedIds = Game1.objectData.Keys
            .Where(itemId => itemId != "96" && LibraryMuseum.IsItemSuitableForDonation("(O)" + itemId))
            .OrderBy(itemId => int.TryParse(itemId, out var numeric) ? numeric : int.MaxValue)
            .ThenBy(itemId => itemId, StringComparer.Ordinal)
            .Take(request.ExpectedDonatedCountBefore.Value)
            .ToArray();
        if (donationTiles.Length < request.ExpectedDonatedCountBefore.Value + 1 || donatedIds.Length != request.ExpectedDonatedCountBefore.Value)
        {
            return BlockedWithPrimitive(request, "debug_setup_museum_donation", "museum.fixture=ready", "fixture_catalog=insufficient", "museum_fixture_catalog_or_tiles_insufficient");
        }

        for (var index = 0; index < donatedIds.Length; index++)
        {
            museum.museumPieces.Add(donationTiles[index].ToVector2(), donatedIds[index]);
        }

        var rewards = DataLoader.MuseumRewards(Game1.content);
        foreach (var pair in rewards)
        {
            Game1.player.mailReceived.Remove(pair.Key);
            if (pair.Value.RewardItemId is not null)
            {
                var rewardItem = ItemRegistry.Create(pair.Value.RewardItemId, pair.Value.RewardItemCount);
                Game1.player.mailReceived.Remove(museum.getRewardItemKey(rewardItem));
            }
        }
        Game1.player.achievements.Remove(5);
        Game1.MasterPlayer.eventsSeen.Remove("295672");
        Game1.MasterPlayer.eventsSeen.Remove("66");
        Game1.MasterPlayer.hasRustyKey = false;

        Game1.player.removeQuest("24");
        if (request.FieldGuideQuestPresentBefore == true)
        {
            Game1.player.addQuest("24");
        }

        var slot = request.InventorySlotIndex ?? 11;
        if (slot < 0 || slot >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(request, "debug_setup_museum_donation", "museum.fixture=ready", "inventory_slot=invalid", "museum_fixture_inventory_slot_invalid");
        }
        Game1.player.Items[slot] = ItemRegistry.Create(targetQualifiedId, 2);

        var actionTile = MuseumGuntherActionTile(museum);
        var standTile = actionTile.HasValue ? MuseumFixtureStandTile(museum, actionTile.Value) : null;
        if (!actionTile.HasValue || !standTile.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_museum_donation", "museum.fixture=ready", "gunther_endpoint=missing", "museum_fixture_gunther_endpoint_missing");
        }
        Game1.currentLocation = museum;
        Game1.player.currentLocation = museum;
        Game1.player.Position = standTile.Value.ToVector2() * Game1.tileSize;
        Game1.player.forceCanMove();
        Game1.player.CurrentToolIndex = slot;

        var quest = Game1.player.questLog.FirstOrDefault(row => row.id.Value == "24");
        var verified = museum.museumPieces.Count() == request.ExpectedDonatedCountBefore.Value &&
            Game1.player.Items[slot]?.QualifiedItemId == targetQualifiedId && Game1.player.Items[slot]?.Stack == 2 &&
            ReferenceEquals(Game1.currentLocation, museum) && Game1.player.TilePoint == standTile.Value &&
            (quest is not null) == (request.FieldGuideQuestPresentBefore == true) &&
            museum.isTileSuitableForMuseumPiece(donationTiles[request.ExpectedDonatedCountBefore.Value].X, donationTiles[request.ExpectedDonatedCountBefore.Value].Y);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_museum_donation",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_museum_fixture_installed", "donated_count=" + request.ExpectedDonatedCountBefore.Value, "quest24_present=" + (quest is not null).ToString().ToLowerInvariant() }
                : new[] { "museum_fixture_projection_mismatch" },
            RequestedEffect = "museum.fixture=ready",
            ObservedEffect = "donated_count=" + museum.museumPieces.Count() + ";slot=" + slot + ";location=" + museum.NameOrUniqueName,
            TargetLocation = museum.NameOrUniqueName,
            TargetTileX = actionTile.Value.X,
            TargetTileY = actionTile.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "museum_fixture_projection_mismatch" }
        };
    }

    private static Point[] MuseumDonationTiles(LibraryMuseum museum)
    {
        var bounds = museum.getMuseumDonationBounds();
        var tiles = new List<Point>();
        for (var x = bounds.X; x <= bounds.Right; x++)
        {
            for (var y = bounds.Y; y <= bounds.Bottom; y++)
            {
                if (museum.isTileSuitableForMuseumPiece(x, y))
                {
                    tiles.Add(new Point(x, y));
                }
            }
        }
        return tiles.ToArray();
    }

    private static Point? MuseumGuntherActionTile(LibraryMuseum museum)
    {
        var buildings = museum.Map?.GetLayer("Buildings");
        if (buildings is null)
        {
            return null;
        }
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var action = museum.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "Gunther", StringComparison.OrdinalIgnoreCase))
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }

    private static Point? MuseumFixtureStandTile(LibraryMuseum museum, Point actionTile)
    {
        var adjacent = new[]
        {
            new Point(actionTile.X, actionTile.Y - 1),
            new Point(actionTile.X + 1, actionTile.Y),
            new Point(actionTile.X, actionTile.Y + 1),
            new Point(actionTile.X - 1, actionTile.Y)
        };
        foreach (var tile in adjacent)
        {
            if (IsTileOnMap(museum, tile) && IsTileWalkable(museum, tile) && !IsTileOccupiedByCharacter(museum, tile))
            {
                return tile;
            }
        }
        return null;
    }
}
