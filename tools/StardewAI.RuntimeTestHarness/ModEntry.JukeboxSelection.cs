using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string JukeboxSelectionNativeContract =
        "Saloon_Jukebox_checkAction->ChooseFromListMenu(default_index_0)->receiveLeftClick_forward_exact_index->receiveLeftClick_ok->Game1_default_music_request_receipt->receiveLeftClick_cancel";

    private void StartJukeboxSelection(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (string.IsNullOrWhiteSpace(request.JukeboxTrackId) || request.ConfirmJukeboxTrack != true ||
            string.IsNullOrWhiteSpace(request.JukeboxReason) || !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue)
        {
            pending.Completion.SetResult(JukeboxSelectionBlocked(request,
                "jukebox_selection_exact_track_reason_confirmation_and_typed_target_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(JukeboxSelectionBlocked(request, "jukebox_selection_player_or_menu_not_ready"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var liveReasons = ValidateJukeboxSelectionLiveState(location, target, stand, request);
        if (liveReasons.Length > 0)
        {
            pending.Completion.SetResult(JukeboxSelectionBlocked(request, liveReasons));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(JukeboxSelectionBlocked(request,
                "jukebox_selection_path_unavailable:" + pathReason));
            return;
        }
        activeJukeboxSelection = new ActiveJukeboxSelection(pending, location, target, stand, path, maxMovementTiles);
    }

    private static string[] ValidateJukeboxSelectionLiveState(
        GameLocation location,
        Point target,
        Point stand,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var tracks = Utility.GetJukeboxTracks(Game1.player, location);
        var trackIndex = tracks.IndexOf(request.JukeboxTrackId);
        var greenRainOverride = Game1.IsGreenRainingHere() &&
            !Game1.currentLocation.InIslandContext() && Game1.IsRainingHere(Game1.currentLocation);
        if (trackIndex < 0 || trackIndex != request.JukeboxTrackIndex ||
            tracks.Count != request.JukeboxUnlockedTrackCount)
            reasons.Add("jukebox_selection_track_catalog_or_index_drifted");
        if (greenRainOverride && !string.Equals(request.JukeboxTrackId, "rain", StringComparison.Ordinal))
            reasons.Add("jukebox_selection_blocked_by_native_green_rain_guard");
        if (!string.Equals(location.NameOrUniqueName, "Saloon", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.Ordinal) ||
            location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") != "Jukebox")
            reasons.Add("jukebox_selection_exact_Saloon_Jukebox_endpoint_missing_or_drifted");
        if (!AreAdjacent(target, stand) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("jukebox_selection_interaction_geometry_drifted");
        if (request.JukeboxDefaultTrackBefore != Game1.getMusicTrackName() ||
            request.JukeboxGreenRainOverride != greenRainOverride ||
            request.JukeboxProjectionFingerprint.Length != 64 ||
            request.JukeboxActionRaw != "Jukebox" || request.ExpectedMenuTypeAfter != "ChooseFromListMenu" ||
            request.ExpectedMenuKind != "jukebox" || request.NativeContract != JukeboxSelectionNativeContract)
            reasons.Add("jukebox_selection_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickJukeboxSelection()
    {
        var active = activeJukeboxSelection;
        if (active is null)
            return;
        active.StageTicks++;
        if (!active.ActionIssued)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "jukebox_selection", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteJukeboxSelection(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving)
                return;
            var liveReasons = ValidateJukeboxSelectionLiveState(active.Location, active.Target, active.Stand,
                active.Pending.Request);
            if (liveReasons.Length > 0)
            {
                CompleteJukeboxSelection(active, false, liveReasons);
                return;
            }
            Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new xTile.Dimensions.Location(active.Target.X, active.Target.Y), Game1.viewport, Game1.player);
            active.ActionIssued = true;
            active.StageTicks = 0;
            if (!active.NativeHandled)
                CompleteJukeboxSelection(active, false, "jukebox_selection_native_action_not_handled");
            return;
        }

        if (Game1.activeClickableMenu is not ChooseFromListMenu menu)
        {
            if (active.OkClicked && active.CancelClicked && Game1.activeClickableMenu is null)
                CompleteJukeboxSelection(active, true);
            else if (active.StageTicks > 180)
                CompleteJukeboxSelection(active, false, "jukebox_selection_native_menu_open_or_close_timeout");
            return;
        }
        if (menu.forwardButton is null || menu.okButton is null || menu.cancelButton is null)
        {
            CompleteJukeboxSelection(active, false, "jukebox_selection_native_menu_identity_drifted");
            return;
        }

        var request = active.Pending.Request;
        if (active.ForwardClicks < request.JukeboxTrackIndex)
        {
            var forward = menu.forwardButton.bounds.Center;
            menu.receiveLeftClick(forward.X, forward.Y);
            active.ForwardClicks++;
            active.StageTicks = 0;
            return;
        }
        if (!active.OkClicked)
        {
            var ok = menu.okButton.bounds.Center;
            menu.receiveLeftClick(ok.X, ok.Y);
            active.OkClicked = true;
            active.StageTicks = 0;
            return;
        }
        if (!string.Equals(Game1.getMusicTrackName(), request.JukeboxTrackId, StringComparison.Ordinal))
        {
            CompleteJukeboxSelection(active, false, "jukebox_selection_native_default_music_receipt_mismatch");
            return;
        }
        if (!active.CancelClicked)
        {
            var cancel = menu.cancelButton.bounds.Center;
            menu.receiveLeftClick(cancel.X, cancel.Y);
            active.CancelClicked = true;
            active.StageTicks = 0;
            return;
        }
        if (Game1.activeClickableMenu is null)
            CompleteJukeboxSelection(active, true);
        else if (active.StageTicks > 180)
            CompleteJukeboxSelection(active, false, "jukebox_selection_native_menu_close_timeout");
    }

    private void CompleteJukeboxSelection(ActiveJukeboxSelection active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeJukeboxSelection = null;
        var request = active.Pending.Request;
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_Saloon_Jukebox_stand",
                "native_checkAction_opened_ChooseFromListMenu_at_default_index_zero",
                "native_forward_ok_and_cancel_controls_received_exact_left_click_sequence",
                "Game1_default_music_context_request_receipt_verified"
            }
            : reasons.Length == 0 ? new[] { "jukebox_selection_post_state_mismatch" } : reasons;
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
            PrimitiveKind = "choose_jukebox_track",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = JukeboxSelectionRequestedEffect(request),
            ObservedEffect = JukeboxSelectionObservedEffect(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.jukebox_selection.default_music_track",
                    Before = request.JukeboxDefaultTrackBefore,
                    After = Game1.getMusicTrackName()
                }
            }
        });
    }

    private static TrainingExecutionResult JukeboxSelectionBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "choose_jukebox_track", JukeboxSelectionRequestedEffect(request),
            JukeboxSelectionObservedEffect(), reasons);

    private static string JukeboxSelectionRequestedEffect(TrainingExecutionRequest request) =>
        "default_music_track=" + request.JukeboxTrackId + ";track_index=" + request.JukeboxTrackIndex;

    private static string JukeboxSelectionObservedEffect() =>
        "default_music_track=" + Game1.getMusicTrackName() +
        ";requested_music_track=" + (Game1.requestedMusicTrack ?? string.Empty) +
        ";current_song=" + (Game1.currentSong?.Name ?? string.Empty) +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
}
