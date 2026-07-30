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
    private void StartVolcanoCombat(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var requested = "volcano.monsters[target].present=false;native_input=melee";
        if (string.IsNullOrWhiteSpace(request.TargetRuntimeIdentity) ||
            string.IsNullOrWhiteSpace(request.TargetRuntimeType) ||
            string.IsNullOrWhiteSpace(request.TargetName))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "target=missing_or_incomplete", "volcano_combat_target_identity_required"));
            return;
        }
        if (Game1.currentLocation is not VolcanoDungeon volcano)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "location=not_loaded_volcano", "volcano_combat_requires_loaded_volcano_dungeon"));
            return;
        }

        var targets = volcano.characters.OfType<Monster>()
            .Where(monster => monster.Health > 0)
            .Where(monster => string.Equals(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(monster).ToString("X8"), request.TargetRuntimeIdentity, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.GetType().FullName, request.TargetRuntimeType, StringComparison.Ordinal))
            .Where(monster => string.Equals(monster.Name, request.TargetName, StringComparison.Ordinal))
            .ToArray();
        if (targets.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "matching_target_count=" + targets.Length, targets.Length == 0 ? "volcano_combat_target_not_found_or_moved" : "volcano_combat_target_ambiguous"));
            return;
        }

        var target = targets[0];
        if (target is Spiker || target.GetType().Assembly != typeof(Monster).Assembly)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "target_type=" + target.GetType().FullName, "volcano_combat_target_melee_semantics_unsupported"));
            return;
        }
        if (!request.CombatWeaponSlotIndex.HasValue ||
            request.CombatWeaponSlotIndex.Value < 0 ||
            request.CombatWeaponSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.CombatWeaponSlotIndex.Value] is not MeleeWeapon weapon ||
            weapon.isScythe())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "combat_volcano_monster", requested, "weapon=missing", "volcano_combat_melee_weapon_slot_invalid"));
            return;
        }

        activeVolcanoCombat = new ActiveVolcanoCombat(
            pending,
            volcano,
            target,
            weapon,
            request.CombatWeaponSlotIndex.Value,
            Game1.player.CurrentToolIndex,
            Math.Clamp(request.MaxAttacks, 1, 256),
            Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512),
            requested);
    }

    private void TickVolcanoCombat()
    {
        if (activeVolcanoCombat is null)
        {
            return;
        }

        if (activeEmergencyCombatFood is not null)
        {
            return;
        }

        var active = activeVolcanoCombat;
        try
        {
            if (activeEmergencyCombatFood is not null)
            {
                active.LastNoProgressReason =
                    "emergency_food_in_progress";
                return;
            }
            if (Context.IsWorldReady &&
                Game1.currentLocation is VolcanoDungeon volcano &&
                EmergencyCombatFoodNeeded(volcano))
            {
                TryApplySmapiButtonOverride(
                    HeavyHitterInputButton(active.Weapon),
                    pressed: false,
                    out _);
                active.AttackButtonHeld = false;
                if (active.ClearanceTool is not null)
                {
                    TryApplySmapiButtonOverride(
                        HeavyHitterInputButton(active.ClearanceTool),
                        pressed: false,
                        out _);
                }
                active.ClearanceButtonHeld = false;
                StopAllMovement();
                TryStartEmergencyCombatFood(volcano);
                return;
            }

            TickVolcanoCombatCore(active);
        }
        catch (Exception ex)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickVolcanoCombatCore(ActiveVolcanoCombat active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            Game1.currentLocation is not VolcanoDungeon volcano ||
            !ReferenceEquals(volcano, active.Volcano))
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_location_changed_or_world_unavailable");
            return;
        }

        RecordVolcanoCombatHealth(active);
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_timeout");
            return;
        }
        if (Game1.player.health <= 0)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_player_defeated");
            return;
        }

        var targetPresent = volcano.characters.Contains(active.Target);
        if (active.Target.Health <= 0 || !targetPresent)
        {
            if (active.Target.Health > 0)
            {
                CompleteVolcanoCombatBlocked(active, "volcano_combat_target_disappeared_without_defeat");
                return;
            }

            StopAllMovement();
            if (active.AttackButtonHeld)
            {
                if (!TryApplySmapiButtonOverride(
                        HeavyHitterInputButton(active.Weapon),
                        pressed: false,
                        out var releaseReason))
                {
                    CompleteVolcanoCombatBlocked(active, releaseReason);
                    return;
                }
                active.AttackButtonHeld = false;
                return;
            }
            if (TickVolcanoCombatDefeatDialogue(active))
            {
                return;
            }
            if (Game1.player.UsingTool ||
                !Game1.player.CanMove ||
                Game1.player.FarmerSprite.PauseForSingleAnimation)
            {
                active.DefeatSettleTicks++;
                if (active.DefeatSettleTicks > 180)
                {
                    CompleteVolcanoCombatBlocked(active, "volcano_combat_defeat_animation_settle_timeout");
                }
                return;
            }

            CompleteVolcanoCombat(active);
            return;
        }

        if (TrackVolcanoCombatProgress(active) > 600)
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_no_movement_or_damage_progress:" +
                    active.LastNoProgressReason);
            return;
        }

        var releasedAttackThisTick = false;
        if (active.AttackButtonHeld)
        {
            if (!TryApplySmapiButtonOverride(
                    HeavyHitterInputButton(active.Weapon),
                    pressed: false,
                    out var releaseReason))
            {
                CompleteVolcanoCombatBlocked(active, releaseReason);
                return;
            }
            active.AttackButtonHeld = false;
            releasedAttackThisTick = true;
            active.LastNoProgressReason =
                "released_attack_input";
        }

        if (TickVolcanoCombatClearance(active, volcano))
        {
            active.LastNoProgressReason =
                "route_clearance";
            return;
        }

        if (!IsMonsterWithinCombatReach(active.Target, active.Weapon))
        {
            var targetTile = active.Target.TilePoint;
            if (active.PathIndex >= active.Path.Count || ManhattanDistance(active.PathTarget, targetTile) > 2)
            {
                var path = BuildAdjacentToolPath(
                    volcano,
                    targetTile,
                    Math.Max(1, active.MaxMovementTiles - active.MovementTiles),
                    out var pathReason,
                    avoidSoftObstacles: true,
                    allowRemovableObstacles: true);
                if (path is null)
                {
                    if (active.Target.isGlider.Value)
                    {
                        StopAllMovement();
                        active.Path.Clear();
                        active.PathIndex = 0;
                        active.LastNoProgressReason =
                            "glider_waiting_for_reachable_approach";
                        return;
                    }

                    active.PathFailures++;
                    if (active.PathFailures > 120)
                    {
                        CompleteVolcanoCombatBlocked(active, "volcano_combat_dynamic_path_unavailable:" + pathReason);
                    }
                    return;
                }
                active.Path = path;
                active.PathIndex = 0;
                active.PathTarget = targetTile;
                active.PathFailures = 0;
            }

            if (active.PathIndex >= active.Path.Count)
            {
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            if (IsTileOccupiedByCharacter(volcano, next))
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.PathFailures++;
                return;
            }
            if (volcano.warps.Any(
                    warp => warp.X == next.X && warp.Y == next.Y))
            {
                StopAllMovement();
                active.Path.Clear();
                active.PathIndex = 0;
                if (!active.Target.isGlider.Value)
                {
                    active.PathFailures++;
                    if (active.PathFailures > 120)
                    {
                        CompleteVolcanoCombatBlocked(
                            active,
                            "volcano_combat_route_crosses_connector");
                    }
                }
                return;
            }
            if (!IsTileWalkable(volcano, next))
            {
                if (BeginVolcanoCombatClearance(active, volcano, next))
                {
                    return;
                }

                active.Path.Clear();
                active.PathIndex = 0;
                active.PathFailures++;
                return;
            }

            ObserveVolcanoCombatMovement(active);
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteVolcanoCombatBlocked(active, "volcano_combat_movement_budget_exceeded");
                return;
            }
            StartMoving(DirectionTo(Game1.player.TilePoint, next));
            MovePlayerForTick();
            active.LastNoProgressReason =
                "moving_to_combat_reach";
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (active.StuckTicks > 45)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        if (active.Target.isInvincible())
        {
            active.LastNoProgressReason =
                "target_invincible";
            return;
        }
        if (Game1.player.UsingTool)
        {
            active.LastNoProgressReason =
                "native_weapon_animation";
            return;
        }
        if (releasedAttackThisTick)
        {
            active.LastNoProgressReason =
                "released_attack_input";
            return;
        }
        if (Game1.activeClickableMenu is not null)
        {
            active.LastNoProgressReason =
                "active_menu:" +
                    Game1.activeClickableMenu.GetType().Name;
            return;
        }
        if (Game1.eventUp)
        {
            active.LastNoProgressReason =
                "event_up";
            return;
        }
        if (active.AttackCount >= active.MaxAttacks)
        {
            CompleteVolcanoCombatBlocked(active, "volcano_combat_attack_budget_exceeded");
            return;
        }

        var targetCenter = active.Target.GetBoundingBox().Center;
        SelectTool(active.Weapon);
        Game1.player.faceDirection(DirectionToPixel(Game1.player.GetBoundingBox().Center, targetCenter, Game1.player.FacingDirection));
        Game1.player.lastClick = new Vector2(targetCenter.X, targetCenter.Y);
        if (!TryApplySmapiButtonOverride(
                HeavyHitterInputButton(active.Weapon),
                pressed: true,
                out var inputReason))
        {
            CompleteVolcanoCombatBlocked(active, inputReason);
            return;
        }
        active.AttackButtonHeld = true;
        active.AttackCount++;
        active.LastNoProgressReason =
            "attack_input_issued";
    }

    private bool TickVolcanoCombatDefeatDialogue(ActiveVolcanoCombat active)
    {
        if (active.DefeatDialogueButtonHeld)
        {
            if (!TryApplySmapiLeftButtonOverride(
                    pressed: false,
                    out var releaseReason))
            {
                CompleteVolcanoCombatBlocked(
                    active,
                    "volcano_combat_defeat_dialogue_release_failed:" +
                        releaseReason);
                return true;
            }
            active.DefeatDialogueButtonHeld = false;
            return true;
        }

        if (Game1.activeClickableMenu is null)
        {
            return false;
        }
        if (Game1.activeClickableMenu is not DialogueBox dialogue ||
            dialogue.isQuestion ||
            dialogue.characterDialogue is not null ||
            Game1.eventUp)
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_defeat_interrupted_by_non_incidental_menu");
            return true;
        }

        StopAllMovement();
        if (dialogue.transitioning || dialogue.safetyTimer > 0)
        {
            return true;
        }
        if (active.DefeatDialoguePressAttempts >= 16)
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_defeat_dialogue_dismiss_budget_exceeded");
            return true;
        }
        if (!TryApplySmapiLeftButtonOverride(
                pressed: true,
                out var pressReason))
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_defeat_dialogue_press_failed:" +
                    pressReason);
            return true;
        }

        active.DefeatDialoguePressAttempts++;
        active.DefeatDialogueButtonHeld = true;
        return true;
    }

    private bool BeginVolcanoCombatClearance(
        ActiveVolcanoCombat active,
        VolcanoDungeon volcano,
        Point tile)
    {
        var tool = SelectClearanceTool(volcano, tile);
        if (tool is null || !AreAdjacent(Game1.player.TilePoint, tile))
        {
            return false;
        }

        StopAllMovement();
        active.ClearanceTarget = tile;
        active.ClearanceTool = tool;
        active.ClearanceBefore = ObstacleLabel(volcano, tile);
        active.ClearanceSwings = 0;
        active.Path.Clear();
        active.PathIndex = 0;
        return true;
    }

    private bool TickVolcanoCombatClearance(
        ActiveVolcanoCombat active,
        VolcanoDungeon volcano)
    {
        if (!active.ClearanceTarget.HasValue)
        {
            return false;
        }

        var target = active.ClearanceTarget.Value;
        if (active.ClearanceButtonHeld)
        {
            TryApplySmapiButtonOverride(
                HeavyHitterInputButton(active.ClearanceTool!),
                pressed: false,
                out _);
            active.ClearanceButtonHeld = false;
            return true;
        }

        if (string.Equals(
                ObstacleLabel(volcano, target),
                "clear",
                StringComparison.Ordinal))
        {
            active.Pending.MovementClearanceActions++;
            active.Pending.ChangedFacts.Add(new SimulatedFactChange
            {
                Path = "volcano.combat.route_clearance[" +
                    target.X + "," + target.Y + "]",
                Before = active.ClearanceBefore,
                After = "clear"
            });
            ResetVolcanoCombatClearance(active);
            return true;
        }

        if (IsMonsterWithinCombatReach(active.Target, active.Weapon))
        {
            ResetVolcanoCombatClearance(active);
            return false;
        }

        if (!AreAdjacent(Game1.player.TilePoint, target))
        {
            ResetVolcanoCombatClearance(active);
            active.Path.Clear();
            active.PathIndex = 0;
            return true;
        }

        var tool = SelectClearanceTool(volcano, target);
        if (tool is null)
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_route_obstacle_not_clearable");
            return true;
        }
        if (Game1.player.UsingTool)
        {
            return true;
        }
        if (active.ClearanceSwings >= 64)
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_clearance_swing_budget_exceeded");
            return true;
        }

        active.ClearanceTool = tool;
        SelectTool(tool);
        Game1.player.faceDirection(
            DirectionTo(Game1.player.TilePoint, target));
        Game1.player.lastClick = new Vector2(
            target.X * Game1.tileSize,
            target.Y * Game1.tileSize);
        if (!TryApplySmapiButtonOverride(
                HeavyHitterInputButton(tool),
                pressed: true,
                out var inputReason))
        {
            CompleteVolcanoCombatBlocked(
                active,
                "volcano_combat_clearance_" + inputReason);
            return true;
        }

        active.ClearanceButtonHeld = true;
        active.ClearanceSwings++;
        active.NoProgressTicks = 0;
        return true;
    }

    private static void ResetVolcanoCombatClearance(
        ActiveVolcanoCombat active)
    {
        active.ClearanceTarget = null;
        active.ClearanceTool = null;
        active.ClearanceBefore = string.Empty;
        active.ClearanceSwings = 0;
        active.NoProgressTicks = 0;
    }

    private static int TrackVolcanoCombatProgress(ActiveVolcanoCombat active)
    {
        if (Vector2.DistanceSquared(active.LastProgressPosition, Game1.player.Position) >= 0.01f ||
            Vector2.DistanceSquared(active.LastProgressTargetPosition, active.Target.Position) >= 0.01f ||
            active.Target.Health < active.LastProgressTargetHealth)
        {
            active.LastProgressPosition = Game1.player.Position;
            active.LastProgressTargetPosition = active.Target.Position;
            active.LastProgressTargetHealth = active.Target.Health;
            active.NoProgressTicks = 0;
        }
        else
        {
            active.NoProgressTicks++;
        }
        return active.NoProgressTicks;
    }

    private static void ObserveVolcanoCombatMovement(ActiveVolcanoCombat active)
    {
        var currentPosition = Game1.player.Position;
        active.StuckTicks = Vector2.DistanceSquared(active.LastMovementPosition, currentPosition) < 0.01f
            ? active.StuckTicks + 1
            : 0;
        var currentTile = Game1.player.TilePoint;
        if (currentTile != active.LastMovementTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastMovementTile, currentTile);
        }
        active.LastMovementPosition = currentPosition;
        active.LastMovementTile = currentTile;
    }

    private static void RecordVolcanoCombatHealth(ActiveVolcanoCombat active)
    {
        if (active.TargetHealthSequence.Count == 0 || active.TargetHealthSequence[^1] != active.Target.Health)
        {
            if (active.TargetHealthSequence.Count > 0 && active.Target.Health < active.TargetHealthSequence[^1])
            {
                active.HitCount++;
            }
            active.TargetHealthSequence.Add(active.Target.Health);
        }
        if (active.PlayerHealthSequence.Count == 0 || active.PlayerHealthSequence[^1] != Game1.player.health)
        {
            active.PlayerHealthSequence.Add(Game1.player.health);
        }
    }

    private void CompleteVolcanoCombat(ActiveVolcanoCombat active)
    {
        TryApplySmapiButtonOverride(
            HeavyHitterInputButton(active.Weapon),
            pressed: false,
            out _);
        if (active.ClearanceTool is not null)
        {
            TryApplySmapiButtonOverride(
                HeavyHitterInputButton(active.ClearanceTool),
                pressed: false,
                out _);
        }
        if (active.DefeatDialogueButtonHeld)
        {
            TryApplySmapiLeftButtonOverride(pressed: false, out _);
            active.DefeatDialogueButtonHeld = false;
        }
        StopAllMovement();
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoCombat = null;
        RecordVolcanoCombatHealth(active);
        var request = active.Pending.Request;
        var damageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Volcano.NameOrUniqueName,
            TargetTileX = request.TargetTileX,
            TargetTileY = request.TargetTileY,
            ToolQualifiedItemId = active.Weapon.QualifiedItemId,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "combat_volcano_monster",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = damageTaken == 0
                ? new[] { "native_melee_defeated_volcano_target", "player_health_unchanged" }
                : new[] { "native_melee_defeated_volcano_target", "player_damage_observed=" + damageTaken },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = VolcanoCombatObservedEffect(active),
            CombatTargetRuntimeType = active.TargetRuntimeType,
            CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity,
            CombatTargetName = active.TargetName,
            CombatAttackCount = active.AttackCount,
            CombatHitCount = active.HitCount,
            CombatTargetHealthSequence = active.TargetHealthSequence.ToArray(),
            CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray(),
            CombatDamageTaken = damageTaken,
            CombatTargetDefeated = true,
            CombatMethod = "melee",
            CombatTerminalState = "defeat",
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "volcano.monsters[" + active.TargetRuntimeIdentity + "].present", Before = "true", After = "false" },
                new SimulatedFactChange { Path = "player.health", Before = active.PlayerHealthBefore.ToString(), After = Game1.player.health.ToString() }
            }
        });
    }

    private void CompleteVolcanoCombatBlocked(ActiveVolcanoCombat active, string reason)
    {
        TryApplySmapiButtonOverride(
            HeavyHitterInputButton(active.Weapon),
            pressed: false,
            out _);
        if (active.ClearanceTool is not null)
        {
            TryApplySmapiButtonOverride(
                HeavyHitterInputButton(active.ClearanceTool),
                pressed: false,
                out _);
        }
        if (active.DefeatDialogueButtonHeld)
        {
            TryApplySmapiLeftButtonOverride(pressed: false, out _);
            active.DefeatDialogueButtonHeld = false;
        }
        StopAllMovement();
        if (ReferenceEquals(Game1.player.CurrentTool, active.Weapon))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }
        RestoreSlot(active.RestoreSlotIndex);
        activeVolcanoCombat = null;
        RecordVolcanoCombatHealth(active);
        var result = BlockedWithPrimitive(active.Pending.Request, "combat_volcano_monster", active.RequestedEffect, VolcanoCombatObservedEffect(active), reason);
        result.ToolQualifiedItemId = active.Weapon.QualifiedItemId;
        result.ActualTicks = active.ElapsedTicks;
        result.TrainingImpactScope = "executor_calibration";
        result.CombatTargetRuntimeType = active.TargetRuntimeType;
        result.CombatTargetRuntimeIdentity = active.TargetRuntimeIdentity;
        result.CombatTargetName = active.TargetName;
        result.CombatAttackCount = active.AttackCount;
        result.CombatHitCount = active.HitCount;
        result.CombatTargetHealthSequence = active.TargetHealthSequence.ToArray();
        result.CombatPlayerHealthSequence = active.PlayerHealthSequence.ToArray();
        result.CombatDamageTaken = Math.Max(0, active.PlayerHealthBefore - Game1.player.health);
        result.CombatTargetDefeated = active.Target.Health <= 0;
        result.CombatMethod = "melee";
        result.CombatTerminalState = active.Target.Health <= 0 ? "defeat" : "blocked";
        active.Pending.Completion.SetResult(result);
    }

    private static string VolcanoCombatObservedEffect(ActiveVolcanoCombat active)
    {
        return "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";target_type=" + active.TargetRuntimeType +
            ";target_name=" + active.TargetName +
            ";target_health=" + active.Target.Health +
            ";player_health=" + Game1.player.health +
            ";attacks=" + active.AttackCount +
            ";hits=" + active.HitCount;
    }}
