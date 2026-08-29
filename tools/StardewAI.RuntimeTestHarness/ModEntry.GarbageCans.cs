using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.GarbageCans;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeGarbageCanPayloadSha256 = "34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f";
    private const string RuntimeGarbageCanNativeContract =
        "GameLocation.checkAction -> performAction Garbage -> CheckGarbage -> TryGetGarbageItem -> CheckedGarbage/stat/output/native NPC reaction; no direct checked-set, stat, friendship, inventory, debris, or RNG mutation";

    private static readonly JsonSerializerOptions RuntimeGarbageCanPayloadOptions = new()
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private void StartGarbageCanRummage(PendingExecution pending)
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
            !request.SafeSlotIndex.HasValue || !request.RestoreSlotIndex.HasValue ||
            !request.ExpectedCheckedTodayBefore.HasValue || !request.ExpectedCheckedTodayAfter.HasValue ||
            !request.ExpectedTrashCansCheckedBefore.HasValue || !request.ExpectedTrashCansCheckedDelta.HasValue ||
            !request.ExpectedDailyLuck.HasValue || !request.ExpectedAlleywayBuffetRead.HasValue ||
            !request.PredictedItemProduced.HasValue || !request.SelectedIgnoreBaseChance.HasValue ||
            !request.SelectedMegaSuccess.HasValue || !request.SelectedDoubleMegaSuccess.HasValue ||
            string.IsNullOrWhiteSpace(request.GarbageCanId) || string.IsNullOrWhiteSpace(request.GarbageCanAction) ||
            string.IsNullOrWhiteSpace(request.ExpectedOutputJson) || string.IsNullOrWhiteSpace(request.ReactingNpcJson))
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, "rummage_garbage_typed_target_fields_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, "rummage_garbage_player_busy"));
            return;
        }
        if (!string.Equals(request.GarbageCanDataPayloadSha256, RuntimeGarbageCanPayloadSha256, StringComparison.Ordinal) ||
            !string.Equals(request.GarbageCanDataContractStatus, "exact_locked_base_1.6.15", StringComparison.Ordinal) ||
            !string.Equals(request.GarbageCanPredictionStatus, "exact_native_non_mutating_prediction", StringComparison.Ordinal) ||
            !string.Equals(request.GarbageCanNativeContract, RuntimeGarbageCanNativeContract, StringComparison.Ordinal) ||
            request.GarbageCanProjectionFingerprint.Length != 64)
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, "rummage_garbage_native_contract_mismatch"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var interaction = new Point(request.InteractionTileX.Value, request.InteractionTileY.Value);
        var stand = new Point(request.StandTileX.Value, request.StandTileY.Value);
        var projection = ProjectRuntimeGarbageCan(location, target);
        var reasons = ValidateRuntimeGarbageCan(location, target, interaction, stand, projection, request);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, reasons));
            return;
        }
        var outputIds = projection.Output is null
            ? Array.Empty<string>()
            : new[] { projection.Output.Key.QualifiedItemId };
        var questReason = ValidateQuestResourceSourceTarget(request, outputIds);
        if (!string.IsNullOrWhiteSpace(questReason))
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, questReason));
            return;
        }
        if (projection.Output is not null &&
            !ValidateSpecialOrderCollectSourceTarget(request, projection.Output.Key.QualifiedItemId, out var specialOrderReason))
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, specialOrderReason));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(GarbageCanBlocked(request, "rummage_garbage_path_unavailable:" + pathReason));
            return;
        }
        activeGarbageCanRummage = new ActiveGarbageCanRummage(
            pending, location, target, interaction, stand, path, projection, maxMovementTiles);
    }

    private static RuntimeGarbageCanProjection ProjectRuntimeGarbageCan(GameLocation location, Point target)
    {
        var action = location.doesTileHaveProperty(target.X, target.Y, "Action", "Buildings") ?? string.Empty;
        var parts = ArgUtility.SplitBySpace(action);
        var id = parts.Length > 1 ? NormalizeRuntimeGarbageCanId(parts[1]) : string.Empty;
        var data = DataLoader.GarbageCans(Game1.content);
        var payloadHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            data, typeof(GarbageCanData), RuntimeGarbageCanPayloadOptions))).ToLowerInvariant();
        var errors = new List<string>();
        Item? item = null;
        GarbageCanItemData? selected = null;
        var produced = false;
        if (payloadHash == RuntimeGarbageCanPayloadSha256 && data.GarbageCans.ContainsKey(id))
            produced = location.TryGetGarbageItem(id, Game1.player.DailyLuck, out item, out selected, out _, errors.Add);
        RuntimeGarbageOutput? output = item is null
            ? null
            : new RuntimeGarbageOutput(
                ClearanceOutputItemKey.FromInventoryReceipt(item),
                Math.Max(1, item.Stack),
                item.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                item.sellToStorePrice(-1L));
        var npc = Utility.GetNpcsWithinDistance(target.ToVector2(), 7, location).FirstOrDefault(candidate => candidate is not Horse);
        return new RuntimeGarbageCanProjection(
            action, id, data.GarbageCans.ContainsKey(id), payloadHash, errors.ToArray(), produced, selected?.Id ?? string.Empty,
            selected?.IgnoreBaseChance ?? false, selected?.IsMegaSuccess ?? false,
            selected?.IsDoubleMegaSuccess ?? false,
            selected?.AddToInventoryDirectly == true ? "direct_inventory" :
                selected?.CreateMultipleDebris == true ? "multiple_debris" : produced ? "single_debris" : "none",
            output, ProjectRuntimeGarbageReaction(npc, target));
    }

    private static string[] ValidateRuntimeGarbageCan(
        GameLocation location,
        Point target,
        Point interaction,
        Point stand,
        RuntimeGarbageCanProjection projection,
        TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (interaction != target || !AreAdjacent(stand, interaction) || !IsTileOnMap(location, stand) ||
            !IsTileWalkable(location, stand) || IsTileOccupiedByCharacter(location, stand))
            reasons.Add("rummage_garbage_interaction_geometry_drifted");
        if (!projection.Action.StartsWith("Garbage", StringComparison.Ordinal) ||
            !string.Equals(projection.Action, request.GarbageCanAction, StringComparison.Ordinal) ||
            !string.Equals(projection.Id, request.GarbageCanId, StringComparison.Ordinal))
            reasons.Add("rummage_garbage_action_or_id_drifted");
        if (!projection.KnownId || projection.PayloadHash != RuntimeGarbageCanPayloadSha256 || projection.Errors.Length > 0)
            reasons.Add("rummage_garbage_data_or_prediction_drifted");
        var checkedBefore = Game1.netWorldState.Value.CheckedGarbage.Contains(projection.Id);
        if (checkedBefore != request.ExpectedCheckedTodayBefore || checkedBefore || request.ExpectedCheckedTodayAfter != true ||
            Game1.stats.Get("trashCansChecked") != (uint)request.ExpectedTrashCansCheckedBefore.GetValueOrDefault() || request.ExpectedTrashCansCheckedDelta != 1 ||
            Math.Abs(Game1.player.DailyLuck - request.ExpectedDailyLuck.GetValueOrDefault()) > 1e-12 ||
            (Game1.player.stats.Get("Book_Trash") != 0) != request.ExpectedAlleywayBuffetRead)
            reasons.Add("rummage_garbage_day_or_player_state_drifted");
        if (projection.Produced != request.PredictedItemProduced ||
            !string.Equals(projection.SelectedEntryId, request.SelectedEntryId, StringComparison.Ordinal) ||
            projection.IgnoreBaseChance != request.SelectedIgnoreBaseChance || projection.MegaSuccess != request.SelectedMegaSuccess ||
            projection.DoubleMegaSuccess != request.SelectedDoubleMegaSuccess ||
            !string.Equals(projection.OutputDelivery, request.OutputDelivery, StringComparison.Ordinal) ||
            !RuntimeGarbageOutputMatches(projection.Output, request.ExpectedOutputJson))
            reasons.Add("rummage_garbage_output_projection_drifted");
        if (!RuntimeGarbageReactionMatches(projection.Reaction, request.ReactingNpcJson))
            reasons.Add("rummage_garbage_npc_reaction_drifted");
        if (projection.Reaction is not null && projection.Reaction.Status != "exact_linus_non_negative")
            reasons.Add("rummage_garbage_negative_friendship_witness");
        if (request.SafeSlotIndex is < 0 or > 11 || request.RestoreSlotIndex is < 0 or > 11 ||
            request.SafeSlotIndex >= Game1.player.Items.Count || Game1.player.Items[request.SafeSlotIndex.GetValueOrDefault()] is not null ||
            request.RestoreSlotIndex != Game1.player.CurrentToolIndex)
            reasons.Add("rummage_garbage_safe_slot_drifted");
        if (projection.OutputDelivery == "direct_inventory" && projection.Output is not null)
        {
            var predicted = location.TryGetGarbageItem(projection.Id, Game1.player.DailyLuck, out var item, out _, out _, _ => { });
            if (!predicted || item is null || !Game1.player.couldInventoryAcceptThisItem(item))
                reasons.Add("rummage_garbage_direct_inventory_capacity_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickGarbageCanRummage()
    {
        if (activeGarbageCanRummage is null) return;
        var active = activeGarbageCanRummage;
        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Location))
        {
            CompleteGarbageCanBlocked(active, "rummage_garbage_location_changed");
            return;
        }
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteGarbageCanBlocked(active, "rummage_garbage_timeout");
            return;
        }
        if (active.ActionIssued)
        {
            var status = GarbageCanPostconditionStatus(active);
            if (status == "verified") CompleteGarbageCanRummage(active);
            else if (status != "pending") CompleteGarbageCanBlocked(active, status);
            return;
        }
        if (Game1.player.UsingTool || Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteGarbageCanBlocked(active, "rummage_garbage_player_busy_during_execution");
            return;
        }
        var playerTile = Game1.player.TilePoint;
        if (playerTile != active.LastObservedTile)
        {
            active.MovementTiles += ManhattanDistance(active.LastObservedTile, playerTile);
            active.LastObservedTile = playerTile;
            if (active.MovementTiles > active.MaxMovementTiles)
            {
                CompleteGarbageCanBlocked(active, "rummage_garbage_movement_budget_exceeded");
                return;
            }
        }
        if (playerTile != active.Stand)
        {
            if (active.PathIndex >= active.Path.Count)
            {
                CompleteGarbageCanBlocked(active, "rummage_garbage_path_exhausted_before_stand");
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
                CompleteGarbageCanBlocked(active, "rummage_garbage_dynamic_path_blocked");
                return;
            }
            var moved = Vector2.DistanceSquared(active.LastPosition, Game1.player.Position) >= 0.01f;
            active.LastPosition = Game1.player.Position;
            StartMoving(DirectionTo(playerTile, next));
            MovePlayerForTick();
            if (Game1.player.TilePoint == next) active.PathIndex++;
            if (!moved && ++active.StuckTicks > 45) CompleteGarbageCanBlocked(active, "rummage_garbage_movement_stuck");
            else if (moved) active.StuckTicks = 0;
            return;
        }

        StopAllMovement();
        if (Game1.player.CurrentToolIndex != active.RestoreSlotIndex || Game1.player.Items[active.SafeSlotIndex] is not null)
        {
            CompleteGarbageCanBlocked(active, "rummage_garbage_safe_slot_drifted_before_action");
            return;
        }
        active.OutputsBefore = CaptureGarbageOutputs(active.Location);
        active.FriendshipBefore = active.Projection.Reaction?.FriendshipPointsBefore;
        Game1.player.faceDirection(DirectionTo(playerTile, active.Interaction));
        var handled = false;
        try
        {
            Game1.player.CurrentToolIndex = active.SafeSlotIndex;
            handled = active.Location.checkAction(
                new TileLocation(active.Interaction.X, active.Interaction.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
        }
        finally
        {
            Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        }
        active.ActionIssued = true;
        if (!handled) CompleteGarbageCanBlocked(active, "rummage_garbage_native_action_not_handled");
    }

    private static string GarbageCanPostconditionStatus(ActiveGarbageCanRummage active)
    {
        if (active.OutputsBefore is null) return "rummage_garbage_output_baseline_missing";
        if (!Game1.netWorldState.Value.CheckedGarbage.Contains(active.Projection.Id)) return "pending";
        if (Game1.stats.Get("trashCansChecked") - active.TrashCansCheckedBefore != 1)
            return "rummage_garbage_stat_delta_mismatch";
        var after = CaptureGarbageOutputs(active.Location);
        if (!GarbageOutputDeltaMatches(active.OutputsBefore, after, active.Projection.Output))
            return active.ElapsedTicks < active.MaxTicks - 30 ? "pending" : "rummage_garbage_output_delta_mismatch";
        if (active.Projection.Reaction is not null)
        {
            if (!Game1.player.friendshipData.TryGetValue(active.Projection.Reaction.Name, out var friendship) ||
                friendship.Points != active.Projection.Reaction.ExpectedFriendshipPointsAfter)
                return "rummage_garbage_friendship_delta_mismatch";
        }
        return Game1.player.CurrentToolIndex == active.RestoreSlotIndex
            ? "verified"
            : "rummage_garbage_restore_slot_mismatch";
    }

    private void CompleteGarbageCanRummage(ActiveGarbageCanRummage active)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        if (Game1.activeClickableMenu is not null) Game1.activeClickableMenu.exitThisMenuNoSound();
        activeGarbageCanRummage = null;
        var request = active.Pending.Request;
        var outputAfter = CaptureGarbageOutputs(active.Location);
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
            PrimitiveKind = "rummage_garbage",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[]
            {
                "native_checkAction_CheckGarbage_invoked",
                "checked_set_and_stat_delta_verified",
                "deterministic_output_receipt_verified",
                "npc_friendship_branch_verified",
                "safe_empty_slot_restored"
            },
            RequestedEffect = GarbageCanRequestedEffect(active),
            ObservedEffect = GarbageCanObservedEffect(active.Location, active.Projection, outputAfter),
            ChangedFacts = GarbageCanChangedFacts(active, outputAfter)
        };
        ApplyQuestResourceSourceFeedback(result, request);
        ApplySpecialOrderCollectSourceFeedback(result, request);
        active.Pending.Completion.SetResult(result);
    }

    private void CompleteGarbageCanBlocked(ActiveGarbageCanRummage active, string reason)
    {
        StopAllMovement();
        Game1.player.CurrentToolIndex = active.RestoreSlotIndex;
        activeGarbageCanRummage = null;
        active.Pending.Completion.SetResult(GarbageCanBlocked(active.Pending.Request, reason));
    }

    private static TrainingExecutionResult GarbageCanBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "rummage_garbage", "garbage_can.checked_today=true;trashCansChecked+=1;predicted_output=" + request.ExpectedOutputJson,
            "garbage_can_id=" + request.GarbageCanId + ";checked=" + Game1.netWorldState.Value.CheckedGarbage.Contains(request.GarbageCanId).ToString().ToLowerInvariant() +
            ";trashCansChecked=" + Game1.stats.Get("trashCansChecked"), reasons);

    private static Dictionary<ClearanceOutputItemKey, int> CaptureGarbageOutputs(GameLocation location)
    {
        var result = new Dictionary<ClearanceOutputItemKey, int>();
        foreach (var item in Game1.player.Items.Where(item => item is not null))
        {
            var key = ClearanceOutputItemKey.FromInventoryReceipt(item!);
            result[key] = result.GetValueOrDefault(key) + Math.Max(1, item!.Stack);
        }
        foreach (var debris in location.debris.Where(debris => debris.item is not null))
        {
            var item = debris.item!;
            var key = ClearanceOutputItemKey.FromInventoryReceipt(item);
            result[key] = result.GetValueOrDefault(key) + Math.Max(1, item.Stack);
        }
        return result;
    }

    private static bool GarbageOutputDeltaMatches(
        IReadOnlyDictionary<ClearanceOutputItemKey, int> before,
        IReadOnlyDictionary<ClearanceOutputItemKey, int> after,
        RuntimeGarbageOutput? expected)
    {
        foreach (var key in before.Keys.Concat(after.Keys).Concat(expected is null ? Array.Empty<ClearanceOutputItemKey>() : new[] { expected.Key }).Distinct())
        {
            var delta = after.GetValueOrDefault(key) - before.GetValueOrDefault(key);
            var expectedDelta = expected is not null && key == expected.Key ? expected.Quantity : 0;
            if (delta != expectedDelta) return false;
        }
        return true;
    }

    private static bool RuntimeGarbageOutputMatches(RuntimeGarbageOutput? output, string expectedJson)
    {
        try
        {
            using var document = JsonDocument.Parse(expectedJson);
            if (document.RootElement.ValueKind == JsonValueKind.Null) return output is null;
            if (output is null || document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var row = document.RootElement;
            return ReadRuntimeString(row, "runtime_type") == output.Key.RuntimeType &&
                ReadRuntimeString(row, "qualified_item_id") == output.Key.QualifiedItemId &&
                ReadRuntimeInt(row, "quality") == output.Key.Quality &&
                ReadRuntimeString(row, "unit_state_sha256") == output.Key.UnitStateSha256 &&
                ReadRuntimeInt(row, "quantity") == output.Quantity &&
                ReadRuntimeInt(row, "unit_sale_price") == output.UnitSalePrice &&
                ReadRuntimeStringArray(row, "context_tags").SequenceEqual(output.ContextTags, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static RuntimeGarbageReaction? ProjectRuntimeGarbageReaction(NPC? npc, Point target)
    {
        if (npc is null) return null;
        var effect = npc.GetData()?.DumpsterDiveFriendshipEffect ?? -25;
        var before = Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship) ? friendship.Points : (int?)null;
        var expectedDelta = 0;
        var expectedAfter = before;
        if (before.HasValue && npc.IsVillager)
        {
            var applied = effect > 0 && Game1.player.stats.Get("Book_Friendship") != 0 ? (int)(effect * 1.1f) : effect;
            var maximum = (Utility.GetMaximumHeartsForCharacter(npc) + 1) * NPC.friendshipPointsPerHeartLevel - 1;
            expectedAfter = Math.Clamp(before.Value + applied, 0, maximum);
            expectedDelta = expectedAfter.Value - before.Value;
        }
        return new RuntimeGarbageReaction(
            npc.Name, npc.GetType().FullName ?? npc.GetType().Name, npc.TilePoint.X, npc.TilePoint.Y,
            Vector2.Distance(npc.Tile, target.ToVector2()), effect, expectedDelta, before, expectedAfter,
            npc.Name == "Linus" && effect == 5 && expectedDelta >= 0 ? "exact_linus_non_negative" : "negative_or_unverified_witness");
    }

    private static bool RuntimeGarbageReactionMatches(RuntimeGarbageReaction? reaction, string expectedJson)
    {
        try
        {
            using var document = JsonDocument.Parse(expectedJson);
            if (document.RootElement.ValueKind == JsonValueKind.Null) return reaction is null;
            if (reaction is null || document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var row = document.RootElement;
            return ReadRuntimeString(row, "name") == reaction.Name &&
                ReadRuntimeString(row, "runtime_type") == reaction.RuntimeType &&
                ReadRuntimeInt(row, "tile_x") == reaction.TileX && ReadRuntimeInt(row, "tile_y") == reaction.TileY &&
                Math.Abs(ReadRuntimeDouble(row, "distance") - reaction.Distance) < 0.0001 &&
                ReadRuntimeInt(row, "dumpster_dive_friendship_effect") == reaction.BaseFriendshipEffect &&
                ReadRuntimeInt(row, "expected_friendship_delta") == reaction.ExpectedFriendshipDelta &&
                ReadRuntimeNullableInt(row, "friendship_points_before") == reaction.FriendshipPointsBefore &&
                ReadRuntimeNullableInt(row, "expected_friendship_points_after") == reaction.ExpectedFriendshipPointsAfter &&
                ReadRuntimeString(row, "reaction_status") == reaction.Status;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeRuntimeGarbageCanId(string id) => id switch
    {
        "0" => "JodiAndKent", "1" => "EmilyAndHaley", "2" => "Mayor", "3" => "Museum",
        "4" => "Blacksmith", "5" => "Saloon", "6" => "Evelyn", "7" => "JojaMart", _ => id
    };

    private static string ReadRuntimeString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static int ReadRuntimeInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;
    private static int? ReadRuntimeNullableInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.TryGetInt32(out var parsed) ? parsed : null;
    private static double ReadRuntimeDouble(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.TryGetDouble(out var parsed) ? parsed : 0d;
    private static string[] ReadRuntimeStringArray(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
            : Array.Empty<string>();

    private static string GarbageCanRequestedEffect(ActiveGarbageCanRummage active) =>
        "garbage_can_id=" + active.Projection.Id + ";checked_today=true;trashCansChecked_delta=1;predicted_output=" + active.Pending.Request.ExpectedOutputJson;

    private static string GarbageCanObservedEffect(GameLocation location, RuntimeGarbageCanProjection projection, IReadOnlyDictionary<ClearanceOutputItemKey, int> outputs) =>
        "location=" + location.NameOrUniqueName + ";garbage_can_id=" + projection.Id +
        ";checked=" + Game1.netWorldState.Value.CheckedGarbage.Contains(projection.Id).ToString().ToLowerInvariant() +
        ";trashCansChecked=" + Game1.stats.Get("trashCansChecked").ToString(CultureInfo.InvariantCulture) +
        ";outputs=" + string.Join(",", outputs.OrderBy(row => row.Key.QualifiedItemId).Select(row => row.Key.QualifiedItemId + "@" + row.Key.UnitStateSha256[..8] + "=" + row.Value));

    private static SimulatedFactChange[] GarbageCanChangedFacts(ActiveGarbageCanRummage active, IReadOnlyDictionary<ClearanceOutputItemKey, int> after)
    {
        var changed = new List<SimulatedFactChange>
        {
            new() { Path = "current_location.garbage_cans[" + active.Projection.Id + "].checked_today", Before = "false", After = "true" },
            new() { Path = "player.stats.trashCansChecked", Before = active.TrashCansCheckedBefore.ToString(CultureInfo.InvariantCulture), After = Game1.stats.Get("trashCansChecked").ToString(CultureInfo.InvariantCulture) }
        };
        if (active.Projection.Output is not null && active.OutputsBefore is not null)
            changed.Add(new SimulatedFactChange
            {
                Path = "combined_inventory_debris_output[" + active.Projection.Output.Key.QualifiedItemId + "," + active.Projection.Output.Key.UnitStateSha256 + "]",
                Before = active.OutputsBefore.GetValueOrDefault(active.Projection.Output.Key).ToString(CultureInfo.InvariantCulture),
                After = after.GetValueOrDefault(active.Projection.Output.Key).ToString(CultureInfo.InvariantCulture)
            });
        if (active.Projection.Reaction is not null)
            changed.Add(new SimulatedFactChange
            {
                Path = "player.friendships[" + active.Projection.Reaction.Name + "].points",
                Before = active.Projection.Reaction.FriendshipPointsBefore?.ToString(CultureInfo.InvariantCulture) ?? "missing",
                After = active.Projection.Reaction.ExpectedFriendshipPointsAfter?.ToString(CultureInfo.InvariantCulture) ?? "missing"
            });
        return changed.ToArray();
    }

    private sealed class ActiveGarbageCanRummage
    {
        public ActiveGarbageCanRummage(PendingExecution pending, GameLocation location, Point target, Point interaction, Point stand,
            List<Point> path, RuntimeGarbageCanProjection projection, int maxMovementTiles)
        {
            Pending = pending; Location = location; Target = target; Interaction = interaction; Stand = stand; Path = path;
            Projection = projection; MaxMovementTiles = maxMovementTiles;
            SafeSlotIndex = pending.Request.SafeSlotIndex!.Value; RestoreSlotIndex = pending.Request.RestoreSlotIndex!.Value;
            TrashCansCheckedBefore = Game1.stats.Get("trashCansChecked");
            LastPosition = Game1.player.Position; LastObservedTile = Game1.player.TilePoint;
        }
        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Interaction { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public RuntimeGarbageCanProjection Projection { get; }
        public int MaxMovementTiles { get; }
        public int SafeSlotIndex { get; }
        public int RestoreSlotIndex { get; }
        public uint TrashCansCheckedBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool ActionIssued { get; set; }
        public int? FriendshipBefore { get; set; }
        public Dictionary<ClearanceOutputItemKey, int>? OutputsBefore { get; set; }
    }

    private sealed record RuntimeGarbageCanProjection(
        string Action, string Id, bool KnownId, string PayloadHash, string[] Errors, bool Produced, string SelectedEntryId,
        bool IgnoreBaseChance, bool MegaSuccess, bool DoubleMegaSuccess, string OutputDelivery,
        RuntimeGarbageOutput? Output, RuntimeGarbageReaction? Reaction);
    private sealed record RuntimeGarbageOutput(ClearanceOutputItemKey Key, int Quantity, string[] ContextTags, int UnitSalePrice);
    private sealed record RuntimeGarbageReaction(
        string Name, string RuntimeType, int TileX, int TileY, float Distance, int BaseFriendshipEffect,
        int ExpectedFriendshipDelta, int? FriendshipPointsBefore, int? ExpectedFriendshipPointsAfter, string Status);
}
