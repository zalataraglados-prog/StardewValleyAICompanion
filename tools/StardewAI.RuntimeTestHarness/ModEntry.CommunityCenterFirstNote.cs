using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartCommunityCenterFirstNote(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "interact",
                InteractRequestedEffect(request),
                InteractObservedEffect(),
                reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.CommunityCenterNoteTileX.HasValue ||
            !request.CommunityCenterNoteTileY.HasValue ||
            request.BundleAreaId != 1 ||
            !string.Equals(request.BundleAreaName, "Crafts Room", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "CommunityCenterBundleNote", StringComparison.Ordinal) ||
            !string.Equals(
                request.NativeContract,
                "CommunityCenter.checkAction_then_JunimoNoteMenu.setUpMenu",
                StringComparison.Ordinal))
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_typed_contract_required");
            return;
        }
        if (activeCommunityCenterFirstNote is not null ||
            activeCommunityCenterDonation is not null ||
            Game1.activeClickableMenu is not null ||
            Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_player_busy");
            return;
        }
        if (Game1.currentLocation is not CommunityCenter communityCenter ||
            !string.Equals(
                communityCenter.NameOrUniqueName,
                request.LocationId,
                StringComparison.OrdinalIgnoreCase))
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_location_mismatch");
            return;
        }
        if (!Game1.MasterPlayer.mailReceived.Contains("ccDoorUnlock") ||
            Game1.player.hasOrWillReceiveMail("seenJunimoNote") ||
            Game1.player.hasOrWillReceiveMail("canReadJunimoText") ||
            Game1.player.hasOrWillReceiveMail("wizardJunimoNote"))
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_lifecycle_drifted");
            return;
        }

        var jojaLocked = Game1.MasterPlayer.hasOrWillReceiveMail("JojaMember");
        var ccLocked = Game1.MasterPlayer.hasOrWillReceiveMail("ccIsComplete") ||
            Game1.MasterPlayer.hasCompletedCommunityCenter();
        var liveRoute = jojaLocked && ccLocked
            ? "conflicting_irreversible_flags"
            : jojaLocked
                ? "joja_locked"
                : ccLocked
                    ? "community_center_locked"
                    : "undecided";
        if (!string.Equals(request.RouteState, liveRoute, StringComparison.Ordinal) ||
            liveRoute is not ("undecided" or "community_center_locked"))
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_route_state_drifted");
            return;
        }

        var noteTile = new Point(
            request.CommunityCenterNoteTileX.Value,
            request.CommunityCenterNoteTileY.Value);
        var interactionTile = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var standTile = new Point(
            request.StandTileX.Value,
            request.StandTileY.Value);
        var liveNoteTile = CommunityCenterNoteTileRuntime(communityCenter, 1);
        var liveInteractionTile = CommunityCenterInteractionTileRuntime(
            communityCenter,
            1,
            liveNoteTile);
        if (liveNoteTile != noteTile ||
            liveInteractionTile != interactionTile ||
            !AreAdjacent(interactionTile, standTile) ||
            !communityCenter.shouldNoteAppearInArea(1) ||
            !communityCenter.isJunimoNoteAtArea(1) ||
            !CommunityCenterFirstNoteNativeTargetMatches(
                communityCenter,
                interactionTile,
                standTile) ||
            communityCenter.bundleMutexes[1].IsLocked() ||
            !IsTileOnMap(communityCenter, standTile) ||
            !IsTileWalkable(communityCenter, standTile) ||
            IsTileOccupiedByCharacter(communityCenter, standTile))
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_projection_drifted");
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            communityCenter,
            Game1.player.TilePoint,
            standTile,
            maxMovementTiles,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            CompleteCommunityCenterFirstNoteBlocked(
                pending,
                "community_center_first_note_path_unavailable:" + pathReason);
            return;
        }

        activeCommunityCenterFirstNote = new ActiveCommunityCenterFirstNote(
            pending,
            communityCenter,
            interactionTile,
            standTile,
            path,
            maxMovementTiles,
            HasPendingCommunityCenterMail(Game1.player, "wizardJunimoNote"));
    }

    private void TickCommunityCenterFirstNote()
    {
        var active = activeCommunityCenterFirstNote;
        if (active is null)
            return;

        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.CommunityCenter) ||
            active.ElapsedTicks > 1200)
        {
            CompleteCommunityCenterFirstNoteBlocked(
                active,
                "community_center_first_note_world_location_or_timeout");
            return;
        }

        if (!active.OpenIssued && Game1.player.TilePoint != active.StandTile)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteCommunityCenterFirstNoteBlocked(
                    active,
                    "community_center_first_note_path_exhausted");
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
            var playerTile = Game1.player.TilePoint;
            if (playerTile != active.LastObservedTile)
            {
                active.StuckTicks = 0;
                active.MovementTiles += ManhattanDistance(
                    active.LastObservedTile,
                    playerTile);
                active.LastObservedTile = playerTile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteCommunityCenterFirstNoteBlocked(
                        active,
                        "community_center_first_note_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                CompleteCommunityCenterFirstNoteBlocked(
                    active,
                    "community_center_first_note_movement_stuck_or_blocked");
                return;
            }
            if (playerTile == next)
                active.PathIndex++;
            return;
        }

        StopAllMovement();
        if (!active.OpenIssued)
        {
            if (active.CommunityCenter.bundleMutexes[1].IsLocked() ||
                Game1.player.hasOrWillReceiveMail("seenJunimoNote"))
            {
                CompleteCommunityCenterFirstNoteBlocked(
                    active,
                    "community_center_first_note_preopen_projection_drifted");
                return;
            }
            Game1.player.faceDirection(DirectionTo(
                Game1.player.TilePoint,
                active.InteractionTile));
            active.CheckActionHandled = active.CommunityCenter.checkAction(
                new TileLocation(active.InteractionTile.X, active.InteractionTile.Y),
                new TileRectangle(
                    Game1.viewport.X,
                    Game1.viewport.Y,
                    Game1.viewport.Width,
                    Game1.viewport.Height),
                Game1.player);
            active.OpenIssued = true;
            return;
        }

        if (active.CheckActionHandled &&
            Game1.activeClickableMenu is JunimoNoteMenu menu &&
            menu.whichArea == 1 &&
            Game1.player.mailReceived.Contains("seenJunimoNote") &&
            HasPendingCommunityCenterMail(Game1.player, "wizardJunimoNote"))
        {
            CompleteCommunityCenterFirstNote(active);
            return;
        }
        if ((Game1.activeClickableMenu is not null &&
            Game1.activeClickableMenu is not JunimoNoteMenu) ||
            ++active.OpenWaitTicks > 240)
        {
            CompleteCommunityCenterFirstNoteBlocked(
                active,
                "community_center_first_note_native_menu_open_failed");
        }
    }

    private void CompleteCommunityCenterFirstNote(
        ActiveCommunityCenterFirstNote active)
    {
        activeCommunityCenterFirstNote = null;
        StopAllMovement();
        var request = active.Pending.Request;
        var wizardPendingAfter = HasPendingCommunityCenterMail(
            Game1.player,
            "wizardJunimoNote");
        var wizardReceivedAfter = Game1.player.mailReceived.Contains(
            "wizardJunimoNote");
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
            PrimitiveKind = "interact",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "CommunityCenter.checkAction_handled",
                "JunimoNoteMenu.setUpMenu_seenJunimoNote_received",
                "JunimoNoteMenu.setUpMenu_wizardJunimoNote_scheduled"
            },
            RequestedEffect = InteractRequestedEffect(request),
            ObservedEffect = InteractObservedEffect() +
                ";first_junimo_note_seen=true" +
                ";wizard_letter_pending=" + wizardPendingAfter.ToString().ToLowerInvariant() +
                ";wizard_letter_received=" + wizardReceivedAfter.ToString().ToLowerInvariant(),
            BlockReasons = Array.Empty<string>(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "world_progress.community_center.lifecycle.first_junimo_note_seen",
                    Before = "false",
                    After = "true"
                },
                new SimulatedFactChange
                {
                    Path = "world_progress.community_center.lifecycle.wizard_letter_pending",
                    Before = active.WizardPendingBefore.ToString().ToLowerInvariant(),
                    After = wizardPendingAfter.ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "world_progress.community_center.lifecycle.wizard_letter_received",
                    Before = "false",
                    After = wizardReceivedAfter.ToString().ToLowerInvariant()
                }
            }
        });
    }

    private void CompleteCommunityCenterFirstNoteBlocked(
        ActiveCommunityCenterFirstNote active,
        string reason)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is JunimoNoteMenu menu &&
            menu.whichArea == 1)
        {
            menu.exitThisMenu();
        }
        activeCommunityCenterFirstNote = null;
        CompleteCommunityCenterFirstNoteBlocked(active.Pending, reason);
    }

    private static void CompleteCommunityCenterFirstNoteBlocked(
        PendingExecution pending,
        string reason)
    {
        pending.Completion.SetResult(BlockedWithPrimitive(
            pending.Request,
            "interact",
            InteractRequestedEffect(pending.Request),
            InteractObservedEffect(),
            reason));
    }

    private static bool CommunityCenterFirstNoteNativeTargetMatches(
        CommunityCenter communityCenter,
        Point interactionTile,
        Point standTile)
    {
        var tileIndex = communityCenter.getTileIndexAt(
            interactionTile.X,
            interactionTile.Y,
            "Buildings",
            "indoors");
        var areaMethod = typeof(CommunityCenter).GetMethod(
            "getAreaNumberFromLocation",
            System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
        return tileIndex is >= 1824 and <= 1833 &&
            areaMethod?.Invoke(
                communityCenter,
                new object[] { standTile.ToVector2() }) is int areaId &&
            areaId == 1;
    }

    private sealed class ActiveCommunityCenterFirstNote
    {
        public ActiveCommunityCenterFirstNote(
            PendingExecution pending,
            CommunityCenter communityCenter,
            Point interactionTile,
            Point standTile,
            List<Point> path,
            int maxMovementTiles,
            bool wizardPendingBefore)
        {
            Pending = pending;
            CommunityCenter = communityCenter;
            InteractionTile = interactionTile;
            StandTile = standTile;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            WizardPendingBefore = wizardPendingBefore;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public CommunityCenter CommunityCenter { get; }
        public Point InteractionTile { get; }
        public Point StandTile { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public bool WizardPendingBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int MovementTiles { get; set; }
        public int StuckTicks { get; set; }
        public Point LastObservedTile { get; set; }
        public bool OpenIssued { get; set; }
        public bool CheckActionHandled { get; set; }
        public int OpenWaitTicks { get; set; }
    }
}
