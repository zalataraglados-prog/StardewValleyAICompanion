using System.Globalization;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupAnimalProductTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_animal_product_target", "farm.animals[target].harvest_status=ready", "target_tile=missing", "target_tile_required");
        }

        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var toolName = request.RequiredToolKind == "Shears" ? "Shears" : "Milk Pail";
        var animalType = toolName == "Shears" ? "Sheep" : "White Cow";
        var outputId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? toolName == "Shears" ? "(O)440" : "(O)184"
            : request.QualifiedItemId;
        var tool = toolName == "Shears" ? (Tool)new Shears() : new MilkPail();
        var toolSlot = EnsureFixtureTool(tool);
        if (toolSlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_animal_product_target", "farm.animals[target].harvest_status=ready", "tool_slot=unavailable", "fixture_inventory_cannot_accept_animal_tool");
        }

        foreach (var existing in farm.animals.Pairs.Where(pair => pair.Value.TilePoint == target).Select(pair => pair.Key).ToArray())
        {
            farm.animals.Remove(existing);
        }
        var animalId = unchecked(Game1.player.UniqueMultiplayerID + DateTime.UtcNow.Ticks);
        while (farm.animals.ContainsKey(animalId))
        {
            animalId++;
        }
        var animal = new FarmAnimal(animalType, animalId, Game1.player.UniqueMultiplayerID);
        animal.Position = new Vector2(target.X * Game1.tileSize, target.Y * Game1.tileSize);
        animal.Position += new Vector2(
            (target.X - animal.TilePoint.X) * Game1.tileSize,
            (target.Y - animal.TilePoint.Y) * Game1.tileSize);
        animal.age.Value = animal.GetAnimalData()?.DaysToMature ?? 99;
        animal.currentProduce.Value = ItemRegistry.GetDataOrErrorItem(outputId).ItemId;
        animal.produceQuality.Value = Math.Clamp(request.ExpectedOutputQuality ?? 2, 0, 4);
        animal.hasEatenAnimalCracker.Value = request.ExpectedAnimalCrackerMultiplier == 2;
        animal.friendshipTowardFarmer.Value = 500;
        animal.pauseTimer = 60000;
        animal.Halt();
        farm.animals[animal.myID.Value] = animal;
        var moved = MoveFixtureFarmerToFarmAdjacent(target, out var stand, out var moveReason);
        var verified = moved && toolSlot >= 0 && farm.animals.TryGetValue(animal.myID.Value, out var current) &&
            ReferenceEquals(current, animal) && animal.currentProduce.Value is not null && animal.isAdult() && animal.CanGetProduceWithTool(tool);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_animal_product_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_animal_product_ready", "animal_id=" + animal.myID.Value, "tool_slot=" + toolSlot, "stand_tile=" + stand.X + "," + stand.Y }
                : new[] { "fixture_animal_product_not_ready", moveReason },
            RequestedEffect = "farm.animals[target].harvest_status=ready",
            ObservedEffect = AnimalProductObservedEffect(animal),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_animal_product_not_ready:" + moveReason }
        };
    }

    private int EnsureFixtureTool(Tool requested)
    {
        var existing = Game1.player.Items
            .Select((item, index) => new { item, index })
            .FirstOrDefault(entry => entry.item is Tool tool && tool.Name == requested.Name);
        if (existing is not null)
        {
            return existing.index;
        }
        if (!Game1.player.addItemToInventoryBool(requested))
        {
            return -1;
        }
        return Game1.player.Items
            .Select((item, index) => new { item, index })
            .First(entry => ReferenceEquals(entry.item, requested))
            .index;
    }

    private void StartAnimalProductHarvest(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue || !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !long.TryParse(request.TargetRuntimeIdentity, NumberStyles.Integer, CultureInfo.InvariantCulture, out var animalId) ||
            request.RequiredToolKind is not ("Milk Pail" or "Shears") || !request.ToolSlotIndex.HasValue ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) || !request.Quantity.HasValue || !request.ExpectedOutputQuality.HasValue ||
            request.ExpectedSkillId != "farming" || request.ExpectedSkillExperienceDelta != 5 || request.ExpectedEnergyDelta != -4 ||
            !request.ExpectedFriendshipBefore.HasValue || !request.ExpectedFriendshipAfter.HasValue ||
            !TryParseClearanceOutputItems(request.ExpectedOutputItemsJson, out var expectedItems) || expectedItems.Length != 1 ||
            !TryParseAnimalStatIncrements(request.ExpectedStatIncrementsJson, out var statIncrements))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", "request=missing_typed_projection", "collect_animal_product_typed_projection_required"));
            return;
        }
        if (activeAnimalProductHarvest is not null || Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", "player=busy_or_menu_open", "collect_animal_product_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        if (!string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            !location.animals.TryGetValue(animalId, out var animal) || animal.GetType() != typeof(FarmAnimal) ||
            request.TargetRuntimeType != typeof(FarmAnimal).FullName || animal.TilePoint != target ||
            animal.currentProduce.Value is null || !animal.isAdult())
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", "animal=missing_or_drifted", "collect_animal_product_target_not_ready_or_drifted"));
            return;
        }
        if (request.ToolSlotIndex.Value < 0 || request.ToolSlotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[request.ToolSlotIndex.Value] is not Tool tool || tool.Name != request.RequiredToolKind ||
            (tool is not MilkPail && tool is not Shears) || !animal.CanGetProduceWithTool(tool))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(animal), "collect_animal_product_tool_projection_drifted"));
            return;
        }

        var output = ItemRegistry.Create<StardewValley.Object>("(O)" + animal.currentProduce.Value, request.Quantity.Value, animal.produceQuality.Value);
        output.CanBeSetDown = false;
        output.HasBeenInInventory = true;
        var outputKey = ClearanceOutputItemKey.From(output);
        if (request.Quantity.Value != (animal.hasEatenAnimalCracker.Value ? 2 : 1) || request.ExpectedAnimalCrackerMultiplier != request.Quantity.Value ||
            request.ExpectedOutputQuality != animal.produceQuality.Value || expectedItems[0].Key != outputKey || expectedItems[0].Quantity != request.Quantity.Value ||
            !string.Equals(output.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase) || !Game1.player.couldInventoryAcceptThisItem(output))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(animal), "collect_animal_product_output_projection_drifted"));
            return;
        }
        if (animal.friendshipTowardFarmer.Value != request.ExpectedFriendshipBefore.Value ||
            Math.Min(1000, animal.friendshipTowardFarmer.Value + 5) != request.ExpectedFriendshipAfter.Value ||
            statIncrements.Any(stat => Game1.stats.Get(stat.StatName) != stat.Before || stat.After != stat.Before + stat.Amount || stat.Amount != request.Quantity.Value))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(animal), "collect_animal_product_side_effect_projection_drifted"));
            return;
        }
        if (!AreAdjacent(stand, target) || !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(animal), "collect_animal_product_stand_tile_invalid"));
            return;
        }
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512), out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null || !TryInventoryItemMultiset(out var inventoryBefore))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(animal), "collect_animal_product_path_or_inventory_unavailable:" + pathReason));
            return;
        }
        activeAnimalProductHarvest = new ActiveAnimalProductHarvest(
            pending, location, animal, tool, target, stand, path, outputKey,
            inventoryBefore.TryGetValue(outputKey, out var beforeCount) ? beforeCount : 0,
            request.Quantity.Value, request.ExpectedOutputQuality.Value, Game1.player.Stamina,
            Game1.player.experiencePoints[Farmer.farmingSkill], animal.friendshipTowardFarmer.Value,
            statIncrements, Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512));
    }

    private void TickAnimalProductHarvest()
    {
        var active = activeAnimalProductHarvest;
        if (active is null)
        {
            return;
        }
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location) || active.ElapsedTicks > 3600)
        {
            CompleteAnimalProductBlocked(active, "collect_animal_product_world_location_or_timeout");
            return;
        }
        if (!active.Location.animals.TryGetValue(active.Animal.myID.Value, out var current) || !ReferenceEquals(current, active.Animal) ||
            (!active.BeginIssued && (active.Animal.currentProduce.Value is null || !active.Animal.isAdult() || !active.Animal.CanGetProduceWithTool(active.Tool))))
        {
            CompleteAnimalProductBlocked(active, "collect_animal_product_target_drifted");
            return;
        }

        if (!active.BeginIssued && active.Animal.TilePoint != active.Target)
        {
            active.ReplanCount++;
            if (active.ReplanCount > 16)
            {
                CompleteAnimalProductBlocked(active, "collect_animal_product_moving_target_replan_limit");
                return;
            }
            if (!TryReplanAnimalPath(active, out var replanReason))
            {
                CompleteAnimalProductBlocked(active, "collect_animal_product_moving_target_replan_failed:" + replanReason);
            }
            return;
        }
        if (!active.BeginIssued && !AreAdjacent(Game1.player.TilePoint, active.Target))
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteAnimalProductBlocked(active, "collect_animal_product_path_exhausted");
                return;
            }
            var next = active.Path[active.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                active.PathIndex++;
                return;
            }
            var direction = DirectionTo(Game1.player.TilePoint, next);
            StartMoving(direction);
            MovePlayerForTick();
            var playerTile = Game1.player.TilePoint;
            if (playerTile != active.LastObservedTile)
            {
                active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
                active.LastObservedTile = playerTile;
                if (active.MovementTiles > active.MaxMovementTiles)
                {
                    CompleteAnimalProductBlocked(active, "collect_animal_product_movement_budget_exceeded");
                    return;
                }
            }
            if (playerTile == next)
            {
                active.PathIndex++;
            }
            return;
        }

        StopAllMovement();
        if (!active.BeginIssued)
        {
            SelectTool(active.Tool);
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Animal.TilePoint));
            Game1.player.lastClick = active.Animal.Position + new Vector2(Game1.tileSize / 2f);
            Game1.player.BeginUsingTool();
            active.BeginIssued = true;
            var selectedAnimal = active.Tool switch
            {
                MilkPail pail => pail.animal,
                Shears shears => shears.animal,
                _ => null
            };
            if (!ReferenceEquals(selectedAnimal, active.Animal))
            {
                CompleteAnimalProductBlocked(active, "collect_animal_product_native_tool_selected_wrong_animal");
            }
            return;
        }
        if (!active.ReleaseIssued && Game1.player.UsingTool && Game1.player.canReleaseTool)
        {
            Game1.player.EndUsingTool();
            active.ReleaseIssued = true;
            return;
        }
        if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
        {
            return;
        }
        CompleteAnimalProduct(active);
    }

    private bool TryReplanAnimalPath(ActiveAnimalProductHarvest active, out string reason)
    {
        active.Target = active.Animal.TilePoint;
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

    private static IEnumerable<Point> AnimalAdjacentTiles(Point target)
    {
        yield return new Point(target.X, target.Y - 1);
        yield return new Point(target.X + 1, target.Y);
        yield return new Point(target.X, target.Y + 1);
        yield return new Point(target.X - 1, target.Y);
    }

    private void CompleteAnimalProduct(ActiveAnimalProductHarvest active)
    {
        activeAnimalProductHarvest = null;
        StopAllMovement();
        TryInventoryItemMultiset(out var inventoryAfter);
        var outputAfter = inventoryAfter.TryGetValue(active.OutputKey, out var count) ? count : 0;
        var xpDelta = Game1.player.experiencePoints[Farmer.farmingSkill] - active.FarmingExperienceBefore;
        var staminaDelta = Game1.player.Stamina - active.StaminaBefore;
        var friendshipAfter = active.Animal.friendshipTowardFarmer.Value;
        var statsVerified = active.StatIncrements.All(stat => Game1.stats.Get(stat.StatName) == stat.After);
        var verified = active.Animal.currentProduce.Value is null && outputAfter - active.OutputCountBefore == active.ExpectedQuantity &&
            xpDelta == active.Pending.Request.ExpectedSkillExperienceDelta!.Value && Math.Abs(staminaDelta - active.Pending.Request.ExpectedEnergyDelta!.Value) < 0.01f &&
            friendshipAfter == active.Pending.Request.ExpectedFriendshipAfter!.Value && statsVerified;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = active.Pending.Request.RunId,
            QueueId = active.Pending.Request.QueueId,
            QueueItemId = active.Pending.Request.QueueItemId,
            BeforeStateHash = active.Pending.Request.BeforeStateHash,
            OptionId = active.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = active.Location.NameOrUniqueName,
            TargetTileX = active.Target.X,
            TargetTileY = active.Target.Y,
            ActualTicks = active.ElapsedTicks,
            TrainingImpactScope = "executor_calibration",
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "collect_animal_product",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_animal_tool_lifecycle_collected_exact_product", "inventory_energy_friendship_farming_xp_and_stats_verified" }
                : new[] { "animal_product_postcondition_mismatch" },
            RequestedEffect = "animal.current_produce=null;inventory_delta=" + active.ExpectedQuantity + ";farming_xp_delta=5;energy_delta=-4;friendship_delta=5",
            ObservedEffect = AnimalProductObservedEffect(active.Animal) + ";inventory_delta=" + (outputAfter - active.OutputCountBefore) + ";farming_xp_delta=" + xpDelta + ";energy_delta=" + staminaDelta.ToString("0.###", CultureInfo.InvariantCulture),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "collect_animal_product_postcondition_mismatch" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "farm.animals[" + active.Animal.myID.Value + "].current_produce", Before = active.Pending.Request.QualifiedItemId, After = active.Animal.currentProduce.Value ?? "null" },
                new SimulatedFactChange { Path = "player.inventory[" + active.Pending.Request.QualifiedItemId + "]", Before = active.OutputCountBefore.ToString(), After = outputAfter.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = active.StaminaBefore.ToString("0.###", CultureInfo.InvariantCulture), After = Game1.player.Stamina.ToString("0.###", CultureInfo.InvariantCulture) },
                new SimulatedFactChange { Path = "player.skills.farming.experience", Before = active.FarmingExperienceBefore.ToString(), After = Game1.player.experiencePoints[Farmer.farmingSkill].ToString() },
                new SimulatedFactChange { Path = "farm.animals[" + active.Animal.myID.Value + "].friendship", Before = active.FriendshipBefore.ToString(), After = friendshipAfter.ToString() }
            }
        });
    }

    private void CompleteAnimalProductBlocked(ActiveAnimalProductHarvest active, string reason)
    {
        activeAnimalProductHarvest = null;
        StopAllMovement();
        Game1.player.completelyStopAnimatingOrDoingAction();
        active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "collect_animal_product", "animal.current_produce=null", AnimalProductObservedEffect(active.Animal), reason));
    }

    private static bool TryParseAnimalStatIncrements(string json, out ExpectedAnimalStatIncrement[] increments)
    {
        try
        {
            increments = JsonSerializer.Deserialize<ExpectedAnimalStatIncrement[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? Array.Empty<ExpectedAnimalStatIncrement>();
            return increments.All(item => !string.IsNullOrWhiteSpace(item.StatName));
        }
        catch
        {
            increments = Array.Empty<ExpectedAnimalStatIncrement>();
            return false;
        }
    }

    private static string AnimalProductObservedEffect(FarmAnimal animal)
    {
        return "animal_id=" + animal.myID.Value + ";location=" + (animal.currentLocation?.NameOrUniqueName ?? string.Empty) +
            ";tile=" + animal.TilePoint.X + "," + animal.TilePoint.Y + ";current_produce=" + (animal.currentProduce.Value ?? "null") +
            ";quality=" + animal.produceQuality.Value + ";friendship=" + animal.friendshipTowardFarmer.Value;
    }
}
