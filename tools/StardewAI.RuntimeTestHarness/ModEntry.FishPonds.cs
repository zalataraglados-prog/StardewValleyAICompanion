using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Tools;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFishPondService(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return FishPondBlocked(request, request.OptionId.EndsWith("output", StringComparison.Ordinal) ? "output" : "request", "fish_pond_fixture_top_left_required");
        }

        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var requestedTopLeft = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var selectedTopLeft = FindFishPondFixturePlacement(farm, requestedTopLeft);
        if (!selectedTopLeft.HasValue)
        {
            return FishPondBlocked(request, request.OptionId.EndsWith("output", StringComparison.Ordinal) ? "output" : "request", "fish_pond_fixture_no_legal_placement");
        }
        var topLeft = selectedTopLeft.Value.ToVector2();
        var pond = new FishPond(topLeft);
        if (!farm.buildStructure(pond, topLeft, Game1.player, skipSafetyChecks: false))
        {
            return FishPondBlocked(request, request.OptionId.EndsWith("output", StringComparison.Ordinal) ? "output" : "request", "fish_pond_fixture_placement_rejected");
        }

        pond.daysOfConstructionLeft.Value = 0;
        var fishQualifiedId = string.IsNullOrWhiteSpace(request.FishTypeItemId) ? "(O)698" : request.FishTypeItemId;
        var fish = ItemRegistry.Create(fishQualifiedId);
        pond.fishType.Value = fish.ItemId;
        pond.UpdateMaximumOccupancy();
        pond.currentOccupants.Value = Math.Max(1, pond.maxOccupants.Value);
        var mode = request.OptionId.EndsWith("output", StringComparison.Ordinal) ? "output" : "request";
        var fixtureReason = string.Empty;
        if (mode == "output")
        {
            var outputId = string.IsNullOrWhiteSpace(request.QualifiedItemId) ? "(O)812" : request.QualifiedItemId;
            pond.output.Value = ItemRegistry.Create(outputId, Math.Max(1, request.Quantity ?? 1));
        }
        else
        {
            var data = pond.GetFishPondData();
            if (data is null)
            {
                fixtureReason = "fish_pond_fixture_data_unavailable";
            }
            else
            {
                pond.daysSinceSpawn.Value = Math.Max(0, data.SpawnTime);
                pond.dayUpdate(Game1.dayOfMonth);
                pond.output.Value = null;
                var needed = pond.neededItem.Value;
                if (!pond.HasUnresolvedNeeds() || needed is null || pond.neededItemCount.Value <= 0)
                {
                    fixtureReason = "fish_pond_fixture_request_not_generated";
                }
                else
                {
                    if (pond.IsValidSignItem(needed))
                    {
                        pond.sign.Value = needed.getOne() as StardewValley.Object;
                    }
                    if (needed.QualifiedItemId == "(O)GoldenAnimalCracker")
                    {
                        pond.goldenAnimalCracker.Value = true;
                    }
                    var slot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
                        .FirstOrDefault(index => Game1.player.Items[index] is null);
                    if (slot < 0 || slot >= Math.Min(12, Game1.player.Items.Count) || Game1.player.Items[slot] is not null)
                    {
                        slot = Math.Min(11, Game1.player.Items.Count - 1);
                    }
                    Game1.player.Items[slot] = ItemRegistry.Create(needed.QualifiedItemId, pond.neededItemCount.Value);
                }
            }
        }

        var interactionTarget = new Point(pond.tileX.Value, pond.tileY.Value);
        var moved = MoveFixtureFarmerToFarmAdjacent(interactionTarget, out var stand, out var moveReason);
        var verified = string.IsNullOrWhiteSpace(fixtureReason) && moved && farm.buildings.Contains(pond) &&
            (mode == "output" ? pond.output.Value is not null : pond.HasUnresolvedNeeds() && pond.output.Value is null);
        var started = DateTimeOffset.UtcNow.ToString("O");
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
            PrimitiveKind = "debug_setup_fish_pond_" + mode,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_fish_pond_" + mode + "_fixture_ready", "stand_tile=" + stand.X + "," + stand.Y }
                : new[] { string.IsNullOrWhiteSpace(fixtureReason) ? moveReason : fixtureReason },
            RequestedEffect = "farm.fish_pond." + mode + "_ready=true",
            ObservedEffect = FishPondObservedEffect(pond),
            BlockReasons = verified ? Array.Empty<string>() : new[] { string.IsNullOrWhiteSpace(fixtureReason) ? moveReason : fixtureReason },
            ChangedFacts = verified
                ? new[] { new SimulatedFactChange { Path = "farm.buildings[" + pond.tileX.Value + "," + pond.tileY.Value + "].fish_pond", Before = string.Empty, After = mode + "_ready" } }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private void StartFishPondService(PendingExecution pending)
    {
        var request = pending.Request;
        var mode = request.OptionId == "executor.collect_fish_pond_output" ? "output" : "request";
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.BuildingTileX.HasValue || !request.BuildingTileY.HasValue ||
            !request.ExpectedFishCount.HasValue || !request.ExpectedMaximumOccupantsBefore.HasValue ||
            !request.ExpectedLastUnlockedPopulationGateBefore.HasValue || !request.ExpectedDaysSinceSpawnBefore.HasValue ||
            !request.ExpectedSkillExperienceDelta.HasValue || request.ExpectedSkillId != "fishing" ||
            string.IsNullOrWhiteSpace(request.FishTypeItemId) || string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_typed_projection_required"));
            return;
        }
        if (activeFishPondService is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp ||
            Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_player_busy"));
            return;
        }

        var farm = Game1.getFarm();
        if (!ReferenceEquals(Game1.currentLocation, farm) || !ReferenceEquals(Game1.player.currentLocation, farm))
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_player_not_on_farm"));
            return;
        }
        var pond = farm.buildings.OfType<FishPond>().FirstOrDefault(candidate =>
            candidate.tileX.Value == request.BuildingTileX.Value && candidate.tileY.Value == request.BuildingTileY.Value);
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (pond is null || pond.GetType() != typeof(FishPond) ||
            !string.Equals(request.TargetRuntimeType, typeof(FishPond).FullName, StringComparison.Ordinal) ||
            !pond.occupiesTile(target.ToVector2()) || !AreAdjacent(stand, target) ||
            !IsTileOnMap(farm, stand) || !IsTileWalkable(farm, stand) || IsTileOccupiedByCharacter(farm, stand))
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_target_or_geometry_drifted"));
            return;
        }
        if (!FishPondCommonStateMatches(pond, request))
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_common_state_drifted"));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(farm, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, "fish_pond_path_unavailable:" + pathReason));
            return;
        }

        var active = new ActiveFishPondService(
            pending, farm, pond, target, stand, path, mode, Game1.player.CurrentToolIndex, maxMovementTiles);
        if (mode == "output")
        {
            if (!PrepareFishPondOutput(active, request, out var outputReason))
            {
                pending.Completion.SetResult(FishPondBlocked(request, mode, outputReason));
                return;
            }
        }
        else if (!PrepareFishPondRequest(active, request, out var requestReason))
        {
            pending.Completion.SetResult(FishPondBlocked(request, mode, requestReason));
            return;
        }
        activeFishPondService = active;
    }

    private static bool FishPondCommonStateMatches(FishPond pond, TrainingExecutionRequest request)
    {
        return pond.daysOfConstructionLeft.Value <= 0 && !pond.isUnderConstruction() &&
            string.Equals(pond.fishType.Value, request.FishTypeItemId, StringComparison.Ordinal) &&
            pond.FishCount == request.ExpectedFishCount &&
            pond.maxOccupants.Value == request.ExpectedMaximumOccupantsBefore &&
            pond.lastUnlockedPopulationGate.Value == request.ExpectedLastUnlockedPopulationGateBefore &&
            pond.daysSinceSpawn.Value == request.ExpectedDaysSinceSpawnBefore;
    }

    private bool PrepareFishPondOutput(ActiveFishPondService active, TrainingExecutionRequest request, out string reason)
    {
        reason = string.Empty;
        var output = active.Pond.output.Value;
        if (output is null || !request.SafeSlotIndex.HasValue || request.SafeSlotIndex.Value is < 0 or > 11 ||
            request.NativeReceiptCallbacksStatus != "runtime_observed" ||
            !TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out var expectedItems) || expectedItems.Length != 1)
        {
            reason = "fish_pond_output_typed_projection_required";
            return false;
        }
        var safeItem = request.SafeSlotIndex.Value < Game1.player.Items.Count ? Game1.player.Items[request.SafeSlotIndex.Value] : null;
        if (safeItem is not null and not Tool)
        {
            reason = "fish_pond_output_safe_slot_drifted";
            return false;
        }
        var inventoryUnit = output.getOne();
        inventoryUnit.Stack = 1;
        inventoryUnit.HasBeenInInventory = true;
        var outputKey = ClearanceOutputItemKey.From(inventoryUnit);
        var expected = expectedItems[0];
        var expectedExperience = FishPond.HARVEST_BASE_EXP +
            (output is StardewValley.Object obj
                ? (int)(obj.sellToStorePrice(-1L) * FishPond.HARVEST_OUTPUT_EXP_MULTIPLIER)
                : 0);
        if (expected.Key != outputKey || expected.Quantity != output.Stack || request.Quantity != output.Stack ||
            !string.Equals(output.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            request.ExpectedSkillExperienceDelta != expectedExperience ||
            !Game1.player.couldInventoryAcceptThisItem(output) || !TryInventoryItemMultiset(out var inventoryBefore))
        {
            reason = "fish_pond_output_projection_drifted";
            return false;
        }
        active.OutputKey = outputKey;
        active.OutputCountBefore = inventoryBefore.TryGetValue(outputKey, out var count) ? count : 0;
        return true;
    }

    private static bool PrepareFishPondRequest(ActiveFishPondService active, TrainingExecutionRequest request, out string reason)
    {
        reason = string.Empty;
        var pond = active.Pond;
        var needed = pond.neededItem.Value;
        if (pond.output.Value is not null)
        {
            reason = "fish_pond_output_precedes_request";
            return false;
        }
        var pondData = FishPond.GetRawData(pond.fishType.Value);
        if (needed is null || !IsFishPondRequestUnresolved(pond, pondData) || pond.neededItemCount.Value != request.Quantity ||
            !string.Equals(needed.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(needed.GetType().FullName, request.RequestItemRuntimeType, StringComparison.Ordinal) ||
            pond.IsValidSignItem(needed) && !string.Equals(pond.sign.Value?.QualifiedItemId, needed.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            needed.QualifiedItemId == "(O)GoldenAnimalCracker" && !pond.goldenAnimalCracker.Value && pond.FishCount > 0)
        {
            reason = "fish_pond_request_projection_drifted_or_intercepted";
            return false;
        }
        if (!TryParseFishPondRequestSlots(request.RequestItemToolbarSlotsJson, out var slots) || slots.Length == 0)
        {
            reason = "fish_pond_request_toolbar_binding_invalid";
            return false;
        }
        var boundCount = 0;
        foreach (var slot in slots)
        {
            if (slot is < 0 or > 11 || slot >= Game1.player.Items.Count ||
                Game1.player.Items[slot] is not Item item ||
                !string.Equals(item.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
            {
                reason = "fish_pond_request_toolbar_binding_drifted";
                return false;
            }
            boundCount += item.Stack;
        }
        if (boundCount < request.Quantity || !FishPondRequestExpectedStateMatches(pond, request))
        {
            reason = "fish_pond_request_expected_state_drifted";
            return false;
        }
        active.RequestSlots = slots;
        active.RequestItemCountBefore = CountInventoryQualifiedItem(request.QualifiedItemId);
        return true;
    }

    private static bool FishPondRequestExpectedStateMatches(FishPond pond, TrainingExecutionRequest request)
    {
        var data = FishPond.GetRawData(pond.fishType.Value);
        var spawnTime = ResolveRuntimeFishPondSpawnTime(data, pond.fishType.Value);
        var expectedExperience = !spawnTime.HasValue
            ? 0
            : FishPond.QUEST_BASE_EXP + (int)(spawnTime.Value * FishPond.QUEST_SPAWNRATE_EXP_MULTIPIER);
        var expectedGate = pond.maxOccupants.Value + 1;
        var expectedMaximum = data is null
            ? pond.maxOccupants.Value
            : ProjectRuntimeFishPondMaximum(data, expectedGate, pond.maxOccupants.Value);
        return data is not null && request.ExpectedSkillExperienceDelta == expectedExperience &&
            request.ExpectedMaximumOccupantsAfter == expectedMaximum &&
            request.ExpectedLastUnlockedPopulationGateAfter == expectedGate &&
            request.ExpectedDaysSinceSpawnAfter == 0 && request.ExpectedNeededItemCountAfter == -1 &&
            request.ExpectedHasCompletedRequestAfter == 1;
    }

    private static bool IsFishPondRequestUnresolved(FishPond pond, StardewValley.GameData.FishPonds.FishPondData? data)
    {
        return pond.neededItem.Value is not null && !pond.hasCompletedRequest.Value &&
            pond.currentOccupants.Value >= pond.maxOccupants.Value &&
            pond.maxOccupants.Value + 1 > pond.lastUnlockedPopulationGate.Value &&
            (data?.PopulationGates?.ContainsKey(pond.maxOccupants.Value + 1) ?? false);
    }

    private static int? ResolveRuntimeFishPondSpawnTime(StardewValley.GameData.FishPonds.FishPondData? data, string? fishItemId)
    {
        if (data is null || string.IsNullOrWhiteSpace(fishItemId))
        {
            return null;
        }
        if (data.SpawnTime >= 0)
        {
            return data.SpawnTime;
        }
        if (!Game1.objectData.TryGetValue(fishItemId, out var objectData))
        {
            return null;
        }
        return objectData.Price <= 30 ? 1
            : objectData.Price <= 80 ? 2
            : objectData.Price <= 120 ? 3
            : objectData.Price <= 250 ? 4
            : 5;
    }

    private static int ProjectRuntimeFishPondMaximum(
        StardewValley.GameData.FishPonds.FishPondData data,
        int lastUnlockedGate,
        int currentMaximum)
    {
        if (data.MaxPopulation > 0)
        {
            return data.MaxPopulation;
        }
        var maximum = currentMaximum;
        for (var population = 1; population <= FishPond.MAXIMUM_OCCUPANCY; population++)
        {
            if (population <= lastUnlockedGate || !(data.PopulationGates?.ContainsKey(population) ?? false))
            {
                maximum = population;
                continue;
            }
            break;
        }
        return maximum;
    }

    private void TickFishPondService()
    {
        var active = activeFishPondService;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location) ||
            !active.Location.buildings.Contains(active.Pond))
        {
            CompleteFishPondBlocked(active, "fish_pond_location_or_target_changed");
            return;
        }
        if (active.ElapsedTicks > 3600)
        {
            CompleteFishPondBlocked(active, "fish_pond_execution_timeout");
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool)
        {
            CompleteFishPondBlocked(active, "fish_pond_player_busy_during_execution");
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteFishPondBlocked(active, "fish_pond_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            TickFishPondMovement(active, playerTile);
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Target));
        if (active.Mode == "output")
        {
            Game1.player.CurrentToolIndex = active.Pending.Request.SafeSlotIndex!.Value;
            var handled = CheckFishPondAction(active);
            CompleteFishPondOutput(active, handled);
            return;
        }
        TickFishPondRequestInteractions(active);
    }

    private void TickFishPondMovement(ActiveFishPondService active, Point playerTile)
    {
        if (active.PathIndex >= active.Path.Count)
        {
            CompleteFishPondBlocked(active, "fish_pond_path_exhausted_before_stand");
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
            CompleteFishPondBlocked(active, "fish_pond_dynamic_path_blocked");
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
            CompleteFishPondBlocked(active, "fish_pond_movement_stuck");
        }
        else if (moved)
        {
            active.StuckTicks = 0;
        }
    }

    private void TickFishPondRequestInteractions(ActiveFishPondService active)
    {
        var request = active.Pending.Request;
        if (active.FinalInteractionIssued)
        {
            if (active.Pond.hasCompletedRequest.Value)
            {
                CompleteFishPondRequest(active);
            }
            return;
        }
        if (active.ElapsedTicks < active.NextInteractionTick)
        {
            return;
        }
        var slot = active.RequestSlots.FirstOrDefault(index =>
            index < Game1.player.Items.Count && Game1.player.Items[index] is Item item &&
            string.Equals(item.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase));
        if (slot < 0 || slot >= Game1.player.Items.Count || Game1.player.Items[slot] is not Item)
        {
            CompleteFishPondBlocked(active, "fish_pond_request_bound_items_exhausted");
            return;
        }
        Game1.player.CurrentToolIndex = slot;
        var before = active.Pond.neededItemCount.Value;
        var handled = CheckFishPondAction(active);
        var after = active.Pond.neededItemCount.Value;
        if (!handled || after >= before)
        {
            CompleteFishPondBlocked(active, "fish_pond_request_native_interaction_not_consumed");
            return;
        }
        active.DeliveredCount++;
        active.NextInteractionTick = active.ElapsedTicks + 12;
        active.FinalInteractionIssued = after <= 0;
    }

    private static bool CheckFishPondAction(ActiveFishPondService active)
    {
        return active.Location.checkAction(
            new TileLocation(active.Target.X, active.Target.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
    }

    private void CompleteFishPondOutput(ActiveFishPondService active, bool handled)
    {
        var request = active.Pending.Request;
        var outputKey = active.OutputKey!.Value;
        var inventoryReadable = TryInventoryItemMultiset(out var inventoryAfter);
        var outputCountAfter = inventoryReadable && inventoryAfter.TryGetValue(outputKey, out var count) ? count : 0;
        var fishingAfter = Game1.player.experiencePoints[Farmer.fishingSkill];
        var verified = handled && inventoryReadable && active.Pond.output.Value is null &&
            outputCountAfter - active.OutputCountBefore == request.Quantity &&
            fishingAfter - active.FishingExperienceBefore == request.ExpectedSkillExperienceDelta;
        FinishFishPond(active, verified, "collect_fish_pond_output", verified
            ? "native_fish_pond_output_collected_and_verified"
            : "fish_pond_output_post_state_mismatch",
            new[]
            {
                new SimulatedFactChange { Path = FishPondPath(active) + ".output", Before = request.QualifiedItemId + "x" + request.Quantity, After = string.Empty },
                new SimulatedFactChange { Path = "player.inventory.item_multiset[" + outputKey.QualifiedItemId + "," + outputKey.UnitStateSha256 + "]", Before = active.OutputCountBefore.ToString(CultureInfo.InvariantCulture), After = outputCountAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.fishing.experience", Before = active.FishingExperienceBefore.ToString(CultureInfo.InvariantCulture), After = fishingAfter.ToString(CultureInfo.InvariantCulture) }
            });
    }

    private void CompleteFishPondRequest(ActiveFishPondService active)
    {
        var request = active.Pending.Request;
        var fishingAfter = Game1.player.experiencePoints[Farmer.fishingSkill];
        var itemCountAfter = CountInventoryQualifiedItem(request.QualifiedItemId);
        var verified = active.DeliveredCount == request.Quantity &&
            active.RequestItemCountBefore - itemCountAfter == request.Quantity &&
            active.Pond.hasCompletedRequest.Value == (request.ExpectedHasCompletedRequestAfter == 1) &&
            active.Pond.neededItemCount.Value == request.ExpectedNeededItemCountAfter &&
            active.Pond.maxOccupants.Value == request.ExpectedMaximumOccupantsAfter &&
            active.Pond.lastUnlockedPopulationGate.Value == request.ExpectedLastUnlockedPopulationGateAfter &&
            active.Pond.daysSinceSpawn.Value == request.ExpectedDaysSinceSpawnAfter &&
            fishingAfter - active.FishingExperienceBefore == request.ExpectedSkillExperienceDelta;
        FinishFishPond(active, verified, "complete_fish_pond_request", verified
            ? "native_fish_pond_request_completed_and_verified"
            : "fish_pond_request_post_state_mismatch",
            new[]
            {
                new SimulatedFactChange { Path = FishPondPath(active) + ".needed_item_count", Before = request.Quantity?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, After = active.Pond.neededItemCount.Value.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = FishPondPath(active) + ".maximum_occupants", Before = request.ExpectedMaximumOccupantsBefore?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, After = active.Pond.maxOccupants.Value.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.inventory.count[" + request.QualifiedItemId + "]", Before = active.RequestItemCountBefore.ToString(CultureInfo.InvariantCulture), After = itemCountAfter.ToString(CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.fishing.experience", Before = active.FishingExperienceBefore.ToString(CultureInfo.InvariantCulture), After = fishingAfter.ToString(CultureInfo.InvariantCulture) }
            });
    }

    private void FinishFishPond(ActiveFishPondService active, bool verified, string primitive, string reason, SimulatedFactChange[] changes)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        activeFishPondService = null;
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
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = primitive,
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = new[] { reason },
            RequestedEffect = primitive,
            ObservedEffect = FishPondObservedEffect(active.Pond),
            BlockReasons = verified ? Array.Empty<string>() : new[] { reason },
            ChangedFacts = changes
        });
    }

    private void CompleteFishPondBlocked(ActiveFishPondService active, string reason)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        activeFishPondService = null;
        active.Pending.Completion.SetResult(FishPondBlocked(active.Pending.Request, active.Mode, reason, active.Pond));
    }

    private static TrainingExecutionResult FishPondBlocked(TrainingExecutionRequest request, string mode, string reason, FishPond? pond = null)
    {
        return BlockedWithPrimitive(
            request,
            mode == "output" ? "collect_fish_pond_output" : "complete_fish_pond_request",
            mode == "output" ? "fish_pond.output=null" : "fish_pond.has_completed_request=true",
            pond is null ? "fish_pond=unavailable" : FishPondObservedEffect(pond),
            reason);
    }

    private static string FishPondObservedEffect(FishPond pond)
    {
        return "location=" + pond.GetParentLocation().NameOrUniqueName +
            ";building_tile=" + pond.tileX.Value + "," + pond.tileY.Value +
            ";fish_type=" + (pond.fishType.Value ?? string.Empty) +
            ";fish_count=" + pond.FishCount +
            ";maximum_occupants=" + pond.maxOccupants.Value +
            ";output=" + (pond.output.Value?.QualifiedItemId ?? string.Empty) +
            ";needed_item=" + (pond.neededItem.Value?.QualifiedItemId ?? string.Empty) +
            ";needed_count=" + pond.neededItemCount.Value +
            ";completed_request=" + pond.hasCompletedRequest.Value.ToString().ToLowerInvariant();
    }

    private static string FishPondPath(ActiveFishPondService active)
    {
        return "farm.buildings[" + active.Pond.tileX.Value + "," + active.Pond.tileY.Value + "].fish_pond";
    }

    private static int CountInventoryQualifiedItem(string qualifiedItemId)
    {
        return Game1.player.Items
            .Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item!.Stack);
    }

    private static bool TryParseFishPondRequestSlots(string json, out int[] slots)
    {
        slots = Array.Empty<int>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            var parsed = new List<int>();
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("slot_index", out var value) ||
                    value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var slot) || parsed.Contains(slot))
                {
                    return false;
                }
                parsed.Add(slot);
            }
            slots = parsed.ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
