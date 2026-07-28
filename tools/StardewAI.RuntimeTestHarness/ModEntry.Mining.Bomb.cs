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
    private void StartPlaceBomb(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        const string requested = "bomb.placed=true;escape_damage_square=true;native_input=MouseRight+WASD";
        if (Game1.currentLocation is not MineShaft mine ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.EscapeTileX.HasValue || !request.EscapeTileY.HasValue ||
            !request.BombSlotIndex.HasValue || !request.BombRadiusTiles.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "bomb_contract=missing", "bomb_target_escape_and_slot_required"));
            return;
        }
        var slot = request.BombSlotIndex.Value;
        if (slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object bomb ||
            !string.Equals(bomb.QualifiedItemId, request.BombQualifiedItemId, StringComparison.Ordinal) ||
            bomb.Stack <= 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "bomb=missing_or_drifted", "bomb_inventory_contract_not_met"));
            return;
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var escape = new Point(request.EscapeTileX.Value, request.EscapeTileY.Value);
        if (!AreAdjacent(stand, target))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "stand_target=not_adjacent", "bomb_placement_stand_invalid"));
            return;
        }
        if (mine.objects.ContainsKey(new Vector2(target.X, target.Y)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "target=occupied", "bomb_placement_tile_not_empty"));
            return;
        }
        Monster? targetMonster = null;
        if (!string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity))
        {
            var targetMonsters = mine.characters.OfType<Monster>()
                .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
                .Where(monster => string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
                    string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
                .ToArray();
            if (targetMonsters.Length != 1 ||
                request.CombatTerminalState == "mummy_finalized" &&
                (targetMonsters[0] is not Mummy mummy || mummy.reviveTimer.Value <= 0))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested,
                    "matching_target_count=" + targetMonsters.Length, "bomb_target_terminal_state_not_ready"));
                return;
            }
            targetMonster = targetMonsters[0];
            if (!ValidateQuestSlayTarget(request, targetMonster, out var questSlayReason))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(
                    request,
                    "place_bomb",
                    requested,
                    "quest_slay_target=drifted",
                    questSlayReason));
                return;
            }
            var damageRectangle = new Rectangle(
                (target.X - request.BombRadiusTiles.Value) * Game1.tileSize,
                (target.Y - request.BombRadiusTiles.Value) * Game1.tileSize,
                (request.BombRadiusTiles.Value * 2 + 1) * Game1.tileSize,
                (request.BombRadiusTiles.Value * 2 + 1) * Game1.tileSize);
            if (!damageRectangle.Intersects(targetMonster.GetBoundingBox()))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested,
                    "target_monster=outside_damage_square", "bomb_target_outside_damage_square"));
                return;
            }
        }
        var path = TryBuildTilePath(mine, Game1.player.TilePoint, stand, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason, avoidSoftObstacles: true);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "place_bomb", requested, "placement_path=blocked", pathReason));
            return;
        }
        activePlaceBomb = new ActivePlaceBomb(
            pending,
            mine,
            target,
            stand,
            escape,
            path,
            slot,
            bomb,
            request.BombRadiusTiles.Value,
            Game1.player.CurrentToolIndex,
            BombAffectedObjectCount(mine, target, request.BombRadiusTiles.Value),
            targetMonster,
            request.CombatTerminalState,
            requested);
    }

    private void TickPlaceBomb()
    {
        if (activePlaceBomb is null)
        {
            return;
        }
        var active = activePlaceBomb;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompletePlaceBombBlocked(active, "bomb_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompletePlaceBombBlocked(active, "bomb_timeout");
            return;
        }

        if (active.Stage == PlaceBombStage.MoveToPlacement)
        {
            if (!TickBombPathMovement(active, active.Path, out var movementReason))
            {
                if (!string.IsNullOrEmpty(movementReason))
                {
                    CompletePlaceBombBlocked(active, movementReason);
                }
                return;
            }
            active.Stage = PlaceBombStage.AimPlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.AimPlacement)
        {
            StopAllMovement();
            if (!PrepareNativeBombPlacement(active, out var reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            Game1.player.CurrentToolIndex = active.BombSlotIndex;
            active.Stage = PlaceBombStage.PressPlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.PressPlacement)
        {
            StopAllMovement();
            if (!PrepareNativeBombPlacement(active, out var reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            Game1.player.CurrentToolIndex = active.BombSlotIndex;
            if (!TryApplySmapiRightButtonOverride(pressed: true, out reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            active.Stage = PlaceBombStage.ReleasePlacement;
            return;
        }

        if (active.Stage == PlaceBombStage.ReleasePlacement)
        {
            if (!TryApplySmapiRightButtonOverride(pressed: false, out var reason))
            {
                CompletePlaceBombBlocked(active, reason);
                return;
            }
            BombPlacementCursorPatch.Clear();
            var stackAfter = BombStackAt(active.BombSlotIndex, active.BombQualifiedItemId);
            if (stackAfter >= active.BombStackBefore)
            {
                CompletePlaceBombBlocked(active, "bomb_native_placement_not_observed");
                return;
            }
            active.PlacedAtTick = active.ElapsedTicks;
            active.EscapePath = TryBuildTilePath(active.Mine, Game1.player.TilePoint, active.Escape, 64, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false) ?? new List<Point>();
            if (active.EscapePath.Count == 0)
            {
                if (!TryRebuildBombEscape(active))
                {
                    CompletePlaceBombBlocked(active, "bomb_escape_path_drifted:" + pathReason);
                }
                return;
            }
            active.PathIndex = 0;
            active.Stage = PlaceBombStage.Escape;
        }

        if (active.Stage == PlaceBombStage.Escape)
        {
            if (!TickBombPathMovement(active, active.EscapePath, out var movementReason))
            {
                if (!string.IsNullOrEmpty(movementReason))
                {
                    if (!TryRebuildBombEscape(active))
                    {
                        CompletePlaceBombBlocked(active, movementReason);
                    }
                }
                return;
            }
            if (Math.Abs(Game1.player.TilePoint.X - active.Target.X) <= active.Radius &&
                Math.Abs(Game1.player.TilePoint.Y - active.Target.Y) <= active.Radius)
            {
                CompletePlaceBombBlocked(active, "bomb_escape_finished_inside_damage_square");
                return;
            }
            active.Stage = PlaceBombStage.WaitForExplosion;
        }

        if (active.Stage == PlaceBombStage.WaitForExplosion &&
            active.ElapsedTicks - active.PlacedAtTick >= 180 &&
            !active.Mine.temporarySprites.Any(sprite => sprite.bombRadius == active.Radius &&
                sprite.position.Equals(new Vector2(active.Target.X * Game1.tileSize, active.Target.Y * Game1.tileSize))))
        {
            CompletePlaceBomb(active);
        }
    }

    private static bool PrepareNativeBombPlacement(ActivePlaceBomb active, out string reason)
    {
        if (Game1.player.TilePoint != active.Stand)
        {
            reason = "bomb_player_not_on_planned_stand_tile";
            return false;
        }
        if (!AreAdjacent(active.Stand, active.Target))
        {
            reason = "bomb_target_no_longer_adjacent";
            return false;
        }
        if (active.Mine.objects.ContainsKey(active.Target.ToVector2()) ||
            active.Mine.terrainFeatures.ContainsKey(active.Target.ToVector2()))
        {
            reason = "bomb_placement_tile_became_occupied";
            return false;
        }

        Game1.player.faceDirection(DirectionTo(active.Stand, active.Target));
        var grabTile = Game1.player.GetGrabTile();
        if ((int)grabTile.X != active.Target.X || (int)grabTile.Y != active.Target.Y)
        {
            reason = "bomb_facing_grab_tile_mismatch";
            return false;
        }

        BombPlacementCursorPatch.ScreenPixel = new Point(
            active.Target.X * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.X,
            active.Target.Y * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.Y);
        BombPlacementCursorPatch.Active = true;
        reason = string.Empty;
        return true;
    }

    private bool TryRebuildBombEscape(ActivePlaceBomb active)
    {
        if (active.Mine.map?.Layers.Count is not > 0)
        {
            return false;
        }
        var layer = active.Mine.map.Layers[0];
        List<Point>? best = null;
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                if (Math.Abs(x - active.Target.X) <= active.Radius && Math.Abs(y - active.Target.Y) <= active.Radius)
                {
                    continue;
                }
                var path = TryBuildTilePath(active.Mine, Game1.player.TilePoint, new Point(x, y), 64, out _,
                    avoidSoftObstacles: true, allowRemovableObstacles: false);
                if (path is not null && (best is null || path.Count < best.Count))
                {
                    best = path;
                }
            }
        }
        if (best is null)
        {
            return false;
        }
        active.EscapePath = best;
        active.PathIndex = 0;
        active.StuckTicks = 0;
        active.LastPosition = Game1.player.Position;
        return true;
    }

    private bool TickBombPathMovement(ActivePlaceBomb active, List<Point> path, out string reason)
    {
        reason = string.Empty;
        while (active.PathIndex < path.Count && Game1.player.TilePoint == path[active.PathIndex])
        {
            active.PathIndex++;
        }
        if (active.PathIndex >= path.Count)
        {
            StopAllMovement();
            return true;
        }
        var next = path[active.PathIndex];
        if (!IsTileWalkable(active.Mine, next) || IsTileOccupiedByCharacter(active.Mine, next))
        {
            reason = "bomb_path_drifted";
            StopAllMovement();
            return false;
        }
        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
        if (Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) < 0.01f)
        {
            active.StuckTicks++;
        }
        else
        {
            active.StuckTicks = 0;
        }
        active.LastPosition = Game1.player.Position;
        if (active.StuckTicks > 60)
        {
            reason = "bomb_path_stuck";
        }
        return false;
    }

    private static int BombAffectedObjectCount(MineShaft mine, Point center, int radius)
    {
        return BombAffectedTiles(center, radius).Count(tile =>
            mine.objects.TryGetValue(new Vector2(tile.X, tile.Y), out var obj) &&
            (obj.IsBreakableStone() || obj is BreakableContainer));
    }

    private static IEnumerable<Point> BombAffectedTiles(Point center, int radius)
    {
        var outline = Game1.getCircleOutlineGrid(radius);
        var fill = 0;
        for (var x = 0; x < radius * 2 + 1; x++)
        {
            for (var y = 0; y < radius * 2 + 1; y++)
            {
                var include = false;
                if (x == 0 || y == 0 || x == radius * 2 || y == radius * 2)
                {
                    fill = outline[x, y] ? 1 : 0;
                }
                else if (outline[x, y])
                {
                    fill += y <= radius ? 1 : -1;
                    include = fill <= 0;
                }
                if (fill >= 1)
                {
                    include = true;
                }
                if (include)
                {
                    yield return new Point(center.X + x - radius, center.Y + y - radius);
                }
            }
        }
    }

    private static int BombStackAt(int slotIndex, string qualifiedItemId)
    {
        return slotIndex >= 0 && slotIndex < Game1.player.Items.Count &&
            Game1.player.Items[slotIndex] is StardewValley.Object obj &&
            string.Equals(obj.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)
                ? obj.Stack
                : 0;
    }

    private void CompletePlaceBomb(ActivePlaceBomb active)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        BombPlacementCursorPatch.Clear();
        StopAllMovement();
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activePlaceBomb = null;
        var objectCountAfter = BombAffectedObjectCount(active.Mine, active.Target, active.Radius);
        var stackAfter = BombStackAt(active.BombSlotIndex, active.BombQualifiedItemId);
        var targetFinalized = active.TargetMonster is not null &&
            (active.TargetMonster.Health <= 0 || !active.Mine.characters.Contains(active.TargetMonster));
        var requiresTargetFinalization = active.TerminalState == "mummy_finalized";
        var verified = requiresTargetFinalization
            ? targetFinalized
            : objectCountAfter < active.ObjectCountBefore;
        var result = new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = active.Mine.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "place_bomb",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? targetFinalized
                    ? new[] { "native_bomb_consumption_observed", "escape_tile_outside_damage_square", "natural_explosion_finalized_target_monster" }
                    : new[] { "native_bomb_consumption_observed", "escape_tile_outside_damage_square", "natural_explosion_removed_breakable_objects" }
                : new[] { requiresTargetFinalization ? "bomb_target_mummy_not_finalized" : "bomb_explosion_did_not_reduce_predicted_breakable_cluster" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "bomb_stack=" + stackAfter + ";breakable_objects=" + objectCountAfter +
                ";target_finalized=" + targetFinalized.ToString().ToLowerInvariant() +
                ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
            CombatMethod = "bomb",
            CombatTerminalState = active.TerminalState,
            CombatConsumableQualifiedItemId = active.BombQualifiedItemId,
            CombatConsumableCountBefore = active.BombStackBefore,
            CombatConsumableCountAfter = stackAfter,
            BombRadiusTiles = active.Radius,
            BombEscapeTileX = active.Escape.X,
            BombEscapeTileY = active.Escape.Y,
            BombObjectCountBefore = active.ObjectCountBefore,
            BombObjectCountAfter = objectCountAfter,
            CombatTargetRuntimeType = active.TargetMonster?.GetType().FullName ?? string.Empty,
            CombatTargetRuntimeIdentity = active.TargetMonster is null ? string.Empty : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.TargetMonster).ToString("X8"),
            CombatTargetName = active.TargetMonster?.Name ?? string.Empty,
            CombatTargetDefeated = active.TargetMonster is null ? null : targetFinalized,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { requiresTargetFinalization ? "bomb_target_mummy_not_finalized" : "bomb_effect_verification_failed" },
            ChangedFacts = new[]
                {
                    new SimulatedFactChange { Path = "player.inventory.bomb.stack", Before = active.BombStackBefore.ToString(), After = stackAfter.ToString() },
                    new SimulatedFactChange { Path = "mining.blast.breakable_object_count", Before = active.ObjectCountBefore.ToString(), After = objectCountAfter.ToString() }
                }
                .Concat(active.TargetMonster is null
                    ? Array.Empty<SimulatedFactChange>()
                    : new[]
                    {
                        new SimulatedFactChange
                        {
                            Path = "mining.monsters[" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(active.TargetMonster).ToString("X8") + "].present",
                            Before = "true",
                            After = (!targetFinalized).ToString().ToLowerInvariant()
                        }
                    })
                .ToArray()
        };
        ApplyQuestSlayFeedback(
            result,
            active.Pending.Request,
            requireProgress: verified &&
                targetFinalized &&
                active.Pending.Request.QuestSlayTargetStep);
        active.Pending.Completion.SetResult(result);
    }

    private void CompletePlaceBombBlocked(ActivePlaceBomb active, string reason)
    {
        TryApplySmapiRightButtonOverride(pressed: false, out _);
        BombPlacementCursorPatch.Clear();
        StopAllMovement();
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        activePlaceBomb = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "place_bomb", active.RequestedEffect,
            "player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y, reason));
    }
}
