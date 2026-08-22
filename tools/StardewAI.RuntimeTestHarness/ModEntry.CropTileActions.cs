using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void StartAdjacentTileAction(PendingExecution pending, string primitiveKind)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, "current_location.adjacent_tile_action=applied", "target_tile=missing", "target_tile_required"));
            return;
        }

        var location = Game1.currentLocation;
        if (!string.IsNullOrWhiteSpace(request.LocationId) &&
            !string.Equals(request.LocationId, location.NameOrUniqueName, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, "current_location.adjacent_tile_action=applied", "location=" + location.NameOrUniqueName, "wrong_location"));
            return;
        }

        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var path = BuildAdjacentToolPath(location, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, primitiveKind, "current_location.adjacent_tile_action=applied", "target=" + target.X + "," + target.Y, moveReason));
            return;
        }

        activeAdjacentTileAction = new ActiveAdjacentTileAction(pending, primitiveKind, location.NameOrUniqueName, target, path);
    }

    private void TickAdjacentTileAction()
    {
        if (activeAdjacentTileAction is not { } action)
        {
            return;
        }

        action.ElapsedTicks++;
        var location = Game1.currentLocation;
        if (!Context.IsWorldReady || location is null ||
            !string.Equals(location.NameOrUniqueName, action.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            CompleteAdjacentTileActionBlocked(action, "location_changed_during_adjacent_action");
            return;
        }
        if (action.ElapsedTicks > action.MaxTicks)
        {
            CompleteAdjacentTileActionBlocked(action, "adjacent_action_movement_timeout");
            return;
        }

        if (!AreAdjacent(Game1.player.TilePoint, action.Target))
        {
            if (action.PathIndex >= action.Path.Count)
            {
                CompleteAdjacentTileActionBlocked(action, "adjacent_action_target_not_adjacent");
                return;
            }

            var next = action.Path[action.PathIndex];
            if (Game1.player.TilePoint == next)
            {
                action.PathIndex++;
                action.StuckTicks = 0;
                action.LastPosition = Game1.player.Position;
                return;
            }
            if (!IsTileWalkable(location, next) || IsTileOccupiedByCharacter(location, next))
            {
                CompleteAdjacentTileActionBlocked(action, "adjacent_action_route_drifted");
                return;
            }

            var direction = DirectionTo(Game1.player.TilePoint, next);
            var moved = Vector2.DistanceSquared(action.LastPosition, Game1.player.Position) >= 0.01f;
            action.LastPosition = Game1.player.Position;
            StartMoving(direction);
            MovePlayerForTick();
            if (Game1.player.TilePoint == next)
            {
                action.PathIndex++;
            }
            action.StuckTicks = moved ? 0 : action.StuckTicks + 1;
            if (action.StuckTicks > 45)
            {
                CompleteAdjacentTileActionBlocked(action, "adjacent_action_movement_stuck");
            }
            return;
        }

        StopAllMovement();
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, action.Target));
        activeAdjacentTileAction = null;
        TrainingExecutionResult result;
        try
        {
            result = action.PrimitiveKind switch
            {
                "plant_seed" => ExecutePlantSeed(action.Pending.Request),
                "apply_fertilizer" => ExecuteApplyFertilizer(action.Pending.Request),
                "apply_tree_treatment" => ExecuteApplyTreeTreatment(action.Pending.Request),
                "place_cookout_kit" => ExecutePlaceCookoutKit(action.Pending.Request),
                "place_crab_pot" => ExecutePlaceCrabPot(action.Pending.Request),
                "load_crab_pot_bait" => ExecuteLoadCrabPotBait(action.Pending.Request),
                "harvest_crop" => ExecuteHarvestCrop(action.Pending.Request),
                _ => BlockedWithPrimitive(action.Pending.Request, action.PrimitiveKind, "current_location.adjacent_tile_action=applied", "unsupported", "unsupported_adjacent_tile_action")
            };
        }
        catch (Exception ex)
        {
            Monitor.Log($"Adjacent tile action '{action.PrimitiveKind}' failed: {ex}", LogLevel.Error);
            result = BlockedWithPrimitive(
                action.Pending.Request,
                action.PrimitiveKind,
                "current_location.adjacent_tile_action=applied",
                "exception=" + ex.GetType().Name,
                "adjacent_action_execution_exception:" + ex.GetType().Name);
        }
        action.Pending.Completion.SetResult(result);
    }

    private void CompleteAdjacentTileActionBlocked(ActiveAdjacentTileAction action, string reason)
    {
        StopAllMovement();
        activeAdjacentTileAction = null;
        action.Pending.Completion.SetResult(BlockedWithPrimitive(
            action.Pending.Request,
            action.PrimitiveKind,
            "current_location.adjacent_tile_action=applied",
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") + ";target=" + action.Target.X + "," + action.Target.Y,
            reason));
    }

    private TrainingExecutionResult ExecuteApplyFertilizer(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        var target = new Vector2(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var dirt = location.GetHoeDirtAtTile(target);
        var qualifiedItemId = request.QualifiedItemId;
        var slotIndex = request.SlotIndex;
        var requested = "current_location.planting_context[" + (int)target.X + "," + (int)target.Y + "].fertilizer_id=" + qualifiedItemId;
        if (dirt is null)
        {
            return BlockedWithPrimitive(request, "apply_fertilizer", requested, "has_hoe_dirt=false", "apply_fertilizer_target_not_hoe_dirt");
        }
        if (!slotIndex.HasValue || slotIndex.Value < 0 || slotIndex.Value >= Game1.player.Items.Count ||
            Game1.player.Items[slotIndex.Value] is not StardewObject fertilizer ||
            fertilizer.Category != StardewObject.fertilizerCategory ||
            !string.Equals(fertilizer.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "apply_fertilizer", requested, "inventory_identity_mismatch", "apply_fertilizer_inventory_identity_drift");
        }
        if (!dirt.CanApplyFertilizer(qualifiedItemId))
        {
            return BlockedWithPrimitive(request, "apply_fertilizer", requested, "apply_status=" + dirt.CheckApplyFertilizerRules(qualifiedItemId), "apply_fertilizer_native_rule_blocked");
        }

        var beforeStack = fertilizer.Stack;
        var beforeFertilizer = dirt.fertilizer.Value ?? string.Empty;
        var indoorPot = location.objects.TryGetValue(target, out var targetObject)
            ? targetObject as IndoorPot
            : null;
        var previousSlot = Game1.player.CurrentToolIndex;
        var applied = false;
        try
        {
            Game1.player.CurrentToolIndex = slotIndex.Value;
            if (!ReferenceEquals(Game1.player.ActiveObject, fertilizer))
            {
                return BlockedWithPrimitive(request, "apply_fertilizer", requested, "active_object_identity_mismatch", "apply_fertilizer_active_slot_drift");
            }

            applied = indoorPot is not null
                ? indoorPot.performObjectDropInAction(fertilizer, probe: false, Game1.player)
                : fertilizer.placementAction(location, (int)target.X * Game1.tileSize, (int)target.Y * Game1.tileSize, Game1.player);
            if (applied)
            {
                ConsumeOneInventoryItem(slotIndex.Value);
            }
        }
        finally
        {
            Game1.player.CurrentToolIndex = previousSlot;
        }
        var afterStack = Game1.player.Items.ElementAtOrDefault(slotIndex.Value)?.Stack ?? 0;
        var afterFertilizer = dirt.fertilizer.Value ?? string.Empty;
        var verified = applied && string.Equals(afterFertilizer, qualifiedItemId, StringComparison.OrdinalIgnoreCase) && afterStack == beforeStack - 1;
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
            PrimitiveKind = "apply_fertilizer",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    indoorPot is null
                        ? "native_Object_placementAction_applied_fertilizer"
                        : "native_IndoorPot_performObjectDropInAction_applied_fertilizer",
                    "exact_fertilizer_stack_decreased"
                }
                : new[] { "apply_fertilizer_post_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = "fertilizer_before=" + beforeFertilizer + ";fertilizer_after=" + afterFertilizer + ";stack_before=" + beforeStack + ";stack_after=" + afterStack,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "apply_fertilizer_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "current_location.planting_context[" + (int)target.X + "," + (int)target.Y + "].fertilizer_id", Before = beforeFertilizer, After = afterFertilizer },
                    new SimulatedFactChange { Path = "player.inventory[" + slotIndex.Value + "].stack", Before = beforeStack.ToString(), After = afterStack.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
