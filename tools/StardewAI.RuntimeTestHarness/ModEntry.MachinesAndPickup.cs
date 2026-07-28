using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private void StartPickupDebris(PendingExecution pending)
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
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", "location.debris[target].chunk_count_decreases_or_removed=true", "target_tile=missing", "target_tile_required"));
            return;
        }
        if (activePickupDebris is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), "executor=busy", "pickup_debris_executor_busy"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), "player=busy_or_menu_open", "pickup_debris_tool_or_menu_conflict"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var beforeObserved = DebrisObservedEffect(location, target, request.DebrisIndex);
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_item_identity_required"));
            return;
        }
        var debris = DebrisAt(location, target, request.DebrisIndex, request.QualifiedItemId);
        var chunk = debris is null ? null : DebrisChunkAt(debris, target);
        if (debris is null || chunk is null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_target_not_found"));
            return;
        }

        var itemId = DebrisQualifiedItemId(debris);
        if (!string.Equals(itemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_item_mismatch"));
            return;
        }
        var questReceiptReason = ValidateQuestResourceReceiptTarget(request, itemId);
        if (questReceiptReason is not null)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, questReceiptReason));
            return;
        }
        if (!Game1.player.couldInventoryAcceptThisItem(debris.item ?? ItemRegistry.Create(itemId, 1, debris.itemQuality)))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "pickup_debris", DebrisRequestedEffect(request), beforeObserved, "pickup_debris_inventory_cannot_accept_item"));
            return;
        }
        activePickupDebris = new ActivePickupDebris(
            pending,
            location,
            debris,
            chunk,
            target,
            itemId,
            location.debris.Count,
            debris.Chunks.Count,
            CountInventoryItem(itemId),
            InventoryStackSignature(),
            DebrisRequestedEffect(request));
    }

    private void TickPickupDebris()
    {
        if (activePickupDebris is null)
        {
            return;
        }

        var active = activePickupDebris;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_location_changed");
            return;
        }
        if (active.ElapsedTicks - active.CombatInterruptedTicks > active.MaxTicks)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_natural_collection_timeout");
            return;
        }

        var debrisStillPresent = active.Location.debris.Contains(active.Debris);
        var chunkStillPresent = debrisStillPresent && active.Debris.Chunks.Contains(active.Chunk);
        var itemCountAfter = CountInventoryItem(active.QualifiedItemId);
        if ((!debrisStillPresent || !chunkStillPresent) && itemCountAfter > active.ItemCountBefore)
        {
            CompletePickupDebris(active, itemCountAfter);
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

        if (!chunkStillPresent)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_removed_without_inventory_gain");
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompletePickupDebrisBlocked(active, "pickup_debris_tool_or_menu_conflict_during_move");
            return;
        }

        var target = DebrisChunkTile(active.Chunk);
        if (Game1.player.TilePoint == target)
        {
            StopAllMovement();
            active.WaitAtTargetTicks++;
            if (active.WaitAtTargetTicks > 120)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.WaitAtTargetTicks = 0;
            }
            return;
        }

        if (active.PathIndex >= active.Path.Count || active.PathTarget != target)
        {
            var path = TryBuildTilePath(active.Location, Game1.player.TilePoint, target, 512, out var pathReason, avoidSoftObstacles: true);
            if (path is null)
            {
                active.PathFailures++;
                if (active.PathFailures > 90)
                {
                    CompletePickupDebrisBlocked(active, "pickup_debris_dynamic_path_unavailable:" + pathReason);
                }
                return;
            }
            active.Path = path;
            active.PathIndex = 0;
            active.PathTarget = target;
            active.PathFailures = 0;
        }

        if (active.PathIndex >= active.Path.Count)
        {
            return;
        }
        var next = active.Path[active.PathIndex];
        if (Game1.player.TilePoint == next)
        {
            active.PathIndex++;
            return;
        }
        if (!IsTileWalkable(active.Location, next) || IsTileOccupiedByCharacter(active.Location, next))
        {
            active.Path.Clear();
            active.PathIndex = 0;
            return;
        }

        var movedSinceLastTick = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
        active.LastPosition = Game1.player.Position;
        StartMoving(DirectionTo(Game1.player.TilePoint, next));
        MovePlayerForTick();
        if (Game1.player.TilePoint == next)
        {
            active.PathIndex++;
        }
        if (!movedSinceLastTick)
        {
            active.StuckTicks++;
            if (active.StuckTicks > 45)
            {
                active.Path.Clear();
                active.PathIndex = 0;
                active.StuckTicks = 0;
            }
        }
        else
        {
            active.StuckTicks = 0;
        }
    }

    private void CompletePickupDebris(ActivePickupDebris active, int itemCountAfter)
    {
        StopAllMovement();
        activePickupDebris = null;
        var request = active.Pending.Request;
        var inventoryAfter = InventoryStackSignature();
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
            PrimitiveKind = "pickup_debris",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "bfs_reached_live_debris", "game_update_naturally_collected_chunk", "inventory_item_count_increased", "no_direct_debris_collect_call" },
            RequestedEffect = active.RequestedEffect,
            ObservedEffect = "debris_present=" + active.Location.debris.Contains(active.Debris).ToString().ToLowerInvariant() + ";item_count=" + itemCountAfter + ";player.tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "locations[" + active.LocationId + "].debris.count", Before = active.DebrisCountBefore.ToString(), After = active.Location.debris.Count.ToString() },
                new SimulatedFactChange { Path = "player.inventory.stack_signature", Before = active.InventoryBefore, After = inventoryAfter },
                new SimulatedFactChange { Path = "player.inventory.count[" + active.QualifiedItemId + "]", Before = active.ItemCountBefore.ToString(), After = itemCountAfter.ToString() }
            }
        };
        ApplyQuestResourceReceiptFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompletePickupDebrisBlocked(ActivePickupDebris active, string reason)
    {
        StopAllMovement();
        activePickupDebris = null;
        active.Pending.Completion.SetResult(BlockedWithPrimitive(
            active.Pending.Request,
            "pickup_debris",
            active.RequestedEffect,
            "debris_present=" + active.Location.debris.Contains(active.Debris).ToString().ToLowerInvariant() + ";item_count=" + CountInventoryItem(active.QualifiedItemId),
            reason));
    }

    private static Point DebrisChunkTile(Chunk chunk)
    {
        return new Point(
            (int)((chunk.position.X + 32f) / Game1.tileSize),
            (int)((chunk.position.Y + 32f) / Game1.tileSize));
    }

    private static Debris? DebrisAt(GameLocation location, Point target, int? debrisIndex, string qualifiedItemId = "")
    {
        var indexes = Enumerable.Range(0, location.debris.Count);
        if (debrisIndex.HasValue && debrisIndex.Value >= 0 && debrisIndex.Value < location.debris.Count)
        {
            indexes = new[] { debrisIndex.Value }
                .Concat(indexes.Where(index => index != debrisIndex.Value));
        }

        foreach (var index in indexes)
        {
            var debris = location.debris[index];
            if (DebrisChunkAt(debris, target) is not null &&
                (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                 string.Equals(DebrisQualifiedItemId(debris), qualifiedItemId, StringComparison.OrdinalIgnoreCase)))
            {
                return debris;
            }
        }

        return null;
    }

    private static string DebrisQualifiedItemId(Debris debris)
    {
        return debris.item?.QualifiedItemId ?? ItemRegistry.QualifyItemId(debris.itemId.Value) ?? debris.itemId.Value;
    }

    private static Chunk? DebrisChunkAt(Debris debris, Point target)
    {
        return debris.Chunks.FirstOrDefault(chunk =>
            (int)((chunk.position.X + 32f) / Game1.tileSize) == target.X &&
            (int)((chunk.position.Y + 32f) / Game1.tileSize) == target.Y);
    }

    private static string DebrisRequestedEffect(TrainingExecutionRequest request)
    {
        return "location.debris[" + (request.DebrisIndex.HasValue ? request.DebrisIndex.Value.ToString() : request.TargetTileX + "," + request.TargetTileY) + "].chunk_count_decreases_or_removed=true;player.inventory.updated;collection=native_proximity";
    }

    private static string DebrisObservedEffect(GameLocation location, Point target, int? debrisIndex)
    {
        var debris = DebrisAt(location, target, debrisIndex);
        if (debris is null)
        {
            return "debris_present=false";
        }

        var index = location.debris.IndexOf(debris);
        return "debris_present=true;debris_index=" + index + ";chunk_count=" + debris.Chunks.Count + ";qualified_item_id=" + (debris.item?.QualifiedItemId ?? debris.itemId.Value);
    }

    private static int CountInventoryItem(string qualifiedItemId)
    {
        if (string.IsNullOrWhiteSpace(qualifiedItemId))
        {
            return 0;
        }

        return Game1.player.Items
            .Where(item => item is not null && string.Equals(item.QualifiedItemId, qualifiedItemId, StringComparison.OrdinalIgnoreCase))
            .Sum(item => item!.Stack);
    }

    private TrainingExecutionResult ExecuteSetupMachineOutputTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_machine_output_target", "farm.machines[target].ready_for_harvest=true", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var outputItemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "388" : request.ShopItemId)
            : request.QualifiedItemId;
        var beforeMachine = MachineObservedEffect(farm, target);
        ClearReadyMachineOutputsForFixture(farm, tile);
        farm.objects.Remove(tile);

        var machine = new StardewValley.Object(tile, string.IsNullOrWhiteSpace(request.ExpectedShopId) ? "12" : request.ExpectedShopId)
        {
            heldObject =
            {
                Value = ItemRegistry.Create<StardewValley.Object>(outputItemId, Math.Max(1, request.Quantity ?? 1))
            },
            readyForHarvest =
            {
                Value = true
            }
        };
        machine.MinutesUntilReady = 0;
        farm.objects[tile] = machine;
        MoveFixtureFarmerToFarmAdjacent(target);

        var verified = MachineAt(farm, target) is { readyForHarvest.Value: true, heldObject.Value: not null };
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
            PrimitiveKind = "debug_setup_machine_output_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_machine_output_ready", "qualified_item_id=" + outputItemId }
                : new[] { "fixture_machine_output_not_ready", "qualified_item_id=" + outputItemId },
            RequestedEffect = "farm.machines[" + target.X + "," + target.Y + "].ready_for_harvest=true;held_item=" + outputItemId,
            ObservedEffect = MachineObservedEffect(farm, target),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_machine_output_not_ready" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + target.X + "," + target.Y + "]",
                        Before = beforeMachine,
                        After = MachineObservedEffect(farm, target)
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteCollectMachineOutput(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", "farm.machines[target].held_item=null;player.inventory.updated", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", MachineRequestedEffect(request), "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "collect_machine_output_location_mismatch");
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = MachineRequestedEffect(request);
        var beforeObserved = MachineObservedEffect(location, target);
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_target_not_found");
        }

        if (!machine.readyForHarvest.Value || machine.heldObject.Value is null)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_not_ready");
        }

        var output = machine.heldObject.Value;
        var outputId = output.QualifiedItemId;
        if (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
            !string.Equals(outputId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_item_mismatch");
        }
        var resourceQuestReason = ValidateQuestResourceReceiptTarget(request, outputId);
        if (resourceQuestReason is not null)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, resourceQuestReason);
        }
        if (!ValidateSpecialOrderCollectItemTarget(
            request,
            output,
            out var specialOrderCollectCountBefore,
            out var specialOrderCollectReason))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, specialOrderCollectReason);
        }

        if (!Game1.player.couldInventoryAcceptThisItem(output))
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_inventory_cannot_accept_item");
        }

        if (!TryReadExpectedSkillExperience(request, out var expectedExperience, out var expectedMasteryDelta) ||
            !TryProjectMachineHarvestExperience(machine, out var projectedExperience, out var projectedMasteryDelta) ||
            !expectedExperience.SequenceEqual(projectedExperience) || expectedMasteryDelta != projectedMasteryDelta)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_experience_projection_drifted");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) + Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "collect_machine_output", requested, beforeObserved, "collect_machine_output_player_not_adjacent");
        }

        var beforeInventory = InventoryStackSignature();
        var beforeItemCount = CountInventoryItem(outputId);
        var experienceBefore = Enumerable.Range(0, 6).Select(index => Game1.player.experiencePoints[index]).ToArray();
        var masteryBefore = (int)Game1.stats.Get("MasteryExp");
        var acted = machine.checkForAction(Game1.player);
        var afterInventory = InventoryStackSignature();
        var afterItemCount = CountInventoryItem(outputId);
        var afterObserved = MachineObservedEffect(location, target);
        var actualExperience = Enumerable.Range(0, 6)
            .Select(index => new SkillExperienceDelta(
                Farmer.getSkillNameFromIndex(index).ToLowerInvariant(),
                index,
                Game1.player.experiencePoints[index] - experienceBefore[index]))
            .Where(delta => delta.Delta != 0 || expectedExperience.Any(expected => expected.SkillIndex == delta.SkillIndex))
            .ToArray();
        var actualMasteryDelta = (int)Game1.stats.Get("MasteryExp") - masteryBefore;
        var experienceVerified = actualExperience.SequenceEqual(expectedExperience) && actualMasteryDelta == expectedMasteryDelta;
        var verified = acted &&
            machine.heldObject.Value is null &&
            !machine.readyForHarvest.Value &&
            (!string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal) || afterItemCount > beforeItemCount) &&
            experienceVerified;

        var result = new TrainingExecutionResult
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
            PrimitiveKind = "collect_machine_output",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "machine_output_collected", "inventory_updated", "machine_harvest_skill_and_mastery_experience_verified", "qualified_item_id=" + outputId }
                : new[] { acted ? "checkForAction_returned_true" : "checkForAction_returned_false", machine.heldObject.Value is null ? "held_item_cleared" : "held_item_still_present", experienceVerified ? "machine_harvest_experience_verified" : "machine_harvest_experience_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = afterObserved +
                ";skill_experience_deltas_json=" + JsonSerializer.Serialize(actualExperience) +
                ";mastery_experience_delta=" + actualMasteryDelta,
            BlockReasons = verified ? Array.Empty<string>() : new[] { acted ? "collect_machine_output_post_state_mismatch" : "collect_machine_output_action_failed" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + location.NameOrUniqueName + ":" + target.X + "," + target.Y + "].held_item",
                        Before = beforeObserved,
                        After = afterObserved
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.stack_signature",
                        Before = beforeInventory,
                        After = afterInventory
                    }
                }
                .Concat(actualExperience
                    .Where(delta => delta.Delta != 0)
                    .Select(delta => new SimulatedFactChange
                    {
                        Path = "player.skills." + delta.SkillId + ".experience",
                        Before = experienceBefore[delta.SkillIndex].ToString(CultureInfo.InvariantCulture),
                        After = Game1.player.experiencePoints[delta.SkillIndex].ToString(CultureInfo.InvariantCulture)
                    }))
                .Concat(actualMasteryDelta == 0
                    ? Array.Empty<SimulatedFactChange>()
                    : new[]
                    {
                        new SimulatedFactChange
                        {
                            Path = "stats.MasteryExp",
                            Before = masteryBefore.ToString(CultureInfo.InvariantCulture),
                            After = Game1.stats.Get("MasteryExp").ToString(CultureInfo.InvariantCulture)
                        }
                    })
                .ToArray()
                : Array.Empty<SimulatedFactChange>()
        };
        ApplyQuestResourceReceiptFeedback(result, request);
        if (string.Equals(request.QuestFamily, "special_order", StringComparison.Ordinal))
        {
            ApplySpecialOrderCollectFeedback(result, request, specialOrderCollectCountBefore);
        }
        return result;
    }

    private static bool TryReadExpectedSkillExperience(
        TrainingExecutionRequest request,
        out SkillExperienceDelta[] deltas,
        out int masteryDelta)
    {
        deltas = Array.Empty<SkillExperienceDelta>();
        masteryDelta = 0;
        if (string.IsNullOrWhiteSpace(request.ExpectedSkillExperienceDeltasJson) ||
            !request.ExpectedMasteryExperienceDelta.HasValue)
        {
            return false;
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<SkillExperienceDelta[]>(request.ExpectedSkillExperienceDeltasJson, JsonOptions);
            if (parsed is null || parsed.Any(delta =>
                delta.SkillIndex is < 0 or > 5 ||
                delta.Delta < 0 ||
                !string.Equals(delta.SkillId, Farmer.getSkillNameFromIndex(delta.SkillIndex).ToLowerInvariant(), StringComparison.Ordinal)) ||
                parsed.Select(delta => delta.SkillIndex).Distinct().Count() != parsed.Length)
            {
                return false;
            }
            deltas = parsed.OrderBy(delta => delta.SkillIndex).ToArray();
            masteryDelta = request.ExpectedMasteryExperienceDelta.Value;
            return masteryDelta >= 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryProjectMachineHarvestExperience(
        StardewValley.Object machine,
        out SkillExperienceDelta[] deltas,
        out int masteryDelta)
    {
        deltas = Array.Empty<SkillExperienceDelta>();
        masteryDelta = 0;
        var raw = machine.GetMachineData()?.ExperienceGainOnHarvest ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return true;
        }

        var tokens = raw.Split(' ');
        var aggregate = new Dictionary<int, int>();
        var experience = Enumerable.Range(0, 6).Select(index => Game1.player.experiencePoints[index]).ToArray();
        var levels = Enumerable.Range(0, 6).Select(Game1.player.GetUnmodifiedSkillLevel).ToArray();
        try
        {
            for (var index = 0; index < tokens.Length; index += 2)
            {
                var skillIndex = Farmer.getSkillNumberFromName(tokens[index]);
                if (skillIndex == -1 || !ArgUtility.TryGetInt(tokens, index + 1, out var amount, out _, "int amount"))
                {
                    continue;
                }

                var levelBeforeCall = levels.Sum() / 2;
                var effectiveDelta = skillIndex == Farmer.luckSkill || amount <= 0 ? 0 : amount;
                aggregate[skillIndex] = aggregate.TryGetValue(skillIndex, out var current)
                    ? checked(current + effectiveDelta)
                    : effectiveDelta;
                if (effectiveDelta <= 0)
                {
                    continue;
                }
                if (levelBeforeCall >= 25)
                {
                    masteryDelta = checked(masteryDelta + Math.Max(1, skillIndex == Farmer.farmingSkill ? effectiveDelta / 2 : effectiveDelta));
                }

                var oldExperience = experience[skillIndex];
                var newExperience = checked(oldExperience + effectiveDelta);
                var gainedLevel = Farmer.checkForLevelGain(oldExperience, newExperience);
                experience[skillIndex] = newExperience;
                if (gainedLevel != -1)
                {
                    levels[skillIndex] = gainedLevel;
                }
            }
        }
        catch (OverflowException)
        {
            deltas = Array.Empty<SkillExperienceDelta>();
            masteryDelta = 0;
            return false;
        }

        deltas = aggregate
            .OrderBy(pair => pair.Key)
            .Select(pair => new SkillExperienceDelta(
                Farmer.getSkillNameFromIndex(pair.Key).ToLowerInvariant(),
                pair.Key,
                pair.Value))
            .ToArray();
        return true;
    }

    private sealed record SkillExperienceDelta(string SkillId, int SkillIndex, int Delta);

    private TrainingExecutionResult ExecuteSetupMachineInputTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "debug_setup_machine_input_target", "farm.machines[target].loadable_inputs.length>0", "target_tile=missing", "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var locationId = string.IsNullOrWhiteSpace(request.LocationId)
            ? "Farm"
            : request.LocationId;
        var location = Game1.getLocationFromName(locationId);
        if (location is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_input_target",
                "location.machines[target].loadable_inputs.length>0",
                "location_id=" + locationId,
                "fixture_location_not_found");
        }
        Game1.currentLocation = location;
        Game1.player.currentLocation = location;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var tile = new Vector2(target.X, target.Y);
        var inputItemId = string.IsNullOrWhiteSpace(request.QualifiedItemId)
            ? QualifyObjectId(string.IsNullOrWhiteSpace(request.ShopItemId) ? "262" : request.ShopItemId)
            : request.QualifiedItemId;
        var beforeMachine = MachineObservedEffect(location, target);
        location.objects.Remove(tile);

        var machineItemId = string.IsNullOrWhiteSpace(request.ExpectedShopId)
            ? "(BC)12"
            : request.ExpectedShopId.StartsWith("(", StringComparison.Ordinal)
                ? request.ExpectedShopId
                : "(BC)" + request.ExpectedShopId;
        var machineTemplate = ItemRegistry.Create<StardewValley.Object>(machineItemId);
        long? removedFixtureAnimalId = null;
        if (machineTemplate.GetMachineData()?.IsIncubator == true &&
            location is AnimalHouse animalHouse &&
            animalHouse.isFull() &&
            animalHouse.animalsThatLiveHere.Count > 0)
        {
            removedFixtureAnimalId =
                animalHouse.animalsThatLiveHere[^1];
            animalHouse.animalsThatLiveHere.RemoveAt(
                animalHouse.animalsThatLiveHere.Count - 1);
            animalHouse.animals.Remove(
                removedFixtureAnimalId.Value);
        }
        var placementAccepted = machineTemplate.placementAction(
            location,
            target.X * Game1.tileSize,
            target.Y * Game1.tileSize,
            Game1.player);
        var machine = MachineAt(location, target) ?? machineTemplate;
        if (!location.objects.ContainsKey(tile))
        {
            location.objects[tile] = machine;
        }
        machine.MinutesUntilReady = -1;
        machine.readyForHarvest.Value = false;
        machine.heldObject.Value = null;
        location.objects[tile] = machine;
        var additionalRows = ParseMachineLifecycleAdditionalItems(
            request.ProcessAdditionalItemsJson,
            out var additionalParseReason);
        if (additionalRows is null)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_machine_input_target",
                "location.machines[target].loadable_inputs.length>0",
                "additional_items=" + additionalParseReason,
                "fixture_machine_input_additional_items_invalid");
        }
        var inputQuantity = Math.Max(1, request.Quantity ?? 1);
        var inputReserve = additionalRows
            .Where(row => string.Equals(
                row.QualifiedItemId,
                inputItemId,
                StringComparison.OrdinalIgnoreCase))
            .Sum(row => row.Quantity);
        var inputSlot = EnsureInventoryItem(
            inputItemId,
            checked(inputQuantity + inputReserve));
        var additionalSlots = new List<string>();
        foreach (var row in additionalRows)
        {
            var slot = EnsureInventoryItem(
                row.QualifiedItemId,
                row.Quantity);
            if (slot < 0)
            {
                return BlockedWithPrimitive(
                    request,
                    "debug_setup_machine_input_target",
                    "location.machines[target].loadable_inputs.length>0",
                    "additional_item=" + row.QualifiedItemId,
                    "fixture_machine_input_inventory_full");
            }
            additionalSlots.Add(
                row.QualifiedItemId + "@" + slot + ":" + row.Quantity);
        }
        MoveFixtureFarmerToLocationAdjacent(
            location,
            target,
            out var stand,
            out var moveReason);

        var input = inputSlot >= 0 ? Game1.player.Items[inputSlot] : null;
        var accepts = input is not null && machine.performObjectDropInAction(input, probe: true, Game1.player);
        var verified = MachineAt(location, target) is not null &&
            placementAccepted &&
            inputSlot >= 0 &&
            accepts &&
            Game1.player.TilePoint == stand;
        RefreshTransparentMachineProbeCache();
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
            PrimitiveKind = "debug_setup_machine_input_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_fixture_machine_accepts_input_probe", "location_id=" + locationId, "qualified_item_id=" + inputItemId, "input_slot_index=" + inputSlot, "additional_items=" + string.Join(",", additionalSlots), "removed_fixture_animal_id=" + (removedFixtureAnimalId?.ToString() ?? "none") }
                : new[] { "fixture_machine_input_probe_rejected", "location_id=" + locationId, "qualified_item_id=" + inputItemId, "input_slot_index=" + inputSlot, moveReason },
            RequestedEffect = "location.machines[" + locationId + ":" + target.X + "," + target.Y + "].loadable_inputs.length>0;qualified_item_id=" + inputItemId,
            ObservedEffect = MachineObservedEffect(location, target) + ";location_id=" + locationId + ";stand_tile=" + stand.X + "," + stand.Y + ";input_slot_index=" + inputSlot + ";input_probe_accepts=" + accepts.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "fixture_machine_input_probe_rejected" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "locations[" + locationId + "].machines[" + target.X + "," + target.Y + "]",
                        Before = beforeMachine,
                        After = MachineObservedEffect(location, target)
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.input_slot_index",
                        Before = "unknown",
                        After = inputSlot.ToString()
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private TrainingExecutionResult ExecuteLoadMachineInput(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(request, "load_machine_input", "farm.machines[target].minutes_until_ready>0_or_ready=true;player.inventory.updated", "target_tile=missing", "target_tile_required");
        }

        if (!request.InputSlotIndex.HasValue)
        {
            return BlockedWithPrimitive(request, "load_machine_input", "farm.machines[target].minutes_until_ready>0_or_ready=true;player.inventory.updated", "input_slot=missing", "input_slot_index_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var location = Game1.currentLocation;
        if (location is null || string.IsNullOrWhiteSpace(request.LocationId) ||
            !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "load_machine_input", MachineInputRequestedEffect(request), "location_id=" + (location?.NameOrUniqueName ?? "unavailable"), "load_machine_input_location_mismatch");
        }
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var requested = MachineInputRequestedEffect(request);
        var beforeObserved = MachineObservedEffect(location, target);
        var machine = MachineAt(location, target);
        if (machine is null)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_target_not_found");
        }

        if (machine.MinutesUntilReady > 0 || machine.readyForHarvest.Value)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_target_busy");
        }

        var inputSlot = request.InputSlotIndex.Value;
        if (inputSlot < 0 || inputSlot >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_slot_out_of_range");
        }

        var input = Game1.player.Items[inputSlot];
        if (input is null)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_slot_empty");
        }

        if (!string.IsNullOrWhiteSpace(request.QualifiedItemId) &&
            !string.Equals(input.QualifiedItemId, request.QualifiedItemId, StringComparison.OrdinalIgnoreCase))
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_item_mismatch");
        }

        var tracksAnvilDistribution =
            string.Equals(
                request.MachinePredictionTrainingKind,
                "complete_distribution",
                StringComparison.Ordinal);
        var beforeAnvilFeedback = default(
            AnvilReforgeFeedback);
        var beforeAnvilReason = string.Empty;
        if (tracksAnvilDistribution &&
            (!string.Equals(
                machine.QualifiedItemId,
                "(BC)Anvil",
                StringComparison.Ordinal) ||
             string.IsNullOrWhiteSpace(
                 request
                     .MachinePredictionContractFingerprint) ||
             !TryReadAnvilReforgeFeedback(
                 request,
                 input,
                 out beforeAnvilFeedback,
                 out beforeAnvilReason) ||
             !string.Equals(
                 beforeAnvilFeedback.Metric,
                 request.AnvilReforgeUtilityMetric,
                 StringComparison.Ordinal) ||
             !AnvilUtilityMatches(
                 request.AnvilReforgeCurrentUtility,
                 beforeAnvilFeedback.Utility) ||
             !AnvilDistributionRequestIsValid(
                 request)))
        {
            return BlockedWithPrimitive(
                request,
                "load_machine_input",
                requested,
                beforeObserved,
                string.IsNullOrWhiteSpace(
                    beforeAnvilReason)
                        ? "anvil_reforge_distribution_contract_invalid"
                        : beforeAnvilReason);
        }

        if (!machine.performObjectDropInAction(input, probe: true, Game1.player))
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_probe_rejected");
        }

        var playerTile = Game1.player.TilePoint;
        if (Math.Abs(playerTile.X - target.X) + Math.Abs(playerTile.Y - target.Y) != 1)
        {
            return BlockedWithPrimitive(request, "load_machine_input", requested, beforeObserved, "load_machine_input_player_not_adjacent");
        }

        var beforeInventory = InventoryStackSignature();
        var beforeStack = input.Stack;
        var inputId = input.QualifiedItemId;
        var acted = machine.performObjectDropInAction(input, probe: false, Game1.player);
        var afterInventory = InventoryStackSignature();
        var afterObserved = MachineObservedEffect(location, target);
        var afterSlotItem = inputSlot < Game1.player.Items.Count ? Game1.player.Items[inputSlot] : null;
        var afterStack = afterSlotItem?.Stack ?? 0;
        var machineStarted = machine.MinutesUntilReady > 0 || machine.readyForHarvest.Value || machine.heldObject.Value is not null;
        var inventoryChanged = !string.Equals(beforeInventory, afterInventory, StringComparison.Ordinal) || afterStack < beforeStack;
        var afterAnvilFeedback = default(
            AnvilReforgeFeedback);
        var anvilFeedbackVerified =
            !tracksAnvilDistribution ||
            machine.heldObject.Value is not null &&
            TryReadAnvilReforgeFeedback(
                request,
                machine.heldObject.Value,
                out afterAnvilFeedback,
                out _) &&
            string.Equals(
                afterAnvilFeedback.Metric,
                beforeAnvilFeedback.Metric,
                StringComparison.Ordinal);
        var verified = acted &&
            machineStarted &&
            inventoryChanged &&
            anvilFeedbackVerified;
        var recordedAnvilFeedback =
            tracksAnvilDistribution &&
            anvilFeedbackVerified;
        RefreshTransparentMachineProbeCache();
        var realizedDelta = recordedAnvilFeedback
            ? afterAnvilFeedback.Utility -
              beforeAnvilFeedback.Utility
            : (double?)null;

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
            PrimitiveKind = "load_machine_input",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "machine_input_loaded", "machine_processing_started_or_output_ready", "inventory_updated", tracksAnvilDistribution ? "anvil_reforge_realized_utility_recorded" : "deterministic_or_non_anvil_machine_output", "qualified_item_id=" + inputId }
                : new[] { acted ? "performObjectDropInAction_returned_true" : "performObjectDropInAction_returned_false", machineStarted ? "machine_started" : "machine_not_started", inventoryChanged ? "inventory_changed" : "inventory_not_changed", anvilFeedbackVerified ? "anvil_feedback_verified_or_not_required" : "anvil_feedback_unavailable" },
            RequestedEffect = requested,
            ObservedEffect = afterObserved + ";input_slot_index=" + inputSlot + ";input_stack_before=" + beforeStack + ";input_stack_after=" + afterStack +
                (recordedAnvilFeedback
                    ? ";anvil_reforge_utility_metric=" + afterAnvilFeedback.Metric +
                      ";anvil_reforge_current_utility=" + beforeAnvilFeedback.UtilityText +
                      ";anvil_reforge_realized_utility=" + afterAnvilFeedback.UtilityText +
                      ";anvil_reforge_realized_utility_delta=" +
                      Math.Round(realizedDelta ?? 0, 8).ToString("0.########", CultureInfo.InvariantCulture)
                    : string.Empty),
            BlockReasons = verified ? Array.Empty<string>() : new[] { acted ? "load_machine_input_post_state_mismatch" : "load_machine_input_action_failed" },
            MachineOutputDistributionOutcomeKind =
                tracksAnvilDistribution
                    ? request
                        .MachineOutputDistributionOutcomeKind
                    : string.Empty,
            AnvilReforgeUtilityMetric =
                tracksAnvilDistribution
                    ? afterAnvilFeedback.Metric
                    : string.Empty,
            AnvilReforgeCurrentUtility =
                tracksAnvilDistribution
                    ? beforeAnvilFeedback.Utility
                    : null,
            AnvilReforgeExpectedUtility =
                tracksAnvilDistribution
                    ? request.AnvilReforgeExpectedUtility
                    : null,
            AnvilReforgeRealizedUtility =
                recordedAnvilFeedback
                    ? afterAnvilFeedback.Utility
                    : null,
            AnvilReforgeRealizedUtilityDelta =
                realizedDelta,
            AnvilReforgeRealizedImproved =
                recordedAnvilFeedback
                    ? realizedDelta > 0
                    : null,
            AnvilReforgeRealizedOutcomeJson =
                recordedAnvilFeedback
                    ? afterAnvilFeedback.OutcomeJson
                    : string.Empty,
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.machines[" + location.NameOrUniqueName + ":" + target.X + "," + target.Y + "]",
                        Before = beforeObserved,
                        After = afterObserved
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.stack_signature",
                        Before = beforeInventory,
                        After = afterInventory
                    }
                }
                .Concat(
                    tracksAnvilDistribution
                        ? new[]
                        {
                            new SimulatedFactChange
                            {
                                Path =
                                    "machine.anvil.reforge.utility",
                                Before =
                                    beforeAnvilFeedback.UtilityText,
                                After =
                                    afterAnvilFeedback.UtilityText
                            },
                            new SimulatedFactChange
                            {
                                Path =
                                    "machine.anvil.reforge.outcome",
                                Before =
                                    beforeAnvilFeedback.OutcomeJson,
                                After =
                                    afterAnvilFeedback.OutcomeJson
                            }
                        }
                        : Array.Empty<
                            SimulatedFactChange>())
                .ToArray()
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static StardewValley.Object? MachineAt(GameLocation location, Point target)
    {
        return location.objects.TryGetValue(new Vector2(target.X, target.Y), out var obj) &&
            obj.bigCraftable.Value
            ? obj
            : null;
    }
}
