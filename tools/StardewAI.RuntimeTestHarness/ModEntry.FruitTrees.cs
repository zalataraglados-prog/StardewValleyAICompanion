using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string FruitTreeHarvestNativeContract =
        "GameLocation.checkAction -> FruitTree.performUseAction -> FruitTree.shake; no direct fruit, debris, inventory, or skill mutation";

    private void StartFruitTreeHarvest(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.InteractionTileX.HasValue || !request.InteractionTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.ExpectedFruitCountBefore.HasValue || !request.ExpectedFruitCountAfter.HasValue ||
            !request.ExpectedForagingExperienceDelta.HasValue ||
            string.IsNullOrWhiteSpace(request.ExpectedOutputItemsJson))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                "request=missing_typed_fields",
                "harvest_fruit_tree_typed_target_fields_required"));
            return;
        }
        if (!string.Equals(request.FruitTreeNativeContract, FruitTreeHarvestNativeContract, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                "fruit_tree_native_contract=" + request.FruitTreeNativeContract,
                "harvest_fruit_tree_native_contract_mismatch"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                "player=busy_or_menu_open",
                "harvest_fruit_tree_player_busy"));
            return;
        }
        if (!TryParseFruitTreeOutputs(request.ExpectedOutputItemsJson, out var requestedOutputs))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                "expected_output_items_json=invalid",
                "harvest_fruit_tree_output_projection_invalid"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var interaction = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var tree = location.terrainFeatures.TryGetValue(target.ToVector2(), out var feature)
            ? feature as FruitTree
            : null;
        var projection = tree is null ? null : ProjectFruitTreeHarvest(tree);
        var precheck = ValidateFruitTreeHarvestTarget(
            location,
            tree,
            projection,
            requestedOutputs,
            target,
            interaction,
            stand,
            request);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                FruitTreeHarvestObservedEffect(location, tree, target, requestedOutputs),
                precheck));
            return;
        }

        var outputIds = requestedOutputs.Select(output => output.QualifiedItemId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var resourceQuestReason = ValidateQuestResourceSourceTarget(request, outputIds);
        if (!string.IsNullOrWhiteSpace(resourceQuestReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                FruitTreeHarvestObservedEffect(location, tree, target, requestedOutputs),
                resourceQuestReason));
            return;
        }
        if (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
            !ValidateSpecialOrderCollectSourceTarget(request, request.QualifiedItemId, out var questReason))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                FruitTreeHarvestObservedEffect(location, tree, target, requestedOutputs),
                questReason));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(
            location,
            Game1.player.TilePoint,
            stand,
            maxMovementTiles,
            out var pathReason,
            avoidSoftObstacles: true,
            allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "harvest_fruit_tree",
                "fruit_tree.fruit_count=0",
                FruitTreeHarvestObservedEffect(location, tree, target, requestedOutputs),
                "harvest_fruit_tree_path_unavailable:" + pathReason));
            return;
        }

        activeFruitTreeHarvest = new ActiveFruitTreeHarvest(
            pending,
            location,
            tree!,
            target,
            interaction,
            stand,
            path,
            projection!.Outputs,
            maxMovementTiles);
    }

    private static string[] ValidateFruitTreeHarvestTarget(
        GameLocation location,
        FruitTree? tree,
        FruitTreeHarvestProjection? projection,
        IReadOnlyList<FruitTreeOutputExpectation> requestedOutputs,
        Point target,
        Point interaction,
        Point stand,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (tree is null || tree.GetType() != typeof(FruitTree) || projection is null)
        {
            return new[] { "harvest_fruit_tree_target_not_exact_vanilla_fruit_tree" };
        }
        if (!string.Equals(projection.Status, "ready", StringComparison.Ordinal))
        {
            reasons.Add("harvest_fruit_tree_not_ready:" + projection.Status);
        }
        if (!string.IsNullOrWhiteSpace(request.TargetRuntimeType) &&
            !string.Equals(request.TargetRuntimeType, typeof(FruitTree).FullName, StringComparison.Ordinal))
        {
            reasons.Add("harvest_fruit_tree_runtime_type_drifted");
        }
        if (interaction != target || !AreAdjacent(stand, interaction) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) ||
            IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("harvest_fruit_tree_interaction_geometry_drifted");
        }
        if (!string.Equals(request.FruitTreeId, tree.treeId.Value, StringComparison.Ordinal) ||
            request.ExpectedFruitCountBefore != tree.fruit.Count ||
            request.ExpectedFruitCountAfter != 0 ||
            request.ExpectedForagingExperienceDelta != 0 ||
            !FruitTreeOutputsEqual(requestedOutputs, projection.Outputs))
        {
            reasons.Add("harvest_fruit_tree_output_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickFruitTreeHarvest()
    {
        if (activeFruitTreeHarvest is null)
        {
            return;
        }

        var active = activeFruitTreeHarvest;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_timeout");
            return;
        }
        if (!active.Location.terrainFeatures.TryGetValue(active.Target.ToVector2(), out var feature) ||
            !ReferenceEquals(feature, active.Tree))
        {
            CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_target_removed_during_execution");
            return;
        }
        if (active.ActionIssued)
        {
            if (FruitTreeHarvestPostconditionsMet(active))
            {
                CompleteFruitTreeHarvest(active);
            }
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_player_busy_during_execution");
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_path_exhausted_before_stand");
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
                CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_dynamic_path_blocked");
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
                CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_movement_stuck");
            }
            else if (moved)
            {
                active.StuckTicks = 0;
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(playerTile, active.Interaction));
        var handled = active.Location.checkAction(
            new TileLocation(active.Interaction.X, active.Interaction.Y),
            new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
            Game1.player);
        active.ActionIssued = true;
        if (!handled)
        {
            CompleteFruitTreeHarvestBlocked(active, "harvest_fruit_tree_native_action_not_handled");
        }
        else if (FruitTreeHarvestPostconditionsMet(active))
        {
            CompleteFruitTreeHarvest(active);
        }
    }

    private static bool FruitTreeHarvestPostconditionsMet(ActiveFruitTreeHarvest active)
    {
        var xpDelta = Game1.player.experiencePoints[Farmer.foragingSkill] - active.ForagingExperienceBefore;
        return active.Tree.fruit.Count == 0 && xpDelta == 0 && active.Outputs.All(output =>
            CountFruitTreeOutput(active.Location, output.QualifiedItemId, output.Quality) -
            active.OutputCountsBefore[output.Key] == output.Quantity);
    }

    private void CompleteFruitTreeHarvest(ActiveFruitTreeHarvest active)
    {
        StopAllMovement();
        activeFruitTreeHarvest = null;
        var request = active.Pending.Request;
        var changedFacts = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "current_location.terrain_features[" + active.Target.X + "," + active.Target.Y + "].fruit_count",
                Before = active.FruitCountBefore.ToString(),
                After = active.Tree.fruit.Count.ToString()
            },
            new()
            {
                Path = "player.skills.foraging.experience",
                Before = active.ForagingExperienceBefore.ToString(),
                After = Game1.player.experiencePoints[Farmer.foragingSkill].ToString()
            }
        };
        changedFacts.AddRange(active.Outputs.Select(output => new SimulatedFactChange
        {
            Path = "current_location.output.count[" + output.QualifiedItemId + ",quality=" + output.Quality + "]",
            Before = active.OutputCountsBefore[output.Key].ToString(),
            After = CountFruitTreeOutput(active.Location, output.QualifiedItemId, output.Quality).ToString()
        }));
        var result = new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "executor_calibration",
            PrimitiveKind = "harvest_fruit_tree",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_checkAction_invoked_exact_fruit_tree",
                "native_fruit_list_clear_verified",
                "all_projected_item_quality_quantity_deltas_verified",
                "zero_foraging_xp_delta_verified"
            },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = FruitTreeHarvestObservedEffect(active.Location, active.Tree, active.Target, active.Outputs),
            ChangedFacts = changedFacts.ToArray()
        };
        ApplyQuestResourceSourceFeedback(result, request);
        ApplySpecialOrderCollectSourceFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompleteFruitTreeHarvestBlocked(ActiveFruitTreeHarvest active, string reason)
    {
        StopAllMovement();
        activeFruitTreeHarvest = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "harvest_fruit_tree",
            active.RequestedEffect,
            FruitTreeHarvestObservedEffect(active.Location, active.Tree, active.Target, active.Outputs),
            reason));
    }

    private static FruitTreeHarvestProjection ProjectFruitTreeHarvest(FruitTree tree)
    {
        var quality = tree.GetQuality();
        var lightning = tree.struckByLightningCountdown.Value > 0;
        var outputs = tree.fruit
            .Where(item => item is not null)
            .Select(item => new FruitTreeOutputExpectation(
                lightning ? "(O)382" : item.QualifiedItemId,
                quality,
                lightning ? 1 : Math.Max(1, item.Stack)))
            .GroupBy(output => output.Key, StringComparer.Ordinal)
            .Select(group => new FruitTreeOutputExpectation(
                group.First().QualifiedItemId,
                group.First().Quality,
                group.Sum(output => output.Quantity)))
            .OrderBy(output => output.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(output => output.Quality)
            .ToArray();
        var status = tree.GetType() != typeof(FruitTree) ? "custom_fruit_tree_runtime_type" :
            tree.stump.Value ? "fruit_tree_is_stump" :
            tree.growthStage.Value < FruitTree.treeStage ? "fruit_tree_not_mature" :
            tree.fruit.Count == 0 ? "fruit_tree_has_no_fruit" :
            tree.fruit.Any(item => item is null) ? "fruit_tree_contains_transient_null_fruit" :
            tree.maxShake != 0f ? "fruit_tree_shake_in_progress" :
            outputs.Length == 0 ? "fruit_tree_output_projection_unavailable" : "ready";
        return new FruitTreeHarvestProjection(status, outputs);
    }

    private static bool TryParseFruitTreeOutputs(
        string json,
        out IReadOnlyList<FruitTreeOutputExpectation> outputs)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                outputs = Array.Empty<FruitTreeOutputExpectation>();
                return false;
            }
            var parsed = document.RootElement.EnumerateArray()
                .Select(row => new FruitTreeOutputExpectation(
                    row.GetProperty("qualified_item_id").GetString() ?? string.Empty,
                    row.GetProperty("quality").GetInt32(),
                    row.GetProperty("quantity").GetInt32()))
                .Where(output => !string.IsNullOrWhiteSpace(output.QualifiedItemId) && output.Quantity > 0)
                .OrderBy(output => output.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(output => output.Quality)
                .ToArray();
            outputs = parsed;
            return parsed.Length > 0;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            outputs = Array.Empty<FruitTreeOutputExpectation>();
            return false;
        }
    }

    private static bool FruitTreeOutputsEqual(
        IReadOnlyList<FruitTreeOutputExpectation> left,
        IReadOnlyList<FruitTreeOutputExpectation> right)
    {
        return left.Count == right.Count && left
            .OrderBy(output => output.Key, StringComparer.Ordinal)
            .SequenceEqual(right.OrderBy(output => output.Key, StringComparer.Ordinal));
    }

    private static int CountFruitTreeOutput(GameLocation location, string qualifiedItemId, int quality)
    {
        var debrisCount = location.debris
            .Where(debris => string.Equals(DebrisQualifiedItemId(debris), qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                (debris.item?.Quality ?? debris.itemQuality) == quality)
            .Sum(debris => debris.item?.Stack ?? Math.Max(1, debris.Chunks.Count));
        return debrisCount + CountInventoryItemAtQuality(qualifiedItemId, quality);
    }

    private static string FruitTreeHarvestObservedEffect(
        GameLocation location,
        FruitTree? tree,
        Point target,
        IReadOnlyList<FruitTreeOutputExpectation> outputs)
    {
        return "location=" + location.NameOrUniqueName +
            ";target=" + target.X + "," + target.Y +
            ";fruit_tree_present=" + (tree is not null && location.terrainFeatures.ContainsKey(target.ToVector2())).ToString().ToLowerInvariant() +
            ";fruit_count=" + (tree?.fruit.Count.ToString() ?? "missing") +
            ";outputs=" + string.Join(",", outputs.Select(output => output.Key + "=" + CountFruitTreeOutput(location, output.QualifiedItemId, output.Quality))) +
            ";foraging_xp=" + Game1.player.experiencePoints[Farmer.foragingSkill];
    }

    private static string FruitTreeOutputsJson(IReadOnlyList<FruitTreeOutputExpectation> outputs)
    {
        return JsonSerializer.Serialize(outputs.Select(output => new
        {
            qualified_item_id = output.QualifiedItemId,
            quality = output.Quality,
            quantity = output.Quantity
        }));
    }

    private sealed record FruitTreeOutputExpectation(
        string QualifiedItemId,
        int Quality,
        int Quantity)
    {
        public string Key => QualifiedItemId + "|" + Quality;
    }

    private sealed record FruitTreeHarvestProjection(
        string Status,
        IReadOnlyList<FruitTreeOutputExpectation> Outputs);
}
