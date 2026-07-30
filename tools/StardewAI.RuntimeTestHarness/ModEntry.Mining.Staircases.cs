using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartPlaceStaircase(PendingExecution pending)
    {
        var request = pending.Request;
        const string requested =
            "player.inventory.(BC)71=before-1;" +
            "mining.tiles.ladders[target]=present;" +
            "native_input=MouseRight";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                reasons.ToArray()));
            return;
        }

        if (Game1.currentLocation is not MineShaft mine ||
            !request.TargetTileX.HasValue ||
            !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue ||
            !request.StandTileY.HasValue ||
            !request.SlotIndex.HasValue ||
            !request.InventoryItemTotalBefore.HasValue ||
            !request.InventoryItemTotalAfter.HasValue ||
            !string.Equals(
                request.QualifiedItemId,
                "(BC)71",
                StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                "staircase_exact_contract_required"));
            return;
        }

        if (!mine.shouldCreateLadderOnThisLevel() ||
            Game1.activeClickableMenu is not null ||
            Game1.dialogueUp ||
            Game1.player.UsingTool ||
            !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                "staircase_native_floor_or_input_gate_blocked"));
            return;
        }

        var slotIndex = request.SlotIndex.Value;
        if (slotIndex < 0 ||
            slotIndex >= Game1.player.Items.Count ||
            Game1.player.Items[slotIndex] is not StardewValley.Object
            {
                QualifiedItemId: "(BC)71",
                Stack: > 0
            } ||
            CountInventoryItems("(BC)71") !=
            request.InventoryItemTotalBefore.Value ||
            request.InventoryItemTotalAfter.Value !=
            request.InventoryItemTotalBefore.Value - 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                "staircase_inventory_contract_drifted"));
            return;
        }

        var target = new Point(
            request.TargetTileX.Value,
            request.TargetTileY.Value);
        var stand = new Point(
            request.StandTileX.Value,
            request.StandTileY.Value);
        if (!AreAdjacent(stand, target) ||
            !IsDirectNativeStaircaseTile(mine, target))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                "staircase_direct_native_target_invalid"));
            return;
        }

        var maxMovement = Math.Clamp(
            request.MaxMovementTiles ?? 512,
            1,
            512);
        var path = BuildCompilerAdjacentPath(
            mine,
            target,
            stand,
            maxMovement,
            out var pathReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "place_staircase",
                requested,
                StaircaseObservedEffect(),
                "staircase_path_unavailable:" + pathReason));
            return;
        }

        activePlaceStaircase = new ActivePlaceStaircase(
            pending,
            mine,
            target,
            stand,
            path,
            slotIndex,
            request.InventoryItemTotalBefore.Value,
            maxMovement,
            request.RestoreSlotIndex ?? Game1.player.CurrentToolIndex,
            requested);
    }

    private void TickPlaceStaircase()
    {
        if (activePlaceStaircase is null)
        {
            return;
        }

        var active = activePlaceStaircase;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompletePlaceStaircaseBlocked(
                active,
                "staircase_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks >
            active.MaxTicks)
        {
            CompletePlaceStaircaseBlocked(active, "staircase_timeout");
            return;
        }

        if (active.Stage == StaircasePlacementStage.MoveToPlacement)
        {
            if (ImmediateMiningThreat(active.Mine))
            {
                StopAllMovement();
                active.CombatInterrupted = true;
                active.CombatInterruptedTicks++;
                return;
            }
            active.CombatInterrupted = false;

            while (active.PathIndex < active.Path.Count &&
                Game1.player.TilePoint == active.Path[active.PathIndex])
            {
                active.PathIndex++;
            }
            if (Game1.player.TilePoint == active.Stand)
            {
                StopAllMovement();
                active.Stage = StaircasePlacementStage.AimPlacement;
                active.StageEnteredTick = active.ElapsedTicks;
                return;
            }
            if (active.PathIndex >= active.Path.Count)
            {
                if (!TryReplanStaircase(active, out var exhaustedReason))
                {
                    CompletePlaceStaircaseBlocked(
                        active,
                        "staircase_path_exhausted:" + exhaustedReason);
                }
                return;
            }

            var next = active.Path[active.PathIndex];
            if (!IsTileWalkable(active.Mine, next) ||
                IsTileOccupiedByCharacter(active.Mine, next))
            {
                if (!TryReplanStaircase(active, out var driftReason))
                {
                    CompletePlaceStaircaseBlocked(
                        active,
                        "staircase_path_drifted:" + driftReason);
                }
                return;
            }

            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            if (Vector2.DistanceSquared(
                    active.LastPosition,
                    Game1.player.Position) < 0.01f)
            {
                active.StuckTicks++;
            }
            else
            {
                active.StuckTicks = 0;
            }
            active.LastPosition = Game1.player.Position;
            if (active.StuckTicks > 45 &&
                !TryReplanStaircase(active, out var stuckReason))
            {
                CompletePlaceStaircaseBlocked(
                    active,
                    "staircase_path_stuck:" + stuckReason);
            }
            return;
        }

        StopAllMovement();
        if (active.Stage == StaircasePlacementStage.AimPlacement)
        {
            if (!PrepareNativeStaircasePlacement(active, out var reason))
            {
                CompletePlaceStaircaseBlocked(active, reason);
                return;
            }
            Game1.player.CurrentToolIndex = active.SlotIndex;
            active.Stage = StaircasePlacementStage.PressPlacement;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == StaircasePlacementStage.PressPlacement)
        {
            if (!PrepareNativeStaircasePlacement(active, out var reason))
            {
                CompletePlaceStaircaseBlocked(active, reason);
                return;
            }
            Game1.player.CurrentToolIndex = active.SlotIndex;
            if (!TryApplySmapiRightButtonOverride(
                    pressed: true,
                    out reason))
            {
                CompletePlaceStaircaseBlocked(active, reason);
                return;
            }
            active.Stage = StaircasePlacementStage.ReleasePlacement;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == StaircasePlacementStage.ReleasePlacement)
        {
            if (!TryApplySmapiRightButtonOverride(
                    pressed: false,
                    out var reason))
            {
                CompletePlaceStaircaseBlocked(active, reason);
                return;
            }
            PlacementCursorPatch.Clear();
            active.Stage = StaircasePlacementStage.WaitForLadder;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == StaircasePlacementStage.WaitForLadder)
        {
            if (active.Mine.getTileIndexAt(
                active.Target.X,
                active.Target.Y,
                "Buildings",
                "mine") == 173)
            {
                CompletePlaceStaircase(active);
                return;
            }
            if (active.ElapsedTicks - active.StageEnteredTick > 120)
            {
                CompletePlaceStaircaseBlocked(
                    active,
                    "staircase_native_placement_not_observed");
            }
        }
    }

    private static bool IsDirectNativeStaircaseTile(
        MineShaft mine,
        Point target)
    {
        var tile = target.ToVector2();
        return !mine.IsTileOccupiedBy(tile) &&
            mine.isTileOnClearAndSolidGround(tile) &&
            string.Equals(
                mine.doesTileHaveProperty(
                    target.X,
                    target.Y,
                    "Type",
                    "Back"),
                "Stone",
                StringComparison.Ordinal);
    }

    private static bool PrepareNativeStaircasePlacement(
        ActivePlaceStaircase active,
        out string reason)
    {
        if (Game1.player.TilePoint != active.Stand ||
            !AreAdjacent(active.Stand, active.Target) ||
            !active.Mine.shouldCreateLadderOnThisLevel() ||
            !IsDirectNativeStaircaseTile(active.Mine, active.Target) ||
            active.SlotIndex < 0 ||
            active.SlotIndex >= Game1.player.Items.Count ||
            Game1.player.Items[active.SlotIndex] is not StardewValley.Object
            {
                QualifiedItemId: "(BC)71",
                Stack: > 0
            })
        {
            reason = "staircase_native_precondition_drifted";
            return false;
        }

        var direction = DirectionTo(active.Stand, active.Target);
        Game1.player.faceDirection(direction);
        PlacementCursorPatch.ScreenPixel = new Point(
            active.Target.X * Game1.tileSize +
                Game1.tileSize / 2 -
                Game1.viewport.X,
            active.Target.Y * Game1.tileSize +
                Game1.tileSize / 2 -
                Game1.viewport.Y);
        PlacementCursorPatch.Active = true;
        reason = string.Empty;
        return true;
    }

    private static bool TryReplanStaircase(
        ActivePlaceStaircase active,
        out string reason)
    {
        var path = BuildCompilerAdjacentPath(
            active.Mine,
            active.Target,
            active.Stand,
            active.MaxMovementTiles,
            out reason);
        if (path is null)
        {
            return false;
        }
        active.Path = path;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private void CompletePlaceStaircase(ActivePlaceStaircase active)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        PlacementCursorPatch.Clear();
        StopAllMovement();
        var totalAfter = CountInventoryItems("(BC)71");
        var verified = totalAfter == active.TotalBefore - 1 &&
            active.Mine.getTileIndexAt(
                active.Target.X,
                active.Target.Y,
                "Buildings",
                "mine") == 173;
        RestoreSlot(active.RestoreSlotIndex);
        activePlaceStaircase = null;

        var request = active.Pending.Request;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "place_staircase",
            PrimitiveVerificationStatus =
                verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_right_click_consumed_exact_(BC)71",
                    "projected_direct_tile_became_live_ladder",
                    "next_snapshot_reuses_descend_ladder"
                }
                : new[]
                {
                    "staircase_consumption_or_ladder_verification_failed"
                },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = StaircaseObservedEffect(),
            CombatConsumableQualifiedItemId = "(BC)71",
            CombatConsumableCountBefore = active.TotalBefore,
            CombatConsumableCountAfter = totalAfter,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "staircase_postcondition_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "player.inventory.(BC)71.total",
                    Before = active.TotalBefore.ToString(),
                    After = totalAfter.ToString()
                },
                new SimulatedFactChange
                {
                    Path =
                        "mining.tiles.ladders[" +
                        active.Target.X + "," +
                        active.Target.Y + "].present",
                    Before = "false",
                    After = verified.ToString().ToLowerInvariant()
                }
            }
        });
    }

    private void CompletePlaceStaircaseBlocked(
        ActivePlaceStaircase active,
        string reason)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        PlacementCursorPatch.Clear();
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activePlaceStaircase = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "place_staircase",
            active.RequestedEffect,
            StaircaseObservedEffect(),
            reason));
    }

    private static string StaircaseObservedEffect()
    {
        return Game1.currentLocation is MineShaft mine
            ? "location=" + mine.NameOrUniqueName +
                ";mine_level=" + mine.mineLevel +
                ";staircase_count=" +
                CountInventoryItems("(BC)71")
            : "location=" +
                (Game1.currentLocation?.NameOrUniqueName ?? "none") +
                ";mine_level=none";
    }
}
