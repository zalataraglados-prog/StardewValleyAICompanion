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
            PrimitiveVerificationStatus = "verified_native_carpenter_lifecycle",
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
}
