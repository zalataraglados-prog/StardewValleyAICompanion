using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartSleep(PendingExecution pending)
    {
        var reasons = ValidateExecutionRequest(pending.Request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), reasons.ToArray()));
            return;
        }

        var resumesExistingPrompt = string.Equals(
            pending.Request.SleepResumeMode,
            "existing_exact_prompt",
            StringComparison.Ordinal);
        if (Game1.activeClickableMenu is not null &&
            (!resumesExistingPrompt || !SleepPromptOpen()))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), "active_menu_must_be_closed_before_sleep"));
            return;
        }

        if (activeSleep is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), "sleep_executor_busy"));
            return;
        }

        var startTile = Game1.player.TilePoint;
        var target = ResolveHomeSleepTarget(startTile, out var targetReason);
        if (target is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), targetReason));
            return;
        }

        if (resumesExistingPrompt &&
            !AreAdjacent(startTile, target.BedTile) &&
            startTile != target.BedTile)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                pending.Request,
                "sleep",
                "day_transition=new_day",
                SleepObservedEffect(),
                "sleep_resume_player_not_at_or_adjacent_to_bed"));
            return;
        }

        var blockReason = string.Empty;
        var path = resumesExistingPrompt
            ? new List<Point>()
            : TryBuildTilePath(
                Game1.currentLocation,
                startTile,
                target.StandTile,
                512,
                out blockReason,
                avoidSoftObstacles: true);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), blockReason));
            return;
        }

        activeSleep = new ActiveSleep(pending, startTile, target.BedTile, target.StandTile, path, Game1.year, Game1.dayOfMonth, Game1.timeOfDay, Game1.currentSeason);
        if (resumesExistingPrompt)
        {
            activeSleep.Stage = SleepStage.ConfirmPrompt;
        }
        Monitor.Log($"Started terminal sleep macro via stand tile {target.StandTile.X},{target.StandTile.Y} and bed touch tile {target.BedTile.X},{target.BedTile.Y}.", LogLevel.Info);
    }

    private void TickSleep()
    {
        if (activeSleep is null)
        {
            return;
        }

        var sleep = activeSleep;
        if (!Context.IsWorldReady || Game1.currentLocation is null)
        {
            CompleteBlockedSleep(sleep, "world_not_ready_during_sleep");
            return;
        }

        sleep.ElapsedTicks++;
        if (sleep.ElapsedTicks > sleep.MaxTicks)
        {
            CompleteBlockedSleep(sleep, "sleep_macro_timeout");
            return;
        }

        if ((sleep.Stage == SleepStage.MoveToStand || sleep.Stage == SleepStage.StepOntoSleepTouchTile) && SleepPromptOpen())
        {
            StopAllMovement();
            sleep.StuckTicks = 0;
            sleep.Stage = SleepStage.ConfirmPrompt;
        }

        if (sleep.Stage == SleepStage.MoveToStand)
        {
            if (!TickSleepMoveToStand(sleep))
            {
                return;
            }

            sleep.Stage = SleepStage.StepOntoSleepTouchTile;
        }

        if (sleep.Stage == SleepStage.StepOntoSleepTouchTile)
        {
            if (Game1.player.TilePoint != sleep.BedTile && !AreAdjacent(Game1.player.TilePoint, sleep.BedTile))
            {
                CompleteBlockedSleep(sleep, "sleep_bed_tile_not_adjacent_after_move");
                return;
            }

            if (Game1.player.TilePoint != sleep.BedTile)
            {
                var movedSinceLastTick = Vector2.DistanceSquared(sleep.LastPosition, Game1.player.Position) >= 0.01f;
                sleep.LastPosition = Game1.player.Position;
                StartMoving(DirectionTo(Game1.player.TilePoint, sleep.BedTile));
                MovePlayerForTick();
                if (!movedSinceLastTick)
                {
                    sleep.StuckTicks++;
                    if (sleep.StuckTicks > 45)
                    {
                        CompleteBlockedSleep(sleep, "sleep_bed_step_stuck_or_collision_blocked");
                    }
                    return;
                }

                sleep.StuckTicks = 0;
                return;
            }

            StopAllMovement();
            sleep.Stage = SleepStage.TriggerPrompt;
        }

        if (sleep.Stage == SleepStage.TriggerPrompt)
        {
            var touchAction = Game1.currentLocation.doesTileHaveProperty(sleep.BedTile.X, sleep.BedTile.Y, "TouchAction", "Back");
            if (!string.Equals(touchAction, "Sleep", StringComparison.Ordinal))
            {
                CompleteBlockedSleep(sleep, "sleep_touch_action_missing");
                return;
            }

            Game1.currentLocation.performTouchAction("Sleep", new Vector2(sleep.BedTile.X, sleep.BedTile.Y));
            sleep.Stage = SleepStage.ConfirmPrompt;
            return;
        }

        if (sleep.Stage == SleepStage.ConfirmPrompt)
        {
            if (!SleepPromptOpen())
            {
                sleep.PromptWaitTicks++;
                if (sleep.PromptWaitTicks > 60)
                {
                    CompleteBlockedSleep(sleep, "sleep_prompt_not_open_after_touch_action");
                }
                return;
            }

            Game1.currentLocation.answerDialogueAction("Sleep_Yes", new[] { "Sleep" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            sleep.Stage = SleepStage.WaitForNewDay;
            return;
        }

        if (sleep.Stage == SleepStage.WaitForNewDay)
        {
            if (Game1.year != sleep.StartYear || Game1.dayOfMonth != sleep.StartDay || !string.Equals(Game1.currentSeason, sleep.StartSeason, StringComparison.Ordinal))
            {
                sleep.Stage = SleepStage.WaitForPostSleepStable;
                return;
            }
        }

        if (sleep.Stage == SleepStage.WaitForPostSleepStable)
        {
            var menu = Game1.activeClickableMenu;
            if (menu is null)
            {
                if (sleep.SummaryPhase != default)
                {
                    ReleaseSmapiLeftButtonOverride();
                }
                try
                {
                    TrySettleActiveRunPendingShippingReceipts();
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Post-sleep shipping receipt settlement threw: {ex.Message}", LogLevel.Error);
                    CompleteBlockedSleep(sleep, "post_sleep_receipt_settlement_threw:" + ex.GetType().Name);
                    return;
                }
                CompleteSleep(sleep, "verified", new[] { "sleep_yes_confirmed", "new_day_observed", "post_sleep_menu_closed" });
                return;
            }

            if (menu is ShippingMenu shippingMenu)
            {
                TickShipSummaryClosePhase(sleep, shippingMenu);
                return;
            }

            sleep.PostSleepWaitTicks++;
            if (sleep.PostSleepWaitTicks > 600)
            {
                CompleteBlockedSleep(sleep, "post_sleep_menu_not_closed");
            }
        }
    }

    private bool TickSleepMoveToStand(ActiveSleep sleep)
    {
        if (Game1.player.TilePoint == sleep.StandTile)
        {
            StopAllMovement();
            return true;
        }

        if (sleep.PathIndex >= sleep.Path.Count)
        {
            CompleteBlockedSleep(sleep, "sleep_path_exhausted_before_stand_tile");
            return false;
        }

        var currentTile = Game1.player.TilePoint;
        var nextTile = sleep.Path[sleep.PathIndex];
        if (currentTile == nextTile)
        {
            sleep.PathIndex++;
            sleep.StuckTicks = 0;
            StopAllMovement();
            return false;
        }

        if (!AreAdjacent(currentTile, nextTile))
        {
            CompleteBlockedSleep(sleep, "sleep_path_desynchronized");
            return false;
        }

        if (!IsTileWalkable(Game1.currentLocation, nextTile))
        {
            CompleteBlockedSleep(sleep, "sleep_path_blocked");
            return false;
        }

        var movedSinceLastTick = Vector2.DistanceSquared(sleep.LastPosition, Game1.player.Position) >= 0.01f;
        sleep.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(currentTile, nextTile));
        MovePlayerForTick();
        if (Game1.player.TilePoint == nextTile)
        {
            sleep.PathIndex++;
        }

        if (!movedSinceLastTick)
        {
            sleep.StuckTicks++;
        }
        else
        {
            sleep.StuckTicks = 0;
        }

        if (sleep.StuckTicks > 45)
        {
            CompleteBlockedSleep(sleep, "sleep_movement_stuck_or_collision_blocked");
        }

        return false;
    }

    private void TickShipSummaryClosePhase(ActiveSleep sleep, ShippingMenu shippingMenu)
    {
        switch (sleep.SummaryPhase)
        {
            case ShipSummaryClosePhase.WaitReady:
                {
                    if (shippingMenu.CanReceiveInput() && shippingMenu.currentPage == -1)
                    {
                        sleep.SummaryPhase = ShipSummaryClosePhase.Position;
                        sleep.SummaryPositionSet = false;
                        sleep.SummaryPositionVerified = false;
                        sleep.SummaryButtonPressed = false;
                        sleep.SummaryButtonReleased = false;
                    }
                }
                break;

            case ShipSummaryClosePhase.Position:
                if (sleep.SummaryPositionSet && !sleep.SummaryPositionVerified)
                    sleep.SummaryPhase = ShipSummaryClosePhase.PositionVerify;
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (sleep.SummaryPositionVerified && !sleep.SummaryButtonPressed)
                    sleep.SummaryPhase = ShipSummaryClosePhase.Press;
                break;

            case ShipSummaryClosePhase.Press:
                if (sleep.SummaryButtonPressed && !sleep.SummaryButtonReleased)
                    sleep.SummaryPhase = ShipSummaryClosePhase.Release;
                break;

            case ShipSummaryClosePhase.Release:
                if (sleep.SummaryButtonReleased)
                {
                    sleep.SummaryButtonPressed = false;
                    sleep.SummaryButtonReleased = false;
                    sleep.SummaryPositionSet = false;
                    sleep.SummaryPositionVerified = false;
                    sleep.SummaryPhase = ShipSummaryClosePhase.WaitClose;
                }
                break;

            case ShipSummaryClosePhase.WaitClose:
                break;
        }
    }

    private void ApplyShipSummaryInput(ActiveSleep sleep)
    {
        if (Game1.activeClickableMenu is not ShippingMenu shippingMenu) return;

        switch (sleep.SummaryPhase)
        {
            case ShipSummaryClosePhase.Position:
                if (!sleep.SummaryPositionSet)
                {
                    var okButton = shippingMenu.okButton;
                    if (okButton is null)
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep, "shipping_summary_ok_button_null");
                        return;
                    }
                    var bounds = okButton.bounds;
                    var target = new Point(bounds.Center.X, bounds.Center.Y);
                    Game1.setMousePosition(target.X, target.Y, ui_scale: true);
                    sleep.SummaryPositionTarget = target;
                    sleep.SummaryPositionSet = true;
                }
                break;

            case ShipSummaryClosePhase.PositionVerify:
                if (sleep.SummaryPositionSet && !sleep.SummaryPositionVerified)
                {
                    var ax = Game1.getMouseX(ui_scale: true);
                    var ay = Game1.getMouseY(ui_scale: true);
                    if (Math.Abs(ax - sleep.SummaryPositionTarget.X) > 2 || Math.Abs(ay - sleep.SummaryPositionTarget.Y) > 2)
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep,
                            "shipping_summary_cursor_position_mismatch:expected=" + sleep.SummaryPositionTarget.X + "," + sleep.SummaryPositionTarget.Y + ";actual=" + ax + "," + ay);
                        return;
                    }
                    sleep.SummaryPositionVerified = true;
                }
                break;

            case ShipSummaryClosePhase.Press:
                if (!sleep.SummaryButtonPressed)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: true, out var reason))
                    {
                        ReleaseSmapiLeftButtonOverride();
                        CompleteBlockedSleep(sleep, "shipping_summary_press_failed:" + reason);
                        return;
                    }
                    sleep.SummaryButtonPressed = true;
                }
                break;

            case ShipSummaryClosePhase.Release:
                if (!sleep.SummaryButtonReleased)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: false, out var relReason))
                    {
                        sleep.SummaryReleaseRetries++;
                        if (sleep.SummaryReleaseRetries > 3)
                        {
                            ReleaseSmapiLeftButtonOverride();
                            CompleteBlockedSleep(sleep, "shipping_summary_release_failed_after_retries:" + relReason);
                            return;
                        }
                        return;
                    }
                    sleep.SummaryButtonReleased = true;
                }
                break;
        }
    }

    private void CompleteSleep(ActiveSleep sleep, string verificationStatus, string[] verificationReasons)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeSleep = null;
        sleep.Pending.Completion.SetResult(CompletedSleep(sleep, verificationStatus, verificationReasons));
    }

    private void CompleteBlockedSleep(ActiveSleep sleep, string reason)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeSleep = null;
        sleep.Pending.Completion.SetResult(BlockedWithPrimitive(sleep.Pending.Request, "sleep", "day_transition=new_day", SleepObservedEffect(), reason));
    }

    private static TrainingExecutionResult CompletedSleep(ActiveSleep sleep, string verificationStatus, string[] verificationReasons)
    {
        var request = sleep.Pending.Request;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verificationStatus == "verified" ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = sleep.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "sleep",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "day_transition=new_day",
            ObservedEffect = SleepObservedEffect(),
            BlockReasons = verificationStatus == "verified" ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.tile", Before = sleep.StartTile.X + "," + sleep.StartTile.Y, After = Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y },
                new SimulatedFactChange { Path = "time.year", Before = sleep.StartYear.ToString(), After = Game1.year.ToString() },
                new SimulatedFactChange { Path = "time.season", Before = sleep.StartSeason, After = Game1.currentSeason },
                new SimulatedFactChange { Path = "time.day", Before = sleep.StartDay.ToString(), After = Game1.dayOfMonth.ToString() },
                new SimulatedFactChange { Path = "time.time", Before = sleep.StartTime.ToString(), After = Game1.timeOfDay.ToString() },
                new SimulatedFactChange { Path = "menus.active_menu.is_open", Before = sleep.StartMenuOpen.ToString().ToLowerInvariant(), After = (Game1.activeClickableMenu is not null).ToString().ToLowerInvariant() }
            }
        };
    }

    private static SleepTarget? ResolveHomeSleepTarget(Point startTile, out string reason)
    {
        reason = string.Empty;
        if (Game1.currentLocation is not FarmHouse farmHouse)
        {
            reason = "sleep_current_location_not_home";
            return null;
        }

        var bedTile = farmHouse.GetPlayerBedSpot();
        if (!BedFurniture.IsBedHere(farmHouse, bedTile.X, bedTile.Y))
        {
            reason = "sleep_bed_tile_unverified";
            return null;
        }

        var stand = new[]
        {
            new Point(bedTile.X - 1, bedTile.Y),
            new Point(bedTile.X + 1, bedTile.Y),
            new Point(bedTile.X, bedTile.Y + 1),
            new Point(bedTile.X, bedTile.Y - 1)
        }
            .Where(tile => IsTileWalkable(farmHouse, tile))
            .OrderBy(tile => tile == startTile ? 1 : 0)
            .ThenBy(tile => Math.Abs(startTile.X - tile.X) + Math.Abs(startTile.Y - tile.Y))
            .FirstOrDefault();

        if (stand == default)
        {
            reason = "sleep_stand_tile_unavailable";
            return null;
        }

        return new SleepTarget(bedTile, stand);
    }

    private static bool SleepPromptOpen()
    {
        return Game1.activeClickableMenu is DialogueBox && string.Equals(Game1.currentLocation?.lastQuestionKey, "Sleep", StringComparison.Ordinal);
    }

    private static string SleepObservedEffect()
    {
        return "time.day=" + Game1.dayOfMonth + ";time.time=" + Game1.timeOfDay + ";location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }

    private TrainingExecutionResult CompletedMove(PendingExecution pending, Point startTile, Point targetTile, Point observedTile, string verificationStatus, string[] verificationReasons)
    {
        var request = pending.Request;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verificationStatus == "verified" ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "move_to_tile",
            PrimitiveVerificationStatus = verificationStatus,
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "player.tile=" + targetTile.X + "," + targetTile.Y,
            ObservedEffect = "player.tile=" + observedTile.X + "," + observedTile.Y + MovementCostSuffix(pending),
            BlockReasons = verificationStatus == "verified" ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = MovementChangedFacts(pending, startTile, observedTile)
        };
    }
}
