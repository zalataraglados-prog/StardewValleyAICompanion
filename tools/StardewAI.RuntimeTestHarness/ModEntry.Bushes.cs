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
    private void StartBushHarvest(PendingExecution pending)
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
            string.IsNullOrWhiteSpace(request.QualifiedItemId) || !request.Quantity.HasValue ||
            !request.ExpectedOutputQuality.HasValue || !request.ExpectedForagingExperienceDelta.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_bush", "bush.tile_sheet_offset=0", "request=missing_typed_fields", "harvest_bush_typed_target_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_bush", "bush.tile_sheet_offset=0", "player=busy_or_menu_open", "harvest_bush_player_busy"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var interaction = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var bush = location.largeTerrainFeatures
            .OfType<Bush>()
            .FirstOrDefault(candidate => (int)candidate.Tile.X == target.X && (int)candidate.Tile.Y == target.Y);
        var projection = bush is null ? null : ProjectBushHarvest(location, bush);
        var precheck = ValidateBushHarvestTarget(location, bush, projection, target, interaction, stand, request);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_bush", "bush.tile_sheet_offset=0", BushHarvestObservedEffect(location, bush, target, request.QualifiedItemId, request.ExpectedOutputQuality.Value), precheck));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_bush", "bush.tile_sheet_offset=0", BushHarvestObservedEffect(location, bush, target, request.QualifiedItemId, request.ExpectedOutputQuality.Value), "harvest_bush_path_unavailable:" + pathReason));
            return;
        }

        activeBushHarvest = new ActiveBushHarvest(
            pending,
            location,
            bush!,
            target,
            interaction,
            stand,
            path,
            projection!.Branch,
            projection.QualifiedItemId,
            projection.Quantity,
            projection.Quality,
            projection.ForagingExperience,
            projection.NutKey,
            maxMovementTiles);
    }

    private static string[] ValidateBushHarvestTarget(
        GameLocation location,
        Bush? bush,
        BushHarvestProjection? projection,
        Point target,
        Point interaction,
        Point stand,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (bush is null || bush.GetType() != typeof(Bush) || projection is null)
        {
            return new[] { "harvest_bush_target_not_exact_vanilla_bush" };
        }
        if (!string.Equals(projection.Status, "ready", StringComparison.Ordinal))
        {
            reasons.Add("harvest_bush_not_ready:" + projection.Status);
        }
        if (!string.IsNullOrWhiteSpace(request.TargetRuntimeType) &&
            !string.Equals(request.TargetRuntimeType, typeof(Bush).FullName, StringComparison.Ordinal))
        {
            reasons.Add("harvest_bush_runtime_type_drifted");
        }
        if (!AreAdjacent(stand, interaction) ||
            !bush.getBoundingBox().Contains(interaction.X * Game1.tileSize + Game1.tileSize / 2, interaction.Y * Game1.tileSize + Game1.tileSize / 2) ||
            bush.getBoundingBox().Contains(stand.X * Game1.tileSize + Game1.tileSize / 2, stand.Y * Game1.tileSize + Game1.tileSize / 2) ||
            !IsTileOnMap(location, stand) || !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
        {
            reasons.Add("harvest_bush_interaction_geometry_drifted");
        }
        if (!string.Equals(request.QualifiedItemId, projection.QualifiedItemId, StringComparison.OrdinalIgnoreCase) ||
            request.Quantity != projection.Quantity || request.ExpectedOutputQuality != projection.Quality ||
            request.ExpectedForagingExperienceDelta != projection.ForagingExperience)
        {
            reasons.Add("harvest_bush_output_projection_drifted");
        }
        if ((int)bush.Tile.X != target.X || (int)bush.Tile.Y != target.Y)
        {
            reasons.Add("harvest_bush_anchor_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickBushHarvest()
    {
        if (activeBushHarvest is null)
        {
            return;
        }

        var active = activeBushHarvest;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteBushHarvestBlocked(active, "harvest_bush_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteBushHarvestBlocked(active, "harvest_bush_timeout");
            return;
        }
        if (!active.Location.largeTerrainFeatures.Contains(active.Bush))
        {
            CompleteBushHarvestBlocked(active, "harvest_bush_target_removed_during_execution");
            return;
        }
        if (active.ActionIssued)
        {
            if (BushHarvestPostconditionsMet(active))
            {
                CompleteBushHarvest(active);
            }
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteBushHarvestBlocked(active, "harvest_bush_player_busy_during_execution");
            return;
        }

        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteBushHarvestBlocked(active, "harvest_bush_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteBushHarvestBlocked(active, "harvest_bush_path_exhausted_before_stand");
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
                CompleteBushHarvestBlocked(active, "harvest_bush_dynamic_path_blocked");
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
                CompleteBushHarvestBlocked(active, "harvest_bush_movement_stuck");
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
            CompleteBushHarvestBlocked(active, "harvest_bush_native_action_not_handled");
        }
        else if (BushHarvestPostconditionsMet(active))
        {
            CompleteBushHarvest(active);
        }
    }

    private static bool BushHarvestPostconditionsMet(ActiveBushHarvest active)
    {
        var outputDelta = CountBushOutput(active.Location, active.QualifiedItemId, active.ExpectedQuality) - active.OutputCountBefore;
        var xpDelta = Game1.player.experiencePoints[Farmer.foragingSkill] - active.ForagingExperienceBefore;
        var nutCollected = string.IsNullOrWhiteSpace(active.NutKey) || Game1.player.team.collectedNutTracker.Contains(active.NutKey);
        return active.Bush.tileSheetOffset.Value == 0 && outputDelta == active.ExpectedQuantity &&
            xpDelta == active.ExpectedForagingExperience && nutCollected;
    }

    private void CompleteBushHarvest(ActiveBushHarvest active)
    {
        StopAllMovement();
        activeBushHarvest = null;
        var request = active.Pending.Request;
        var outputAfter = CountBushOutput(active.Location, active.QualifiedItemId, active.ExpectedQuality);
        var xpAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var nutAfter = !string.IsNullOrWhiteSpace(active.NutKey) && Game1.player.team.collectedNutTracker.Contains(active.NutKey);
        active.Pending.Completion.SetResult(new TrainingExecutionResult
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
            PrimitiveKind = "harvest_bush",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_checkAction_invoked_exact_bush", "native_bush_offset_and_output_delta_verified", "branch_xp_and_nut_tracker_contract_verified" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = BushHarvestObservedEffect(active.Location, active.Bush, active.Target, active.QualifiedItemId, active.ExpectedQuality),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.large_terrain_features[" + active.Target.X + "," + active.Target.Y + "].tile_sheet_offset", Before = active.TileSheetOffsetBefore.ToString(), After = active.Bush.tileSheetOffset.Value.ToString() },
                new SimulatedFactChange { Path = "current_location.output.count[" + active.QualifiedItemId + ",quality=" + active.ExpectedQuality + "]", Before = active.OutputCountBefore.ToString(), After = outputAfter.ToString() },
                new SimulatedFactChange { Path = "player.skills.foraging.experience", Before = active.ForagingExperienceBefore.ToString(), After = xpAfter.ToString() },
                new SimulatedFactChange { Path = "world_progress.collected_nut_tracker[" + active.NutKey + "]", Before = active.NutCollectedBefore.ToString().ToLowerInvariant(), After = nutAfter.ToString().ToLowerInvariant() }
            }
        });
    }

    private void CompleteBushHarvestBlocked(ActiveBushHarvest active, string reason)
    {
        StopAllMovement();
        activeBushHarvest = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "harvest_bush",
            active.RequestedEffect,
            BushHarvestObservedEffect(active.Location, active.Bush, active.Target, active.QualifiedItemId, active.ExpectedQuality),
            reason));
    }

    private static BushHarvestProjection ProjectBushHarvest(GameLocation location, Bush bush)
    {
        var size = bush.size.Value;
        var branch = size switch { 3 => "tea_leaf", 4 => "golden_walnut", _ => "ordinary_berry" };
        var outputId = bush.GetType() == typeof(Bush) ? bush.GetShakeOffItem() ?? string.Empty : string.Empty;
        var quantity = size is 3 or 4 ? 1 : 1 + Game1.player.ForagingLevel / 4;
        var quality = size is 3 or 4 ? 0 : Game1.player.professions.Contains(16) ? 4 : 0;
        var xp = size is 3 or 4 ? 0 : quantity;
        var nutKey = size == 4 ? "Bush_" + location.Name + "_" + bush.Tile.X + "_" + bush.Tile.Y : string.Empty;
        var nutCollected = size == 4 && Game1.player.team.collectedNutTracker.Contains(nutKey);
        var status = bush.GetType() != typeof(Bush) ? "custom_bush_runtime_type" :
            bush.townBush.Value ? "town_bush_not_harvestable" :
            !bush.readyForHarvest() ? "bush_not_ready" :
            !bush.inBloom() ? "bush_not_in_bloom" :
            bush.shakeTimer > 0f ? "bush_shake_cooldown_active" :
            string.IsNullOrWhiteSpace(outputId) ? "bush_output_identity_unavailable" :
            nutCollected ? "golden_walnut_already_collected" : "ready";
        return new BushHarvestProjection(status, branch, outputId, quantity, quality, xp, nutKey);
    }

    private static int CountBushOutput(GameLocation location, string qualifiedItemId, int quality)
    {
        var debrisCount = location.debris
            .Where(debris => string.Equals(DebrisQualifiedItemId(debris), qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                (debris.item?.Quality ?? debris.itemQuality) == quality)
            .Sum(debris => debris.item?.Stack ?? Math.Max(1, debris.Chunks.Count));
        return debrisCount + CountInventoryItemAtQuality(qualifiedItemId, quality);
    }

    private static string BushHarvestObservedEffect(GameLocation location, Bush? bush, Point target, string qualifiedItemId, int quality)
    {
        return "location=" + location.NameOrUniqueName +
            ";target=" + target.X + "," + target.Y +
            ";bush_present=" + (bush is not null && location.largeTerrainFeatures.Contains(bush)).ToString().ToLowerInvariant() +
            ";tile_sheet_offset=" + (bush?.tileSheetOffset.Value.ToString() ?? "missing") +
            ";output_count=" + CountBushOutput(location, qualifiedItemId, quality) +
            ";foraging_xp=" + Game1.player.experiencePoints[Farmer.foragingSkill];
    }

    private sealed record BushHarvestProjection(
        string Status,
        string Branch,
        string QualifiedItemId,
        int Quantity,
        int Quality,
        int ForagingExperience,
        string NutKey);
}
