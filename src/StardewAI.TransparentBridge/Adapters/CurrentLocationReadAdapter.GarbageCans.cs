using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.GarbageCans;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string GarbageCanDataPayloadSha256 = "34621d9c92c472019c6e0a6bae4ac86a62576b7bccae4b9191590ed11e46911f";
    internal const string GarbageCanNativeContract =
        "GameLocation.checkAction -> performAction Garbage -> CheckGarbage -> TryGetGarbageItem -> CheckedGarbage/stat/output/native NPC reaction; no direct checked-set, stat, friendship, inventory, debris, or RNG mutation";

    private static readonly JsonSerializerOptions GarbageCanPayloadOptions = new()
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static object ReadGarbageCanAction(GameLocation location, string action, string[] parts, int x, int y)
    {
        var rawId = parts.Length > 1 ? parts[1] : string.Empty;
        var id = NormalizeGarbageCanId(rawId);
        var data = DataLoader.GarbageCans(Game1.content);
        var payloadHash = GarbagePayloadHash(data);
        var dataStatus = string.Equals(payloadHash, GarbageCanDataPayloadSha256, StringComparison.Ordinal)
            ? "exact_locked_base_1.6.15"
            : "drifted_data_garbage_cans_payload";
        var knownId = !string.IsNullOrWhiteSpace(id) && data.GarbageCans.ContainsKey(id);
        var checkedBefore = !string.IsNullOrWhiteSpace(id) && Game1.netWorldState.Value.CheckedGarbage.Contains(id);
        var errors = new List<string>();
        Item? item = null;
        GarbageCanItemData? selected = null;
        var produced = false;
        if (knownId && dataStatus == "exact_locked_base_1.6.15")
        {
            produced = location.TryGetGarbageItem(id, Game1.player.DailyLuck, out item, out selected, out _, errors.Add);
        }

        var output = item is null ? null : ClearanceOutputItemProjection.FromInventoryReceipt(item);
        var outputContextTags = item?.GetContextTags()
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        var outputUnitSalePrice = item?.sellToStorePrice(-1L) ?? 0;
        var reactingNpc = Utility.GetNpcsWithinDistance(new Microsoft.Xna.Framework.Vector2(x, y), 7, location)
            .FirstOrDefault(npc => npc is not Horse);
        var reaction = ReadGarbageCanReaction(reactingNpc, x, y);
        var safeSlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .Cast<int?>()
            .FirstOrDefault(index => Game1.player.Items[index!.Value] is null);
        var directInventoryAccepted = item is null || selected?.AddToInventoryDirectly != true || Game1.player.couldInventoryAcceptThisItem(item);
        var status = dataStatus != "exact_locked_base_1.6.15"
            ? "blocked_data_garbage_cans_drifted"
            : !knownId
                ? "blocked_unknown_garbage_can_id"
                : errors.Count > 0
                    ? "blocked_native_prediction_error"
                    : checkedBefore
                        ? "blocked_already_checked_today"
                        : reaction is not null && reaction.Status != "exact_linus_non_negative"
                            ? "blocked_negative_friendship_witness"
                            : !directInventoryAccepted
                                ? "blocked_direct_inventory_output_not_accepted"
                                : safeSlot is null
                                    ? "blocked_empty_toolbar_slot_required"
                                    : "ready";
        var fingerprintSource = string.Join("|", location.NameOrUniqueName, x, y, action, id, payloadHash,
            checkedBefore, Game1.stats.Get("trashCansChecked"), Game1.player.DailyLuck, Game1.player.stats.Get("Book_Trash"),
            selected?.Id ?? string.Empty, output?.RuntimeType ?? string.Empty, output?.QualifiedItemId ?? string.Empty,
            output?.Quality ?? 0, output?.UnitStateSha256 ?? string.Empty, output?.Quantity ?? 0,
            string.Join(",", outputContextTags), outputUnitSalePrice,
            selected?.AddToInventoryDirectly ?? false, selected?.CreateMultipleDebris ?? false,
            reaction?.Name ?? string.Empty, reaction?.ExpectedFriendshipDelta ?? 0, safeSlot, Game1.player.CurrentToolIndex);

        return new
        {
            location_id = location.NameOrUniqueName,
            tile_x = x,
            tile_y = y,
            action,
            action_type = "Garbage",
            raw_garbage_can_id = rawId,
            garbage_can_id = id,
            garbage_can_id_known = knownId,
            checked_today = checkedBefore,
            expected_checked_today_after = true,
            trash_cans_checked_before = Game1.stats.Get("trashCansChecked"),
            expected_trash_cans_checked_delta = 1,
            daily_luck = Game1.player.DailyLuck,
            alleyway_buffet_read = Game1.player.stats.Get("Book_Trash") != 0,
            predicted_item_produced = produced,
            selected_entry_id = selected?.Id ?? string.Empty,
            selected_ignore_base_chance = selected?.IgnoreBaseChance ?? false,
            selected_mega_success = selected?.IsMegaSuccess ?? false,
            selected_double_mega_success = selected?.IsDoubleMegaSuccess ?? false,
            output_delivery = selected?.AddToInventoryDirectly == true
                ? "direct_inventory"
                : selected?.CreateMultipleDebris == true
                    ? "multiple_debris"
                    : produced ? "single_debris" : "none",
            direct_inventory_accepted = directInventoryAccepted,
            expected_output = output is null ? null : new
            {
                runtime_type = output.RuntimeType,
                qualified_item_id = output.QualifiedItemId,
                quality = output.Quality,
                unit_state_sha256 = output.UnitStateSha256,
                quantity = output.Quantity,
                context_tags = outputContextTags,
                unit_sale_price = outputUnitSalePrice
            },
            reacting_npc = reaction is null ? null : new
            {
                name = reaction.Name,
                runtime_type = reaction.RuntimeType,
                tile_x = reaction.TileX,
                tile_y = reaction.TileY,
                distance = reaction.Distance,
                dumpster_dive_friendship_effect = reaction.BaseFriendshipEffect,
                expected_friendship_delta = reaction.ExpectedFriendshipDelta,
                friendship_points_before = reaction.FriendshipPointsBefore,
                expected_friendship_points_after = reaction.ExpectedFriendshipPointsAfter,
                reaction_status = reaction.Status
            },
            safe_slot_index = safeSlot,
            restore_slot_index = Game1.player.CurrentToolIndex,
            data_payload_sha256 = payloadHash,
            data_contract_status = dataStatus,
            prediction_status = errors.Count == 0 ? "exact_native_non_mutating_prediction" : "native_prediction_error",
            prediction_errors = errors.ToArray(),
            native_contract = GarbageCanNativeContract,
            projection_fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource))).ToLowerInvariant(),
            rummage_status = status
        };
    }

    private static GarbageCanNpcReaction? ReadGarbageCanReaction(NPC? npc, int targetX, int targetY)
    {
        if (npc is null)
        {
            return null;
        }
        var effect = npc.GetData()?.DumpsterDiveFriendshipEffect ?? -25;
        var before = Game1.player.friendshipData.TryGetValue(npc.Name, out var friendship) ? friendship.Points : (int?)null;
        var expectedDelta = 0;
        var expectedAfter = before;
        if (before.HasValue && npc.IsVillager)
        {
            var applied = effect > 0 && Game1.player.stats.Get("Book_Friendship") != 0
                ? (int)(effect * 1.1f)
                : effect;
            var maximum = (Utility.GetMaximumHeartsForCharacter(npc) + 1) * NPC.friendshipPointsPerHeartLevel - 1;
            expectedAfter = Math.Clamp(before.Value + applied, 0, maximum);
            expectedDelta = expectedAfter.Value - before.Value;
        }
        var exactSafePositive = npc.Name == "Linus" && effect == 5 && expectedDelta >= 0;
        return new GarbageCanNpcReaction(
            npc.Name,
            npc.GetType().FullName ?? npc.GetType().Name,
            npc.TilePoint.X,
            npc.TilePoint.Y,
            Microsoft.Xna.Framework.Vector2.Distance(npc.Tile, new Microsoft.Xna.Framework.Vector2(targetX, targetY)),
            effect,
            expectedDelta,
            before,
            expectedAfter,
            exactSafePositive ? "exact_linus_non_negative" : "negative_or_unverified_witness");
    }

    private static string GarbagePayloadHash(GarbageCanData data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, typeof(GarbageCanData), GarbageCanPayloadOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string NormalizeGarbageCanId(string id) => id switch
    {
        "0" => "JodiAndKent",
        "1" => "EmilyAndHaley",
        "2" => "Mayor",
        "3" => "Museum",
        "4" => "Blacksmith",
        "5" => "Saloon",
        "6" => "Evelyn",
        "7" => "JojaMart",
        _ => id
    };

    private sealed record GarbageCanNpcReaction(
        string Name,
        string RuntimeType,
        int TileX,
        int TileY,
        float Distance,
        int BaseFriendshipEffect,
        int ExpectedFriendshipDelta,
        int? FriendshipPointsBefore,
        int? ExpectedFriendshipPointsAfter,
        string Status);
}
