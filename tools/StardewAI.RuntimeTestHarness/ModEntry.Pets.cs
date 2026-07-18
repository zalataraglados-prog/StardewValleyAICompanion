using System.Reflection;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartPetInteraction(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!Guid.TryParse(request.TargetRuntimeIdentity, out var petId) ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.SafeSlotIndex.HasValue || !request.ExpectedFriendshipBefore.HasValue || !request.ExpectedFriendshipAfter.HasValue ||
            !request.ExpectedLastPetDayAfter.HasValue || !request.ExpectedTimesPetBefore.HasValue || !request.ExpectedTimesPetAfter.HasValue ||
            !request.ExpectedGrantedFriendshipBefore.HasValue || !request.ExpectedGrantedFriendshipAfter.HasValue ||
            !request.ExpectedPetLoveMailBefore.HasValue || !request.ExpectedPetLoveMailAfter.HasValue ||
            !request.ExpectedMarniePetAdoptionMailBeforeOrPending.HasValue || !request.ExpectedMarniePetAdoptionMailAfterOrPending.HasValue ||
            !request.PetGiftTriggerExpected.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", "request=missing_typed_projection", "pet_interaction_typed_projection_required"));
            return;
        }
        if (!request.ExpectedLastPetDayBeforeMissing && !request.ExpectedLastPetDayBefore.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", "request=last_pet_day_before_missing", "pet_interaction_typed_projection_required"));
            return;
        }
        if (activePetInteraction is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", "player=busy_or_menu_open", "pet_interaction_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var pet = Utility.findPet(petId);
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var checkAction = pet?.GetType().GetMethod(
            nameof(Pet.checkAction),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            new[] { typeof(Farmer), typeof(GameLocation) },
            modifiers: null);
        if (pet is null || !ReferenceEquals(pet.currentLocation, location) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            pet.TilePoint != target || pet.GetType().FullName != request.TargetRuntimeType || !IsSupportedVanillaPetRuntimeType(pet.GetType()) || checkAction?.DeclaringType != typeof(Pet))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(pet), "pet_interaction_target_not_ready_or_drifted"));
            return;
        }
        if (request.SafeSlotIndex.Value is < 0 or > 11 || request.SafeSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.SafeSlotIndex.Value] is not (null or Tool))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(pet), "pet_interaction_safe_slot_drifted"));
            return;
        }

        var hasLastPetDay = pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastPetDay);
        var data = pet.GetPetData();
        var giftTrigger = data is not null && Utility.CreateDaySaveRandom(pet.timesPet.Value, 71928.0, pet.petId.Value.GetHashCode()).NextDouble() < data.GiftChance;
        if (data is null || pet.friendshipTowardFarmer.Value != request.ExpectedFriendshipBefore.Value ||
            Math.Min(Pet.maxFriendship, pet.friendshipTowardFarmer.Value + 12) != request.ExpectedFriendshipAfter.Value ||
            hasLastPetDay != !request.ExpectedLastPetDayBeforeMissing ||
            (hasLastPetDay && lastPetDay != request.ExpectedLastPetDayBefore!.Value) ||
            hasLastPetDay && lastPetDay == Game1.Date.TotalDays ||
            pet.timesPet.Value != request.ExpectedTimesPetBefore.Value || request.ExpectedTimesPetAfter.Value != request.ExpectedTimesPetBefore.Value + 1 ||
            pet.grantedFriendshipForPet.Value != request.ExpectedGrantedFriendshipBefore.Value || pet.grantedFriendshipForPet.Value ||
            !request.ExpectedGrantedFriendshipAfter.Value ||
            request.ExpectedLastPetDayAfter.Value != Game1.Date.TotalDays ||
            Game1.player.mailReceived.Contains("petLoveMessage") != request.ExpectedPetLoveMailBefore.Value ||
            request.ExpectedPetLoveMailAfter.Value != (request.ExpectedPetLoveMailBefore.Value || request.ExpectedFriendshipAfter.Value >= Pet.maxFriendship) ||
            Game1.player.hasOrWillReceiveMail("MarniePetAdoption") != request.ExpectedMarniePetAdoptionMailBeforeOrPending.Value ||
            request.ExpectedMarniePetAdoptionMailAfterOrPending.Value !=
                (request.ExpectedMarniePetAdoptionMailBeforeOrPending.Value || request.ExpectedFriendshipAfter.Value >= Pet.maxFriendship) ||
            request.PetGiftTriggerExpected.Value != giftTrigger ||
            request.PetGiftSelectionStatus != (giftTrigger ? "runtime_observed_global_rng_selection" : "not_triggered"))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(pet), "pet_interaction_projection_drifted"));
            return;
        }
        if (!AreAdjacent(stand, target) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(pet), "pet_interaction_stand_tile_invalid"));
            return;
        }
        var maxMovement = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovement, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(pet), "pet_interaction_path_unavailable:" + pathReason));
            return;
        }
        activePetInteraction = new ActivePetInteraction(
            pending, location, pet, target, stand, path, maxMovement, request.SafeSlotIndex.Value,
            pet.friendshipTowardFarmer.Value, !hasLastPetDay, hasLastPetDay ? lastPetDay : null,
            pet.timesPet.Value, pet.grantedFriendshipForPet.Value,
            Game1.player.mailReceived.Contains("petLoveMessage"),
            Game1.player.hasOrWillReceiveMail("MarniePetAdoption"), location.debris.Count);
    }

    private void TickPetInteraction()
    {
        var active = activePetInteraction;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location) || active.ElapsedTicks > 3600 ||
            !ReferenceEquals(Utility.findPet(active.Pet.petId.Value), active.Pet) || !ReferenceEquals(active.Pet.currentLocation, active.Location))
        {
            CompletePetInteractionBlocked(active, "pet_interaction_world_location_target_or_timeout");
            return;
        }

        if (!active.InteractionIssued && active.Pet.TilePoint != active.Target)
        {
            active.ReplanCount++;
            if (!TryReplanPetPath(active, out var reason))
            {
                CompletePetInteractionBlocked(active, "pet_interaction_moving_target_replan_failed:" + reason);
            }
            return;
        }
        if (!active.InteractionIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompletePetInteractionBlocked(active, "pet_interaction_path_exhausted");
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
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
                active.LastObservedTile = playerTile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompletePetInteractionBlocked(active, "pet_interaction_movement_budget_exceeded");
                    return;
                }
            }
            else if (++active.StuckTicks > 60)
            {
                active.ReplanCount++;
                active.StuckTicks = 0;
                if (!TryReplanPetPath(active, out var reason))
                {
                    CompletePetInteractionBlocked(active, "pet_interaction_dynamic_blocker_replan_failed:" + reason);
                }
                return;
            }
            if (playerTile == next)
            {
                active.PathIndex++;
            }
            return;
        }

        StopAllMovement();
        if (!active.InteractionIssued)
        {
            Game1.player.CurrentToolIndex = active.SafeSlotIndex;
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Pet.TilePoint));
            if (!active.Pet.checkAction(Game1.player, active.Location))
            {
                CompletePetInteractionBlocked(active, "pet_interaction_native_check_action_returned_false");
                return;
            }
            active.InteractionIssued = true;
            return;
        }

        active.SettleTicks++;
        var request = active.Pending.Request;
        var hasLastDay = active.Pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDay);
        var settled = hasLastDay && lastDay == request.ExpectedLastPetDayAfter!.Value &&
            active.Pet.friendshipTowardFarmer.Value == request.ExpectedFriendshipAfter!.Value &&
            active.Pet.timesPet.Value == request.ExpectedTimesPetAfter!.Value &&
            active.Pet.grantedFriendshipForPet.Value == request.ExpectedGrantedFriendshipAfter!.Value &&
            Game1.player.mailReceived.Contains("petLoveMessage") == request.ExpectedPetLoveMailAfter!.Value &&
            Game1.player.hasOrWillReceiveMail("MarniePetAdoption") == request.ExpectedMarniePetAdoptionMailAfterOrPending!.Value;
        if (settled)
        {
            CompletePetInteraction(active);
        }
        else if (active.SettleTicks > 180)
        {
            CompletePetInteractionBlocked(active, "pet_interaction_native_settlement_timeout_or_mismatch");
        }
    }

    private bool TryReplanPetPath(ActivePetInteraction active, out string reason)
    {
        active.Target = active.Pet.TilePoint;
        foreach (var candidate in AnimalAdjacentTiles(active.Target).OrderBy(tile => ManhattanDistance(Game1.player.TilePoint, tile)))
        {
            if (!IsTileOnMap(active.Location, candidate) || !IsTileWalkable(active.Location, candidate) || IsTileOccupiedByCharacter(active.Location, candidate))
            {
                continue;
            }
            var path = TryBuildTilePath(active.Location, Game1.player.TilePoint, candidate, active.MaxMovementTiles - active.MovementTiles, out reason, avoidSoftObstacles: true, allowRemovableObstacles: false);
            if (path is not null)
            {
                active.Stand = candidate;
                active.Path = path;
                active.PathIndex = 0;
                return true;
            }
        }
        reason = "no_reachable_adjacent_tile";
        return false;
    }

    private void CompletePetInteraction(ActivePetInteraction active)
    {
        activePetInteraction = null;
        StopAllMovement();
        var request = active.Pending.Request;
        active.Pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDayAfter);
        var mailAfter = Game1.player.mailReceived.Contains("petLoveMessage");
        var adoptionMailAfter = Game1.player.hasOrWillReceiveMail("MarniePetAdoption");
        var debrisAfter = active.Location.debris.Count;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            TargetLocation = active.Location.NameOrUniqueName,
            TargetTileX = active.Pet.TilePoint.X,
            TargetTileY = active.Pet.TilePoint.Y,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "pet_interact",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_Pet.checkAction_completed", "friendship_lastPetDay_timesPet_love_mail_and_adoption_mail_verified", "gift_output_recorded_without_predicting_global_rng_selection" },
            RequestedEffect = "pet.friendship=" + request.ExpectedFriendshipAfter + ";pet.last_pet_day=" + request.ExpectedLastPetDayAfter + ";pet.times_pet=" + request.ExpectedTimesPetAfter + ";pet_love_mail=" + request.ExpectedPetLoveMailAfter + ";MarniePetAdoption=" + request.ExpectedMarniePetAdoptionMailAfterOrPending,
            ObservedEffect = PetObservedEffect(active.Pet) + ";gift_debris_count_before=" + active.GiftDebrisCountBefore + ";gift_debris_count_after=" + debrisAfter + ";moving_target_replans=" + active.ReplanCount + ";movement_tiles=" + active.MovementTiles,
            PetId = active.Pet.petId.Value.ToString("D"),
            PetFriendshipBefore = active.FriendshipBefore,
            PetFriendshipAfter = active.Pet.friendshipTowardFarmer.Value,
            PetLastPetDayBefore = active.LastPetDayBefore,
            PetLastPetDayBeforeMissing = active.LastPetDayBeforeMissing,
            PetLastPetDayAfter = lastDayAfter,
            PetTimesPetBefore = active.TimesPetBefore,
            PetTimesPetAfter = active.Pet.timesPet.Value,
            PetGrantedFriendshipBefore = active.GrantedFriendshipBefore,
            PetGrantedFriendshipAfter = active.Pet.grantedFriendshipForPet.Value,
            PetLoveMailBefore = active.PetLoveMailBefore,
            PetLoveMailAfter = mailAfter,
            MarniePetAdoptionMailBeforeOrPending = active.MarniePetAdoptionMailBeforeOrPending,
            MarniePetAdoptionMailAfterOrPending = adoptionMailAfter,
            PetGiftTriggerExpected = request.PetGiftTriggerExpected,
            PetGiftDebrisCountBefore = active.GiftDebrisCountBefore,
            PetGiftDebrisCountAfter = debrisAfter,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "farm.pets[" + active.Pet.petId.Value.ToString("D") + "].friendship", Before = active.FriendshipBefore.ToString(), After = active.Pet.friendshipTowardFarmer.Value.ToString() },
                new SimulatedFactChange { Path = "farm.pets[" + active.Pet.petId.Value.ToString("D") + "].last_pet_day", Before = active.LastPetDayBeforeMissing ? "missing" : active.LastPetDayBefore?.ToString() ?? "missing", After = lastDayAfter.ToString() },
                new SimulatedFactChange { Path = "farm.pets[" + active.Pet.petId.Value.ToString("D") + "].times_pet", Before = active.TimesPetBefore.ToString(), After = active.Pet.timesPet.Value.ToString() },
                new SimulatedFactChange { Path = "quests.mail_received.petLoveMessage", Before = active.PetLoveMailBefore.ToString().ToLowerInvariant(), After = mailAfter.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "quests.mail_received_or_pending.MarniePetAdoption", Before = active.MarniePetAdoptionMailBeforeOrPending.ToString().ToLowerInvariant(), After = adoptionMailAfter.ToString().ToLowerInvariant() }
            }
        });
    }

    private void CompletePetInteractionBlocked(ActivePetInteraction active, string reason)
    {
        activePetInteraction = null;
        StopAllMovement();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "pet_interact", "pet.daily_interaction=applied", PetObservedEffect(active.Pet), reason));
    }

    private void StartFillPetBowl(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.ToolSlotIndex.HasValue ||
            !request.ExpectedBowlWateredBefore.HasValue || !request.ExpectedBowlWateredAfter.HasValue ||
            !request.ExpectedWaterBefore.HasValue || !request.ExpectedWaterAfter.HasValue || !request.ExpectedWateringCanBottomless.HasValue ||
            !request.ExpectedFriendshipBefore.HasValue || !request.ExpectedNextDayFriendshipAfter.HasValue ||
            !request.ExpectedPetLoveMailBefore.HasValue || !request.ExpectedNextDayPetLoveMail.HasValue ||
            !request.ExpectedMarniePetAdoptionMailBeforeOrPending.HasValue || !request.ExpectedNextDayMarniePetAdoptionMail.HasValue ||
            !Guid.TryParse(request.TargetRuntimeIdentity, out var petId) || request.RequiredToolKind != "Watering Can")
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "fill_pet_bowl", "pet_bowl.watered=true", "request=missing_typed_projection", "fill_pet_bowl_typed_projection_required"));
            return;
        }
        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var bowl = FindPetBowlAtActionTile(location, target);
        var pet = Utility.findPet(petId);
        var can = request.ToolSlotIndex.Value >= 0 && request.ToolSlotIndex.Value < Game1.player.Items.Count
            ? Game1.player.Items[request.ToolSlotIndex.Value] as WateringCan
            : null;
        if (bowl is null || pet is null || bowl.GetType().FullName != request.TargetRuntimeType || bowl.petId.Value != petId ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            bowl.GetType() != typeof(PetBowl) || bowl.watered.Value != request.ExpectedBowlWateredBefore.Value || bowl.watered.Value ||
            pet.friendshipTowardFarmer.Value != request.ExpectedFriendshipBefore.Value ||
            request.ExpectedNextDayFriendshipAfter.Value != Math.Min(Pet.maxFriendship, pet.friendshipTowardFarmer.Value + 6) ||
            Game1.player.mailReceived.Contains("petLoveMessage") != request.ExpectedPetLoveMailBefore.Value ||
            request.ExpectedNextDayPetLoveMail.Value != (request.ExpectedPetLoveMailBefore.Value || request.ExpectedNextDayFriendshipAfter.Value >= Pet.maxFriendship) ||
            Game1.player.hasOrWillReceiveMail("MarniePetAdoption") != request.ExpectedMarniePetAdoptionMailBeforeOrPending.Value ||
            request.ExpectedNextDayMarniePetAdoptionMail.Value !=
                (request.ExpectedMarniePetAdoptionMailBeforeOrPending.Value || request.ExpectedNextDayFriendshipAfter.Value >= Pet.maxFriendship) ||
            request.DelayedSettlement != "Pet.dayUpdate consumes watered=true and applies min(1000,friendship+6)" ||
            can is null || can.GetType() != typeof(WateringCan) || !request.ExpectedBowlWateredAfter.Value ||
            can.WaterLeft != request.ExpectedWaterBefore.Value || can.IsBottomless != request.ExpectedWateringCanBottomless.Value ||
            request.ExpectedWaterAfter.Value != (can.IsBottomless ? can.WaterLeft : can.WaterLeft - 1))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "fill_pet_bowl", "pet_bowl.watered=true", PetBowlObservedEffect(location, target), "fill_pet_bowl_projection_drifted"));
            return;
        }
        var precheck = ValidatePetBowlTarget(location, target, can);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "fill_pet_bowl", "pet_bowl.watered=true", PetBowlObservedEffect(location, target), precheck));
            return;
        }
        var path = BuildAdjacentToolPath(location, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "fill_pet_bowl", "pet_bowl.watered=true", PetBowlObservedEffect(location, target), moveReason));
            return;
        }
        activeNativeTool = ActiveNativeTool.WaterPetBowl(
            pending, location.NameOrUniqueName, target, path, can, Game1.player.Stamina, can.WaterLeft,
            DateTimeOffset.UtcNow.ToString("O"), EstimateRuntimeToolTicks(target),
            "pet_bowl.watered=true;friendship_settlement=next_day", bowl.watered.Value, WateringCanEnergyCost(can));
    }

    private static string[] ValidatePetBowlTarget(GameLocation location, Point target, WateringCan? can)
    {
        var reasons = new List<string>();
        var bowl = FindPetBowlAtActionTile(location, target);
        if (bowl is null)
        {
            reasons.Add("pet_bowl_action_tile_missing");
        }
        else if (!bowl.HasPet())
        {
            reasons.Add("pet_bowl_unassigned");
        }
        else if (bowl.watered.Value)
        {
            reasons.Add("pet_bowl_already_watered");
        }
        if (can is null)
        {
            reasons.Add("watering_can_missing");
        }
        else if (can.WaterLeft <= 0 && !Game1.player.hasWateringCanEnchantment)
        {
            reasons.Add("watering_can_empty");
        }
        if (can is not null && Game1.player.Stamina < WateringCanEnergyCost(can))
        {
            reasons.Add("insufficient_stamina");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void CompleteFillPetBowlNativeTool(ActiveNativeTool tool)
    {
        var request = tool.Pending.Request;
        var bowl = FindPetBowlAtActionTile(Game1.currentLocation, tool.Target);
        var pet = Guid.TryParse(request.TargetRuntimeIdentity, out var petId) ? Utility.findPet(petId) : null;
        var wateredAfter = bowl?.watered.Value;
        var mailAfter = Game1.player.mailReceived.Contains("petLoveMessage");
        var adoptionMailAfter = Game1.player.hasOrWillReceiveMail("MarniePetAdoption");
        var friendshipAfter = pet?.friendshipTowardFarmer.Value;
        var waterAfter = tool.Tool is WateringCan can ? can.WaterLeft : (int?)null;
        var energyCost = tool.StaminaBefore - Game1.player.Stamina;
        var verified = !tool.BeforeWatered.GetValueOrDefault() && wateredAfter == true &&
            friendshipAfter == request.ExpectedFriendshipBefore!.Value && mailAfter == request.ExpectedPetLoveMailBefore!.Value &&
            adoptionMailAfter == request.ExpectedMarniePetAdoptionMailBeforeOrPending!.Value &&
            tool.WaterBefore == request.ExpectedWaterBefore!.Value && waterAfter == request.ExpectedWaterAfter!.Value &&
            Math.Abs(energyCost - tool.ExpectedEnergyCost) <= 0.001d;
        tool.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            WateredCount = verified ? 1 : 0,
            EnergyBefore = tool.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = Game1.currentLocation.NameOrUniqueName,
            TargetTileX = tool.Target.X,
            TargetTileY = tool.Target.Y,
            ToolQualifiedItemId = tool.Tool.QualifiedItemId,
            ToolUpgradeLevel = tool.Tool.UpgradeLevel,
            WaterBefore = tool.WaterBefore,
            WaterAfter = waterAfter,
            EstimatedTicks = tool.EstimatedTicks,
            ActualTicks = tool.ElapsedTicks,
            FailureCategory = verified ? string.Empty : "fill_pet_bowl_postcondition_mismatch",
            TrainingImpactScope = "executor_calibration",
            StartedAt = tool.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "fill_pet_bowl",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? NativeToolVerifiedReasons(tool)
                : new[] { wateredAfter == true ? "pet_bowl_native_energy_delta_mismatch" : "pet_bowl_water_state_unchanged_after_native_tool_lifecycle" },
            RequestedEffect = tool.RequestedEffect,
            ObservedEffect = PetBowlObservedEffect(Game1.currentLocation, tool.Target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fill_pet_bowl_postcondition_mismatch" },
            PetId = request.TargetRuntimeIdentity,
            PetFriendshipBefore = request.ExpectedFriendshipBefore,
            PetFriendshipAfter = friendshipAfter,
            PetLoveMailBefore = request.ExpectedPetLoveMailBefore,
            PetLoveMailAfter = mailAfter,
            MarniePetAdoptionMailBeforeOrPending = request.ExpectedMarniePetAdoptionMailBeforeOrPending,
            MarniePetAdoptionMailAfterOrPending = adoptionMailAfter,
            PetBowlWateredBefore = tool.BeforeWatered,
            PetBowlWateredAfter = wateredAfter,
            PetNextDayFriendshipExpectedAfter = request.ExpectedNextDayFriendshipAfter,
            PetNextDayLoveMailExpectedAfter = request.ExpectedNextDayPetLoveMail,
            PetNextDayMarnieAdoptionMailExpectedAfter = request.ExpectedNextDayMarniePetAdoptionMail,
            PetNextDaySettlementStatus = verified ? "pending_Pet.dayUpdate" : "not_scheduled",
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "farm.pet_bowls[" + tool.Target.X + "," + tool.Target.Y + "].watered", Before = "false", After = "true" },
                    new SimulatedFactChange { Path = "farm.pets[" + request.TargetRuntimeIdentity + "].friendship", Before = request.ExpectedFriendshipBefore?.ToString() ?? "missing", After = friendshipAfter?.ToString() ?? "missing" },
                    new SimulatedFactChange { Path = "player.watering_can.water_left", Before = tool.WaterBefore?.ToString() ?? "missing", After = waterAfter?.ToString() ?? "missing" }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static PetBowl? FindPetBowlAtActionTile(GameLocation location, Point target)
    {
        foreach (var bowl in location.buildings.OfType<PetBowl>())
        {
            string propertyValue = null!;
            if (bowl.doesTileHaveProperty(target.X, target.Y, "PetBowl", "Buildings", ref propertyValue))
            {
                return bowl;
            }
        }
        return null;
    }

    private static double WateringCanEnergyCost(WateringCan can)
    {
        return can.IsEfficient ? 0d : Math.Max(0d, 2d - Game1.player.FarmingLevel * 0.1d);
    }

    private static string PetObservedEffect(Pet? pet)
    {
        if (pet is null)
        {
            return "pet=missing";
        }
        var hasLastDay = pet.lastPetDay.TryGetValue(Game1.player.UniqueMultiplayerID, out var lastDay);
        return "pet_id=" + pet.petId.Value.ToString("D") + ";location=" + (pet.currentLocation?.NameOrUniqueName ?? "missing") +
            ";tile=" + pet.TilePoint.X + "," + pet.TilePoint.Y + ";friendship=" + pet.friendshipTowardFarmer.Value +
            ";last_pet_day=" + (hasLastDay ? lastDay.ToString() : "missing") + ";times_pet=" + pet.timesPet.Value +
            ";granted_friendship=" + pet.grantedFriendshipForPet.Value.ToString().ToLowerInvariant() +
            ";pet_love_mail=" + Game1.player.mailReceived.Contains("petLoveMessage").ToString().ToLowerInvariant() +
            ";MarniePetAdoption=" + Game1.player.hasOrWillReceiveMail("MarniePetAdoption").ToString().ToLowerInvariant();
    }

    private static bool IsSupportedVanillaPetRuntimeType(Type runtimeType)
    {
        return runtimeType == typeof(Pet) || runtimeType == typeof(Cat) || runtimeType == typeof(Dog);
    }

    private static string PetBowlObservedEffect(GameLocation location, Point target)
    {
        var bowl = FindPetBowlAtActionTile(location, target);
        return "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y +
            ";bowl_present=" + (bowl is not null).ToString().ToLowerInvariant() +
            ";watered=" + (bowl?.watered.Value.ToString().ToLowerInvariant() ?? "missing") +
            ";pet_id=" + (bowl?.petId.Value.ToString("D") ?? "missing") +
            ";MarniePetAdoption=" + Game1.player.hasOrWillReceiveMail("MarniePetAdoption").ToString().ToLowerInvariant();
    }
}
