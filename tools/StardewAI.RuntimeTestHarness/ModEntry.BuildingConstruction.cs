using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Quests;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string BuildingConstructionNativeContract = "GameLocation.checkAction_Carpenter->answerDialogue_carpenter_Construct->CarpenterMenu.receiveLeftClick->tryToBuild->Building.FinishConstruction->HaveBuildingQuest.OnBuildingExists";

    private sealed class ActiveBuildingConstruction
    {
        public ActiveBuildingConstruction(PendingExecution pending, GameLocation service, Point action, Point stand, List<Point> path, Dictionary<string, int> materialCounts)
        {
            Pending = pending;
            Service = service;
            Action = action;
            Stand = stand;
            Path = path;
            MaterialCountsBefore = materialCounts;
            LastTile = Game1.player.TilePoint;
            MoneyBefore = Game1.player.Money;
        }
        public PendingExecution Pending { get; }
        public GameLocation Service { get; }
        public Point Action { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public Dictionary<string, int> MaterialCountsBefore { get; }
        public int MoneyBefore { get; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public Point LastTile { get; set; }
        public bool CarpenterOpened { get; set; }
        public bool ConstructChosen { get; set; }
        public bool BlueprintChosen { get; set; }
        public bool OkClicked { get; set; }
        public bool CursorPositioned { get; set; }
        public bool PlacementClicked { get; set; }
        public int Cooldown { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }

    private void StartBuildingConstruction(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (activeBuildingConstruction is not null || HasActiveExecutorOperation() ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, "construct_building_player_busy"));
            return;
        }
        if (!BuildingConstructionRequestExact(request, out var materials, out var requestReason))
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, requestReason));
            return;
        }
        var service = Game1.currentLocation;
        if (service is null || service.NameOrUniqueName != "ScienceHouse" ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, "construct_building_service_or_tiles_missing"));
            return;
        }
        var action = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!AreAdjacent(action, stand) || service.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != "Carpenter")
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, "construct_building_carpenter_action_drifted"));
            return;
        }
        if (!BuildingConstructionLivePreconditions(request, materials))
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, "construct_building_live_preconditions_drifted"));
            return;
        }
        var path = TryBuildTilePath(service, Game1.player.TilePoint, stand, request.MaxMovementTiles ?? 512,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BuildingConstructionBlocked(request, "construct_building_path_unavailable:" + pathReason));
            return;
        }
        activeBuildingConstruction = new ActiveBuildingConstruction(pending, service, action, stand, path, materials);
    }

    private void TickBuildingConstruction()
    {
        var active = activeBuildingConstruction;
        if (active is null)
        {
            return;
        }
        try
        {
            active.ElapsedTicks++;
            if (active.ElapsedTicks > 4200)
            {
                CompleteBuildingConstructionBlocked(active, "construct_building_timeout");
                return;
            }
            if (!active.CarpenterOpened && Game1.player.TilePoint != active.Stand)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_path_exhausted");
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
                if (Game1.player.TilePoint != active.LastTile)
                {
                    active.LastTile = Game1.player.TilePoint;
                    active.StuckTicks = 0;
                }
                else if (++active.StuckTicks > 60)
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_movement_stuck");
                }
                return;
            }
            StopAllMovement();
            if (active.Cooldown > 0)
            {
                active.Cooldown--;
                return;
            }
            if (!active.CarpenterOpened)
            {
                Game1.player.faceDirection(DirectionTo(active.Stand, active.Action));
                if (!active.Service.checkAction(
                        new xTile.Dimensions.Location(active.Action.X, active.Action.Y),
                        new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                        Game1.player))
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_carpenter_action_not_handled");
                    return;
                }
                active.CarpenterOpened = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.ConstructChosen)
            {
                if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion || active.Service.lastQuestionKey != "carpenter")
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_expected_carpenter_question_missing");
                    return;
                }
                var response = dialogue.responses?.FirstOrDefault(value => value.responseKey == "Construct");
                if (response is null || !active.Service.answerDialogue(response))
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_construct_response_failed");
                    return;
                }
                active.ConstructChosen = true;
                active.Cooldown = 8;
                return;
            }
            if (Game1.activeClickableMenu is DialogueBox locationDialogue && locationDialogue.isQuestion)
            {
                var farmResponse = locationDialogue.responses?.FirstOrDefault(value => value.responseKey == "Farm");
                if (farmResponse is null || !active.Service.answerDialogue(farmResponse))
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_farm_location_response_failed");
                }
                active.Cooldown = 8;
                return;
            }
            if (Game1.activeClickableMenu is not CarpenterMenu menu)
            {
                if (active.PlacementClicked)
                {
                    VerifyBuildingConstructionSettlement(active);
                    return;
                }
                return;
            }
            var request = active.Pending.Request;
            if (!active.BlueprintChosen)
            {
                var blueprint = menu.Blueprints.FirstOrDefault(value => value.Id == request.ConstructionBuildingType && value.Skin is null);
                if (blueprint is null)
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_blueprint_missing");
                    return;
                }
                menu.SetNewActiveBlueprint(blueprint);
                if (!menu.CanBuildCurrentBlueprint())
                {
                    CompleteBuildingConstructionBlocked(active, "construct_building_native_blueprint_precheck_failed");
                    return;
                }
                active.BlueprintChosen = true;
                active.Cooldown = 4;
                return;
            }
            if (!active.OkClicked)
            {
                menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
                active.OkClicked = true;
                active.Cooldown = 12;
                return;
            }
            if (!menu.onFarm || Game1.IsFading() || Game1.currentLocation?.NameOrUniqueName != request.PlacementLocationId)
            {
                return;
            }
            var worldX = request.BuildingTileX!.Value * Game1.tileSize + Game1.tileSize / 2;
            var worldY = request.BuildingTileY!.Value * Game1.tileSize + Game1.tileSize / 2;
            Game1.viewport.X = Math.Max(0, worldX - Game1.viewport.Width / 2);
            Game1.viewport.Y = Math.Max(0, worldY - Game1.viewport.Height / 2);
            var screenX = worldX - Game1.viewport.X;
            var screenY = worldY - Game1.viewport.Y;
            if (!active.CursorPositioned)
            {
                Game1.setMousePosition(screenX, screenY, ui_scale: false);
                active.CursorPositioned = true;
                active.Cooldown = 2;
                return;
            }
            if (!active.PlacementClicked)
            {
                menu.receiveLeftClick(screenX, screenY);
                active.PlacementClicked = true;
                active.Cooldown = 12;
                return;
            }
            VerifyBuildingConstructionSettlement(active);
        }
        catch (Exception ex)
        {
            CompleteBuildingConstructionBlocked(active, "construct_building_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private void VerifyBuildingConstructionSettlement(ActiveBuildingConstruction active)
    {
        var request = active.Pending.Request;
        var farm = Game1.getLocationFromName(request.PlacementLocationId);
        var building = farm?.buildings.FirstOrDefault(value =>
            value.buildingType.Value == request.ConstructionBuildingType &&
            value.tileX.Value == request.BuildingTileX && value.tileY.Value == request.BuildingTileY);
        if (building is null)
        {
            if (active.ElapsedTicks < 4100)
            {
                return;
            }
            CompleteBuildingConstructionBlocked(active, "construct_building_native_settlement_missing");
            return;
        }
        if (building.daysOfConstructionLeft.Value != request.ConstructionBuildDays ||
            Game1.player.Money != request.ExpectedMoneyAfter ||
            active.MaterialCountsBefore.Any(pair => Game1.player.Items.CountId(pair.Key) != pair.Value - RequiredMaterialCount(request, pair.Key)))
        {
            CompleteBuildingConstructionBlocked(active, "construct_building_native_postconditions_mismatch");
            return;
        }
        activeBuildingConstruction = null;
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
            PrimitiveKind = "construct_building",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_Carpenter_Construct_selected", "native_CarpenterMenu_blueprint_and_placement_clicked", "native_building_countdown_and_resource_consumption_verified" },
            RequestedEffect = "farm.buildings[" + request.ConstructionBuildingType + "].days_of_construction_left=" + request.ConstructionBuildDays,
            ObservedEffect = "building=" + request.ConstructionBuildingType + ";tile=" + request.BuildingTileX + "," + request.BuildingTileY + ";days_left=" + building.daysOfConstructionLeft.Value,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = request.PlacementLocationId,
            TargetTileX = request.BuildingTileX,
            TargetTileY = request.BuildingTileY,
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestId = request.QuestId,
            QuestPresentBefore = true,
            QuestPresentAfter = Game1.player.questLog.Any(value => value.id.Value == request.QuestId),
            QuestCompletedBefore = false,
            QuestCompletedAfter = false
        });
    }

    private static bool BuildingConstructionRequestExact(TrainingExecutionRequest request, out Dictionary<string, int> materials, out string reason)
    {
        materials = new Dictionary<string, int>(StringComparer.Ordinal);
        reason = "construct_building_typed_request_invalid";
        if (request.QuestFamily != "ordinary_quest" || request.QuestRuntimeType != "HaveBuildingQuest" ||
            request.ConstructionBuildingType != request.ProjectId || request.NativeContract != BuildingConstructionNativeContract ||
            request.PlacementLocationId != "Farm" || !request.BuildingTileX.HasValue || !request.BuildingTileY.HasValue ||
            !request.ConstructionBuildDays.HasValue || !request.Price.HasValue || request.Price < 0 ||
            request.ExpectedMoneyAfter != request.ExpectedMoneyBefore - request.Price ||
            request.PlacementVerification != "static_native_predicates_passed_runtime_recheck_required")
        {
            return false;
        }
        try
        {
            using var document = JsonDocument.Parse(request.ConstructionMaterialsJson);
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var id = row.GetProperty("qualified_item_id").GetString() ?? string.Empty;
                var available = row.GetProperty("available_count").GetInt32();
                var required = row.GetProperty("required_count").GetInt32();
                if (string.IsNullOrWhiteSpace(id) || required <= 0 || available < required)
                {
                    return false;
                }
                materials[id] = available;
            }
        }
        catch (JsonException)
        {
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool BuildingConstructionLivePreconditions(TrainingExecutionRequest request, IReadOnlyDictionary<string, int> materials)
    {
        var quest = Game1.player.questLog.OfType<HaveBuildingQuest>().SingleOrDefault(value =>
            value.id.Value == request.QuestId && value.buildingType.Value == request.ConstructionBuildingType &&
            value.accepted.Value && !value.completed.Value);
        return quest is not null && !Game1.IsThereABuildingUnderConstruction() &&
            Game1.player.Money == request.ExpectedMoneyBefore &&
            materials.All(pair => Game1.player.Items.CountId(pair.Key) == pair.Value);
    }

    private static int RequiredMaterialCount(TrainingExecutionRequest request, string itemId)
    {
        using var document = JsonDocument.Parse(request.ConstructionMaterialsJson);
        return document.RootElement.EnumerateArray()
            .Where(row => row.GetProperty("qualified_item_id").GetString() == itemId)
            .Select(row => row.GetProperty("required_count").GetInt32())
            .FirstOrDefault();
    }

    private void CompleteBuildingConstructionBlocked(ActiveBuildingConstruction active, string reason)
    {
        StopAllMovement();
        activeBuildingConstruction = null;
        active.Pending.Completion.SetResult(BuildingConstructionBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult BuildingConstructionBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "construct_building",
            "farm.buildings[" + request.ConstructionBuildingType + "].construction_started=true",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") + ";money=" + Game1.player.Money,
            reason);
}
