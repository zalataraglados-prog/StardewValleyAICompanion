using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FarmhouseUpgradeNativeContract = "GameLocation.checkAction_Carpenter_then_answerDialogue_carpenter_Upgrade_then_upgrade_Yes";

    private void StartFarmhouseUpgrade(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!FarmhouseUpgradeRequestExact(request) || !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, "farmhouse_upgrade_typed_projection_required"));
            return;
        }
        if (activeFarmhouseUpgrade is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, "farmhouse_upgrade_player_busy"));
            return;
        }
        var house = Game1.currentLocation;
        if (house is null || !string.Equals(house.NameOrUniqueName, "ScienceHouse", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(house.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, "farmhouse_upgrade_target_location_mismatch"));
            return;
        }
        var actionTile = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var standTile = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!AreAdjacent(actionTile, standTile) || house.doesTileHaveProperty(actionTile.X, actionTile.Y, "Action", "Buildings") != "Carpenter" ||
            !IsTileOnMap(house, standTile) || !IsTileWalkable(house, standTile) || IsTileOccupiedByCharacter(house, standTile))
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, "farmhouse_upgrade_action_or_stand_tile_drifted"));
            return;
        }
        if (!FarmhouseUpgradeLivePreconditionsMatch(request, house, actionTile, out var liveReason))
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, liveReason));
            return;
        }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(house, Game1.player.TilePoint, standTile, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FarmhouseUpgradeBlocked(request, "farmhouse_upgrade_path_unavailable:" + pathReason));
            return;
        }
        activeFarmhouseUpgrade = new ActiveFarmhouseUpgrade(pending, house, actionTile, standTile, path, maxMovement);
    }

    private void TickFarmhouseUpgrade()
    {
        var active = activeFarmhouseUpgrade;
        if (active is null)
        {
            return;
        }
        try
        {
            TickFarmhouseUpgradeCore(active);
        }
        catch (Exception ex)
        {
            CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void TickFarmhouseUpgradeCore(ActiveFarmhouseUpgrade active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.House) || active.ElapsedTicks > 4200)
        {
            CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_world_location_or_timeout");
            return;
        }
        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            var tile = Game1.player.TilePoint;
            if (tile != active.LastObservedTile)
            {
                active.StuckTicks = 0;
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, tile);
                active.LastObservedTile = tile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_movement_stuck_or_blocked");
                return;
            }
            if (tile == next)
            {
                active.PathIndex++;
            }
            return;
        }

        StopAllMovement();
        var request = active.Pending.Request;
        if (!active.OpenIssued)
        {
            if (!FarmhouseUpgradeLivePreconditionsMatch(request, active.House, active.ActionTile, out var liveReason))
            {
                CompleteFarmhouseUpgradeBlocked(active, liveReason);
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.ActionTile));
            var handled = active.House.checkAction(
                new xTile.Dimensions.Location(active.ActionTile.X, active.ActionTile.Y),
                new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            if (!handled || Game1.activeClickableMenu is not DialogueBox)
            {
                CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_carpenter_action_not_handled");
                return;
            }
            active.OpenIssued = true;
            active.DialogueCooldown = 8;
            return;
        }

        if (active.PurchaseIssued)
        {
            active.SettlementTicks++;
            if (!FarmhouseUpgradePostconditionsMatch(request))
            {
                if (active.SettlementTicks > 1260)
                {
                    CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_native_settlement_timeout_or_mismatch");
                }
                return;
            }
            if (Game1.activeClickableMenu is DialogueBox confirmation)
            {
                if (confirmation.isQuestion)
                {
                    CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_unexpected_post_purchase_question");
                    return;
                }
                AdvanceFarmhouseDialogue(active, confirmation);
                return;
            }
            if (Game1.activeClickableMenu is null)
            {
                CompleteFarmhouseUpgrade(active);
            }
            return;
        }

        if (Game1.activeClickableMenu is not DialogueBox menu || !menu.isQuestion || menu.characterDialogue is not null)
        {
            CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_expected_question_missing");
            return;
        }
        if (active.DialogueCooldown > 0)
        {
            active.DialogueCooldown--;
            return;
        }
        var expectedKey = active.UpgradeResponseChosen ? "upgrade" : "carpenter";
        var responseKey = active.UpgradeResponseChosen ? "Yes" : "Upgrade";
        if (active.House.lastQuestionKey != expectedKey)
        {
            CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_question_key_drifted");
            return;
        }
        var response = menu.responses?.FirstOrDefault(row => row.responseKey == responseKey);
        if (response is null || !active.House.answerDialogue(response))
        {
            CompleteFarmhouseUpgradeBlocked(active, "farmhouse_upgrade_native_response_failed:" + responseKey);
            return;
        }
        if (!active.UpgradeResponseChosen)
        {
            active.UpgradeResponseChosen = true;
            active.DialogueCooldown = 8;
            return;
        }
        active.PurchaseIssued = true;
        active.SettlementTicks = 0;
    }

    private static void AdvanceFarmhouseDialogue(ActiveFarmhouseUpgrade active, DialogueBox menu)
    {
        if (active.DialogueCooldown > 0)
        {
            active.DialogueCooldown--;
            return;
        }
        if (menu.transitioning || menu.safetyTimer > 0)
        {
            return;
        }
        menu.receiveLeftClick(menu.xPositionOnScreen + menu.width / 2, menu.yPositionOnScreen + menu.height / 2);
        active.DialogueCooldown = 8;
    }

    private static bool FarmhouseUpgradeRequestExact(TrainingExecutionRequest request)
    {
        if (request.PurchaseKind != "farmhouse_upgrade" || request.JoinActionRaw != "Carpenter" || request.NativeContract != FarmhouseUpgradeNativeContract ||
            !request.ExpectedHouseUpgradeLevelBefore.HasValue || !request.ExpectedHouseUpgradeLevelAfterConstruction.HasValue ||
            request.ExpectedDaysUntilHouseUpgradeBefore != -1 || request.ExpectedDaysUntilHouseUpgradeAfter != 3 ||
            !request.ExpectedMoneyBefore.HasValue || !request.Price.HasValue || !request.ExpectedMoneyAfter.HasValue ||
            request.ExpectedMoneyAfter != request.ExpectedMoneyBefore - request.Price || !request.RequiredStack.HasValue ||
            !request.InventoryItemTotalBefore.HasValue || !request.InventoryItemTotalAfter.HasValue ||
            request.InventoryItemTotalAfter != request.InventoryItemTotalBefore - request.RequiredStack)
        {
            return false;
        }
        return (request.ExpectedHouseUpgradeLevelBefore.Value, request.ExpectedHouseUpgradeLevelAfterConstruction.Value,
            request.Price.Value, request.QualifiedItemId, request.RequiredStack.Value, request.ProjectId) switch
        {
            (0, 1, 10000, "(O)388", 450, "farmhouse_level_1") => true,
            (1, 2, 65000, "(O)709", 100, "farmhouse_level_2") => true,
            (2, 3, 100000, "", 0, "farmhouse_level_3") => true,
            _ => false
        };
    }

    private static bool FarmhouseUpgradeLivePreconditionsMatch(TrainingExecutionRequest request, GameLocation house, Point actionTile, out string reason)
    {
        reason = string.Empty;
        var robin = house.characters.FirstOrDefault(npc => npc.Name == "Robin");
        if (!FarmhouseUpgradeRequestExact(request) || Game1.player.HouseUpgradeLevel != request.ExpectedHouseUpgradeLevelBefore ||
            Game1.player.daysUntilHouseUpgrade.Value != request.ExpectedDaysUntilHouseUpgradeBefore ||
            Game1.player.Money != request.ExpectedMoneyBefore || Game1.player.Items.CountId(request.QualifiedItemId) != request.InventoryItemTotalBefore ||
            Game1.IsThereABuildingUnderConstruction() || robin is null || Vector2.Distance(robin.Tile, new Vector2(actionTile.X, actionTile.Y)) > 3f)
        {
            reason = "farmhouse_upgrade_live_preconditions_drifted";
            return false;
        }
        return true;
    }

    private static bool FarmhouseUpgradePostconditionsMatch(TrainingExecutionRequest request) =>
        Game1.player.HouseUpgradeLevel == request.ExpectedHouseUpgradeLevelBefore &&
        Game1.player.daysUntilHouseUpgrade.Value == request.ExpectedDaysUntilHouseUpgradeAfter &&
        Game1.player.Money == request.ExpectedMoneyAfter &&
        Game1.player.Items.CountId(request.QualifiedItemId) == request.InventoryItemTotalAfter;

    private void CompleteFarmhouseUpgrade(ActiveFarmhouseUpgrade active)
    {
        activeFarmhouseUpgrade = null;
        StopAllMovement();
        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "purchase_farmhouse_upgrade",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "GameLocation.checkAction_Carpenter_completed", "GameLocation.answerDialogue_carpenter_Upgrade_completed", "GameLocation.answerDialogue_upgrade_Yes_completed" },
            RequestedEffect = "player.days_until_farmhouse_upgrade=3;eventual.player.farmhouse_upgrade_level=" + request.ExpectedHouseUpgradeLevelAfterConstruction,
            ObservedEffect = FarmhouseUpgradeObservedEffect(request),
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 300,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.House.NameOrUniqueName,
            TargetTileX = active.ActionTile.X,
            TargetTileY = active.ActionTile.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = request.ExpectedMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                new SimulatedFactChange { Path = "player.inventory." + request.QualifiedItemId, Before = request.InventoryItemTotalBefore.ToString()!, After = Game1.player.Items.CountId(request.QualifiedItemId).ToString() },
                new SimulatedFactChange { Path = "player.days_until_farmhouse_upgrade", Before = request.ExpectedDaysUntilHouseUpgradeBefore.ToString()!, After = Game1.player.daysUntilHouseUpgrade.Value.ToString() }
            }
        });
    }

    private void CompleteFarmhouseUpgradeBlocked(ActiveFarmhouseUpgrade active, string reason)
    {
        StopAllMovement();
        activeFarmhouseUpgrade = null;
        active.Pending.Completion.SetResult(FarmhouseUpgradeBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult FarmhouseUpgradeBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "purchase_farmhouse_upgrade",
            "player.days_until_farmhouse_upgrade=3;eventual.player.farmhouse_upgrade_level=" + request.ExpectedHouseUpgradeLevelAfterConstruction,
            FarmhouseUpgradeObservedEffect(request), reason);

    private static string FarmhouseUpgradeObservedEffect(TrainingExecutionRequest request) =>
        "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";money=" + Game1.player.Money +
        ";house_level=" + Game1.player.HouseUpgradeLevel +
        ";days_until_upgrade=" + Game1.player.daysUntilHouseUpgrade.Value +
        ";material_count=" + Game1.player.Items.CountId(request.QualifiedItemId);

    private TrainingExecutionResult ExecuteSetupFarmhouseUpgradeFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var levelBefore = request.ExpectedHouseUpgradeLevelBefore;
        var fixture = levelBefore switch
        {
            0 => (Price: 10000, ItemId: "(O)388", RequiredCount: 450),
            1 => (Price: 65000, ItemId: "(O)709", RequiredCount: 100),
            2 => (Price: 100000, ItemId: string.Empty, RequiredCount: 0),
            _ => default
        };
        if (!levelBefore.HasValue || levelBefore < 0 || levelBefore > 2)
        {
            return BlockedWithPrimitive(request, "debug_setup_farmhouse_upgrade",
                "farmhouse_upgrade_fixture=ready", "level_before=" + levelBefore,
                "farmhouse_upgrade_fixture_level_0_2_required");
        }

        var house = Game1.getLocationFromName("ScienceHouse");
        var actionTile = house is null ? null : FarmhouseFixtureActionTile(house);
        var standTile = house is null || !actionTile.HasValue ? null : FarmhouseFixtureStandTile(house, actionTile.Value);
        var robinTile = house is null || !actionTile.HasValue || !standTile.HasValue
            ? null
            : FarmhouseFixtureRobinTile(house, actionTile.Value, standTile.Value);
        var robin = Game1.getCharacterFromName("Robin");
        if (house is null || !actionTile.HasValue || !standTile.HasValue || !robinTile.HasValue || robin is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_farmhouse_upgrade",
                "farmhouse_upgrade_fixture=ready", "location=ScienceHouse",
                "farmhouse_upgrade_fixture_location_action_stand_or_robin_missing");
        }

        var player = Game1.player;
        var beforeLevel = player.HouseUpgradeLevel;
        var beforeDays = player.daysUntilHouseUpgrade.Value;
        var beforeMoney = player.Money;
        var beforeMaterialCount = string.IsNullOrEmpty(fixture.ItemId) ? 0 : player.Items.CountId(fixture.ItemId);
        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var beforeRobinLocation = robin.currentLocation?.NameOrUniqueName ?? string.Empty;
        var expectedMoney = fixture.Price + 5000;
        var expectedMaterialCount = fixture.RequiredCount == 0 ? 0 : fixture.RequiredCount + 25;

        EnsureFixtureInventoryCapacity(player);
        if (!string.IsNullOrEmpty(fixture.ItemId))
        {
            for (var index = 0; index < player.Items.Count; index++)
            {
                if (string.Equals(player.Items[index]?.QualifiedItemId, fixture.ItemId, StringComparison.Ordinal))
                {
                    player.Items[index] = null;
                }
            }
            InstallFixtureItem(player, ItemRegistry.Create(fixture.ItemId, expectedMaterialCount));
        }

        Game1.activeClickableMenu = null;
        Game1.dialogueUp = false;
        Game1.eventUp = false;
        Game1.eventOver = false;
        player.UsingTool = false;
        player.canMove = true;
        player.HouseUpgradeLevel = levelBefore.Value;
        player.daysUntilHouseUpgrade.Value = -1;
        player.Money = expectedMoney;
        Game1.currentLocation = house;
        player.currentLocation = house;
        player.Position = new Vector2(standTile.Value.X * Game1.tileSize, standTile.Value.Y * Game1.tileSize);

        robin.currentLocation?.characters.Remove(robin);
        if (!house.characters.Contains(robin))
        {
            house.characters.Add(robin);
        }
        robin.currentLocation = house;
        robin.Position = new Vector2(robinTile.Value.X * Game1.tileSize, robinTile.Value.Y * Game1.tileSize);

        var verified = ReferenceEquals(Game1.currentLocation, house) &&
            player.HouseUpgradeLevel == levelBefore.Value &&
            player.daysUntilHouseUpgrade.Value == -1 &&
            player.Money == expectedMoney &&
            (string.IsNullOrEmpty(fixture.ItemId) || player.Items.CountId(fixture.ItemId) == expectedMaterialCount) &&
            house.characters.Contains(robin) &&
            robin.TilePoint != actionTile.Value && robin.TilePoint != standTile.Value &&
            Vector2.Distance(robin.Tile, new Vector2(actionTile.Value.X, actionTile.Value.Y)) <= 3f &&
            player.TilePoint == standTile.Value;
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
            PrimitiveKind = "debug_setup_farmhouse_upgrade",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_save_fixture_ready", "carpenter_action_and_robin_ready", "native_purchase_resources_ready" }
                : new[] { "farmhouse_upgrade_fixture_post_state_mismatch" },
            RequestedEffect = "farmhouse_upgrade_fixture=ready;level_before=" + levelBefore.Value,
            ObservedEffect = "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";level_before=" + player.HouseUpgradeLevel +
                ";days_until_upgrade=" + player.daysUntilHouseUpgrade.Value +
                ";money=" + player.Money +
                ";material_count=" + (string.IsNullOrEmpty(fixture.ItemId) ? 0 : player.Items.CountId(fixture.ItemId)) +
                ";action_tile=" + actionTile.Value.X + "," + actionTile.Value.Y +
                ";stand_tile=" + standTile.Value.X + "," + standTile.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "farmhouse_upgrade_fixture_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = beforeLocation, After = house.NameOrUniqueName },
                    new SimulatedFactChange { Path = "player.farmhouse_upgrade_level", Before = beforeLevel.ToString(), After = player.HouseUpgradeLevel.ToString() },
                    new SimulatedFactChange { Path = "player.days_until_farmhouse_upgrade", Before = beforeDays.ToString(), After = player.daysUntilHouseUpgrade.Value.ToString() },
                    new SimulatedFactChange { Path = "player.money", Before = beforeMoney.ToString(), After = player.Money.ToString() },
                    new SimulatedFactChange { Path = "player.inventory." + fixture.ItemId, Before = beforeMaterialCount.ToString(), After = expectedMaterialCount.ToString() },
                    new SimulatedFactChange { Path = "npcs.Robin.location_id", Before = beforeRobinLocation, After = house.NameOrUniqueName },
                    new SimulatedFactChange { Path = "npcs.Robin.tile", Before = string.Empty, After = robinTile.Value.X + "," + robinTile.Value.Y }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FarmhouseFixtureActionTile(GameLocation house)
    {
        var buildings = house.Map?.GetLayer("Buildings");
        if (buildings is null)
        {
            return null;
        }
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                if (house.doesTileHaveProperty(x, y, "Action", "Buildings") == "Carpenter")
                {
                    return new Point(x, y);
                }
            }
        }
        return null;
    }

    private static Point? FarmhouseFixtureStandTile(GameLocation house, Point actionTile)
    {
        foreach (var tile in new[]
        {
            new Point(actionTile.X, actionTile.Y + 1),
            new Point(actionTile.X - 1, actionTile.Y),
            new Point(actionTile.X + 1, actionTile.Y),
            new Point(actionTile.X, actionTile.Y - 1)
        })
        {
            if (IsTileOnMap(house, tile) && IsTileWalkable(house, tile) && !IsTileOccupiedByCharacter(house, tile))
            {
                return tile;
            }
        }
        return null;
    }

    private static Point? FarmhouseFixtureRobinTile(GameLocation house, Point actionTile, Point standTile)
    {
        foreach (var tile in new[]
        {
            new Point(actionTile.X, actionTile.Y - 1),
            new Point(actionTile.X - 1, actionTile.Y - 1),
            new Point(actionTile.X + 1, actionTile.Y - 1),
            new Point(actionTile.X - 2, actionTile.Y),
            new Point(actionTile.X + 2, actionTile.Y)
        })
        {
            if (tile != actionTile && tile != standTile && IsTileOnMap(house, tile) &&
                Vector2.Distance(new Vector2(tile.X, tile.Y), new Vector2(actionTile.X, actionTile.Y)) <= 3f)
            {
                return tile;
            }
        }
        return null;
    }
}
