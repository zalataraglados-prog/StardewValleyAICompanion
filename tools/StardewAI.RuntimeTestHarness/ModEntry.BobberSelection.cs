using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string BobberSelectionNativeContract =
        "FishShop_Bobbers_checkAction->ChooseFromIconsMenu(bobbers)->receiveLeftClick_exact_unlocked_icon->Farmer.bobberStyle_and_usingRandomizedBobber_receipt->native_close_button";

    private void StartBobberSelection(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.BobberStyleId.HasValue || request.ConfirmBobberStyle != true ||
            string.IsNullOrWhiteSpace(request.BobberReason) || !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(BobberSelectionBlocked(request,
                "bobber_selection_exact_style_reason_confirmation_and_typed_target_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BobberSelectionBlocked(request, "bobber_selection_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var liveReasons = ValidateBobberSelectionLiveState(location, target, stand, request);
        if (liveReasons.Length > 0)
        {
            pending.Completion.SetResult(BobberSelectionBlocked(request, liveReasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BobberSelectionBlocked(request,
                "bobber_selection_path_unavailable:" + pathReason));
            return;
        }
        activeBobberSelection = new ActiveBobberSelection(pending, location, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateBobberSelectionLiveState(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var styleId = request.BobberStyleId ?? -1;
        var unlockQuotient = Game1.player.fishCaught.Count() / 2;
        if (styleId is < -2 or > 38 || styleId == -1 || styleId >= 0 && styleId > unlockQuotient)
            reasons.Add("bobber_selection_style_locked_or_unknown");
        if (!string.Equals(location.NameOrUniqueName, "FishShop", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
            location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") != "Bobbers")
            reasons.Add("bobber_selection_exact_FishShop_Bobbers_endpoint_missing_or_drifted");
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("bobber_selection_interaction_geometry_drifted");
        if (request.BobberStyleBefore != Game1.player.bobberStyle.Value ||
            request.BobberRandomBefore != Game1.player.usingRandomizedBobber ||
            request.BobberRandomAfter != (styleId == -2) ||
            request.BobberFishCaughtSpeciesCount != Game1.player.fishCaught.Count() ||
            request.BobberNativeUnlockQuotient != unlockQuotient ||
            request.BobberProjectionFingerprint.Length != 64 ||
            request.BobberActionRaw != "Bobbers" || request.ExpectedMenuTypeAfter != "ChooseFromIconsMenu" ||
            request.ExpectedMenuKind != "bobbers" || request.NativeContract != BobberSelectionNativeContract)
            reasons.Add("bobber_selection_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickBobberSelection()
    {
        var active = activeBobberSelection;
        if (active is null)
            return;
        active.StageTicks++;
        if (!active.ActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "bobber_selection", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteBobberSelection(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving)
                return;
            var liveReasons = ValidateBobberSelectionLiveState(active.Location, active.Target, active.Stand,
                active.Pending.Request);
            if (liveReasons.Length > 0)
            {
                CompleteBobberSelection(active, false, liveReasons);
                return;
            }
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new xTile.Dimensions.Location(active.Target.X, active.Target.Y), Game1.viewport, Game1.player);
            active.ActionIssued = true;
            active.StageTicks = 0;
            if (!active.NativeHandled)
                CompleteBobberSelection(active, false, "bobber_selection_native_action_not_handled");
            return;
        }

        if (!active.IconClicked)
        {
            if (Game1.activeClickableMenu is not ChooseFromIconsMenu menu)
            {
                if (active.StageTicks > 180)
                    CompleteBobberSelection(active, false, "bobber_selection_native_menu_open_timeout");
                return;
            }
            var expectedNames = Enumerable.Range(0, FishingRod.NUM_BOBBER_STYLES)
                .Select(value => value.ToString()).Append("-2");
            if (menu.icons.Count != FishingRod.NUM_BOBBER_STYLES + 1 ||
                menu.iconFronts.Count != menu.icons.Count || !menu.icons.Select(icon => icon.name).SequenceEqual(expectedNames))
            {
                CompleteBobberSelection(active, false, "bobber_selection_native_menu_identity_drifted");
                return;
            }
            var index = active.Pending.Request.BobberStyleId == -2
                ? FishingRod.NUM_BOBBER_STYLES
                : active.Pending.Request.BobberStyleId!.Value;
            if (menu.iconFronts[index].name.Contains("ghosted", StringComparison.Ordinal))
            {
                CompleteBobberSelection(active, false, "bobber_selection_native_icon_became_locked");
                return;
            }
            var center = menu.icons[index].bounds.Center;
            menu.receiveLeftClick(center.X, center.Y);
            active.IconClicked = true;
            active.StageTicks = 0;
            return;
        }

        var request = active.Pending.Request;
        if (Game1.player.bobberStyle.Value != request.BobberStyleId ||
            Game1.player.usingRandomizedBobber != request.BobberRandomAfter)
        {
            CompleteBobberSelection(active, false, "bobber_selection_native_preference_receipt_mismatch");
            return;
        }
        if (Game1.activeClickableMenu is ChooseFromIconsMenu openMenu && !active.CloseClicked)
        {
            if (openMenu.upperRightCloseButton is null || !openMenu.readyToClose())
            {
                if (active.StageTicks > 180)
                    CompleteBobberSelection(active, false, "bobber_selection_native_close_control_unavailable");
                return;
            }
            var close = openMenu.upperRightCloseButton.bounds.Center;
            openMenu.receiveLeftClick(close.X, close.Y);
            active.CloseClicked = true;
            active.StageTicks = 0;
            return;
        }
        if (Game1.activeClickableMenu is null)
            CompleteBobberSelection(active, true);
        else if (active.StageTicks > 180)
            CompleteBobberSelection(active, false, "bobber_selection_native_menu_close_timeout");
    }

    private void CompleteBobberSelection(ActiveBobberSelection active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeBobberSelection = null;
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_FishShop_Bobbers_stand",
                "native_checkAction_opened_exact_bobbers_ChooseFromIconsMenu",
                "native_unlocked_icon_and_close_controls_received_left_clicks",
                "bobberStyle_and_usingRandomizedBobber_receipt_verified"
            }
            : reasons.Length == 0 ? new[] { "bobber_selection_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_only_executor_calibration",
            PrimitiveKind = "choose_bobber_style",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = BobberSelectionRequestedEffect(request),
            ObservedEffect = BobberSelectionObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.bobber_selection.current_style_id", Before = request.BobberStyleBefore?.ToString() ?? string.Empty, After = Game1.player.bobberStyle.Value.ToString() },
                new SimulatedFactChange { Path = "player.bobber_selection.using_randomized_bobber", Before = request.BobberRandomBefore?.ToString() ?? string.Empty, After = Game1.player.usingRandomizedBobber.ToString() }
            }
        });
    }

    private static TrainingExecutionResult BobberSelectionBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "choose_bobber_style", BobberSelectionRequestedEffect(request),
            BobberSelectionObservedEffect(), reasons);

    private static string BobberSelectionRequestedEffect(TrainingExecutionRequest request) =>
        "bobber_style_id=" + request.BobberStyleId + ";using_randomized_bobber=" + request.BobberRandomAfter;

    private static string BobberSelectionObservedEffect() =>
        "bobber_style_id=" + Game1.player.bobberStyle.Value +
        ";using_randomized_bobber=" + Game1.player.usingRandomizedBobber.ToString().ToLowerInvariant() +
        ";fish_caught_species_count=" + Game1.player.fishCaught.Count() +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
}
