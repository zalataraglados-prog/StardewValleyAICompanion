using Microsoft.Xna.Framework;
using System.Globalization;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartSpawnedObjectPickup(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", "request=missing_typed_fields", "collect_spawned_object_typed_target_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", "player=busy_or_menu_open", "collect_spawned_object_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var targetVector = target.ToVector2();
        if (!location.objects.TryGetValue(targetVector, out var item) ||
            !item.IsSpawnedObject ||
            item.GetType() != typeof(StardewObject) ||
            !string.Equals(item.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_target_not_found_or_drifted"));
            return;
        }
        var questReceiptReason = ValidateQuestResourceReceiptTarget(request, item.QualifiedItemId);
        if (questReceiptReason is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), questReceiptReason));
            return;
        }
        if (!AreAdjacent(stand, target) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_stand_tile_invalid"));
            return;
        }
        if (ItemRegistry.GetDataOrErrorItem(item.QualifiedItemId).IsErrorItem ||
            string.IsNullOrWhiteSpace(item.Type) || item.Stack != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_identity_or_type_unsupported"));
            return;
        }
        if (item.questItem.Value && !string.IsNullOrWhiteSpace(item.questId.Value) && item.questId.Value != "0" && !Game1.player.hasQuest(item.questId.Value))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_required_quest_not_active"));
            return;
        }

        var random = Utility.CreateDaySaveRandom(target.X, target.Y * 777f);
        var expectedQuality = item.isForage()
            ? location.GetHarvestSpawnedObjectQuality(Game1.player, true, targetVector, random)
            : item.Quality;
        var primary = (StardewObject)item.getOne();
        primary.Quality = expectedQuality;
        primary.Stack = 1;
        if (!Game1.player.couldInventoryAcceptThisItem(primary))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_inventory_cannot_accept_item"));
            return;
        }

        var farmBuildingInterior = location.isFarmBuildingInterior();
        var gathererRoll = Game1.player.professions.Contains(13) && random.NextDouble() < 0.2;
        var twoItems = (StardewObject)primary.getOne();
        twoItems.Stack = 2;
        var gathererDuplicate = gathererRoll && !item.questItem.Value && !farmBuildingInterior && Game1.player.couldInventoryAcceptThisItem(twoItems);
        var expectedQuantity = gathererDuplicate ? 2 : 1;
        if ((request.Quantity ?? 1) != expectedQuantity)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_quantity_projection_drifted"));
            return;
        }
        var expectedForagingExperience = 0;
        var expectedFarmingExperience = 0;
        if (farmBuildingInterior)
        {
            expectedFarmingExperience = 5;
        }
        else if (item.isForage() && item.SpecialVariable == 724519)
        {
            expectedForagingExperience = 2;
            expectedFarmingExperience = 3;
        }
        else if (item.isForage())
        {
            expectedForagingExperience = 7;
        }
        if (gathererDuplicate)
        {
            expectedForagingExperience += 7;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_spawned_object", "current_location.objects[target].present=false", SpawnedObjectObservedEffect(location, target), "collect_spawned_object_path_unavailable:" + pathReason));
            return;
        }

        activeSpawnedObjectPickup = new ActiveSpawnedObjectPickup(
            pending,
            location,
            item,
            target,
            stand,
            path,
            item.QualifiedItemId,
            expectedQuantity,
            expectedQuality,
            expectedForagingExperience,
            expectedFarmingExperience,
            maxMovementTiles);
    }

    private void TickSpawnedObjectPickup()
    {
        if (activeSpawnedObjectPickup is null)
        {
            return;
        }

        var active = activeSpawnedObjectPickup;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_timeout");
            return;
        }

        var present = active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) &&
            ReferenceEquals(current, active.TargetObject);
        var itemCountAfter = CountInventoryItem(active.QualifiedItemId);
        if (!present)
        {
            if (itemCountAfter > active.ItemCountBefore)
            {
                CompleteSpawnedObjectPickup(active, itemCountAfter);
            }
            else
            {
                CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_removed_without_inventory_gain");
            }
            return;
        }
        if (active.ActionIssued)
        {
            return;
        }
        if (active.Location is MineShaft mine && ImmediateMiningThreat(mine))
        {
            StopAllMovement();
            active.CombatInterrupted = true;
            active.CombatInterruptedTicks++;
            return;
        }
        active.CombatInterrupted = false;
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_player_busy_during_execution");
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_path_exhausted_before_stand");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (playerTile == next)
            {
                active.PathIndex++;
                active.StuckTicks = 0;
                return;
            }
            if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
            {
                CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_dynamic_path_blocked");
                return;
            }

            var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(playerTile, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!movedSinceLastTick && ++active.StuckTicks > 45)
            {
                CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_movement_stuck");
            }
            else if (movedSinceLastTick)
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        var handled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        if (!handled)
        {
            CompleteSpawnedObjectPickupBlocked(active, "collect_spawned_object_native_action_not_handled");
        }
    }

    private void CompleteSpawnedObjectPickup(ActiveSpawnedObjectPickup active, int itemCountAfter)
    {
        StopAllMovement();
        activeSpawnedObjectPickup = null;
        var request = active.Pending.Request;
        var quantityDelta = itemCountAfter - active.ItemCountBefore;
        var qualityCountAfter = CountInventoryItemAtQuality(active.QualifiedItemId, active.ExpectedQuality);
        var qualityDelta = qualityCountAfter - active.QualityItemCountBefore;
        var foragingExperienceAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var farmingExperienceAfter = Game1.player.experiencePoints[Farmer.farmingSkill];
        var foragingExperienceDelta = foragingExperienceAfter - active.ForagingExperienceBefore;
        var farmingExperienceDelta = farmingExperienceAfter - active.FarmingExperienceBefore;
        var verified = quantityDelta == active.ExpectedQuantity &&
            qualityDelta == active.ExpectedQuantity &&
            foragingExperienceDelta == active.ExpectedForagingExperience &&
            farmingExperienceDelta == active.ExpectedFarmingExperience;
        var reasons = verified
            ? new[] { "native_checkAction_removed_exact_spawned_object", "inventory_quantity_and_quality_match_projection", "skill_deltas_observed" }
            : new[] { "collect_spawned_object_projected_output_mismatch" };
        var result = new TrainingExecutionResult
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
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "collect_spawned_object",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = SpawnedObjectObservedEffect(active.Location, active.Target) +
                ";quantity_delta=" + quantityDelta +
                ";expected_quantity=" + active.ExpectedQuantity +
                ";quality=" + active.ExpectedQuality +
                ";quality_quantity_delta=" + qualityDelta +
                ";foraging_experience_delta=" + foragingExperienceDelta +
                ";expected_foraging_experience=" + active.ExpectedForagingExperience +
                ";farming_experience_delta=" + farmingExperienceDelta +
                ";expected_farming_experience=" + active.ExpectedFarmingExperience,
            BlockReasons = verified ? Array.Empty<string>() : reasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.objects[" + active.Target.X + "," + active.Target.Y + "]", Before = active.QualifiedItemId, After = "removed" },
                new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = InventoryStackSignature() },
                new SimulatedFactChange { Path = "player.inventory.count[" + active.QualifiedItemId + "]", Before = active.ItemCountBefore.ToString(), After = itemCountAfter.ToString() },
                new SimulatedFactChange { Path = "player.skills.foraging.experience", Before = active.ForagingExperienceBefore.ToString(CultureInfo.InvariantCulture), After = foragingExperienceAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.farming.experience", Before = active.FarmingExperienceBefore.ToString(CultureInfo.InvariantCulture), After = farmingExperienceAfter.ToString(CultureInfo.InvariantCulture) }
            }
        };
        ApplyQuestResourceReceiptFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompleteSpawnedObjectPickupBlocked(ActiveSpawnedObjectPickup active, string reason)
    {
        StopAllMovement();
        activeSpawnedObjectPickup = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "collect_spawned_object",
            active.RequestedEffect,
            SpawnedObjectObservedEffect(active.Location, active.Target) + ";item_count=" + CountInventoryItem(active.QualifiedItemId),
            reason));
    }

    private static int CountInventoryItemAtQuality(string qualifiedItemId, int quality)
    {
        return Game1.player.Items
            .Where(item => item is not null &&
                string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                item.Quality == quality)
            .Sum(item => item!.Stack);
    }

    private static string SpawnedObjectObservedEffect(GameLocation location, Point target)
    {
        return location.objects.TryGetValue(target.ToVector2(), out var item)
            ? "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y + ";object=" + item.QualifiedItemId + ";spawned=" + item.IsSpawnedObject.ToString().ToLowerInvariant()
            : "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y + ";object=removed_or_missing";
    }
}
