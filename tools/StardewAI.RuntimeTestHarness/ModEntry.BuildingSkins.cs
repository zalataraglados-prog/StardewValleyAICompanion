using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string BuildingSkinNativeContract = "GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.appearance(optional)->BuildingSkinMenu.shortest_exact_clicks->BuildingSkinMenu.Ok";
    private const string BuildingPaintNativeContract = "GameLocation.checkAction->carpenter_Construct->CarpenterMenu.Paint->building_target_click->BuildingPaintMenu.region_navigation->native_slider_or_default_clicks->BuildingPaintMenu.Ok";

    private sealed class ActiveBuildingAppearanceChange
    {
        public ActiveBuildingAppearanceChange(PendingExecution pending, GameLocation service, Point action, Point stand, List<Point> path, Building building)
        {
            Pending = pending;
            Service = service;
            Action = action;
            Stand = stand;
            Path = path;
            Building = building;
            InitialPaint = CaptureBuildingPaint(building);
            LastTile = Game1.player.TilePoint;
        }
        public PendingExecution Pending { get; }
        public GameLocation Service { get; }
        public Point Action { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public Building Building { get; }
        public (bool Default, int Hue, int Saturation, int Lightness)[] InitialPaint { get; }
        public Point LastTile { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public int Cooldown { get; set; }
        public int LocationPageTransitions { get; set; }
        public int SkinClicks { get; set; }
        public int PaintRegionClicks { get; set; }
        public int PaintControlStage { get; set; }
        public bool CarpenterOpened { get; set; }
        public bool ConstructChosen { get; set; }
        public bool PaintChosen { get; set; }
        public bool CursorPositioned { get; set; }
        public int BuildingClickAttempts { get; set; }
        public bool BuildingClicked { get; set; }
        public bool AppearanceClicked { get; set; }
        public bool SkinOkClicked { get; set; }
        public bool PaintOkClicked { get; set; }
        public bool ParentReturnRequested { get; set; }
        public bool ParentExitClicked { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }

    private void StartBuildingAppearanceChange(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (activeBuildingAppearanceChange is not null || HasActiveExecutorOperation() || Game1.activeClickableMenu is not null || Game1.dialogueUp || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BuildingSkinBlocked(request, "change_building_skin_player_busy"));
            return;
        }
        if (!BuildingAppearanceRequestExact(request, out var targetBuilding, out var requestReason))
        {
            pending.Completion.SetResult(BuildingSkinBlocked(request, requestReason));
            return;
        }
        var service = Game1.currentLocation;
        if (service is null || service.NameOrUniqueName != request.LocationId || !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BuildingSkinBlocked(request, "change_building_skin_service_or_tiles_missing"));
            return;
        }
        var action = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!AreAdjacent(action, stand) || service.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != "Carpenter")
        {
            pending.Completion.SetResult(BuildingSkinBlocked(request, "change_building_skin_carpenter_action_drifted"));
            return;
        }
        var path = TryBuildTilePath(service, Game1.player.TilePoint, stand, request.MaxMovementTiles ?? 512,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BuildingSkinBlocked(request, "change_building_skin_path_unavailable:" + pathReason));
            return;
        }
        activeBuildingAppearanceChange = new ActiveBuildingAppearanceChange(pending, service, action, stand, path, targetBuilding!);
    }

    private void TickBuildingAppearanceChange()
    {
        var active = activeBuildingAppearanceChange;
        if (active is null)
            return;
        try
        {
            var request = active.Pending.Request;
            if (++active.ElapsedTicks > 3600)
            {
                CompleteBuildingSkinBlocked(active, "change_building_skin_timeout");
                return;
            }
            if (!active.CarpenterOpened && Game1.player.TilePoint != active.Stand)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_path_exhausted");
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
                    CompleteBuildingSkinBlocked(active, "change_building_skin_movement_stuck");
                return;
            }
            StopAllMovement();
            if (active.Cooldown-- > 0)
                return;
            if (!active.CarpenterOpened)
            {
                Game1.player.faceDirection(DirectionTo(active.Stand, active.Action));
                if (!active.Service.checkAction(new xTile.Dimensions.Location(active.Action.X, active.Action.Y),
                        new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height), Game1.player))
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_carpenter_action_not_handled");
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
                    CompleteBuildingSkinBlocked(active, "change_building_skin_expected_carpenter_question_missing");
                    return;
                }
                var response = dialogue.responses?.FirstOrDefault(value => value.responseKey == "Construct");
                if (response is null || !active.Service.answerDialogue(response))
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_construct_response_failed");
                    return;
                }
                active.ConstructChosen = true;
                active.Cooldown = 8;
                return;
            }
            if (Game1.activeClickableMenu is DialogueBox locationDialogue && locationDialogue.isQuestion)
            {
                var response = locationDialogue.responses?.FirstOrDefault(value => value.responseKey == request.BuildingLocationId);
                if (response is not null && active.Service.answerDialogue(response))
                {
                    active.Cooldown = 8;
                    return;
                }
                var next = locationDialogue.responses?.FirstOrDefault(value => value.responseKey == "nextPage");
                if (next is null || active.LocationPageTransitions++ >= 32 || !active.Service.answerDialogue(next))
                    CompleteBuildingSkinBlocked(active, "change_building_skin_target_location_response_failed");
                active.Cooldown = 8;
                return;
            }
            if (Game1.activeClickableMenu is not CarpenterMenu menu)
            {
                if (active.ParentExitClicked && Game1.activeClickableMenu is null)
                    VerifyBuildingAppearanceSettlement(active);
                return;
            }
            if (!active.PaintChosen)
            {
                if (!menu.paintButton.visible)
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_native_paint_button_unavailable");
                    return;
                }
                menu.receiveLeftClick(menu.paintButton.bounds.Center.X, menu.paintButton.bounds.Center.Y);
                active.PaintChosen = true;
                active.Cooldown = 12;
                return;
            }
            if (active.ParentExitClicked && !menu.onFarm)
            {
                menu.receiveLeftClick(menu.cancelButton.bounds.Center.X, menu.cancelButton.bounds.Center.Y);
                active.Cooldown = 4;
                return;
            }
            if (!menu.onFarm || Game1.IsFading() || Game1.currentLocation?.NameOrUniqueName != request.BuildingLocationId)
                return;
            if (!active.BuildingClicked)
            {
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
                menu.receiveLeftClick(screenX, screenY);
                var opened = menu.GetChildMenu();
                var expectedMenuOpened = !string.IsNullOrWhiteSpace(request.PaintTargetMode)
                    ? opened is BuildingPaintMenu
                    : request.EntryRoute == "building_paint_menu_then_appearance" ? opened is BuildingPaintMenu : opened is BuildingSkinMenu;
                if (!expectedMenuOpened)
                {
                    if (++active.BuildingClickAttempts >= 3)
                    {
                        CompleteBuildingSkinBlocked(active, "change_building_skin_native_building_click_rejected");
                        return;
                    }
                    active.CursorPositioned = false;
                    active.Cooldown = 4;
                    return;
                }
                active.BuildingClicked = true;
                active.Cooldown = 8;
                return;
            }
            var child = menu.GetChildMenu();
            if (!string.IsNullOrWhiteSpace(request.PaintTargetMode) && child is BuildingPaintMenu buildingPaint && !active.PaintOkClicked)
            {
                if (!AdvanceBuildingPaintMenu(active, buildingPaint, out var paintReason))
                {
                    if (!string.IsNullOrEmpty(paintReason))
                        CompleteBuildingSkinBlocked(active, paintReason);
                    return;
                }
                active.PaintOkClicked = true;
                active.Cooldown = 8;
                return;
            }
            if (child is BuildingPaintMenu paint && !active.AppearanceClicked)
            {
                if (request.EntryRoute != "building_paint_menu_then_appearance" || !paint.appearanceButton.visible || !ReferenceEquals(paint.building, active.Building))
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_paint_menu_route_drifted");
                    return;
                }
                paint.receiveLeftClick(paint.appearanceButton.bounds.Center.X, paint.appearanceButton.bounds.Center.Y);
                active.AppearanceClicked = true;
                active.Cooldown = 6;
                return;
            }
            var skin = FindBuildingSkinMenu(menu);
            if (skin is not null && !active.SkinOkClicked)
            {
                if (!ReferenceEquals(skin.Building, active.Building))
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_live_menu_building_drifted");
                    return;
                }
                if (!SkinMenuMatchesRequest(skin, request, active.SkinClicks, out var menuDriftReason))
                {
                    CompleteBuildingSkinBlocked(active, menuDriftReason);
                    return;
                }
                if (active.SkinClicks < request.ShortestClickCount)
                {
                    var button = request.ShortestClickDirection == "next" ? skin.NextSkinButton : skin.PreviousSkinButton;
                    skin.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y);
                    active.SkinClicks++;
                    active.Cooldown = 2;
                    return;
                }
                if (SkinKey(active.Building.skinId.Value) != request.TargetSkinKey)
                {
                    CompleteBuildingSkinBlocked(active, "change_building_skin_target_not_reached_after_exact_clicks");
                    return;
                }
                skin.receiveLeftClick(skin.OkButton.bounds.Center.X, skin.OkButton.bounds.Center.Y);
                active.SkinOkClicked = true;
                active.Cooldown = 8;
                return;
            }
            if ((active.SkinOkClicked || active.PaintOkClicked) && !active.ParentReturnRequested)
            {
                child = menu.GetChildMenu();
                if (child is BuildingPaintMenu returnedPaint)
                    returnedPaint.receiveLeftClick(returnedPaint.okButton.bounds.Center.X, returnedPaint.okButton.bounds.Center.Y);
                else if (child is not null)
                    return;
                active.ParentReturnRequested = true;
                active.Cooldown = 6;
                return;
            }
            if (active.ParentReturnRequested && !active.ParentExitClicked)
            {
                if (menu.GetChildMenu() is not null)
                    return;
                menu.receiveLeftClick(menu.cancelButton.bounds.Center.X, menu.cancelButton.bounds.Center.Y);
                active.ParentExitClicked = true;
                active.Cooldown = 12;
                return;
            }
        }
        catch (Exception ex)
        {
            CompleteBuildingSkinBlocked(active, "change_building_skin_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static BuildingSkinMenu? FindBuildingSkinMenu(IClickableMenu root)
    {
        IClickableMenu? current = root.GetChildMenu();
        while (current is not null)
        {
            if (current is BuildingSkinMenu result)
                return result;
            current = current.GetChildMenu();
        }
        return null;
    }

    private static bool SkinMenuMatchesRequest(
        BuildingSkinMenu menu,
        TrainingExecutionRequest request,
        int appliedClicks,
        out string reason)
    {
        string[] expected;
        try { expected = JsonSerializer.Deserialize<string[]>(request.AvailableSkinKeysJson) ?? Array.Empty<string>(); }
        catch (JsonException)
        {
            reason = "change_building_skin_compiled_menu_keys_invalid_json";
            return false;
        }
        var actual = menu.Skins.Select(value => SkinKey(value.Id)).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal) || actual.Length != request.AvailableSkinCount)
        {
            reason = "change_building_skin_live_menu_order_drifted:actual=" + string.Join(",", actual);
            return false;
        }
        var direction = request.ShortestClickDirection == "next" ? 1 : -1;
        var expectedIndex = (request.CurrentSkinIndex!.Value + direction * appliedClicks) % actual.Length;
        if (expectedIndex < 0)
            expectedIndex += actual.Length;
        if (menu.Skin.Index != expectedIndex)
        {
            reason = "change_building_skin_live_menu_index_drifted:expected=" + expectedIndex + ";actual=" + menu.Skin.Index;
            return false;
        }
        if (request.ShortestClickCount <= 0 || request.ShortestClickDirection is not ("next" or "previous"))
        {
            reason = "change_building_skin_compiled_click_plan_invalid";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool BuildingAppearanceRequestExact(TrainingExecutionRequest request, out Building? building, out string reason)
    {
        building = Game1.getLocationFromName(request.BuildingLocationId)?.buildings.FirstOrDefault(value =>
            value.buildingType.Value == request.BuildingType && value.tileX.Value == request.BuildingTileX && value.tileY.Value == request.BuildingTileY);
        if (!string.IsNullOrWhiteSpace(request.PaintTargetMode))
            return BuildingPaintRequestExact(request, building, out reason);
        reason = "change_building_skin_typed_request_invalid";
        if (building is null || request.NativeContract != BuildingSkinNativeContract || request.BuilderActionRaw != "Carpenter" ||
            string.IsNullOrWhiteSpace(request.AppearanceReason) || request.BuildingIdentity != request.BuildingLocationId + ":" + request.BuildingType + ":" + request.BuildingTileX + "," + request.BuildingTileY ||
            request.CurrentSkinKey != SkinKey(building.skinId.Value) || request.TargetSkinKey == request.CurrentSkinKey ||
            !request.CurrentSkinIndex.HasValue || !request.TargetSkinIndex.HasValue || !request.AvailableSkinCount.HasValue ||
            !request.ShortestClickCount.HasValue || request.ShortestClickCount <= 0 || request.ShortestClickDirection is not ("next" or "previous") ||
            request.EntryRoute != (building.CanBePainted() ? "building_paint_menu_then_appearance" : "direct_building_skin_menu") ||
            !request.SkinChangeResetsAllPaintColorsToDefault || building.daysOfConstructionLeft.Value > 0 || building.daysUntilUpgrade.Value > 0 ||
            !building.CanBeReskinned(ignoreSeparateConstructionEntries: !building.CanBePainted()))
            return false;
        reason = string.Empty;
        return true;
    }

    private static string SkinKey(string? skinId) => skinId ?? "__default__";

    private void VerifyBuildingAppearanceSettlement(ActiveBuildingAppearanceChange active)
    {
        var request = active.Pending.Request;
        if (!string.IsNullOrWhiteSpace(request.PaintTargetMode))
        {
            VerifyBuildingPaintSettlement(active);
            return;
        }
        var colors = active.Building.netBuildingPaintColor.Value;
        if (SkinKey(active.Building.skinId.Value) != request.TargetSkinKey ||
            !colors.Color1Default.Value || !colors.Color2Default.Value || !colors.Color3Default.Value ||
            active.SkinClicks != request.ShortestClickCount)
        {
            CompleteBuildingSkinBlocked(active, "change_building_skin_native_postconditions_mismatch");
            return;
        }
        activeBuildingAppearanceChange = null;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId, Status = "applied", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"), PrimitiveKind = "change_building_skin",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_Carpenter_Paint_selected", "exact_target_building_and_skin_menu_reached", "shortest_exact_skin_clicks_and_default_paint_colors_verified" },
            RequestedEffect = "building=" + request.BuildingIdentity + ";skin=" + request.TargetSkinKey,
            ObservedEffect = "skin=" + SkinKey(active.Building.skinId.Value) + ";paint_colors_default=true;clicks=" + active.SkinClicks,
            ActualTicks = active.ElapsedTicks, TargetLocation = request.BuildingLocationId, TargetTileX = request.BuildingTileX, TargetTileY = request.BuildingTileY
        });
    }

    private void CompleteBuildingSkinBlocked(ActiveBuildingAppearanceChange active, string reason)
    {
        StopAllMovement();
        activeBuildingAppearanceChange = null;
        active.Pending.Completion.SetResult(BuildingSkinBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult BuildingSkinBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, string.IsNullOrWhiteSpace(request.PaintTargetMode) ? "change_building_skin" : "paint_building_region",
            string.IsNullOrWhiteSpace(request.PaintTargetMode) ? "building=" + request.BuildingIdentity + ";skin=" + request.TargetSkinKey : "building=" + request.BuildingIdentity + ";region=" + request.PaintRegionId,
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none"), reason);

}
