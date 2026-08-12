using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Quests;
using StardewValley.Objects;
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
            "craft_item" => SetupCraftingQuestFixture(request),
            "offer_item" => SetupItemDeliveryFixture(request),
            "building_construction" => SetupBuildingConstructionFixture(request, createQuest: true),
            "building_construction_general" => SetupBuildingConstructionFixture(request, createQuest: false),
            "building_skin" => ExecuteSetupBuildingSkinFixture(request),
            "building_paint" => ExecuteSetupBuildingPaintFixture(request),
            "drop_box" => SetupDropBoxFixture(request),
            "drop_box_color" => SetupDropBoxFixture(request, usePreservedParentColor: true),
            _ => BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "interaction_kind=" + request.QuestInteractionKind,
                "quest_terminal_fixture_kind_invalid")
        };
    }

    private TrainingExecutionResult SetupBuildingConstructionFixture(
        TrainingExecutionRequest request,
        bool createQuest)
    {
        const string buildingType = "Coop";
        var player = Game1.player;
        var farm = Game1.getFarm();
        var house = Game1.getLocationFromName("ScienceHouse");
        var actionTile = house is null ? null : FarmhouseFixtureActionTile(house);
        var standTile = house is null || !actionTile.HasValue
            ? null
            : FarmhouseFixtureStandTile(house, actionTile.Value);
        var robinTile = house is null || !actionTile.HasValue || !standTile.HasValue
            ? null
            : FarmhouseFixtureRobinTile(house, actionTile.Value, standTile.Value);
        var robin = Game1.getCharacterFromName("Robin");
        if (house is null || !actionTile.HasValue || !standTile.HasValue ||
            !robinTile.HasValue || robin is null ||
            !Game1.buildingData.TryGetValue(buildingType, out var data) ||
            data.BuildingToUpgrade is not null || data.Builder != "Robin")
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_building_fixture=ready",
                "building_type=" + buildingType,
                "quest_building_fixture_native_data_or_carpenter_topology_missing");
        }

        ClearQuestFixtureState();
        foreach (var existingQuest in player.questLog.OfType<HaveBuildingQuest>().ToArray())
        {
            player.questLog.Remove(existingQuest);
        }
        foreach (var building in farm.buildings
            .Where(building => building.daysOfConstructionLeft.Value > 0)
            .ToArray())
        {
            farm.buildings.Remove(building);
        }
        player.team.constructedBuildings.Remove(buildingType);

        // The fixture copy isolates destructive setup from every real save. Clearing
        // loose farm blockers guarantees that the bridge can publish a native-valid
        // placement without bypassing CarpenterMenu placement validation.
        farm.objects.Clear();
        farm.terrainFeatures.Clear();
        farm.resourceClumps.Clear();
        farm.debris.Clear();

        EnsureFixtureInventoryCapacity(player);
        foreach (var material in data.BuildMaterials)
        {
            for (var index = 0; index < player.Items.Count; index++)
            {
                if (string.Equals(player.Items[index]?.QualifiedItemId, material.ItemId, StringComparison.Ordinal))
                {
                    player.Items[index] = null;
                }
            }
            InstallFixtureItem(player, ItemRegistry.Create(material.ItemId, material.Amount + 25));
        }

        var questId = createQuest
            ? string.IsNullOrWhiteSpace(request.QuestId)
                ? "stardewai.runtime.building"
                : request.QuestId
            : string.Empty;
        HaveBuildingQuest? quest = null;
        if (createQuest)
        {
            quest = new HaveBuildingQuest(buildingType);
            quest.id.Value = questId;
            quest.accepted.Value = true;
            player.questLog.Add(quest);
        }
        player.Money = data.BuildCost + 5000;
        Game1.timeOfDay = 1200;
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        player.UsingTool = false;
        player.canMove = true;
        Game1.currentLocation = house;
        player.currentLocation = house;
        player.Position = standTile.Value.ToVector2() * Game1.tileSize;
        player.faceDirection(DirectionTo(standTile.Value, actionTile.Value));

        robin.currentLocation?.characters.Remove(robin);
        if (!house.characters.Contains(robin))
        {
            house.characters.Add(robin);
        }
        robin.currentLocation = house;
        robin.Position = robinTile.Value.ToVector2() * Game1.tileSize;
        robin.controller = null;
        robin.Halt();
        robin.ignoreScheduleToday = true;
        robin.followSchedule = false;
        robin.isSleeping.Value = false;
        robin.IsInvisible = false;

        var verified = ReferenceEquals(Game1.currentLocation, house) &&
            player.TilePoint == standTile.Value &&
            (createQuest ? quest is not null && player.questLog.Contains(quest) : !player.questLog.OfType<HaveBuildingQuest>().Any()) &&
            !player.team.constructedBuildings.Contains(buildingType) &&
            !Game1.IsThereABuildingUnderConstruction() &&
            player.Money == data.BuildCost + 5000 &&
            data.BuildMaterials.All(material =>
                player.Items.CountId(material.ItemId) == material.Amount + 25) &&
            house.characters.Contains(robin) &&
            Vector2.Distance(robin.Tile, actionTile.Value.ToVector2()) <= 3f;
        return QuestTerminalFixtureResult(
            request,
            verified,
            createQuest ? "building_construction" : "building_construction_general",
            questId,
            string.Empty,
            createQuest ? nameof(HaveBuildingQuest) : string.Empty,
            house.NameOrUniqueName,
            actionTile.Value,
            standTile.Value,
            "Robin",
            buildingType,
            0,
            0,
            data.BuildDays,
            null,
            string.Empty);
    }

    private TrainingExecutionResult SetupCraftingQuestFixture(
        TrainingExecutionRequest request)
    {
        var player = Game1.player;
        var selected = CraftingRecipe.craftingRecipes.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(TryCreateCraftingQuestFixtureRecipe)
            .FirstOrDefault(row => row is not null);
        if (selected is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "native_recipe=unresolved",
                "quest_crafting_fixture_native_recipe_unavailable");
        }

        var home = Utility.getHomeOfFarmer(player);
        var placement = home is null ? null : FindQuestNpcFixturePlacement(home);
        if (home is null || !placement.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "crafting_home_or_placement=missing",
                "quest_crafting_fixture_topology_missing");
        }

        ClearQuestFixtureState();
        for (var slot = 0; slot < player.Items.Count; slot++)
        {
            player.Items[slot] = null;
        }
        for (var index = 0; index < selected.Ingredients.Length; index++)
        {
            player.Items[index] = selected.Ingredients[index];
        }
        player.craftingRecipes[selected.RecipeName] = 0;
        var questId = string.IsNullOrWhiteSpace(request.QuestId)
            ? "stardewai.runtime.crafting"
            : request.QuestId;
        var quest = new CraftingQuest(selected.Output.QualifiedItemId);
        quest.id.Value = questId;
        quest.questType.Value = 2;
        quest.accepted.Value = true;
        player.questLog.Add(quest);
        PrepareQuestFixturePlayer(
            player,
            home,
            placement.Value.Stand,
            placement.Value.Npc);
        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        player.canMove = true;
        player.UsingTool = false;

        var verified = ReferenceEquals(Game1.currentLocation, home) &&
            player.TilePoint == placement.Value.Stand &&
            player.questLog.Contains(quest) &&
            quest.ItemId.Value == selected.Output.QualifiedItemId &&
            player.craftingRecipes.ContainsKey(selected.RecipeName) &&
            selected.Ingredients.Select((item, index) =>
                player.Items[index]?.QualifiedItemId == item.QualifiedItemId &&
                player.Items[index]?.Stack == item.Stack).All(value => value);
        return QuestTerminalFixtureResult(
            request,
            verified,
            "craft_item",
            questId,
            string.Empty,
            nameof(CraftingQuest),
            Game1.currentLocation.NameOrUniqueName,
            player.TilePoint,
            player.TilePoint,
            string.Empty,
            selected.Output.QualifiedItemId,
            0,
            0,
            0,
            null,
            string.Empty);
    }

    private static CraftingQuestFixtureRecipe?
        TryCreateCraftingQuestFixtureRecipe(string recipeName)
    {
        try
        {
            var recipe = new CraftingRecipe(
                recipeName,
                isCookingRecipe: false);
            if (recipe.itemToProduce.Count != 1 ||
                recipe.recipeList.Count == 0 ||
                recipe.recipeList.Count >= Game1.player.Items.Count)
            {
                return null;
            }
            var output = recipe.createItem();
            if (output is not StardewValley.Object outputObject ||
                outputObject.bigCraftable.Value)
            {
                return null;
            }

            var ingredients = new List<Item>();
            foreach (var ingredient in recipe.recipeList)
            {
                if (!int.TryParse(ingredient.Key, out var itemId) ||
                    itemId <= 0 || ingredient.Value <= 0)
                {
                    return null;
                }
                var item = ItemRegistry.Create(
                    ItemRegistry.ManuallyQualifyItemId(
                        ingredient.Key,
                        "(O)"),
                    ingredient.Value);
                if (item.Stack != ingredient.Value ||
                    item.Stack > item.maximumStackSize() ||
                    !CraftingRecipe.ItemMatchesForCrafting(
                        item,
                        ingredient.Key))
                {
                    return null;
                }
                ingredients.Add(item);
            }
            return new CraftingQuestFixtureRecipe(
                recipeName,
                output,
                ingredients.ToArray());
        }
        catch
        {
            return null;
        }
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
        TrainingExecutionRequest request,
        bool usePreservedParentColor = false)
    {
        var questKey = usePreservedParentColor ? "QiChallenge12" : "Gunther";
        var dropBoxId = usePreservedParentColor ? "QiChallengeBox" : "GuntherBox";
        var locationName = usePreservedParentColor ? "QiNutRoom" : "ArchaeologyHouse";
        var qualifiedItemId = usePreservedParentColor ? "(O)348" : "(O)881";
        const int slot = 11;
        if (Game1.getLocationFromName(locationName) is not GameLocation targetLocation ||
            slot >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "drop_box_location_or_slot=missing",
                "quest_drop_box_fixture_topology_missing");
        }

        var actionTile = FindDropBoxActionTile(targetLocation, dropBoxId);
        var standTile = actionTile.HasValue
            ? FindQuestDropBoxStandTile(targetLocation, actionTile.Value)
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
        var collect = usePreservedParentColor
            ? null
            : order.objectives.OfType<CollectObjective>().SingleOrDefault();
        var donate = usePreservedParentColor
            ? order.objectives.OfType<DonateObjective>().FirstOrDefault(objective =>
                objective.acceptableContextTagSets.Any(set =>
                    set.Split(',').Any(group => group.StartsWith("color_red", StringComparison.Ordinal))))
            : order.objectives.OfType<DonateObjective>().SingleOrDefault();
        if ((!usePreservedParentColor && collect is null) || donate is null ||
            !string.Equals(donate.dropBox.Value, dropBoxId, StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_terminal_fixture",
                "quest_terminal_fixture=ready",
                "native_order_shape=drifted",
                "quest_drop_box_fixture_native_order_drifted");
        }

        collect?.SetCount(collect.GetMaxCount());
        donate.SetCount(0);
        donate.confirmed.Value = false;
        order.donatedItems.Clear();
        Game1.player.team.specialOrders.Add(order);
        order.Update();

        PrepareQuestFixtureInventory(Game1.player, slot, qualifiedItemId, 1);
        if (usePreservedParentColor)
        {
            const string parentItemId = "613";
            var parentTags = ItemContextTagManager.GetBaseContextTags(parentItemId);
            if (!parentTags.Contains("color_red"))
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_quest_terminal_fixture",
                    "quest_terminal_fixture=ready",
                    "preserved_parent_color=drifted",
                    "quest_drop_box_fixture_parent_color_drifted");
            }

            var coloredObject = new ColoredObject("348", 1, Color.Red);
            coloredObject.preservedParentSheetIndex.Value = parentItemId;
            Game1.player.Items[slot] = coloredObject;
            qualifiedItemId = coloredObject.QualifiedItemId;
            if (!donate.IsValidItem(coloredObject))
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_quest_terminal_fixture",
                    "quest_terminal_fixture=ready",
                    "native_donate_color_match=false",
                "quest_drop_box_fixture_native_color_match_drifted");
            }

            SuppressEligibleQuestFixtureLocationEvents(targetLocation);
        }
        PrepareQuestFixturePlayer(Game1.player, targetLocation, standTile.Value, actionTile.Value);
        var objectiveIndex = order.objectives.IndexOf(donate);
        var verified = ReferenceEquals(Game1.currentLocation, targetLocation) &&
            Game1.player.TilePoint == standTile.Value &&
            AreAdjacent(standTile.Value, actionTile.Value) &&
            targetLocation.doesTileHaveProperty(
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
            targetLocation.NameOrUniqueName,
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
            .Where(order =>
                string.Equals(order.questKey.Value, "Gunther", StringComparison.Ordinal) ||
                string.Equals(order.questKey.Value, "QiChallenge12", StringComparison.Ordinal))
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

    private static void SuppressEligibleQuestFixtureLocationEvents(GameLocation location)
    {
        if (!location.TryGetLocationEvents(out _, out var events))
        {
            return;
        }

        foreach (var pair in events)
        {
            if (!GameLocation.IsValidLocationEvent(pair.Key, pair.Value))
            {
                continue;
            }

            var eventId = location.checkEventPrecondition(pair.Key);
            if (!string.IsNullOrWhiteSpace(eventId) && eventId != "-1")
            {
                Game1.player.eventsSeen.Add(eventId);
            }
        }
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
            QuestFamily = fixtureKind.StartsWith("drop_box", StringComparison.Ordinal)
                ? "special_order"
                : "ordinary_quest",
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

    private sealed record CraftingQuestFixtureRecipe(
        string RecipeName,
        Item Output,
        Item[] Ingredients);
}
