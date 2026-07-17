using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupCrabPotTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_crab_pot_target", "crab_pot_ready_for_harvest=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = target.ToVector2();
        var outputId = string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "(O)372" : request.QualifiedItemId;
        farm.objects.Remove(tile);
        var pot = new CrabPot();
        pot.owner.Value = Game1.player.UniqueMultiplayerID;
        pot.bait.Value = ItemRegistry.Create<StardewValley.Object>("(O)685");
        pot.heldObject.Value = ItemRegistry.Create<StardewValley.Object>(outputId, Math.Max(1, request.Quantity ?? 1));
        pot.readyForHarvest.Value = true;
        pot.tileIndexToShow = 714;
        farm.objects[tile] = pot;
        var moved = MoveFixtureFarmerToFarmAdjacent(target, out var stand, out var moveReason);
        var verified = moved && farm.objects.TryGetValue(tile, out var current) && ReferenceEquals(current, pot) &&
            pot.Location == farm && pot.readyForHarvest.Value && pot.heldObject.Value is not null;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_crab_pot_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_crab_pot_ready", "stand_tile=" + stand.X + "," + stand.Y }
                : new[] { "fixture_crab_pot_not_ready", moveReason },
            RequestedEffect = "current_location.objects[" + target.X + "," + target.Y + "].crab_pot_ready_for_harvest=true",
            ObservedEffect = CrabPotObservedEffect(farm, target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_crab_pot_not_ready:" + moveReason }
        };
    }

    private void StartCrabPotCollect(PendingExecution pending)
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
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            request.ExpectedSkillId != "fishing" || request.ExpectedSkillExperienceDelta != 5 ||
            !request.ExpectedFishCaughtCountBefore.HasValue || !request.ExpectedFishCaughtCountAfter.HasValue ||
            !request.ExpectedFishCaughtMaxSizeBefore.HasValue || !request.ExpectedCatchSizeMin.HasValue ||
            !request.ExpectedCatchSizeMax.HasValue || request.ExpectedFishCollectionEligible is not (0 or 1) ||
            !TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out var expectedItems) || expectedItems.Length != 1)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", "request=missing_typed_projection", "collect_crab_pot_typed_projection_required"));
            return;
        }
        if (activeCrabPotCollect is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", "player=busy_or_menu_open", "collect_crab_pot_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!location.objects.TryGetValue(target.ToVector2(), out var targetObject) ||
            targetObject is not CrabPot pot || targetObject.GetType() != typeof(CrabPot) ||
            !string.Equals(request.TargetRuntimeType, typeof(CrabPot).FullName, StringComparison.Ordinal) ||
            pot.tileIndexToShow != 714 || !pot.readyForHarvest.Value || pot.heldObject.Value is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_target_not_ready_or_drifted"));
            return;
        }
        if (!AreAdjacent(stand, target) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_stand_tile_invalid"));
            return;
        }

        var output = pot.heldObject.Value;
        var inventoryOutput = output.getOne();
        inventoryOutput.Stack = 1;
        inventoryOutput.HasBeenInInventory = true;
        var outputKey = ClearanceOutputItemKey.From(inventoryOutput);
        var expected = expectedItems[0];
        var baseStack = Math.Max(1, output.Stack);
        var doubleApplied = Utility.CreateDaySaveRandom(
                Game1.uniqueIDForThisGame,
                Game1.stats.DaysPlayed * 77,
                target.X * 777f + target.Y)
            .NextDouble() < 0.25 &&
            Game1.player.stats.Get("Book_Crabbing") != 0 &&
            Game1.player.couldInventoryAcceptThisItem(output.QualifiedItemId, baseStack * 2, output.Quality);
        var expectedQuantity = doubleApplied ? baseStack * 2 : baseStack;
        if (expected.Key != outputKey || expected.Quantity != expectedQuantity ||
            request.Quantity != expectedQuantity ||
            !string.Equals(request.QualifiedItemId, output.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            !Game1.player.couldInventoryAcceptThisItem(output.QualifiedItemId, expectedQuantity, output.Quality))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_output_projection_drifted"));
            return;
        }
        if (!string.Equals(request.ExpectedContainerBaitQualifiedItemId, pot.bait.Value?.QualifiedItemId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_bait_projection_drifted"));
            return;
        }

        var caught = Game1.player.fishCaught.TryGetValue(output.QualifiedItemId, out var currentCaught) ? currentCaught : null;
        var countBefore = caught?[0] ?? 0;
        var maxSizeBefore = caught?[1] ?? 0;
        if (countBefore != request.ExpectedFishCaughtCountBefore.Value || maxSizeBefore != request.ExpectedFishCaughtMaxSizeBefore.Value)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_fish_collection_projection_drifted"));
            return;
        }
        var caughtFishCallExpected = string.Equals(request.CatchSizeProjectionStatus, "runtime_rng_observed", StringComparison.Ordinal);
        if (caughtFishCallExpected && (request.ExpectedCatchSizeMin.Value <= 0 || request.ExpectedCatchSizeMax.Value < request.ExpectedCatchSizeMin.Value))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_catch_size_projection_invalid"));
            return;
        }

        if (!TryInventoryItemMultiset(out var inventoryBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", "inventory_multiset=unreadable", "collect_crab_pot_inventory_state_unreadable"));
            return;
        }
        var outputCountBefore = inventoryBefore.TryGetValue(outputKey, out var existing) ? existing : 0;
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_crab_pot", "crab_pot_ready_for_harvest=false", CrabPotObservedEffect(location, target), "collect_crab_pot_path_unavailable:" + pathReason));
            return;
        }

        activeCrabPotCollect = new ActiveCrabPotCollect(
            pending,
            location,
            pot,
            target,
            stand,
            path,
            outputKey,
            outputCountBefore,
            expectedQuantity,
            request.ExpectedSkillExperienceDelta.Value,
            request.ExpectedFishCaughtCountBefore.Value,
            request.ExpectedFishCaughtCountAfter.Value,
            request.ExpectedFishCaughtMaxSizeBefore.Value,
            request.ExpectedCatchSizeMin.Value,
            request.ExpectedCatchSizeMax.Value,
            caughtFishCallExpected,
            maxMovementTiles);
    }

    private void TickCrabPotCollect()
    {
        var active = activeCrabPotCollect;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteCrabPotCollectBlocked(active, "collect_crab_pot_location_changed");
            return;
        }
        if (active.ElapsedTicks > 3600)
        {
            CompleteCrabPotCollectBlocked(active, "collect_crab_pot_timeout");
            return;
        }
        if (!active.Location.objects.TryGetValue(active.Target.ToVector2(), out var current) ||
            !ReferenceEquals(current, active.Pot) || active.Pot.heldObject.Value is null ||
            active.Pot.tileIndexToShow != 714 || !active.Pot.readyForHarvest.Value)
        {
            CompleteCrabPotCollectBlocked(active, "collect_crab_pot_target_drifted_during_move");
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteCrabPotCollectBlocked(active, "collect_crab_pot_player_busy_during_execution");
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteCrabPotCollectBlocked(active, "collect_crab_pot_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteCrabPotCollectBlocked(active, "collect_crab_pot_path_exhausted_before_stand");
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
                CompleteCrabPotCollectBlocked(active, "collect_crab_pot_dynamic_path_blocked");
                return;
            }
            var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(playerTile, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
            }
            if (!moved && ++active.StuckTicks > 45)
            {
                CompleteCrabPotCollectBlocked(active, "collect_crab_pot_movement_stuck");
            }
            else if (moved)
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        CrabPotCaughtFishPatch.Reset();
        CrabPotCaughtFishPatch.Active = true;
        var handled = active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        CrabPotCaughtFishPatch.Active = false;
        CompleteCrabPotCollect(active, handled);
    }

    private void CompleteCrabPotCollect(ActiveCrabPotCollect active, bool handled)
    {
        StopAllMovement();
        activeCrabPotCollect = null;
        var request = active.Pending.Request;
        var inventoryReadable = TryInventoryItemMultiset(out var inventoryAfter);
        var outputCountAfter = inventoryReadable && inventoryAfter.TryGetValue(active.OutputKey, out var count) ? count : 0;
        var fishingExperienceAfter = Game1.player.experiencePoints[Farmer.fishingSkill];
        var caught = Game1.player.fishCaught.TryGetValue(active.OutputKey.QualifiedItemId, out var currentCaught) ? currentCaught : null;
        var caughtCountAfter = caught?[0] ?? 0;
        var caughtMaxAfter = caught?[1] ?? 0;
        var expectedMaxAfter = request.ExpectedFishCollectionEligible == 1 && CrabPotCaughtFishPatch.Called
            ? Math.Max(active.ExpectedFishCaughtMaxSizeBefore, CrabPotCaughtFishPatch.Size)
            : active.ExpectedFishCaughtMaxSizeBefore;
        var captureMatches = active.CaughtFishCallExpected
            ? CrabPotCaughtFishPatch.Called &&
              string.Equals(CrabPotCaughtFishPatch.ItemId, active.OutputKey.QualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
              CrabPotCaughtFishPatch.NumberCaught == active.ExpectedQuantity &&
              CrabPotCaughtFishPatch.Size >= active.ExpectedCatchSizeMin && CrabPotCaughtFishPatch.Size <= active.ExpectedCatchSizeMax
            : !CrabPotCaughtFishPatch.Called;
        var verified = handled && inventoryReadable &&
            outputCountAfter - active.OutputCountBefore == active.ExpectedQuantity &&
            fishingExperienceAfter - active.FishingExperienceBefore == active.ExpectedFishingExperience &&
            caughtCountAfter == active.ExpectedFishCaughtCountAfter && caughtMaxAfter == expectedMaxAfter &&
            captureMatches && active.Pot.heldObject.Value is null && !active.Pot.readyForHarvest.Value &&
            active.Pot.tileIndexToShow == 710 && active.Pot.bait.Value is null;
        var reason = verified ? string.Empty : "collect_crab_pot_post_state_mismatch";
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
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "collect_crab_pot",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_checkAction_collected_exact_crab_pot_output", "inventory_unit_state_multiset_matched", "bait_and_ready_state_reset", "fishing_experience_matched", "caughtFish_arguments_observed" }
                : new[] { reason },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = CrabPotObservedEffect(active.Location, active.Target) +
                ";output_quantity_delta=" + (outputCountAfter - active.OutputCountBefore) +
                ";fishing_experience_delta=" + (fishingExperienceAfter - active.FishingExperienceBefore) +
                ";fish_caught_count_after=" + caughtCountAfter +
                ";fish_caught_max_size_after=" + caughtMaxAfter +
                ";caught_fish_call=" + CrabPotCaughtFishPatch.Called.ToString().ToLowerInvariant() +
                ";caught_fish_size=" + CrabPotCaughtFishPatch.Size,
            BlockReasons = verified ? Array.Empty<string>() : new[] { reason },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.objects[" + active.Target.X + "," + active.Target.Y + "].crab_pot", Before = "ready=true;bait=" + request.ExpectedContainerBaitQualifiedItemId, After = "ready=false;bait=" },
                new SimulatedFactChange { Path = "player.inventory.item_multiset[" + active.OutputKey.QualifiedItemId + "," + active.OutputKey.UnitStateSha256 + "]", Before = active.OutputCountBefore.ToString(CultureInfo.InvariantCulture), After = outputCountAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.fishing.experience", Before = active.FishingExperienceBefore.ToString(CultureInfo.InvariantCulture), After = fishingExperienceAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.fish_caught[" + active.OutputKey.QualifiedItemId + "]", Before = active.ExpectedFishCaughtCountBefore + "," + active.ExpectedFishCaughtMaxSizeBefore, After = caughtCountAfter + "," + caughtMaxAfter }
            }
        });
        CrabPotCaughtFishPatch.Reset();
    }

    private void CompleteCrabPotCollectBlocked(ActiveCrabPotCollect active, string reason)
    {
        StopAllMovement();
        activeCrabPotCollect = null;
        CrabPotCaughtFishPatch.Reset();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "collect_crab_pot",
            active.RequestedEffect,
            CrabPotObservedEffect(active.Location, active.Target),
            reason));
    }

    private static bool TryInventoryItemMultiset(out Dictionary<ClearanceOutputItemKey, int> quantities)
    {
        quantities = new Dictionary<ClearanceOutputItemKey, int>();
        try
        {
            foreach (var item in Game1.player.Items.Where(item => item is not null))
            {
                var key = ClearanceOutputItemKey.From(item!);
                quantities[key] = (quantities.TryGetValue(key, out var existing) ? existing : 0) + item!.Stack;
            }
            return true;
        }
        catch
        {
            quantities.Clear();
            return false;
        }
    }

    private static string CrabPotObservedEffect(GameLocation location, Point target)
    {
        return location.objects.TryGetValue(target.ToVector2(), out var item) && item is CrabPot pot
            ? "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y +
              ";runtime_type=" + item.GetType().FullName + ";tile_index=" + pot.tileIndexToShow +
              ";ready=" + pot.readyForHarvest.Value.ToString().ToLowerInvariant() +
              ";held_item=" + (pot.heldObject.Value?.QualifiedItemId ?? string.Empty) +
              ";bait=" + (pot.bait.Value?.QualifiedItemId ?? string.Empty)
            : "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y + ";crab_pot=missing";
    }
}
