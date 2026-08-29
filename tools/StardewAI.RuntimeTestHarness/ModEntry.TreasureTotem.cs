using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeTreasureTotemNativeContract =
        "Object.performUseAction((O)TreasureTotem)->outdoors_guard->Object.treasureTotem->TreasureTotemsUsed++->rounded_distance_3_ring->placement_occupancy_front_bush_diggable_or_winter_grass_gate->objects.Add((O)590)";

    private void ExecuteUseTreasureTotem(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "player.inventory[" + request.InventorySlotIndex + "].consume=(O)TreasureTotem;" +
            "treasure_totems_used=" + request.TreasureTotemsUsedAfter +
            ";artifact_spots_spawned=" + request.TreasureTotemExpectedSpawnCount;
        if (!request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue ||
            request.ExpectedStackAfter != request.ExpectedStackBefore - 1 ||
            string.IsNullOrWhiteSpace(request.TreasureTotemProjectionFingerprint) ||
            !TreasureTotemRequestContractIsExact(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_treasure_totem", requested,
                "typed_contract=missing_or_invalid", "use_treasure_totem_typed_fields_required"));
            return;
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var slot = request.InventorySlotIndex.Value;
        var location = Game1.currentLocation;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            Game1.activeClickableMenu is not null || !Game1.player.canMove || Game1.eventUp || Game1.isFestival() ||
            Game1.fadeToBlack || Game1.player.swimming.Value || Game1.player.bathingClothes.Value ||
            Game1.player.onBridge.Value || !location.IsOutdoors || slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object totem || totem.GetType() != typeof(StardewValley.Object) ||
            totem.isTemporarilyInvisible || totem.Stack != request.ExpectedStackBefore ||
            !string.Equals(totem.ItemId, "TreasureTotem", StringComparison.Ordinal) ||
            !string.Equals(totem.QualifiedItemId, "(O)TreasureTotem", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_treasure_totem", requested,
                TreasureTotemObservedEffect(slot, location), "use_treasure_totem_location_gate_or_inventory_drift"));
            return;
        }

        var center = Game1.player.TilePoint;
        var expectedTiles = DeserializeTreasureTotemTiles(request.TreasureTotemExpectedSpawnTilesJson);
        var currentTiles = ReadTreasureTotemSpawnTiles(location, Game1.player.Tile);
        var currentTilesJson = SerializeTreasureTotemTiles(currentTiles);
        var spotsBefore = CountTreasureTotemArtifactSpots(location);
        var usesBefore = Game1.netWorldState.Value.TreasureTotemsUsed;
        if (center.X != request.TreasureTotemCenterTileX || center.Y != request.TreasureTotemCenterTileY ||
            request.TreasureTotemRingCandidateCount != 16 ||
            request.TreasureTotemExpectedSpawnCount != currentTiles.Length || currentTiles.Length == 0 ||
            !string.Equals(request.TreasureTotemExpectedSpawnTilesJson, currentTilesJson, StringComparison.Ordinal) ||
            !expectedTiles.SequenceEqual(currentTiles) ||
            spotsBefore != request.TreasureTotemExistingArtifactSpotCountBefore ||
            usesBefore != request.TreasureTotemsUsedBefore)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_treasure_totem", requested,
                TreasureTotemObservedEffect(slot, location), "use_treasure_totem_center_spawn_or_counter_projection_drifted"));
            return;
        }

        var nativeUse = UseInventoryObjectNative(totem, slot);
        var spotsAfter = CountTreasureTotemArtifactSpots(location);
        var usesAfter = Game1.netWorldState.Value.TreasureTotemsUsed;
        var expectedTilesVerified = expectedTiles.All(tile =>
            location.objects.TryGetValue(new Vector2(tile.tile_x, tile.tile_y), out var obj) &&
            obj.GetType() == typeof(StardewValley.Object) && obj.QualifiedItemId == "(O)590");
        var stackVerified = nativeUse.Used && nativeUse.StackBefore == request.ExpectedStackBefore &&
            nativeUse.StackAfter == request.ExpectedStackAfter;
        var counterVerified = usesAfter == request.TreasureTotemsUsedAfter && usesAfter == usesBefore + 1;
        var spotsVerified = spotsAfter == request.TreasureTotemExistingArtifactSpotCountAfter &&
            spotsAfter == spotsBefore + expectedTiles.Length && expectedTilesVerified;
        var verified = stackVerified && counterVerified && spotsVerified;
        pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_treasure_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Object_performUseAction_succeeded", "exactly_one_treasure_totem_consumed",
                    "native_TreasureTotemsUsed_increment_verified", "all_projected_native_ring_artifact_spots_verified",
                    "generated_artifact_spots_deferred_to_shared_clear_obstacle_projection_and_executor"
                }
                : new[]
                {
                    stackVerified ? "inventory_stack_verified" : "inventory_stack_mismatch",
                    counterVerified ? "treasure_totem_counter_verified" : "treasure_totem_counter_mismatch",
                    spotsVerified ? "artifact_spot_ring_verified" : "artifact_spot_ring_mismatch"
                },
            RequestedEffect = requested,
            ObservedEffect = TreasureTotemObservedEffect(slot, location) +
                ";expected_spawn_tiles_json=" + currentTilesJson,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_treasure_totem_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.inventory[" + slot + "]", Before = "(O)TreasureTotemx" + request.ExpectedStackBefore, After = "(O)TreasureTotemx" + request.ExpectedStackAfter },
                    new SimulatedFactChange { Path = "world.treasure_totems_used", Before = usesBefore.ToString(), After = usesAfter.ToString() },
                    new SimulatedFactChange { Path = "current_location.artifact_spot_count", Before = spotsBefore.ToString(), After = spotsAfter.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static bool TreasureTotemRequestContractIsExact(TrainingExecutionRequest request)
    {
        return string.Equals(request.ItemId, "TreasureTotem", StringComparison.Ordinal) &&
            string.Equals(request.QualifiedItemId, "(O)TreasureTotem", StringComparison.Ordinal) &&
            request.TreasureTotemCenterTileX.HasValue && request.TreasureTotemCenterTileY.HasValue &&
            request.TreasureTotemRingCandidateCount == 16 && request.TreasureTotemExpectedSpawnCount > 0 &&
            !string.IsNullOrWhiteSpace(request.TreasureTotemExpectedSpawnTilesJson) &&
            request.TreasureTotemExistingArtifactSpotCountBefore.HasValue &&
            request.TreasureTotemExistingArtifactSpotCountAfter ==
                request.TreasureTotemExistingArtifactSpotCountBefore + request.TreasureTotemExpectedSpawnCount &&
            request.TreasureTotemsUsedBefore.HasValue &&
            request.TreasureTotemsUsedAfter == request.TreasureTotemsUsedBefore + 1 &&
            request.TreasureTotemRingScanRadius == 4 && request.TreasureTotemRoundedRadius == 3 &&
            string.Equals(request.TreasureTotemArtifactSpotQualifiedItemId, "(O)590", StringComparison.Ordinal) &&
            string.Equals(request.TreasureTotemInitialSound, "treasure_totem", StringComparison.Ordinal) &&
            string.Equals(request.NativeContract, RuntimeTreasureTotemNativeContract, StringComparison.Ordinal);
    }

    private static TreasureTotemTile[] ReadTreasureTotemSpawnTiles(GameLocation location, Vector2 center)
    {
        var result = new List<TreasureTotemTile>(16);
        for (var x = (int)center.X - 4; x < center.X + 4; x++)
        for (var y = (int)center.Y - 4; y < center.Y + 4; y++)
        {
            if (Math.Round(Utility.distance(x, center.X, y, center.Y)) != 3)
                continue;
            var tile = new Vector2(x, y);
            if (location.CanItemBePlacedHere(tile) && !location.IsTileOccupiedBy(tile) &&
                !location.hasTileAt(x, y, "AlwaysFront") && !location.hasTileAt(x, y, "Front") &&
                !location.isBehindBush(tile) &&
                (location.doesTileHaveProperty(x, y, "Diggable", "Back") is not null ||
                 (location.GetSeason() == Season.Winter && location.doesTileHaveProperty(x, y, "Type", "Back") == "Grass")) &&
                location.IsOutdoors)
            {
                result.Add(new TreasureTotemTile(x, y));
            }
        }
        return result.ToArray();
    }

    private static string SerializeTreasureTotemTiles(IEnumerable<TreasureTotemTile> tiles) =>
        JsonSerializer.Serialize(tiles);

    private static TreasureTotemTile[] DeserializeTreasureTotemTiles(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<TreasureTotemTile[]>(json) ?? Array.Empty<TreasureTotemTile>();
        }
        catch (JsonException)
        {
            return Array.Empty<TreasureTotemTile>();
        }
    }

    private static int CountTreasureTotemArtifactSpots(GameLocation location) =>
        location.objects.Pairs.Count(pair => pair.Value.QualifiedItemId == "(O)590");

    private static string TreasureTotemObservedEffect(int slot, GameLocation? location)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        return "slot=" + slot + ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) + ";location=" + (location?.NameOrUniqueName ?? "unavailable") +
            ";center_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
            ";location_is_outdoors=" + (location?.IsOutdoors.ToString().ToLowerInvariant() ?? "unavailable") +
            ";treasure_totems_used=" + Game1.netWorldState.Value.TreasureTotemsUsed +
            ";artifact_spot_count=" + (location is null ? -1 : CountTreasureTotemArtifactSpots(location));
    }

    private sealed record TreasureTotemTile(int tile_x, int tile_y);
}
