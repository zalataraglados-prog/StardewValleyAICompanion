using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartAnimalManagement(PendingExecution pending)
    {
        var request = pending.Request;
        var validation = ValidateExecutionRequest(request);
        if (validation.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, validation.ToArray()));
            return;
        }
        if (!TryValidateAnimalManagementRequest(request, out var requestReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "request_or_player_state=invalid", requestReason));
            return;
        }
        if (activeAnimalManagement is not null || Game1.activeClickableMenu is not null ||
            Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "request_or_player_state=invalid",
                "animal_management_player_state_busy"));
            return;
        }

        var location = Game1.currentLocation;
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            !location.animals.TryGetValue(request.ManagedAnimalId!.Value, out var animal) ||
            animal.GetType() != typeof(FarmAnimal) || request.TargetRuntimeType != typeof(FarmAnimal).FullName ||
            animal.TilePoint != new Point(request.TargetTileX!.Value, request.TargetTileY!.Value) ||
            animal.displayName != request.ExpectedAnimalNameBefore ||
            animal.wasPet.Value == request.AnimalManagementRequiresInitialPet ||
            Game1.player.Items[request.SafeSlotIndex!.Value] is not (null or StardewValley.Tool))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "animal=missing_or_projection_drifted",
                "animal_management_live_projection_drifted"));
            return;
        }
        if (request.AnimalManagementIntent == "sell" &&
            (animal.home is null || animal.home.GetIndoors() is not AnimalHouse))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "animal_home=missing_or_invalid",
                "animal_management_sale_requires_native_home"));
            return;
        }

        Building? targetHome = null;
        AnimalHouse? targetHouse = null;
        if (request.AnimalManagementIntent == "move_home")
        {
            var farm = Game1.getFarm();
            targetHome = farm.buildings.FirstOrDefault(building =>
                building.buildingType.Value == request.TargetAnimalHomeBuildingType &&
                building.tileX.Value == request.TargetAnimalHomeBuildingTileX &&
                building.tileY.Value == request.TargetAnimalHomeBuildingTileY);
            targetHouse = targetHome?.GetIndoors() as AnimalHouse;
            if (targetHome is null || targetHouse is null || !animal.CanLiveIn(targetHome) ||
                targetHome.isUnderConstruction() || targetHouse.isFull() || ReferenceEquals(targetHome, animal.home) ||
                targetHouse.animalsThatLiveHere.Count != request.ExpectedTargetAnimalHomeOccupantCountBefore ||
                targetHouse.animalLimit.Value != request.ExpectedTargetAnimalHomeCapacity)
            {
                pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                    AnimalManagementRequestedEffect(request), "target_home=missing_full_or_drifted",
                    "animal_management_target_home_drifted"));
                return;
            }
        }
        if (!AnimalManagementIntentStillMatches(request, animal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), AnimalManagementObservedEffect(animal),
                "animal_management_intent_projection_drifted"));
            return;
        }

        var target = animal.TilePoint;
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        if (!AreAdjacent(stand, target) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "stand_tile=invalid",
                "animal_management_stand_tile_invalid"));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "manage_animal",
                AnimalManagementRequestedEffect(request), "path=unavailable",
                "animal_management_path_unavailable:" + pathReason));
            return;
        }

        activeAnimalManagement = new ActiveAnimalManagement(
            pending, location, animal, target, stand, path, Game1.player.CurrentToolIndex,
            targetHome, targetHouse, maxMovementTiles);
    }

    private void TickAnimalManagement()
    {
        var active = activeAnimalManagement;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || active.ElapsedTicks > 3600)
        {
            CompleteAnimalManagementBlocked(active,
                "animal_management_world_or_timeout:stage=" + active.Stage);
            return;
        }

        if (active.Stage == AnimalManagementStage.Navigate)
        {
            if (!ReferenceEquals(Game1.currentLocation, active.Location) ||
                !active.Location.animals.TryGetValue(active.Animal.myID.Value, out var current) ||
                !ReferenceEquals(current, active.Animal))
            {
                CompleteAnimalManagementBlocked(active, "animal_management_target_or_location_drifted");
                return;
            }
            if (active.Animal.TilePoint != active.Target && !TryReplanAnimalManagementPath(active, out var replanReason))
            {
                CompleteAnimalManagementBlocked(active, "animal_management_moving_target_replan_failed:" + replanReason);
                return;
            }
            if (!AreAdjacent(Game1.player.TilePoint, active.Animal.TilePoint))
            {
                if (active.PathIndex >= active.Path.Count)
                {
                    CompleteAnimalManagementBlocked(active, "animal_management_path_exhausted");
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
                    active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
                    active.LastObservedTile = playerTile;
                }
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteAnimalManagementBlocked(active, "animal_management_movement_budget_exceeded");
                    return;
                }
                if (playerTile == next)
                {
                    active.PathIndex++;
                }
                return;
            }
            StopAllMovement();
            active.Stage = AnimalManagementStage.OpenQuery;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == AnimalManagementStage.OpenQuery)
        {
            Game1.player.CurrentToolIndex = active.Pending.Request.SafeSlotIndex!.Value;
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Animal.TilePoint));
            var handled = active.Animal.wasPet.Value
                ? active.Location.CheckInspectAnimal(active.Animal.GetBoundingBox(), Game1.player)
                : active.Location.CheckPetAnimal(active.Animal.GetBoundingBox(), Game1.player);
            if (!handled)
            {
                CompleteAnimalManagementBlocked(active, "animal_management_native_animal_action_not_handled");
                return;
            }
            active.Stage = active.Animal.wasPet.Value && Game1.activeClickableMenu is not AnimalQueryMenu
                ? AnimalManagementStage.WaitAfterInitialPet
                : AnimalManagementStage.ApplyMenuOperation;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == AnimalManagementStage.WaitAfterInitialPet)
        {
            if (!Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
            {
                return;
            }
            active.Stage = AnimalManagementStage.OpenQuery;
            return;
        }

        if (active.Stage == AnimalManagementStage.ApplyMenuOperation)
        {
            if (Game1.activeClickableMenu is not AnimalQueryMenu menu || !ReferenceEquals(menu.animal, active.Animal))
            {
                if (active.ElapsedTicks - active.StageEnteredTick > 90)
                {
                    CompleteAnimalManagementBlocked(active, "animal_management_query_menu_not_open");
                }
                return;
            }
            active.Menu = menu;
            var request = active.Pending.Request;
            switch (request.AnimalManagementIntent)
            {
                case "rename":
                    menu.textBox.Text = request.TargetName;
                    if (!string.Equals(menu.textBox.Text, request.TargetName, StringComparison.Ordinal))
                    {
                        CompleteAnimalManagementBlocked(active, "animal_management_target_name_exceeds_native_textbox_width");
                        return;
                    }
                    menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
                    active.Stage = AnimalManagementStage.WaitForReceipt;
                    break;
                case "toggle_reproduction":
                    if (menu.allowReproductionButton is null)
                    {
                        CompleteAnimalManagementBlocked(active, "animal_management_reproduction_button_unavailable");
                        return;
                    }
                    menu.receiveLeftClick(menu.allowReproductionButton.bounds.Center.X, menu.allowReproductionButton.bounds.Center.Y);
                    menu.receiveLeftClick(menu.okButton.bounds.Center.X, menu.okButton.bounds.Center.Y);
                    active.Stage = AnimalManagementStage.WaitForReceipt;
                    break;
                case "sell":
                    menu.receiveLeftClick(menu.sellButton.bounds.Center.X, menu.sellButton.bounds.Center.Y);
                    active.Stage = AnimalManagementStage.ConfirmSale;
                    break;
                case "move_home":
                    menu.receiveLeftClick(menu.moveHomeButton.bounds.Center.X, menu.moveHomeButton.bounds.Center.Y);
                    active.Stage = AnimalManagementStage.WaitForPlacementMode;
                    break;
            }
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == AnimalManagementStage.ConfirmSale)
        {
            if (active.Menu?.confirmingSell != true || active.Menu.yesButton is null)
            {
                if (active.ElapsedTicks - active.StageEnteredTick > 90)
                {
                    CompleteAnimalManagementBlocked(active, "animal_management_sale_confirmation_not_open");
                }
                return;
            }
            active.Menu.receiveLeftClick(active.Menu.yesButton.bounds.Center.X, active.Menu.yesButton.bounds.Center.Y);
            active.Stage = AnimalManagementStage.WaitForReceipt;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == AnimalManagementStage.WaitForPlacementMode)
        {
            if (Game1.globalFade || active.Menu?.movingAnimal != true)
            {
                if (active.ElapsedTicks - active.StageEnteredTick > 300)
                {
                    CompleteAnimalManagementBlocked(active, "animal_management_placement_mode_not_open");
                }
                return;
            }
            active.Stage = AnimalManagementStage.SelectTargetHome;
        }

        if (active.Stage == AnimalManagementStage.SelectTargetHome)
        {
            var home = active.TargetHome!;
            var screenPixel = new Point(
                home.tileX.Value * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.X,
                home.tileY.Value * Game1.tileSize + Game1.tileSize / 2 - Game1.viewport.Y);
            PlacementCursorPatch.ScreenPixel = screenPixel;
            PlacementCursorPatch.Active = true;
            active.Menu!.receiveLeftClick(screenPixel.X, screenPixel.Y);
            PlacementCursorPatch.Clear();
            active.Stage = AnimalManagementStage.WaitForReceipt;
            active.StageEnteredTick = active.ElapsedTicks;
            return;
        }

        if (active.Stage == AnimalManagementStage.WaitForReceipt &&
            !Game1.globalFade && Game1.activeClickableMenu is null)
        {
            CompleteAnimalManagement(active);
        }
    }

    private bool TryReplanAnimalManagementPath(ActiveAnimalManagement active, out string reason)
    {
        active.ReplanCount++;
        if (active.ReplanCount > 16)
        {
            reason = "replan_limit";
            return false;
        }
        active.Target = active.Animal.TilePoint;
        foreach (var candidate in AnimalAdjacentTiles(active.Target)
                     .OrderBy(tile => ManhattanDistance(Game1.player.TilePoint, tile)))
        {
            if (!IsTileOnMap(active.Location, candidate) || !IsTileWalkable(active.Location, candidate) ||
                IsTileOccupiedByCharacter(active.Location, candidate))
            {
                continue;
            }
            var path = TryBuildTilePath(active.Location, Game1.player.TilePoint, candidate,
                active.MaxMovementTiles - active.MovementTiles, out reason,
                avoidSoftObstacles: true, allowRemovableObstacles: false);
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

    private static bool TryValidateAnimalManagementRequest(
        TrainingExecutionRequest request,
        out string reason)
    {
        reason = "animal_management_typed_request_invalid";
        if (request.AnimalManagementIntent is not ("rename" or "toggle_reproduction" or "move_home" or "sell") ||
            string.IsNullOrWhiteSpace(request.AnimalManagementReason) || !request.ManagedAnimalId.HasValue ||
            string.IsNullOrWhiteSpace(request.LocationId) || !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue || !request.SafeSlotIndex.HasValue ||
            request.SafeSlotIndex < 0 || request.SafeSlotIndex >= Game1.player.Items.Count ||
            !request.AnimalManagementRequiresInitialPet.HasValue || string.IsNullOrWhiteSpace(request.ExpectedAnimalNameBefore) ||
            !request.ExpectedAnimalSellPrice.HasValue || !request.ExpectedMoneyBefore.HasValue)
        {
            return false;
        }
        if (request.AnimalManagementIntent == "rename" && string.IsNullOrWhiteSpace(request.TargetName) ||
            request.AnimalManagementIntent == "toggle_reproduction" &&
                (!request.ExpectedAllowReproductionBefore.HasValue || !request.TargetAllowReproduction.HasValue) ||
            request.AnimalManagementIntent == "sell" && !request.ConfirmIrreversibleAnimalSale ||
            request.AnimalManagementIntent == "move_home" &&
                (string.IsNullOrWhiteSpace(request.TargetAnimalHomeBuildingType) ||
                 !request.TargetAnimalHomeBuildingTileX.HasValue || !request.TargetAnimalHomeBuildingTileY.HasValue ||
                 !request.ExpectedTargetAnimalHomeOccupantCountBefore.HasValue || !request.ExpectedTargetAnimalHomeCapacity.HasValue))
        {
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool AnimalManagementIntentStillMatches(
        TrainingExecutionRequest request,
        FarmAnimal animal)
    {
        return request.AnimalManagementIntent switch
        {
            "rename" => !Utility.areThereAnyOtherAnimalsWithThisName(request.TargetName) &&
                animal.displayName != request.TargetName,
            "toggle_reproduction" => !animal.isBaby() && animal.CanHavePregnancy() &&
                animal.allowReproduction.Value == request.ExpectedAllowReproductionBefore &&
                request.TargetAllowReproduction != request.ExpectedAllowReproductionBefore,
            "sell" => animal.getSellPrice() == request.ExpectedAnimalSellPrice &&
                Game1.player.Money == request.ExpectedMoneyBefore,
            "move_home" => animal.home?.buildingType.Value == request.ExpectedAnimalHomeBuildingTypeBefore &&
                animal.home?.tileX.Value == request.ExpectedAnimalHomeBuildingTileXBefore &&
                animal.home?.tileY.Value == request.ExpectedAnimalHomeBuildingTileYBefore,
            _ => false
        };
    }

    private void CompleteAnimalManagement(ActiveAnimalManagement active)
    {
        var request = active.Pending.Request;
        var verified = request.AnimalManagementIntent switch
        {
            "rename" => active.Animal.displayName == request.TargetName &&
                active.Animal.Name == request.TargetName,
            "toggle_reproduction" => active.Animal.allowReproduction.Value == request.TargetAllowReproduction,
            "sell" => active.Animal.health.Value == -1 &&
                Game1.player.Money == active.MoneyBefore + request.ExpectedAnimalSellPrice,
            "move_home" => ReferenceEquals(active.Animal.home, active.TargetHome) &&
                active.TargetHouse!.animalsThatLiveHere.Contains(active.Animal.myID.Value) &&
                active.TargetHouse.animalsThatLiveHere.Count == active.TargetHomeOccupantsBefore + 1,
            _ => false
        };
        activeAnimalManagement = null;
        PlacementCursorPatch.Clear();
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "manage_animal",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_AnimalQueryMenu_operation_verified", "management_intent=" + request.AnimalManagementIntent }
                : new[] { "animal_management_postcondition_mismatch" },
            RequestedEffect = AnimalManagementRequestedEffect(request),
            ObservedEffect = AnimalManagementObservedEffect(active.Animal),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "animal_management_postcondition_mismatch" }
        });
    }

    private void CompleteAnimalManagementBlocked(ActiveAnimalManagement active, string reason)
    {
        activeAnimalManagement = null;
        PlacementCursorPatch.Clear();
        StopAllMovement();
        CloseAnimalManagementMenu(active);
        if (!Game1.player.UsingTool)
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request, "manage_animal", AnimalManagementRequestedEffect(active.Pending.Request),
            AnimalManagementObservedEffect(active.Animal), reason));
    }

    private static void CloseAnimalManagementMenu(ActiveAnimalManagement active)
    {
        if (active.Menu is null || !ReferenceEquals(Game1.activeClickableMenu, active.Menu))
        {
            return;
        }
        if (active.Menu.movingAnimal)
        {
            active.Menu.prepareForReturnFromPlacement();
        }
        Game1.exitActiveMenu();
    }

    private static string AnimalManagementRequestedEffect(TrainingExecutionRequest request) =>
        "animal_id=" + request.ManagedAnimalId + ";management_intent=" + request.AnimalManagementIntent;

    private static string AnimalManagementObservedEffect(FarmAnimal animal) =>
        "animal_id=" + animal.myID.Value + ";name=" + animal.displayName +
        ";allow_reproduction=" + animal.allowReproduction.Value.ToString().ToLowerInvariant() +
        ";sell_price=" + animal.getSellPrice() + ";health=" + animal.health.Value +
        ";home=" + (animal.home?.buildingType.Value ?? "none") + "@" +
        animal.home?.tileX.Value + "," + animal.home?.tileY.Value;
}
