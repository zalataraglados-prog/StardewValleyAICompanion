using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.SaveSerialization;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private bool ReplanTileMove(ActiveTileMove move, bool avoidSoftObstacles)
    {
        var currentTile = Game1.player.TilePoint;
        var remainingTiles = Math.Max(1, 512 - move.PathIndex);
        var path = TryBuildTilePath(Game1.currentLocation, currentTile, move.TargetTile, remainingTiles, out _, avoidSoftObstacles);
        if (path is null)
        {
            return false;
        }

        move.Path = path;
        move.PathIndex = 0;
        move.CurrentDirection = null;
        move.StuckTicks = 0;
        move.SoftObstacleTicks = 0;
        move.Pending.MovementExtraTicks += 30;
        return true;
    }

    private bool TryClearRemovableObstacle(GameLocation location, Point tile, ActiveTileMove move)
    {
        // Route repair is compiled as an explicit clear_obstacle primitive.
        // Movement must never mutate a blocker synchronously between native ticks.
        return false;
    }

    private void StartClearObstacle(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", "current_location.obstacle=clear", ClearObstacleObservedEffect(null), "clear_obstacle_target_tile_required"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = "current_location.obstacle[" + target.X + "," + target.Y + "]=clear";
        if (!CanClearRouteObstacles(location) && !IsExplicitPortableSkillClearanceObject(location, target))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_location_not_whitelisted"));
            return;
        }

        if (ManhattanDistance(Game1.player.TilePoint, target) > 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_target_not_adjacent"));
            return;
        }
        var targetIsArtifactSpot = location.objects.TryGetValue(target.ToVector2(), out var targetObject) &&
            targetObject.QualifiedItemId is "(O)590" or "(O)SeedSpot";
        if (targetIsArtifactSpot)
        {
            var artifactRequestReason = ValidateArtifactSpotExecutionRequest(request);
            if (artifactRequestReason is not null)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), artifactRequestReason));
                return;
            }
        }
        if (location.objects.TryGetValue(target.ToVector2(), out var seedSpot) &&
            seedSpot.QualifiedItemId == "(O)SeedSpot" &&
            Game1.player.stats.Get("ArtifactSpotsDug") >= int.MaxValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "blocked_artifact_spot_stat_projection_overflow"));
            return;
        }

        var projectedTool = SelectClearanceTool(location, target);
        var tool = projectedTool;
        var toolSlotBefore = Game1.player.CurrentToolIndex;
        if (request.ToolSlotIndex.HasValue)
        {
            if (request.ToolSlotIndex.Value < 0 ||
                request.ToolSlotIndex.Value >= Game1.player.Items.Count ||
                Game1.player.Items[request.ToolSlotIndex.Value] is not Tool requestedTool ||
                !CanToolClearTarget(location, target, requestedTool))
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_tool_slot_drifted"));
                return;
            }
            tool = requestedTool;
        }
        if (tool is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_no_matching_tool_or_obstacle"));
            return;
        }
        if (!string.IsNullOrWhiteSpace(request.RequiredToolKind) &&
            !string.Equals(request.RequiredToolKind, ClearanceToolKind(tool), StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_required_tool_kind_drifted"));
            return;
        }
        var projectedStateDrift = ClearanceProjectedStateDriftReason(request);
        if (projectedStateDrift is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), projectedStateDrift));
            return;
        }
        var expectedOutputItems = TryParseClearanceOutputItems(request.ClearOutputItemsJson, out var parsedOutputItems)
            ? parsedOutputItems
            : null;
        if (!string.IsNullOrWhiteSpace(request.ClearOutputProjectionStatus) &&
            (!string.Equals(request.ClearOutputProjectionStatus, "exact", StringComparison.Ordinal) || expectedOutputItems is null))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_output_multiset_projection_invalid"));
            return;
        }
        Dictionary<ClearanceOutputItemKey, int>? outputItemMultisetBefore = null;
        if (expectedOutputItems is not null &&
            !TryReadClearanceDebrisItemMultiset(location, out outputItemMultisetBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "clear_obstacle", requested, ClearObstacleObservedEffect(target), "clear_obstacle_output_multiset_before_unreadable"));
            return;
        }

        var before = ObstacleLabel(location, target);
        var staminaBefore = Game1.player.Stamina;
        var beforeForagingExperience = Game1.player.experiencePoints[Farmer.foragingSkill];
        var expectedForagingExperience = ProjectedClearanceForagingExperience(location, target);
        var outputProjection = ProjectedClearanceOutput(location, target);
        var primaryOutputCountBefore = outputProjection is null
            ? 0
            : CountLocationDebrisItem(location, outputProjection.PrimaryQualifiedItemId);
        var bonusOutputCountBefore = outputProjection is null || string.IsNullOrWhiteSpace(outputProjection.BonusQualifiedItemId)
            ? 0
            : CountLocationDebrisItem(location, outputProjection.BonusQualifiedItemId);
        var artifactSpotsDugBefore = Game1.player.stats.Get("ArtifactSpotsDug");
        var defenseBookMailBefore = Game1.player.mailReceived.Contains("DefenseBookDropped");
        var targetTerrainFeatureBefore = ClearanceTerrainFeatureLabel(location, target);
        activeClearObstacle = new ActiveClearObstacle(
            pending,
            location,
            target,
            tool,
            toolSlotBefore,
            targetIsArtifactSpot,
            expectedOutputItems,
            outputItemMultisetBefore,
            before,
            staminaBefore,
            beforeForagingExperience,
            expectedForagingExperience,
            outputProjection,
            primaryOutputCountBefore,
            bonusOutputCountBefore,
            artifactSpotsDugBefore,
            defenseBookMailBefore,
            targetTerrainFeatureBefore,
            Math.Clamp(request.MaxCrops, 1, 64));
    }

    private void TickClearObstacle()
    {
        if (activeClearObstacle is null)
        {
            return;
        }

        var active = activeClearObstacle;
        try
        {
            TickClearObstacleCore(active);
        }
        catch (Exception ex)
        {
            WriteExecutorDiagnosticDump(
                "clear_obstacle_exception:" + ex.GetType().Name);
            CompleteClearObstacle(
                active,
                "clear_obstacle_execution_exception:" + ex.GetType().Name);
        }
    }

    private void TickClearObstacleCore(ActiveClearObstacle active)
    {
        active.ElapsedTicks++;
        if (!Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteClearObstacle(
                active,
                "clear_obstacle_location_changed_or_world_unavailable");
            return;
        }

        if (active.ElapsedTicks > active.MaxTicks)
        {
            WriteExecutorDiagnosticDump("clear_obstacle_timeout");
            CompleteClearObstacle(active, "clear_obstacle_timeout");
            return;
        }

        var currentLabel = ObstacleLabel(active.Location, active.Target);
        var targetCleared = active.TargetIsArtifactSpot
            ? !active.Location.objects.ContainsKey(active.Target.ToVector2())
            : currentLabel == "clear";
        if (active.Lifecycle.Phase == NativeToolActionPhase.Ready)
        {
            if (targetCleared)
            {
                CompleteClearObstacle(
                    active,
                    active.SwingCount > 0
                        ? null
                        : "clear_obstacle_target_changed_before_native_action");
                return;
            }

            if (active.SwingCount >= active.MaxSwings)
            {
                CompleteClearObstacle(
                    active,
                    "clear_obstacle_swing_budget_exceeded");
                return;
            }

            if (Game1.player.Stamina <= 0f)
            {
                CompleteClearObstacle(
                    active,
                    "clear_obstacle_energy_exhausted");
                return;
            }
        }

        var decision = active.Lifecycle.Advance(ObserveNativeToolAction());
        switch (decision.Command)
        {
            case NativeToolActionCommand.Press:
                SelectTool(active.Tool);
                Game1.player.faceDirection(
                    DirectionTo(Game1.player.TilePoint, active.Target));
                Game1.player.lastClick = new Vector2(
                    active.Target.X * Game1.tileSize,
                    active.Target.Y * Game1.tileSize);
                Game1.player.BeginUsingTool();
                return;

            case NativeToolActionCommand.Release:
                Game1.player.EndUsingTool();
                return;

            case NativeToolActionCommand.CycleCompleted:
                active.SwingCount++;
                active.ObservedLabels.Add(currentLabel);
                active.Lifecycle.Reset();
                if (active.TargetIsArtifactSpot
                        ? !active.Location.objects.ContainsKey(
                            active.Target.ToVector2())
                        : ObstacleLabel(
                            active.Location,
                            active.Target) == "clear")
                {
                    CompleteClearObstacle(active, null);
                }
                return;

            case NativeToolActionCommand.Block:
                WriteExecutorDiagnosticDump(decision.Reason);
                CompleteClearObstacle(active, decision.Reason);
                return;
        }
    }

    private void CompleteClearObstacle(
        ActiveClearObstacle active,
        string? forcedBlockReason)
    {
        StopAllMovement(
            forcedBlockReason is null
                ? "clear_obstacle_completed"
                : forcedBlockReason);
        if (forcedBlockReason is not null &&
            active.Lifecycle.Phase != NativeToolActionPhase.Ready &&
            ReferenceEquals(Game1.player.CurrentTool, active.Tool))
        {
            Game1.player.completelyStopAnimatingOrDoingAction();
        }

        activeClearObstacle = null;
        var request = active.Pending.Request;
        var location = active.Location;
        var target = active.Target;
        var tool = active.Tool;
        var toolSlotBefore = active.ToolSlotBefore;
        var targetIsArtifactSpot = active.TargetIsArtifactSpot;
        var expectedOutputItems = active.ExpectedOutputItems;
        var outputItemMultisetBefore = active.OutputItemMultisetBefore;
        var before = active.Before;
        var staminaBefore = active.StaminaBefore;
        var beforeForagingExperience = active.BeforeForagingExperience;
        var expectedForagingExperience = active.ExpectedForagingExperience;
        var outputProjection = active.OutputProjection;
        var primaryOutputCountBefore = active.PrimaryOutputCountBefore;
        var bonusOutputCountBefore = active.BonusOutputCountBefore;
        var artifactSpotsDugBefore = active.ArtifactSpotsDugBefore;
        var defenseBookMailBefore = active.DefenseBookMailBefore;
        var targetTerrainFeatureBefore = active.TargetTerrainFeatureBefore;
        var observedLabels = active.ObservedLabels;
        var started = active.StartedAt;
        var requested = "current_location.obstacle[" +
            target.X + "," + target.Y + "]=clear";
        var after = ObstacleLabel(location, target);
        var foragingExperienceAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var foragingExperienceDelta = foragingExperienceAfter - beforeForagingExperience;
        var primaryOutputCountAfter = outputProjection is null
            ? 0
            : CountLocationDebrisItem(location, outputProjection.PrimaryQualifiedItemId);
        var bonusOutputCountAfter = outputProjection is null || string.IsNullOrWhiteSpace(outputProjection.BonusQualifiedItemId)
            ? 0
            : CountLocationDebrisItem(location, outputProjection.BonusQualifiedItemId);
        var primaryOutputQuantityDelta = primaryOutputCountAfter - primaryOutputCountBefore;
        var bonusOutputQuantityDelta = bonusOutputCountAfter - bonusOutputCountBefore;
        var artifactSpotsDugAfter = Game1.player.stats.Get("ArtifactSpotsDug");
        var artifactSpotsDugDelta = (long)artifactSpotsDugAfter - artifactSpotsDugBefore;
        var defenseBookMailAfter = Game1.player.mailReceived.Contains("DefenseBookDropped");
        var targetTerrainFeatureAfter = ClearanceTerrainFeatureLabel(location, target);
        Dictionary<ClearanceOutputItemKey, int>? outputItemMultisetAfter = null;
        var outputItemMultisetAfterReadable = expectedOutputItems is null ||
            TryReadClearanceDebrisItemMultiset(location, out outputItemMultisetAfter);
        var outputItemMultisetMatched = expectedOutputItems is null ||
            outputItemMultisetAfterReadable && ClearanceOutputDeltaMatches(
                outputItemMultisetBefore!,
                outputItemMultisetAfter!,
                expectedOutputItems);
        var expectedArtifactSpotsDugDelta = request.ArtifactSpotsDugDelta ?? outputProjection?.ArtifactSpotsDugDelta;
        var expectedArtifactSpotsDugAfter = request.ArtifactSpotsDugExpectedAfter;
        var expectedTerrainFeatureAfter = !string.IsNullOrWhiteSpace(request.ClearTerrainFeatureExpectedAfter)
            ? request.ClearTerrainFeatureExpectedAfter
            : outputProjection?.TerrainFeatureExpectedAfter ?? string.Empty;
        var expectedDefenseBookMailAfter = request.DefenseBookMailExpectedAfter.HasValue
            ? request.DefenseBookMailExpectedAfter.Value == 1
            : outputProjection?.DefenseBookMailExpectedAfter;
        var projectedOutputMatched = outputItemMultisetMatched &&
            (expectedOutputItems is not null || outputProjection is null ||
                primaryOutputQuantityDelta == outputProjection.PrimaryQuantity &&
                bonusOutputQuantityDelta == outputProjection.BonusQuantity) &&
            (!expectedArtifactSpotsDugDelta.HasValue || artifactSpotsDugDelta == expectedArtifactSpotsDugDelta.Value) &&
            (!expectedArtifactSpotsDugAfter.HasValue || (long)artifactSpotsDugAfter == expectedArtifactSpotsDugAfter.Value) &&
            (string.IsNullOrWhiteSpace(expectedTerrainFeatureAfter) || targetTerrainFeatureAfter == expectedTerrainFeatureAfter) &&
            (!expectedDefenseBookMailAfter.HasValue || defenseBookMailAfter == expectedDefenseBookMailAfter.Value);
        var targetClearanceCompleted = targetIsArtifactSpot
            ? !location.objects.ContainsKey(target.ToVector2())
            : after == "clear";
        var verified = forcedBlockReason is null &&
            targetClearanceCompleted &&
            (!expectedForagingExperience.HasValue || foragingExperienceDelta == expectedForagingExperience.Value) &&
            projectedOutputMatched;
        var verificationFailureReason = forcedBlockReason ??
            (!targetClearanceCompleted
            ? "target_obstacle_still_present"
            : expectedForagingExperience.HasValue && foragingExperienceDelta != expectedForagingExperience.Value
                ? "projected_foraging_experience_mismatch"
                : !outputItemMultisetAfterReadable
                    ? "clear_obstacle_output_multiset_after_unreadable"
                    : "projected_clear_output_mismatch");
        var changedFacts = new List<SimulatedFactChange>
        {
            new SimulatedFactChange
            {
                Path = "current_location.obstacle[" + target.X + "," + target.Y + "]",
                Before = before,
                After = after
            },
            new SimulatedFactChange
            {
                Path = "player.energy",
                Before = staminaBefore.ToString("0.###"),
                After = Game1.player.Stamina.ToString("0.###")
            },
            new SimulatedFactChange
            {
                Path = "player.skills.foraging.experience",
                Before = beforeForagingExperience.ToString(System.Globalization.CultureInfo.InvariantCulture),
                After = foragingExperienceAfter.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }
        };
        if (outputProjection is not null && expectedOutputItems is null)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "current_location.debris.count[" + outputProjection.PrimaryQualifiedItemId + "]",
                Before = primaryOutputCountBefore.ToString(CultureInfo.InvariantCulture),
                After = primaryOutputCountAfter.ToString(CultureInfo.InvariantCulture)
            });
            if (!string.IsNullOrWhiteSpace(outputProjection.BonusQualifiedItemId))
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "current_location.debris.count[" + outputProjection.BonusQualifiedItemId + "]",
                    Before = bonusOutputCountBefore.ToString(CultureInfo.InvariantCulture),
                    After = bonusOutputCountAfter.ToString(CultureInfo.InvariantCulture)
                });
            }
            if (outputProjection.ArtifactSpotsDugDelta.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "player.stats.ArtifactSpotsDug",
                    Before = artifactSpotsDugBefore.ToString(CultureInfo.InvariantCulture),
                    After = artifactSpotsDugAfter.ToString(CultureInfo.InvariantCulture)
                });
            }
            if (outputProjection.DefenseBookMailExpectedAfter.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "player.mail_received.DefenseBookDropped",
                    Before = defenseBookMailBefore.ToString().ToLowerInvariant(),
                    After = defenseBookMailAfter.ToString().ToLowerInvariant()
                });
            }
            if (!string.IsNullOrWhiteSpace(outputProjection.TerrainFeatureExpectedAfter))
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "current_location.terrain_feature[" + target.X + "," + target.Y + "]",
                    Before = targetTerrainFeatureBefore,
                    After = targetTerrainFeatureAfter
                });
            }
        }
        if (expectedOutputItems is not null &&
            outputItemMultisetAfterReadable &&
            outputItemMultisetBefore is not null &&
            outputItemMultisetAfter is not null)
        {
            foreach (var key in outputItemMultisetBefore.Keys
                .Concat(outputItemMultisetAfter.Keys)
                .Distinct()
                .OrderBy(key => key.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(key => key.RuntimeType, StringComparer.Ordinal)
                .ThenBy(key => key.Quality)
                .ThenBy(key => key.UnitStateSha256, StringComparer.Ordinal))
            {
                var beforeQuantity = outputItemMultisetBefore.TryGetValue(key, out var beforeValue) ? beforeValue : 0;
                var afterQuantity = outputItemMultisetAfter.TryGetValue(key, out var afterValue) ? afterValue : 0;
                if (beforeQuantity == afterQuantity && !expectedOutputItems.Any(item => item.Key == key))
                {
                    continue;
                }
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "current_location.debris.item_multiset[" + key.QualifiedItemId + "," + key.Quality + "," + key.UnitStateSha256 + "]",
                    Before = beforeQuantity.ToString(CultureInfo.InvariantCulture),
                    After = afterQuantity.ToString(CultureInfo.InvariantCulture)
                });
            }
            if (expectedArtifactSpotsDugDelta.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "player.stats.ArtifactSpotsDug",
                    Before = artifactSpotsDugBefore.ToString(CultureInfo.InvariantCulture),
                    After = artifactSpotsDugAfter.ToString(CultureInfo.InvariantCulture)
                });
            }
            if (expectedDefenseBookMailAfter.HasValue)
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "player.mail_received.DefenseBookDropped",
                    Before = defenseBookMailBefore.ToString().ToLowerInvariant(),
                    After = defenseBookMailAfter.ToString().ToLowerInvariant()
                });
            }
            if (!string.IsNullOrWhiteSpace(expectedTerrainFeatureAfter))
            {
                changedFacts.Add(new SimulatedFactChange
                {
                    Path = "current_location.terrain_feature[" + target.X + "," + target.Y + "]",
                    Before = targetTerrainFeatureBefore,
                    After = targetTerrainFeatureAfter
                });
            }
        }
        if (toolSlotBefore != Game1.player.CurrentToolIndex)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.current_tool_index",
                Before = toolSlotBefore.ToString(CultureInfo.InvariantCulture),
                After = Game1.player.CurrentToolIndex.ToString(CultureInfo.InvariantCulture)
            });
        }
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            EnergyBefore = staminaBefore,
            EnergyAfter = Game1.player.Stamina,
            ToolQualifiedItemId = tool.QualifiedItemId,
            ToolUpgradeLevel = tool.UpgradeLevel,
            ToolUseCount = active.SwingCount,
            ActualTicks = active.ElapsedTicks,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "clear_obstacle",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "target_obstacle_cleared", "tool=" + tool.GetType().Name, "projected_foraging_experience_matched=" + (expectedForagingExperience.HasValue ? "true" : "not_projected"), "projected_output_matched=" + (expectedOutputItems is null && outputProjection is null ? "not_projected" : "true") }
                : new[] { verificationFailureReason, "tool=" + tool.GetType().Name },
            RequestedEffect = requested,
            ObservedEffect = "before=" + before + ";after=" + after + ";labels=" + string.Join(">", observedLabels) +
                ";foraging_experience_delta=" + foragingExperienceDelta +
                ";expected_foraging_experience=" + (expectedForagingExperience?.ToString(CultureInfo.InvariantCulture) ?? "not_projected") +
                ";output_qualified_item_id=" + (outputProjection?.PrimaryQualifiedItemId ?? "not_projected") +
                ";output_quantity_delta=" + primaryOutputQuantityDelta +
                ";bonus_output_qualified_item_id=" + (outputProjection?.BonusQualifiedItemId ?? "not_projected") +
                ";bonus_output_quantity_delta=" + bonusOutputQuantityDelta +
                ";output_multiset_expected_rows=" + (expectedOutputItems?.Length.ToString(CultureInfo.InvariantCulture) ?? "legacy") +
                ";output_multiset_matched=" + outputItemMultisetMatched.ToString().ToLowerInvariant() +
                ";artifact_spots_dug_delta=" + artifactSpotsDugDelta +
                ";target_terrain_feature_after=" + targetTerrainFeatureAfter +
                ";defense_book_mail_after=" + defenseBookMailAfter.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { verificationFailureReason },
            ChangedFacts = changedFacts.ToArray()
        });
    }

    private static string ClearObstacleObservedEffect(Point? target)
    {
        return target.HasValue
            ? "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y + ";target=" + target.Value.X + "," + target.Value.Y + ";obstacle=" + ObstacleLabel(Game1.currentLocation, target.Value)
            : "location=" + Game1.currentLocation.NameOrUniqueName + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }

    private static Tool? SelectClearanceTool(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            if (obj is BreakableContainer)
            {
                return FindHeavyTool();
            }

            if (obj.IsBreakableStone())
            {
                return FindTool<Pickaxe>();
            }

            if (obj.IsWeeds())
            {
                return FindScythe() ?? FindHeavyTool();
            }

            if (obj.IsTwig())
            {
                return FindTool<Axe>();
            }

            if (obj.QualifiedItemId is "(O)590" or "(O)SeedSpot")
            {
                return FindTool<Hoe>();
            }

            return null;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return feature switch
            {
                Grass => FindScythe() ?? FindHeavyTool(),
                Tree => FindTool<Axe>(),
                FruitTree => FindTool<Axe>(),
                _ => null
            };
        }

        var tileRect = TileRectangle(tile);
        foreach (var largeFeature in location.largeTerrainFeatures)
        {
            if (largeFeature.getBoundingBox().Intersects(tileRect))
            {
                return FindTool<Axe>();
            }
        }

        return null;
    }

    private static bool IsExplicitPortableSkillClearanceObject(GameLocation location, Point tile)
    {
        return location.objects.TryGetValue(tile.ToVector2(), out var item) &&
            item.GetType() == typeof(StardewValley.Object) &&
            (item.IsTwig() || item.QualifiedItemId is "(O)590" or "(O)SeedSpot");
    }

    private static string? ValidateArtifactSpotExecutionRequest(TrainingExecutionRequest request)
    {
        if (!request.ToolSlotIndex.HasValue ||
            !string.Equals(request.RequiredToolKind, "hoe", StringComparison.Ordinal))
        {
            return "artifact_spot_typed_hoe_required";
        }
        if (!string.Equals(request.ClearOutputProjectionStatus, "exact", StringComparison.Ordinal) ||
            !TryParseClearanceOutputItems(request.ClearOutputItemsJson, out _))
        {
            return "artifact_spot_output_multiset_projection_required";
        }
        if (!request.ArtifactSpotsDugBefore.HasValue ||
            !request.ArtifactSpotsDugDelta.HasValue ||
            !request.ArtifactSpotsDugExpectedAfter.HasValue ||
            request.ArtifactSpotsDugDelta.Value != 1 ||
            (long)request.ArtifactSpotsDugBefore.Value + request.ArtifactSpotsDugDelta.Value != request.ArtifactSpotsDugExpectedAfter.Value)
        {
            return "artifact_spot_stat_projection_invalid";
        }
        if (string.IsNullOrWhiteSpace(request.ClearTerrainFeatureExpectedAfter))
        {
            return "artifact_spot_terrain_projection_required";
        }
        if (request.DefenseBookMailBefore is not (0 or 1) ||
            request.DefenseBookMailExpectedAfter is not (0 or 1) ||
            request.DefenseBookMailExpectedAfter.Value < request.DefenseBookMailBefore.Value)
        {
            return "artifact_spot_mail_projection_invalid";
        }
        return null;
    }

    private static string? ClearanceProjectedStateDriftReason(TrainingExecutionRequest request)
    {
        if (request.ArtifactSpotsDugBefore.HasValue &&
            (long)Game1.player.stats.Get("ArtifactSpotsDug") != request.ArtifactSpotsDugBefore.Value)
        {
            return "clear_obstacle_artifact_spots_dug_before_drifted";
        }
        if (request.DefenseBookMailBefore.HasValue &&
            Game1.player.mailReceived.Contains("DefenseBookDropped") != (request.DefenseBookMailBefore.Value == 1))
        {
            return "clear_obstacle_defense_book_mail_before_drifted";
        }
        return null;
    }

    private static bool TryParseClearanceOutputItems(
        string json,
        out ClearanceOutputItemExpectation[] items)
    {
        items = Array.Empty<ClearanceOutputItemExpectation>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<ClearanceOutputItemExpectation[]>(json, JsonOptions);
            if (parsed is null || parsed.Any(item =>
                item is null ||
                string.IsNullOrWhiteSpace(item.RuntimeType) ||
                string.IsNullOrWhiteSpace(item.QualifiedItemId) ||
                item.Quantity <= 0 ||
                string.IsNullOrWhiteSpace(item.UnitStateSha256) ||
                item.UnitStateSha256.Length != 64 ||
                item.UnitStateSha256.Any(character => !Uri.IsHexDigit(character))))
            {
                return false;
            }
            if (parsed.Select(item => item.Key).Distinct().Count() != parsed.Length)
            {
                return false;
            }
            items = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadClearanceDebrisItemMultiset(
        GameLocation location,
        out Dictionary<ClearanceOutputItemKey, int> quantities)
    {
        quantities = new Dictionary<ClearanceOutputItemKey, int>();
        try
        {
            foreach (var debris in location.debris)
            {
                if (debris.item is not Item item)
                {
                    continue;
                }
                var key = ClearanceOutputItemKey.From(item);
                quantities[key] = quantities.TryGetValue(key, out var quantity)
                    ? checked(quantity + item.Stack)
                    : item.Stack;
            }
            return true;
        }
        catch
        {
            quantities.Clear();
            return false;
        }
    }

    private static bool ClearanceOutputDeltaMatches(
        IReadOnlyDictionary<ClearanceOutputItemKey, int> before,
        IReadOnlyDictionary<ClearanceOutputItemKey, int> after,
        IReadOnlyList<ClearanceOutputItemExpectation> expected)
    {
        var expectedQuantities = expected.ToDictionary(item => item.Key, item => item.Quantity);
        foreach (var key in before.Keys.Concat(after.Keys).Concat(expectedQuantities.Keys).Distinct())
        {
            var beforeQuantity = before.TryGetValue(key, out var beforeValue) ? beforeValue : 0;
            var afterQuantity = after.TryGetValue(key, out var afterValue) ? afterValue : 0;
            var expectedQuantity = expectedQuantities.TryGetValue(key, out var expectedValue) ? expectedValue : 0;
            if (afterQuantity - beforeQuantity != expectedQuantity)
            {
                return false;
            }
        }
        return true;
    }

    private static bool CanToolClearTarget(GameLocation location, Point tile, Tool tool)
    {
        var key = tile.ToVector2();
        if (location.objects.TryGetValue(key, out var item))
        {
            return item switch
            {
                BreakableContainer _ => tool.isHeavyHitter(),
                _ when item.IsBreakableStone() => tool is Pickaxe,
                _ when item.IsWeeds() => (tool is MeleeWeapon weapon && weapon.isScythe()) || tool.isHeavyHitter(),
                _ when item.IsTwig() => tool is Axe,
                _ when item.QualifiedItemId is "(O)590" or "(O)SeedSpot" => tool is Hoe,
                _ => false
            };
        }
        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return feature switch
            {
                Grass _ => (tool is MeleeWeapon weapon && weapon.isScythe()) || tool.isHeavyHitter(),
                Tree _ => tool is Axe,
                FruitTree _ => tool is Axe,
                _ => false
            };
        }
        var tileRectangle = TileRectangle(tile);
        return location.largeTerrainFeatures.Any(feature =>
            feature.getBoundingBox().Intersects(tileRectangle) && tool is Axe);
    }

    private static int? ProjectedClearanceForagingExperience(GameLocation location, Point tile)
    {
        if (!location.objects.TryGetValue(tile.ToVector2(), out var item) || item.GetType() != typeof(StardewValley.Object))
        {
            return null;
        }
        if (item.IsTwig())
        {
            return 1;
        }
        return item.QualifiedItemId is "(O)590" or "(O)SeedSpot" ? 15 : null;
    }

    private static ClearanceOutputProjection? ProjectedClearanceOutput(GameLocation location, Point tile)
    {
        if (!location.objects.TryGetValue(tile.ToVector2(), out var item) || item.GetType() != typeof(StardewValley.Object))
        {
            return null;
        }
        if (item.IsTwig())
        {
            return new ClearanceOutputProjection("(O)388", 1, string.Empty, 0, null, string.Empty, null);
        }
        if (item.QualifiedItemId != "(O)SeedSpot")
        {
            return null;
        }

        var random = Utility.CreateDaySaveRandom(
            (0f - tile.X) * 7f,
            tile.Y * 777f,
            Game1.netWorldState.Value.TreasureTotemsUsed * 777);
        var artifactSpotsDugAfter = Game1.player.stats.Get("ArtifactSpotsDug") + 1;
        var defenseBookMailBefore = Game1.player.mailReceived.Contains("DefenseBookDropped");
        var defenseBookDropped = artifactSpotsDugAfter > 2 &&
            random.NextDouble() < 0.008 + (!defenseBookMailBefore ? artifactSpotsDugAfter * 0.002 : 0.005);
        var seed = Utility.getRaccoonSeedForCurrentTimeOfYear(Game1.player, random);
        var terrainFeatureExpectedAfter = location.terrainFeatures.TryGetValue(tile.ToVector2(), out var existingFeature)
            ? existingFeature.GetType().Name
            : location is MineShaft mine && mine.getMineArea() == 77377
                ? "none"
                : "HoeDirt";
        return new ClearanceOutputProjection(
            seed.QualifiedItemId,
            seed.Stack,
            "(O)Book_Defense",
            defenseBookDropped ? 1 : 0,
            1,
            terrainFeatureExpectedAfter,
            defenseBookMailBefore || defenseBookDropped);
    }

    private static int CountLocationDebrisItem(GameLocation location, string qualifiedItemId)
    {
        return location.debris.Count(debris =>
            string.Equals(DebrisQualifiedItemId(debris), qualifiedItemId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ClearanceTerrainFeatureLabel(GameLocation location, Point tile)
    {
        return location.terrainFeatures.TryGetValue(tile.ToVector2(), out var feature)
            ? feature.GetType().Name
            : "none";
    }

    private static string ClearanceToolKind(Tool tool)
    {
        return tool switch
        {
            Axe => "axe",
            Hoe => "hoe",
            Pickaxe => "pickaxe",
            MeleeWeapon => "melee_weapon",
            _ => tool.GetType().Name.ToLowerInvariant()
        };
    }

    private sealed record ClearanceOutputProjection(
        string PrimaryQualifiedItemId,
        int PrimaryQuantity,
        string BonusQualifiedItemId,
        int BonusQuantity,
        int? ArtifactSpotsDugDelta,
        string TerrainFeatureExpectedAfter,
        bool? DefenseBookMailExpectedAfter);

    private sealed record ClearanceOutputItemExpectation(
        string RuntimeType,
        string QualifiedItemId,
        int Quality,
        string UnitStateSha256,
        int Quantity)
    {
        public ClearanceOutputItemKey Key => new(RuntimeType, QualifiedItemId, Quality, UnitStateSha256);
    }

    private readonly record struct ClearanceOutputItemKey(
        string RuntimeType,
        string QualifiedItemId,
        int Quality,
        string UnitStateSha256)
    {
        public static ClearanceOutputItemKey From(Item item)
        {
            return FromUnit(item, hasBeenInInventory: item.HasBeenInInventory);
        }

        public static ClearanceOutputItemKey FromInventoryReceipt(Item item)
        {
            return FromUnit(item, hasBeenInInventory: true);
        }

        private static ClearanceOutputItemKey FromUnit(Item item, bool hasBeenInInventory)
        {
            var unit = item.getOne();
            unit.Stack = 1;
            unit.HasBeenInInventory = hasBeenInInventory;
            if (item is Tool sourceTool && unit is Tool unitTool)
            {
                unitTool.swingTicker = sourceTool.swingTicker;
            }
            if (unit is StardewValley.Object objectUnit)
            {
                objectUnit.Flipped = false;
            }
            using var stream = new MemoryStream();
            SaveSerializer.GetSerializer(unit.GetType()).Serialize(stream, unit);
            var stateHash = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
            return new ClearanceOutputItemKey(
                unit.GetType().FullName ?? unit.GetType().Name,
                unit.QualifiedItemId,
                unit.Quality,
                stateHash);
        }
    }

    private static TTool? FindTool<TTool>() where TTool : Tool
    {
        return Game1.player.Items.OfType<TTool>().FirstOrDefault();
    }

    private static Tool? FindScythe()
    {
        return Game1.player.Items.OfType<MeleeWeapon>().FirstOrDefault(weapon => weapon.isScythe());
    }

    private static Tool? FindHeavyTool()
    {
        return Game1.player.Items.OfType<Tool>().FirstOrDefault(tool => tool.isHeavyHitter());
    }

    private static int ClearanceTickCost(Tool tool)
    {
        return tool switch
        {
            MeleeWeapon => 30,
            Axe => 60,
            Pickaxe => 60,
            _ => 60
        };
    }

    private static string ObstacleLabel(GameLocation location, Point tile)
    {
        var key = new Vector2(tile.X, tile.Y);
        if (location.objects.TryGetValue(key, out var obj))
        {
            return "object:" + obj.QualifiedItemId + ":" + obj.Name;
        }

        if (location.terrainFeatures.TryGetValue(key, out var feature))
        {
            return "terrain_feature:" + feature.GetType().Name;
        }

        var tileRect = TileRectangle(tile);
        if (location.largeTerrainFeatures.Any(feature => feature.getBoundingBox().Intersects(tileRect)))
        {
            return "large_terrain_feature";
        }

        if (location.resourceClumps.Any(clump => clump.getBoundingBox().Intersects(tileRect)))
        {
            return "resource_clump";
        }

        return "clear";
    }

    private static bool IsRemovableObstacle(GameLocation location, Point tile)
    {
        return CanClearRouteObstacles(location) && SelectClearanceTool(location, tile) is not null;
    }

    private static bool CanClearRouteObstacles(GameLocation location)
    {
        return location.IsFarm
            || location is MineShaft
            || location is VolcanoDungeon
            || string.Equals(location.NameOrUniqueName, "Farm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTileOccupiedByCharacter(GameLocation location, Point tile)
    {
        var tileRect = TileRectangle(tile);
        return location.characters.Any(character => character.GetBoundingBox().Intersects(tileRect));
    }

    private static XnaRectangle TileRectangle(Point tile)
    {
        return new XnaRectangle(tile.X * Game1.tileSize, tile.Y * Game1.tileSize, Game1.tileSize, Game1.tileSize);
    }

    private static bool IsTileTraversableForPlan(GameLocation location, Point tile, bool avoidSoftObstacles, bool allowRemovableObstacles = true)
    {
        if (!IsTileOnMap(location, tile))
        {
            return false;
        }

        if (avoidSoftObstacles && IsTileOccupiedByCharacter(location, tile))
        {
            return false;
        }

        return IsTileWalkable(location, tile) || allowRemovableObstacles && IsRemovableObstacle(location, tile) || IsTileOccupiedByCharacter(location, tile);
    }

    private static bool IsTileHardBlocked(GameLocation location, Point tile)
    {
        return !IsTileWalkable(location, tile) && !IsRemovableObstacle(location, tile) && !IsTileOccupiedByCharacter(location, tile);
    }

    private static string MovementHardBlockReason(GameLocation location, Point tile)
    {
        if (!IsTileOnMap(location, tile))
        {
            return "movement_target_tile_out_of_map";
        }

        if (IsTileOccupiedByCharacter(location, tile))
        {
            return "movement_target_soft_obstacle";
        }

        if (IsRemovableObstacle(location, tile))
        {
            return "movement_target_requires_clearance";
        }

        return "movement_target_tile_hard_blocked";
    }

}
