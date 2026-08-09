using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupPartnershipFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var fixture = request.PartnershipFixtureCase switch
        {
            "bouquet" => new PartnershipFixture("Abigail", "(O)458", 2000, FriendshipStatus.Friendly, "bouquet"),
            "marriage" => new PartnershipFixture("Abigail", "(O)460", 2500, FriendshipStatus.Dating, "propose_marriage"),
            "roommate" => new PartnershipFixture("Krobus", "(O)808", 2500, FriendshipStatus.Friendly, "propose_roommate"),
            _ => null
        };
        if (fixture is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_partnership_fixture",
                "partnership_fixture=ready", "fixture_case=" + request.PartnershipFixtureCase,
                "partnership_fixture_case_bouquet_marriage_or_roommate_required");
        }

        var player = Game1.player;
        var npc = Game1.getCharacterFromName(fixture.NpcName);
        var home = Utility.getHomeOfFarmer(player);
        if (npc is null || home is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_partnership_fixture",
                "partnership_fixture=ready", "npc_or_home=missing",
                "partnership_fixture_npc_or_home_missing");
        }

        npc.currentLocation?.characters.Remove(npc);
        var placement = FindPartnershipFixturePlacement(home);
        if (!placement.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_partnership_fixture",
                "partnership_fixture=ready", "placement=missing",
                "partnership_fixture_adjacent_walkable_tiles_missing");
        }

        foreach (var friendship in player.friendshipData.Values)
        {
            if (friendship.Status is FriendshipStatus.Dating or FriendshipStatus.Engaged or FriendshipStatus.Married)
            {
                friendship.Clear();
            }
        }
        player.spouse = null;
        var targetFriendship = player.friendshipData.TryGetValue(fixture.NpcName, out var existing)
            ? existing
            : player.friendshipData[fixture.NpcName] = new Friendship();
        targetFriendship.Clear();
        targetFriendship.Points = fixture.Points;
        targetFriendship.Status = fixture.Status;

        EnsureFixtureInventoryCapacity(player);
        for (var index = 0; index < player.Items.Count; index++)
        {
            if (player.Items[index] is not null && player.Items[index] is not Tool)
            {
                player.Items[index] = null;
            }
        }
        var slot = -1;
        for (var index = 0; index < player.Items.Count; index++)
        {
            if (player.Items[index] is null)
            {
                slot = index;
                break;
            }
        }
        if (slot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_partnership_fixture",
                "partnership_fixture=ready", "inventory_slot=missing",
                "partnership_fixture_inventory_slot_unavailable");
        }
        var relationshipItem = ItemRegistry.Create(fixture.QualifiedItemId, 1);
        player.Items[slot] = relationshipItem;

        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.timeOfDay = 1200;
        player.UsingTool = false;
        player.canMove = true;
        player.HouseUpgradeLevel = Math.Max(1, player.HouseUpgradeLevel);
        Game1.currentLocation = home;
        player.currentLocation = home;
        player.Position = placement.Value.Stand.ToVector2() * Game1.tileSize;

        home.characters.Add(npc);
        npc.currentLocation = home;
        npc.Position = placement.Value.Npc.ToVector2() * Game1.tileSize;
        npc.controller = null;
        npc.Halt();
        npc.ignoreScheduleToday = true;
        npc.followSchedule = false;
        npc.isSleeping.Value = false;
        npc.IsInvisible = false;
        player.faceDirection(DirectionTo(placement.Value.Stand, placement.Value.Npc));

        var expectedRoommateTag = ItemContextTagManager.SanitizeContextTag("propose_roommate_" + fixture.NpcName);
        var roommateTagReady = fixture.ActionKind != "propose_roommate" || relationshipItem.HasContextTag(expectedRoommateTag);
        var verified = ReferenceEquals(Game1.currentLocation, home) &&
            home.characters.Contains(npc) &&
            npc.currentLocation == home &&
            npc.TilePoint == placement.Value.Npc &&
            player.TilePoint == placement.Value.Stand &&
            AreAdjacent(player.TilePoint, npc.TilePoint) &&
            targetFriendship.Points == fixture.Points &&
            targetFriendship.Status == fixture.Status &&
            player.HouseUpgradeLevel >= 1 &&
            string.IsNullOrEmpty(player.spouse) &&
            relationshipItem.QualifiedItemId == fixture.QualifiedItemId &&
            relationshipItem.Stack == 1 &&
            roommateTagReady;
        var observed = "case=" + request.PartnershipFixtureCase +
            ";npc=" + fixture.NpcName +
            ";location=" + home.NameOrUniqueName +
            ";npc_tile=" + npc.TilePoint.X + "," + npc.TilePoint.Y +
            ";stand_tile=" + player.TilePoint.X + "," + player.TilePoint.Y +
            ";slot=" + slot +
            ";item=" + relationshipItem.QualifiedItemId +
            ";points=" + targetFriendship.Points +
            ";status=" + targetFriendship.Status +
            ";house_level=" + player.HouseUpgradeLevel +
            ";action_kind=" + fixture.ActionKind;

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_partnership_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_partnership_fixture_ready", "native_relationship_item_created", "npc_and_player_adjacent" }
                : new[] { "partnership_fixture_postcondition_mismatch" },
            RequestedEffect = "partnership_fixture=ready",
            ObservedEffect = observed,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "partnership_fixture_postcondition_mismatch" },
            SocialNpcName = fixture.NpcName,
            SocialActionKind = fixture.ActionKind,
            SocialGiftSlotBefore = slot,
            SocialGiftItemIdBefore = fixture.QualifiedItemId,
            SocialGiftStackBefore = 1,
            PartnershipFriendshipStatusAfter = targetFriendship.Status.ToString(),
            PartnershipSpouseAfter = player.spouse ?? string.Empty,
            PartnershipRoommateMarriageAfter = targetFriendship.RoommateMarriage,
            PartnershipWeddingDateTotalDaysAfter = targetFriendship.WeddingDate?.TotalDays
        };
    }

    private TrainingExecutionResult ExecutePreparePartnershipSleep(TrainingExecutionRequest request)
    {
        return ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_partnership_sleep",
            "isolated_fixture_farmer_moved_to_native_sleep_stand");
    }

    private TrainingExecutionResult ExecutePrepareNativeSleepFixture(
        TrainingExecutionRequest request,
        string primitiveKind,
        string verificationReason)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        var home = Utility.getHomeOfFarmer(Game1.player);
        Game1.currentLocation = home;
        Game1.player.currentLocation = home;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        home.resetForPlayerEntry();
        var target = ResolveHomeSleepTarget(Game1.player.TilePoint, out var targetReason);
        if (target is null)
        {
            return BlockedWithPrimitive(request, primitiveKind,
                "player.at_sleep_stand=true", SleepObservedEffect(), targetReason);
        }

        Game1.player.Position = target.StandTile.ToVector2() * Game1.tileSize;
        Game1.player.faceDirection(DirectionTo(target.StandTile, target.BedTile));
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitiveKind,
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { verificationReason },
            RequestedEffect = "player.at_sleep_stand=true",
            ObservedEffect = SleepObservedEffect()
        };
    }

    private static (Point Stand, Point Npc)? FindPartnershipFixturePlacement(GameLocation location)
    {
        var layer = location.Map?.Layers.FirstOrDefault();
        if (layer is null)
        {
            return null;
        }

        for (var y = 1; y < layer.LayerHeight - 1; y++)
        {
            for (var x = 1; x < layer.LayerWidth - 1; x++)
            {
                var npcTile = new Point(x, y);
                if (!IsTileWalkable(location, npcTile) || IsTileOccupiedByCharacter(location, npcTile))
                {
                    continue;
                }
                foreach (var stand in Neighbors(npcTile))
                {
                    if (IsTileOnMap(location, stand) && IsTileWalkable(location, stand) && !IsTileOccupiedByCharacter(location, stand))
                    {
                        return (stand, npcTile);
                    }
                }
            }
        }
        return null;
    }

    private sealed record PartnershipFixture(
        string NpcName,
        string QualifiedItemId,
        int Points,
        FriendshipStatus Status,
        string ActionKind);
}
