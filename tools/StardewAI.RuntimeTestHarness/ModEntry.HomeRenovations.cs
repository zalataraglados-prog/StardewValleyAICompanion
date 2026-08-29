using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string HomeRenovationPayloadSha256 = "26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b";
    private const string HomeRenovationNativeContract =
        "GameLocation.checkAction Carpenter -> answerDialogue Renovate -> ShopMenu HouseRenovations exact row -> RenovateMenu hover and world-region click -> native validate, money/FirstPurchase, renovation actions, UpdateForRenovation, renovateEvent, animation and return; no direct money, mail, NetInt, map, furniture, menu, viewport or event mutation";
    private static readonly FieldInfo? RenovateMenuRenovationField = AccessTools.Field(typeof(RenovateMenu), "_renovation");
    private static readonly FieldInfo? RenovateMenuSelectedIndexField = AccessTools.Field(typeof(RenovateMenu), "_selectedIndex");
    private static readonly JsonSerializerOptions HomeRenovationPayloadOptions = new()
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private sealed class ActiveHomeRenovation
    {
        public ActiveHomeRenovation(
            PendingExecution pending,
            GameLocation service,
            FarmHouse home,
            Point action,
            Point stand,
            List<Point> path)
        {
            Pending = pending;
            Service = service;
            Home = home;
            Action = action;
            Stand = stand;
            Path = path;
            LastTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Service { get; }
        public FarmHouse Home { get; }
        public Point Action { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public Point LastTile { get; set; }
        public int PathIndex { get; set; }
        public int ElapsedTicks { get; set; }
        public int StuckTicks { get; set; }
        public int Cooldown { get; set; }
        public bool CarpenterOpened { get; set; }
        public bool RenovateResponseChosen { get; set; }
        public bool ShopRowClicked { get; set; }
        public bool RegionHovered { get; set; }
        public bool RegionClicked { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }

    private void StartHomeRenovation(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (activeHomeRenovation is not null || HasActiveExecutorOperation() ||
            Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(HomeRenovationBlocked(request, "home_renovation_player_busy"));
            return;
        }
        var requestReason = string.Empty;
        FarmHouse? home = null;
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !HomeRenovationRequestExact(request, out home, out requestReason))
        {
            pending.Completion.SetResult(HomeRenovationBlocked(request,
                string.IsNullOrWhiteSpace(requestReason) ? "home_renovation_typed_projection_required" : requestReason));
            return;
        }
        var service = Game1.currentLocation;
        var action = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (service is null || !string.Equals(service.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            service.NameOrUniqueName != "ScienceHouse" || !AreAdjacent(action, stand) ||
            service.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") != "Carpenter")
        {
            pending.Completion.SetResult(HomeRenovationBlocked(request, "home_renovation_service_action_or_stand_drifted"));
            return;
        }
        if (!HomeRenovationLivePreconditionsMatch(request, service, home!, action, out requestReason))
        {
            pending.Completion.SetResult(HomeRenovationBlocked(request, requestReason));
            return;
        }
        var path = TryBuildTilePath(service, Game1.player.TilePoint, stand, request.MaxMovementTiles ?? 512,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(HomeRenovationBlocked(request, "home_renovation_path_unavailable:" + pathReason));
            return;
        }
        activeHomeRenovation = new ActiveHomeRenovation(pending, service, home!, action, stand, path);
    }

    private void TickHomeRenovation()
    {
        var active = activeHomeRenovation;
        if (active is null)
            return;
        try
        {
            if (++active.ElapsedTicks > 4200)
            {
                CompleteHomeRenovationBlocked(active, "home_renovation_timeout");
                return;
            }
            if (!active.CarpenterOpened && Game1.player.TilePoint != active.Stand)
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_path_exhausted");
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
                    CompleteHomeRenovationBlocked(active, "home_renovation_movement_stuck");
                return;
            }

            StopAllMovement();
            if (active.Cooldown-- > 0)
                return;
            var request = active.Pending.Request;
            if (!active.CarpenterOpened)
            {
                if (!HomeRenovationLivePreconditionsMatch(request, active.Service, active.Home, active.Action, out var reason))
                {
                    CompleteHomeRenovationBlocked(active, reason);
                    return;
                }
                Game1.player.faceDirection(DirectionTo(active.Stand, active.Action));
                var handled = active.Service.checkAction(
                    new xTile.Dimensions.Location(active.Action.X, active.Action.Y),
                    new xTile.Dimensions.Rectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                    Game1.player);
                if (!handled || Game1.activeClickableMenu is not DialogueBox)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_carpenter_action_not_handled");
                    return;
                }
                active.CarpenterOpened = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.RenovateResponseChosen)
            {
                if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion ||
                    active.Service.lastQuestionKey != "carpenter")
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_expected_carpenter_question_missing");
                    return;
                }
                var response = dialogue.responses?.FirstOrDefault(value => value.responseKey == "Renovate");
                if (response is null || !active.Service.answerDialogue(response))
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_native_renovate_response_failed");
                    return;
                }
                active.RenovateResponseChosen = true;
                active.Cooldown = 8;
                return;
            }
            if (!active.ShopRowClicked)
            {
                if (Game1.activeClickableMenu is not ShopMenu shop || shop.ShopId != "HouseRenovations")
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_expected_native_shop_missing");
                    return;
                }
                if (shop.safetyTimer > 0)
                    return;
                var ids = shop.forSale.OfType<HouseRenovation>().Select(value => value.Name).ToArray();
                if (!TryReadStringArray(request.NativeAvailableRenovationIdsJson, out var expectedIds) ||
                    !ids.SequenceEqual(expectedIds, StringComparer.Ordinal) ||
                    request.NativeRenovationShopIndex is null || request.NativeRenovationShopIndex < 0 ||
                    request.NativeRenovationShopIndex >= ids.Length || ids[request.NativeRenovationShopIndex.Value] != request.RenovationId)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_native_shop_order_drifted");
                    return;
                }
                var index = request.NativeRenovationShopIndex.Value;
                if (index < shop.currentItemIndex)
                {
                    shop.receiveLeftClick(shop.upArrow.bounds.Center.X, shop.upArrow.bounds.Center.Y);
                    active.Cooldown = 2;
                    return;
                }
                if (index >= shop.currentItemIndex + shop.forSaleButtons.Count)
                {
                    shop.receiveLeftClick(shop.downArrow.bounds.Center.X, shop.downArrow.bounds.Center.Y);
                    active.Cooldown = 2;
                    return;
                }
                var row = shop.forSaleButtons[index - shop.currentItemIndex];
                shop.receiveLeftClick(row.bounds.Center.X, row.bounds.Center.Y);
                if (Game1.activeClickableMenu is not RenovateMenu)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_native_shop_row_click_rejected");
                    return;
                }
                active.ShopRowClicked = true;
                active.Cooldown = 10;
                return;
            }
            if (!active.RegionClicked)
            {
                if (Game1.activeClickableMenu is not RenovateMenu menu || Game1.globalFade)
                    return;
                if (!ReferenceEquals(Game1.currentLocation, active.Home) ||
                    RenovateMenuRenovationField?.GetValue(menu) is not HouseRenovation renovation ||
                    renovation.Name != request.RenovationId ||
                    request.RenovationSelectedIndex is null ||
                    request.RenovationSelectedIndex < 0 || request.RenovationSelectedIndex >= renovation.renovationBounds.Count ||
                    !RenovationBoundsMatchRequest(renovation, request))
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_live_menu_identity_or_bounds_drifted");
                    return;
                }
                var rect = renovation.renovationBounds[request.RenovationSelectedIndex.Value].FirstOrDefault();
                if (rect.Width <= 0 || rect.Height <= 0)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_selected_region_empty");
                    return;
                }
                var worldCenter = new Vector2((rect.X + rect.Width / 2f) * Game1.tileSize,
                    (rect.Y + rect.Height / 2f) * Game1.tileSize);
                var screen = Game1.GlobalToLocal(Game1.viewport, worldCenter);
                var screenX = (int)screen.X;
                var screenY = (int)screen.Y;
                if (screenX < 32 || screenX >= Game1.uiViewport.Width - 32 ||
                    screenY < 32 || screenY >= Game1.uiViewport.Height - 32)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_selected_region_not_visible_in_native_view");
                    return;
                }
                Game1.setMousePosition(screenX, screenY, ui_scale: false);
                menu.performHoverAction(screenX, screenY);
                if (RenovateMenuSelectedIndexField?.GetValue(menu) is not int selected || selected != request.RenovationSelectedIndex.Value)
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_native_hover_selection_failed");
                    return;
                }
                active.RegionHovered = true;
                menu.receiveLeftClick(screenX, screenY);
                if (!HomeRenovationImmediatePostconditionsMatch(request, active.Home))
                {
                    CompleteHomeRenovationBlocked(active, "home_renovation_native_click_not_settled_or_postconditions_mismatch");
                    return;
                }
                active.RegionClicked = true;
                active.Cooldown = 4;
                return;
            }

            if (Game1.activeClickableMenu is not null || !ReferenceEquals(Game1.currentLocation, active.Service))
                return;
            if (!HomeRenovationImmediatePostconditionsMatch(request, active.Home) ||
                Game1.player.viewingLocation.Value is not null || !Game1.displayHUD || !Game1.displayFarmer)
            {
                CompleteHomeRenovationBlocked(active, "home_renovation_native_return_or_final_receipt_mismatch");
                return;
            }
            CompleteHomeRenovation(active);
        }
        catch (Exception ex)
        {
            CompleteHomeRenovationBlocked(active, "home_renovation_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static bool HomeRenovationRequestExact(
        TrainingExecutionRequest request,
        out FarmHouse? home,
        out string reason)
    {
        home = Utility.getHomeOfFarmer(Game1.player) as FarmHouse;
        reason = "home_renovation_typed_projection_required";
        if (home is null || request.OptionId != "executor.renovate_home" ||
            !request.RenovationSelectedIndex.HasValue || request.RenovationSelectedIndex < 0 ||
            string.IsNullOrWhiteSpace(request.RenovationId) || string.IsNullOrWhiteSpace(request.RenovationReason) ||
            request.ConfirmRenovation != true ||
            request.RenovationIsDestructive == true && request.ConfirmDestructiveRenovation != true ||
            request.HomeLocationId != home.NameOrUniqueName ||
            request.HomeRuntimeType != (home.GetType().FullName ?? home.GetType().Name) ||
            request.ExpectedHomeHouseUpgradeLevel != home.upgradeLevel || request.ExpectedHomeHouseUpgradeLevel < 2 ||
            request.HomeRenovationDataPayloadSha256 != HomeRenovationPayloadSha256 ||
            request.HomeRenovationDataContractStatus != "exact_locked_base_1.6.15" ||
            request.NativeContract != HomeRenovationNativeContract || request.BuilderActionRaw != "Carpenter" ||
            !request.NativeRenovationShopIndex.HasValue || request.NativeRenovationShopIndex < 0 ||
            string.IsNullOrWhiteSpace(request.RenovationRoomId) ||
            request.RenovationAnimationType is not ("build" or "destroy") ||
            !request.RenovationCheckForObstructions.HasValue ||
            string.IsNullOrWhiteSpace(request.RenovationFirstPurchaseMailId) ||
            !request.RenovationFirstPurchaseMailBefore.HasValue ||
            !request.ExpectedRenovationFirstPurchaseMailAfter.HasValue ||
            !request.RenovationRefundEligible.HasValue ||
            string.IsNullOrWhiteSpace(request.RenovationRequirementsJson) ||
            string.IsNullOrWhiteSpace(request.RenovateActionsJson) ||
            string.IsNullOrWhiteSpace(request.SelectedRegionRectanglesJson) ||
            string.IsNullOrWhiteSpace(request.RenovationProjectionFingerprint) ||
            !request.ExpectedMoneyBefore.HasValue || !request.Price.HasValue || !request.ExpectedMoneyAfter.HasValue)
            return false;
        var expectedMoney = request.Price < 0
            ? request.RenovationRefundEligible == true ? request.ExpectedMoneyBefore - request.Price : request.ExpectedMoneyBefore
            : request.ExpectedMoneyBefore - request.Price;
        if (request.ExpectedMoneyAfter != expectedMoney ||
            request.RenovationFirstPurchaseMailId != "FirstPurchase_" + request.RenovationRoomId ||
            request.ExpectedRenovationFirstPurchaseMailAfter != (request.Price >= 0 || request.RenovationFirstPurchaseMailBefore == true))
            return false;
        reason = string.Empty;
        return true;
    }

    private static bool HomeRenovationLivePreconditionsMatch(
        TrainingExecutionRequest request,
        GameLocation service,
        FarmHouse home,
        Point action,
        out string reason)
    {
        reason = "home_renovation_live_preconditions_drifted";
        if (!request.RenovationSelectedIndex.HasValue)
            return false;
        var selectedIndex = request.RenovationSelectedIndex.Value;
        var data = DataLoader.HomeRenovations(Game1.content);
        var payload = HomeRenovationPayloadHash(data);
        var available = HouseRenovation.GetAvailableRenovations().OfType<HouseRenovation>().ToArray();
        if (!TryReadStringArray(request.NativeAvailableRenovationIdsJson, out var expectedIds) ||
            payload != HomeRenovationPayloadSha256 || payload != request.HomeRenovationDataPayloadSha256 ||
            !available.Select(value => value.Name).SequenceEqual(expectedIds, StringComparer.Ordinal) ||
            request.NativeRenovationShopIndex is null || request.NativeRenovationShopIndex >= available.Length)
            return false;
        var renovation = available[request.NativeRenovationShopIndex.Value];
        var robin = service.characters.FirstOrDefault(value => value.Name == "Robin");
        if (renovation.Name != request.RenovationId || renovation.Price != request.Price ||
            renovation.RoomId != request.RenovationRoomId ||
            renovation.animationType.ToString().Equals(request.RenovationAnimationType, StringComparison.OrdinalIgnoreCase) == false ||
            Game1.player.Money != request.ExpectedMoneyBefore ||
            Game1.player.mailReceived.Contains(request.RenovationFirstPurchaseMailId) != request.RenovationFirstPurchaseMailBefore ||
            Game1.player.daysUntilHouseUpgrade.Value >= 0 || Game1.IsThereABuildingUnderConstruction() ||
            home.upgradeLevel != request.ExpectedHomeHouseUpgradeLevel ||
            robin is null || Vector2.Distance(robin.Tile, new Vector2(action.X, action.Y)) > 3f ||
            !RenovationBoundsMatchRequest(renovation, request) ||
            !RenovationValuesMatchRequest(home, request.RenovationRequirementsJson, selectedIndex, after: false) ||
            !RenovationValuesMatchRequest(home, request.RenovateActionsJson, selectedIndex, after: false))
            return false;
        if (request.RenovationCheckForObstructions == true &&
            (request.SelectedRegionObstructionStatus != "clear" || renovation.validate is null ||
             !renovation.validate(renovation, selectedIndex)))
        {
            reason = "home_renovation_selected_region_obstructed_or_validation_drifted";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool HomeRenovationImmediatePostconditionsMatch(TrainingExecutionRequest request, FarmHouse home) =>
        Game1.player.Money == request.ExpectedMoneyAfter &&
        Game1.player.mailReceived.Contains(request.RenovationFirstPurchaseMailId) == request.ExpectedRenovationFirstPurchaseMailAfter &&
        RenovationValuesMatchRequest(home, request.RenovateActionsJson, request.RenovationSelectedIndex!.Value, after: true);

    private static bool RenovationValuesMatchRequest(FarmHouse home, string json, int selectedIndex, bool after)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var row in document.RootElement.EnumerateArray())
            {
                var type = JsonText(row, "type");
                var key = JsonText(row, "key");
                var expression = JsonText(row, "value_expression");
                if (type == "Value")
                {
                    var field = home.GetType().GetField(key, BindingFlags.Instance | BindingFlags.Public)?.GetValue(home) as NetInt;
                    if (field is null)
                        return false;
                    var expected = after
                        ? expression == "selected" ? selectedIndex : int.Parse(expression)
                        : JsonNullableInt(row, "current_int_value");
                    if (!expected.HasValue || field.Value != expected.Value)
                        return false;
                }
                else if (type == "Mail")
                {
                    var expected = after ? expression != "0" : JsonNullableBool(row, "current_bool_value");
                    if (!expected.HasValue || Game1.player.hasOrWillReceiveMail(key) != expected.Value)
                        return false;
                }
                else
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or OverflowException)
        {
            return false;
        }
    }

    private static bool RenovationBoundsMatchRequest(HouseRenovation renovation, TrainingExecutionRequest request)
    {
        try
        {
            using var document = JsonDocument.Parse(request.SelectedRegionRectanglesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array || request.RenovationSelectedIndex is null ||
                request.RenovationSelectedIndex < 0 || request.RenovationSelectedIndex >= renovation.renovationBounds.Count)
                return false;
            var expected = document.RootElement.EnumerateArray()
                .Select(row => new Rectangle(JsonInt(row, "x"), JsonInt(row, "y"), JsonInt(row, "width"), JsonInt(row, "height")))
                .ToArray();
            return renovation.renovationBounds[request.RenovationSelectedIndex.Value].SequenceEqual(expected);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void CompleteHomeRenovation(ActiveHomeRenovation active)
    {
        StopAllMovement();
        activeHomeRenovation = null;
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
            PrimitiveKind = "renovate_home",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_Carpenter_Renovate_response_completed",
                "exact_HouseRenovations_shop_order_and_row_completed",
                "native_RenovateMenu_hover_and_world_region_click_completed",
                "money_FirstPurchase_action_state_animation_and_return_verified"
            },
            RequestedEffect = "home=" + request.HomeLocationId + ";renovation=" + request.RenovationId +
                ";selected_index=" + request.RenovationSelectedIndex,
            ObservedEffect = "money=" + Game1.player.Money + ";first_purchase=" +
                Game1.player.mailReceived.Contains(request.RenovationFirstPurchaseMailId).ToString().ToLowerInvariant() +
                ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";menu=none;native_action_state_verified=true",
            BlockReasons = Array.Empty<string>(),
            EstimatedTicks = 1800,
            ActualTicks = active.ElapsedTicks,
            TargetLocation = active.Service.NameOrUniqueName,
            TargetTileX = active.Action.X,
            TargetTileY = active.Action.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.money", Before = request.ExpectedMoneyBefore.ToString()!, After = Game1.player.Money.ToString() },
                new SimulatedFactChange { Path = "player.mail_received." + request.RenovationFirstPurchaseMailId, Before = request.RenovationFirstPurchaseMailBefore.ToString()!, After = Game1.player.mailReceived.Contains(request.RenovationFirstPurchaseMailId).ToString() },
                new SimulatedFactChange { Path = "world_progress.marriage_house.home_renovations." + request.RenovationId, Before = "available", After = "native_action_applied" }
            }
        });
    }

    private void CompleteHomeRenovationBlocked(ActiveHomeRenovation active, string reason)
    {
        StopAllMovement();
        activeHomeRenovation = null;
        active.Pending.Completion.SetResult(HomeRenovationBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult HomeRenovationBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(request, "renovate_home",
            "home=" + request.HomeLocationId + ";renovation=" + request.RenovationId + ";selected_index=" + request.RenovationSelectedIndex,
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") + ";money=" + Game1.player.Money,
            reason);

    private static bool TryReadStringArray(string json, out string[] values)
    {
        try
        {
            values = JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
            return true;
        }
        catch (JsonException)
        {
            values = Array.Empty<string>();
            return false;
        }
    }

    private static string HomeRenovationPayloadHash(Dictionary<string, HomeRenovation> data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, typeof(Dictionary<string, HomeRenovation>), HomeRenovationPayloadOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string JsonText(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int JsonInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : int.MinValue;

    private static int? JsonNullableInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static bool? JsonNullableBool(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value)
            ? value.ValueKind == JsonValueKind.True ? true : value.ValueKind == JsonValueKind.False ? false : null
            : null;
}
