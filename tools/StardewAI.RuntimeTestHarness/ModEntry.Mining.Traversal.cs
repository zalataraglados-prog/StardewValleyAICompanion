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
    private void StartDescendLadder(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "mine.level=before+1;native_action=MineShaft.checkAction(ladder)";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_target_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_tool_or_menu_conflict"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 173)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_tile_not_live_ladder"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var requestedStand = request.StandTileX.HasValue && request.StandTileY.HasValue
            ? new Point(request.StandTileX.Value, request.StandTileY.Value)
            : (Point?)null;
        var path = BuildCompilerAdjacentPath(mine, target, requestedStand, maxMovementTiles, out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_ladder", requested, DescendLadderObservedEffect(), "descend_ladder_path_unavailable:" + pathReason));
            return;
        }

        activeDescendLadder = new ActiveDescendLadder(
            pending,
            mine,
            mine.mineLevel,
            target,
            path,
            maxMovementTiles,
            requestedStand,
            requested);
    }

    private void TickDescendLadder()
    {
        if (activeDescendLadder is null)
        {
            return;
        }

        var active = activeDescendLadder;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_timeout");
            return;
        }

        if (active.ActionIssued)
        {
            if (Game1.currentLocation is MineShaft afterMine && afterMine.mineLevel == active.MineLevelBefore + 1)
            {
                CompleteDescendLadder(active, afterMine);
                return;
            }
            if (!ReferenceEquals(Game1.currentLocation, active.MineBefore) && Game1.currentLocation is not MineShaft)
            {
                CompleteDescendLadderBlocked(active, "descend_ladder_unexpected_location_after_action");
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_location_changed_before_action");
            return;
        }
        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                if (!TryReplanDescendLadder(active, out var exhaustedReason))
                {
                    CompleteDescendLadderBlocked(active, "descend_ladder_path_exhausted:" + exhaustedReason);
                }
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                if (!TryReplanDescendLadder(active, out var repairReason))
                {
                    CompleteDescendLadderBlocked(active, "descend_ladder_replan_failed:" + repairReason);
                }
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    StopAllMovement();
                    if (!TryReplanDescendLadder(active, out var stuckReason))
                    {
                        CompleteDescendLadderBlocked(active, "descend_ladder_stuck_replan_failed:" + stuckReason);
                    }
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 173)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled)
        {
            CompleteDescendLadderBlocked(active, "descend_ladder_native_action_not_handled");
            return;
        }
        active.ActionIssued = true;
    }

    private static bool TryReplanDescendLadder(ActiveDescendLadder active, out string blockReason)
    {
        var repaired = BuildCompilerAdjacentPath(
            active.MineBefore,
            active.Target,
            active.RequestedStand,
            active.MaxMovementTiles,
            out blockReason);
        if (repaired is null)
        {
            return false;
        }
        active.Path = repaired;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private void CompleteDescendLadder(ActiveDescendLadder active, MineShaft afterMine)
    {
        StopAllMovement();
        activeDescendLadder = null;
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
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "descend_ladder",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_ladder", "native_mineshaft_check_action_handled", "exact_next_mine_level_loaded", "no_direct_enter_mine_call" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = DescendLadderObservedEffect(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.current_mine.mine_level", Before = active.MineLevelBefore.ToString(), After = afterMine.mineLevel.ToString() },
                new SimulatedFactChange { Path = "player.location_id", Before = active.MineBefore.NameOrUniqueName, After = afterMine.NameOrUniqueName }
            }
        });
    }

    private void CompleteDescendLadderBlocked(ActiveDescendLadder active, string reason)
    {
        StopAllMovement();
        activeDescendLadder = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "descend_ladder", active.RequestedEffect, DescendLadderObservedEffect(), reason));
    }

    private static string DescendLadderObservedEffect()
    {
        return Game1.currentLocation is MineShaft mine
            ? "location=" + mine.NameOrUniqueName + ";mine_level=" + mine.mineLevel + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y
            : "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";mine_level=none";
    }

    private void StartDescendShaft(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "mine.level=before+expected_delta;player.health=expected_after;native_dialogue=Shaft_Jump";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.ExpectedMineLevelDelta.HasValue || !request.ExpectedMineLevelAfter.HasValue ||
            !request.ExpectedHealthCost.HasValue || !request.ExpectedHealthAfter.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_exact_preview_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_requires_loaded_mineshaft"));
            return;
        }
        if (mine.getMineArea() != MineShaft.desertArea || mine.mineLevel <= MineShaft.bottomOfMineLevel)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_requires_skull_cavern"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_tool_or_menu_conflict"));
            return;
        }

        var expectedDelta = request.ExpectedMineLevelDelta.Value;
        var expectedCost = request.ExpectedHealthCost.Value;
        if (expectedDelta <= 0 || expectedCost != expectedDelta * 3 ||
            request.ExpectedMineLevelAfter.Value != mine.mineLevel + expectedDelta ||
            request.ExpectedHealthAfter.Value != Math.Max(1, Game1.player.health - expectedCost))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_preview_mismatch_live_state"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 174)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_tile_not_live_shaft"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var requestedStand = request.StandTileX.HasValue && request.StandTileY.HasValue
            ? new Point(request.StandTileX.Value, request.StandTileY.Value)
            : (Point?)null;
        var path = BuildCompilerAdjacentPath(mine, target, requestedStand, maxMovementTiles, out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "descend_shaft", requested, DescendShaftObservedEffect(), "descend_shaft_path_unavailable:" + pathReason));
            return;
        }

        activeDescendShaft = new ActiveDescendShaft(
            pending,
            mine,
            mine.mineLevel,
            Game1.player.health,
            target,
            path,
            maxMovementTiles,
            requestedStand,
            expectedDelta,
            request.ExpectedMineLevelAfter.Value,
            expectedCost,
            request.ExpectedHealthAfter.Value,
            requested);
    }

    private void TickDescendShaft()
    {
        if (activeDescendShaft is null)
        {
            return;
        }

        var active = activeDescendShaft;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_timeout");
            return;
        }

        if (active.DialogueConfirmed)
        {
            if (Game1.currentLocation is MineShaft afterMine && afterMine.mineLevel == active.ExpectedMineLevelAfter)
            {
                ReleaseSmapiLeftButtonOverride();
                if (Game1.player.health != active.ExpectedHealthAfter)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_health_after_mismatch");
                    return;
                }
                CompleteDescendShaft(active, afterMine);
                return;
            }

            if (Game1.activeClickableMenu is DialogueBox fallDialogue)
            {
                if (fallDialogue.isQuestion || (fallDialogue.responses?.Length ?? 0) > 0)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_unexpected_question_after_jump");
                    return;
                }
                if (active.FallDialoguePressAttempts >= 8)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_fall_dialogue_press_budget_exhausted");
                    return;
                }

                active.FallDialogueSeen = true;
                if (active.FallDialogueButtonHeld)
                {
                    if (!TryApplySmapiLeftButtonOverride(pressed: false, out var releaseReason))
                    {
                        CompleteDescendShaftBlocked(active, "descend_shaft_fall_dialogue_" + releaseReason);
                        return;
                    }
                    active.FallDialogueButtonHeld = false;
                    active.FallDialoguePressAttempts++;
                    return;
                }
                if (fallDialogue.transitioning || fallDialogue.safetyTimer > 0)
                {
                    return;
                }
                if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_fall_dialogue_" + pressReason);
                    return;
                }
                active.FallDialogueButtonHeld = true;
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_location_changed_before_confirmation");
            return;
        }

        if (active.PromptOpened)
        {
            if (Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "Shaft", StringComparison.Ordinal))
            {
                CompleteDescendShaftBlocked(active, "descend_shaft_prompt_drift");
                return;
            }

            active.MineBefore.answerDialogueAction("Shaft_Jump", new[] { "Shaft", "Jump" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            active.DialogueConfirmed = true;
            return;
        }

        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (!AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                if (!TryReplanDescendShaft(active, out var exhaustedReason))
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_path_exhausted:" + exhaustedReason);
                }
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                var repaired = BuildCompilerAdjacentPath(
                    active.MineBefore,
                    active.Target,
                    active.RequestedStand,
                    active.MaxMovementTiles,
                    out var repairReason);
                if (repaired is null)
                {
                    CompleteDescendShaftBlocked(active, "descend_shaft_replan_failed:" + repairReason);
                    return;
                }
                active.Path = repaired;
                active.PathIndex = 0;
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    StopAllMovement();
                    if (!TryReplanDescendShaft(active, out var stuckReason))
                    {
                        CompleteDescendShaftBlocked(active, "descend_shaft_stuck_replan_failed:" + stuckReason);
                    }
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 174)
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled || Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "Shaft", StringComparison.Ordinal))
        {
            CompleteDescendShaftBlocked(active, "descend_shaft_native_prompt_not_opened");
            return;
        }
        active.PromptOpened = true;
    }

    private static bool TryReplanDescendShaft(ActiveDescendShaft active, out string blockReason)
    {
        var repaired = BuildCompilerAdjacentPath(
            active.MineBefore,
            active.Target,
            active.RequestedStand,
            active.MaxMovementTiles,
            out blockReason);
        if (repaired is null)
        {
            return false;
        }
        active.Path = repaired;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private void CompleteDescendShaft(ActiveDescendShaft active, MineShaft afterMine)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeDescendShaft = null;
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
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "descend_shaft",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "bfs_reached_live_shaft",
                "native_shaft_prompt_observed",
                "native_shaft_jump_answer_handled",
                active.FallDialogueSeen ? "native_fall_dialogue_advanced_by_input" : "native_fall_dialogue_not_observed",
                "exact_previewed_floor_and_health_observed"
            },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = DescendShaftObservedEffect(),
            ShaftMineLevelBefore = active.MineLevelBefore,
            ShaftMineLevelAfter = afterMine.mineLevel,
            ShaftLevelDelta = afterMine.mineLevel - active.MineLevelBefore,
            ShaftHealthBefore = active.HealthBefore,
            ShaftHealthAfter = Game1.player.health,
            ShaftNativeDialogueHandled = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.current_mine.mine_level", Before = active.MineLevelBefore.ToString(), After = afterMine.mineLevel.ToString() },
                new SimulatedFactChange { Path = "player.health", Before = active.HealthBefore.ToString(), After = Game1.player.health.ToString() }
            }
        });
    }

    private void CompleteDescendShaftBlocked(ActiveDescendShaft active, string reason)
    {
        ReleaseSmapiLeftButtonOverride();
        StopAllMovement();
        activeDescendShaft = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "descend_shaft", active.RequestedEffect, DescendShaftObservedEffect(), reason);
        result.ShaftMineLevelBefore = active.MineLevelBefore;
        result.ShaftMineLevelAfter = Game1.currentLocation is MineShaft mine ? mine.mineLevel : null;
        result.ShaftLevelDelta = result.ShaftMineLevelAfter - active.MineLevelBefore;
        result.ShaftHealthBefore = active.HealthBefore;
        result.ShaftHealthAfter = Game1.player.health;
        result.ShaftNativeDialogueHandled = active.DialogueConfirmed;
        active.Pending.Completion.SetResult(result);
    }

    private static string DescendShaftObservedEffect()
    {
        return Game1.currentLocation is MineShaft mine
            ? "location=" + mine.NameOrUniqueName + ";mine_level=" + mine.mineLevel + ";health=" + Game1.player.health + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y
            : "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";mine_level=none;health=" + Game1.player.health;
    }

    private void StartExitMine(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "leave_loaded_mine=true;native_dialogue=ExitMine_Leave;reason=" + request.RetreatReason;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.ExpectedTargetLocation) ||
            !request.ExpectedArrivalTileX.HasValue || !request.ExpectedArrivalTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.RetreatReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_exact_target_and_reason_required"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_requires_loaded_mineshaft"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_menu_conflict"));
            return;
        }

        var expectedDestination = ExpectedMineExitDestination(mine.mineLevel);
        if (!string.Equals(request.ExpectedTargetLocation, expectedDestination.LocationId, StringComparison.Ordinal) ||
            request.ExpectedArrivalTileX.Value != expectedDestination.TileX ||
            request.ExpectedArrivalTileY.Value != expectedDestination.TileY)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_destination_mismatch_live_mine_kind"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        if (mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 115)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_tile_not_live_exit"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var requestedStand = request.StandTileX.HasValue && request.StandTileY.HasValue
            ? new Point(request.StandTileX.Value, request.StandTileY.Value)
            : (Point?)null;
        var path = BuildCompilerMineExitPath(mine, target, requestedStand, maxMovementTiles, out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "exit_mine", requested, ExitMineObservedEffect(), "exit_mine_path_unavailable:" + pathReason));
            return;
        }

        activeExitMine = new ActiveExitMine(
            pending,
            mine,
            mine.mineLevel,
            Game1.timeOfDay,
            Game1.player.health,
            Game1.player.Stamina,
            Game1.player.TilePoint,
            target,
            path,
            maxMovementTiles,
            requestedStand,
            expectedDestination.LocationId,
            expectedDestination.TileX,
            expectedDestination.TileY,
            request.RetreatReason,
            requested);
    }

    private void TickExitMine()
    {
        if (activeExitMine is null)
        {
            return;
        }

        var active = activeExitMine;
        active.ElapsedTicks++;
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteExitMineBlocked(active, "exit_mine_timeout");
            return;
        }

        if (active.DialogueConfirmed)
        {
            if (Game1.currentLocation is not MineShaft)
            {
                if (!string.Equals(Game1.currentLocation?.NameOrUniqueName, active.ExpectedLocationId, StringComparison.Ordinal) ||
                    Game1.player.TilePoint.X != active.ExpectedTileX || Game1.player.TilePoint.Y != active.ExpectedTileY)
                {
                    CompleteExitMineBlocked(active, "exit_mine_destination_after_native_answer_mismatch");
                    return;
                }
                CompleteExitMine(active);
            }
            return;
        }

        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.MineBefore))
        {
            CompleteExitMineBlocked(active, "exit_mine_location_changed_before_confirmation");
            return;
        }

        if (activeEmergencyCombatFood is not null)
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }

        if (active.PostClaimDialogueButtonHeld)
        {
            TryApplySmapiLeftButtonOverride(pressed: false, out _);
            active.PostClaimDialogueButtonHeld = false;
            return;
        }
        if (Game1.activeClickableMenu is DialogueBox postClaimDialogue)
        {
            if (IsNativeExitMinePrompt(active, postClaimDialogue))
            {
                active.PromptOpened = true;
            }
            else
            {
                if (!IsGoldenScytheClaimDialogue(active, postClaimDialogue))
                {
                    CompleteExitMineBlocked(active, "exit_mine_unexpected_dialogue_before_move");
                    return;
                }
                if (postClaimDialogue.transitioning || postClaimDialogue.safetyTimer > 0)
                {
                    return;
                }
                if (active.PostClaimDialoguePressAttempts >= 12)
                {
                    CompleteExitMineBlocked(active, "exit_mine_golden_scythe_claim_dialogue_not_closed");
                    return;
                }
                if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                {
                    CompleteExitMineBlocked(active, "exit_mine_golden_scythe_claim_dialogue_press_failed:" + pressReason);
                    return;
                }
                active.PostClaimDialogueButtonHeld = true;
                active.PostClaimDialoguePressAttempts++;
                return;
            }
        }

        if (active.PromptOpened)
        {
            if (Game1.activeClickableMenu is not DialogueBox ||
                !string.Equals(
                    active.MineBefore.lastQuestionKey,
                    "ExitMine",
                    StringComparison.Ordinal))
            {
                CompleteExitMineBlocked(active, "exit_mine_prompt_drift");
                return;
            }

            active.MineBefore.answerDialogueAction("ExitMine_Leave", new[] { "ExitMine", "Leave" });
            Game1.activeClickableMenu = null;
            Game1.dialogueUp = false;
            active.DialogueConfirmed = true;
            return;
        }
        if (Game1.activeClickableMenu is not null)
        {
            CompleteExitMineBlocked(active, "exit_mine_unexpected_menu_before_move");
            return;
        }

        if (Game1.player.UsingTool ||
            !Game1.player.CanMove ||
            Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            StopAllMovement();
            active.PreMoveSettleTicks++;
            if (active.PreMoveSettleTicks > 180)
            {
                CompleteExitMineBlocked(
                    active,
                    "exit_mine_pre_move_animation_timeout");
            }
            return;
        }
        active.PreMoveSettleTicks = 0;

        if (ImmediateMiningThreat(active.MineBefore))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;

        if (ManhattanDistance(Game1.player.TilePoint, active.Target) is < 1 or > 2)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                if (!TryReplanExitMine(active, out var exhaustedReason))
                {
                    CompleteExitMineBlocked(active, "exit_mine_path_exhausted:" + exhaustedReason);
                }
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (!IsTileWalkable(active.MineBefore, next) || IsTileOccupiedByCharacter(active.MineBefore, next))
            {
                var repaired = BuildCompilerMineExitPath(
                    active.MineBefore,
                    active.Target,
                    active.RequestedStand,
                    active.MaxMovementTiles,
                    out var repairReason);
                if (repaired is null)
                {
                    CompleteExitMineBlocked(active, "exit_mine_replan_failed:" + repairReason);
                    return;
                }
                active.Path = repaired;
                active.PathIndex = 0;
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick)
            {
                active.StuckTicks++;
                if (active.StuckTicks > 45)
                {
                    StopAllMovement();
                    if (!TryReplanExitMine(active, out var stuckReason))
                    {
                        CompleteExitMineBlocked(active, "exit_mine_stuck_replan_failed:" + stuckReason);
                    }
                }
            }
            else
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.MineBefore.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 115)
        {
            CompleteExitMineBlocked(active, "exit_mine_tile_drift");
            return;
        }
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
        var handled = active.MineBefore.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        if (!handled || Game1.activeClickableMenu is not DialogueBox || !string.Equals(active.MineBefore.lastQuestionKey, "ExitMine", StringComparison.Ordinal))
        {
            CompleteExitMineBlocked(active, "exit_mine_native_prompt_not_opened");
            return;
        }
        active.PromptOpened = true;
    }

    private static bool IsNativeExitMinePrompt(
        ActiveExitMine active,
        DialogueBox dialogue)
    {
        return ReferenceEquals(Game1.currentLocation, active.MineBefore) &&
            string.Equals(
                active.MineBefore.lastQuestionKey,
                "ExitMine",
                StringComparison.Ordinal) &&
            dialogue.isQuestion &&
            dialogue.responses is { Length: 2 };
    }

    private static bool IsGoldenScytheClaimDialogue(ActiveExitMine active, DialogueBox dialogue)
    {
        return active.MineLevelBefore == 77377 &&
            Game1.player.mailReceived.Contains("gotGoldenScythe") &&
            CountInventoryItems("(W)53") > 0 &&
            !dialogue.isQuestion &&
            (dialogue.responses is null || dialogue.responses.Length == 0) &&
            dialogue.characterDialogue is null &&
            !Game1.eventUp;
    }

    private static bool TryReplanExitMine(ActiveExitMine active, out string blockReason)
    {
        var repaired = BuildCompilerMineExitPath(
            active.MineBefore,
            active.Target,
            active.RequestedStand,
            active.MaxMovementTiles,
            out blockReason);
        if (repaired is null)
        {
            return false;
        }
        active.Path = repaired;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private void CompleteExitMine(ActiveExitMine active)
    {
        TryApplySmapiLeftButtonOverride(pressed: false, out _);
        StopAllMovement();
        activeExitMine = null;
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
            TargetLocation = active.ExpectedLocationId,
            TargetTileX = active.ExpectedTileX,
            TargetTileY = active.ExpectedTileY,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "exit_mine",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_exit", "native_exit_prompt_observed", "native_exit_leave_answer_handled", "exact_decompiled_destination_observed" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = ExitMineObservedEffect(),
            RetreatReason = active.RetreatReason,
            RetreatMineLevelBefore = active.MineLevelBefore,
            RetreatTimeBefore = active.TimeBefore,
            RetreatHealthBefore = active.HealthBefore,
            RetreatEnergyBefore = active.EnergyBefore,
            RetreatDestination = active.ExpectedLocationId + ":" + active.ExpectedTileX + "," + active.ExpectedTileY,
            RetreatNativeDialogueHandled = true,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.location_id", Before = active.MineBefore.NameOrUniqueName, After = active.ExpectedLocationId },
                new SimulatedFactChange { Path = "player.tile", Before = active.PlayerTileBefore.X + "," + active.PlayerTileBefore.Y, After = active.ExpectedTileX + "," + active.ExpectedTileY }
            }
        });
    }

    private void CompleteExitMineBlocked(ActiveExitMine active, string reason)
    {
        TryApplySmapiLeftButtonOverride(pressed: false, out _);
        StopAllMovement();
        activeExitMine = null;
        var result = BlockedWithPrimitive(active.Pending.Request, "exit_mine", active.RequestedEffect, ExitMineObservedEffect(), reason);
        result.RetreatReason = active.RetreatReason;
        result.RetreatMineLevelBefore = active.MineLevelBefore;
        result.RetreatTimeBefore = active.TimeBefore;
        result.RetreatHealthBefore = active.HealthBefore;
        result.RetreatEnergyBefore = active.EnergyBefore;
        result.RetreatDestination = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        result.RetreatNativeDialogueHandled = active.DialogueConfirmed;
        active.Pending.Completion.SetResult(result);
    }

    private static (string LocationId, int TileX, int TileY) ExpectedMineExitDestination(int mineLevel)
    {
        return mineLevel == 77377
            ? ("Mine", 67, 10)
            : mineLevel > 120 ? ("SkullCave", 3, 4) : ("Mine", 23, 8);
    }

    private static string ExitMineObservedEffect()
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";time=" + Game1.timeOfDay +
            ";health=" + Game1.player.health +
            ";energy=" + Game1.player.Stamina.ToString("0.###");
    }
}
