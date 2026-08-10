using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupQuestTerminalFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        return request.QuestInteractionKind switch
        {
            "offer_item" => SetupItemDeliveryFixture(request),
            "drop_box" => SetupDropBoxFixture(request),
            _ => BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "interaction_kind=" + request.QuestInteractionKind,
                "quest_terminal_fixture_kind_invalid")
        };
    }

    private TrainingExecutionResult SetupItemDeliveryFixture(
        TrainingExecutionRequest request)
    {
        const string npcName = "Robin";
        const string qualifiedItemId = "(O)388";
        const int slot = 11;
        var player = Game1.player;
        var home = Utility.getHomeOfFarmer(player);
        var npc = Game1.getCharacterFromName(npcName);
        var placement = home is null ? null : FindQuestNpcFixturePlacement(home);
        if (home is null || npc is null || !placement.HasValue ||
            slot >= player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "npc_home_or_placement=missing",
                "quest_item_delivery_fixture_topology_missing");
        }

        ClearQuestFixtureState();
        npc.currentLocation?.characters.Remove(npc);
        var questId = string.IsNullOrWhiteSpace(request.QuestId)
            ? "stardewai.runtime.item_delivery"
            : request.QuestId;
        var quest = new ItemDeliveryQuest(npcName, qualifiedItemId)
        {
            targetMessage = "Thank you."
        };
        quest.id.Value = questId;
        quest.number.Value = 1;
        quest.accepted.Value = true;
        player.questLog.Add(quest);

        PrepareQuestFixtureInventory(player, slot, qualifiedItemId, 1);
        PrepareQuestFixturePlayer(player, home, placement.Value.Stand, placement.Value.Npc);
        home.characters.Add(npc);
        npc.currentLocation = home;
        npc.Position = placement.Value.Npc.ToVector2() * Game1.tileSize;
        npc.controller = null;
        npc.Halt();
        npc.ignoreScheduleToday = true;
        npc.followSchedule = false;
        npc.isSleeping.Value = false;
        npc.IsInvisible = false;

        var verified = ReferenceEquals(Game1.currentLocation, home) &&
            player.TilePoint == placement.Value.Stand &&
            npc.TilePoint == placement.Value.Npc &&
            AreAdjacent(player.TilePoint, npc.TilePoint) &&
            player.Items[slot]?.QualifiedItemId == qualifiedItemId &&
            player.Items[slot]?.Stack == 1 &&
            player.questLog.Contains(quest);
        return QuestTerminalFixtureResult(
            request,
            verified,
            "offer_item",
            questId,
            string.Empty,
            "ItemDeliveryQuest",
            home.NameOrUniqueName,
            placement.Value.Npc,
            placement.Value.Stand,
            npcName,
            qualifiedItemId,
            slot,
            0,
            1,
            null,
            string.Empty);
    }

    private TrainingExecutionResult SetupDropBoxFixture(
        TrainingExecutionRequest request)
    {
        const string questKey = "Gunther";
        const string dropBoxId = "GuntherBox";
        const string qualifiedItemId = "(O)881";
        const int slot = 11;
        if (Game1.getLocationFromName("ArchaeologyHouse") is not GameLocation museum ||
            slot >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "museum_or_slot=missing",
                "quest_drop_box_fixture_topology_missing");
        }

        var actionTile = FindDropBoxActionTile(museum, dropBoxId);
        var standTile = actionTile.HasValue
            ? FindQuestDropBoxStandTile(museum, actionTile.Value)
            : null;
        if (!actionTile.HasValue || !standTile.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "drop_box_action_or_stand=missing",
                "quest_drop_box_fixture_action_missing");
        }

        ClearQuestFixtureState();
        var order = SpecialOrder.GetSpecialOrder(questKey, 0);
        var collect = order.objectives.OfType<CollectObjective>().SingleOrDefault();
        var donate = order.objectives.OfType<DonateObjective>().SingleOrDefault();
        if (collect is null || donate is null ||
            !string.Equals(donate.dropBox.Value, dropBoxId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "native_order_shape=drifted",
                "quest_drop_box_fixture_native_order_drifted");
        }

        collect.SetCount(collect.GetMaxCount());
        donate.SetCount(0);
        donate.confirmed.Value = false;
        order.donatedItems.Clear();
        Game1.player.team.specialOrders.Add(order);
        order.Update();

        PrepareQuestFixtureInventory(Game1.player, slot, qualifiedItemId, 1);
        PrepareQuestFixturePlayer(Game1.player, museum, standTile.Value, actionTile.Value);
        var objectiveIndex = order.objectives.IndexOf(donate);
        var verified = ReferenceEquals(Game1.currentLocation, museum) &&
            Game1.player.TilePoint == standTile.Value &&
            AreAdjacent(standTile.Value, actionTile.Value) &&
            museum.doesTileHaveProperty(
                actionTile.Value.X,
                actionTile.Value.Y,
                "Action",
                "Buildings") == "DropBox " + dropBoxId &&
            Game1.player.Items[slot]?.QualifiedItemId == qualifiedItemId &&
            donate.GetCount() == 0 &&
            objectiveIndex >= 0 &&
            Game1.player.team.specialOrders.Contains(order);
        return QuestTerminalFixtureResult(
            request,
            verified,
            "drop_box",
            string.Empty,
            questKey,
            "DonateObjective",
            museum.NameOrUniqueName,
            actionTile.Value,
            standTile.Value,
            string.Empty,
            qualifiedItemId,
            slot,
            donate.GetCount(),
            donate.GetMaxCount(),
            objectiveIndex,
            dropBoxId);
    }

    private static void ClearQuestFixtureState()
    {
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        foreach (var quest in Game1.player.questLog
            .Where(quest => quest.id.Value?.StartsWith("stardewai.runtime.", StringComparison.Ordinal) == true)
            .ToArray())
        {
            Game1.player.questLog.Remove(quest);
        }
        foreach (var order in Game1.player.team.specialOrders
            .Where(order => string.Equals(order.questKey.Value, "Gunther", StringComparison.Ordinal))
            .ToArray())
        {
            Game1.player.team.specialOrders.Remove(order);
        }
    }

    private static void PrepareQuestFixtureInventory(
        Farmer player,
        int slot,
        string qualifiedItemId,
        int stack)
    {
        for (var index = 0; index < player.Items.Count; index++)
        {
            if (player.Items[index] is not null && player.Items[index] is not Tool)
            {
                player.Items[index] = null;
            }
        }
        player.Items[slot] = ItemRegistry.Create(qualifiedItemId, stack);
        player.CurrentToolIndex = slot;
    }

    private static void PrepareQuestFixturePlayer(
        Farmer player,
        GameLocation location,
        Point standTile,
        Point targetTile)
    {
        Game1.currentLocation = location;
        player.currentLocation = location;
        location.resetForPlayerEntry();
        Game1.timeOfDay = 1200;
        player.UsingTool = false;
        player.canMove = true;
        player.Position = standTile.ToVector2() * Game1.tileSize;
        player.faceDirection(DirectionTo(standTile, targetTile));
    }

    private static (Point Stand, Point Npc)? FindQuestNpcFixturePlacement(
        GameLocation location)
    {
        var layer = location.Map?.Layers.FirstOrDefault();
        if (layer is null)
        {
            return null;
        }
        for (var y = 1; y < layer.LayerHeight - 1; y++)
        {
            for (var x = 1; x < layer.LayerWidth - 2; x++)
            {
                var stand = new Point(x, y);
                var npc = new Point(x + 1, y);
                if (IsTileWalkable(location, stand) &&
                    IsTileWalkable(location, npc) &&
                    !IsTileOccupiedByCharacter(location, stand) &&
                    !IsTileOccupiedByCharacter(location, npc))
                {
                    return (stand, npc);
                }
            }
        }
        return null;
    }

    private static Point? FindDropBoxActionTile(
        GameLocation location,
        string dropBoxId)
    {
        var layer = location.Map?.GetLayer("Buildings");
        if (layer is null)
        {
            return null;
        }
        var expected = "DropBox " + dropBoxId;
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (string.Equals(
                        location.doesTileHaveProperty(x, y, "Action", "Buildings"),
                        expected,
                        StringComparison.Ordinal))
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }

    private static Point? FindQuestDropBoxStandTile(
        GameLocation location,
        Point actionTile)
    {
        foreach (var tile in Neighbors(actionTile))
        {
            if (IsTileOnMap(location, tile) &&
                IsTileWalkable(location, tile) &&
                !IsTileOccupiedByCharacter(location, tile))
            {
                return tile;
            }
        }
        return null;
    }

    private static TrainingExecutionResult QuestTerminalFixtureResult(
        TrainingExecutionRequest request,
        bool verified,
        string fixtureKind,
        string questId,
        string questKey,
        string runtimeType,
        string locationId,
        Point targetTile,
        Point standTile,
        string npcName,
        string qualifiedItemId,
        int slot,
        int currentCount,
        int targetCount,
        int? objectiveIndex,
        string dropBoxId)
    {
        var observed = "kind=" + fixtureKind +
            ";location=" + locationId +
            ";target=" + targetTile.X + "," + targetTile.Y +
            ";stand=" + standTile.X + "," + standTile.Y +
            ";item=" + qualifiedItemId +
            ";progress=" + currentCount + "/" + targetCount;
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
            PrimitiveKind = "debug_setup_quest_terminal_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_native_quest_terminal_fixture_ready" }
                : new[] { "quest_terminal_fixture_state_mismatch" },
            RequestedEffect = "quest_terminal_fixture=" + fixtureKind,
            ObservedEffect = observed,
            TargetLocation = locationId,
            TargetTileX = targetTile.X,
            TargetTileY = targetTile.Y,
            SocialNpcName = npcName,
            QuestCandidateId = "runtime_fixture:" + (questId.Length > 0 ? questId : questKey),
            QuestFamily = fixtureKind == "drop_box" ? "special_order" : "ordinary_quest",
            QuestId = questId,
            QuestKey = questKey,
            QuestObjectiveIndex = objectiveIndex,
            QuestProgressBefore = currentCount,
            QuestProgressAfter = currentCount,
            QuestTargetCount = targetCount,
            QuestPresentBefore = false,
            QuestPresentAfter = true,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "quest_terminal_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests.runtime_fixture:" + runtimeType,
                        Before = "absent",
                        After = observed
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
