using System.Security.Cryptography;
using System.Text;
using Microsoft.Xna.Framework;
using StardewAI.TransparentBridge.State;
using StardewValley;
using StardewValley.Objects;
using StardewValley.SaveSerialization;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string SignDisplayItemNativeContract =
        "GameLocation.checkAction->Sign.checkForAction(CurrentItem.getOne,no_inventory_consumption)->displayItem/displayType";

    private static object ReadSignState(GameLocation location, Vector2 tile, StardewObject item, Farmer player)
    {
        if (item is Sign sign)
        {
            return new
            {
                status = sign.GetType() == typeof(Sign) ? "available" : "custom_runtime_type_blocked",
                placement_kind = "display_item_sign",
                runtime_type_supported = sign.GetType() == typeof(Sign),
                native_runtime_type = sign.GetType().FullName,
                display_type = sign.displayType.Value,
                display_item = SummarizeItem(sign.displayItem.Value),
                display_item_runtime_type = sign.displayItem.Value?.GetType().FullName ?? string.Empty,
                display_item_state = ReadDirectItemState(sign.displayItem.Value)?.ToProjection(),
                display_item_special_state = sign.displayItem.Value is null
                    ? null
                    : FarmReadAdapter.ReadItemSpecialState(sign.displayItem.Value),
                display_assignment = ReadSignDisplayAssignment(location, tile, sign, player),
                sign_text = string.Empty,
                show_next_index = sign.showNextIndex.Value,
                is_passable = sign.isPassable()
            };
        }
        if (item.IsTextSign())
        {
            return new
            {
                status = item.GetType() == typeof(StardewObject) ? "available" : "custom_runtime_type_blocked",
                placement_kind = "text_sign",
                runtime_type_supported = item.GetType() == typeof(StardewObject),
                native_runtime_type = item.GetType().FullName,
                display_type = 0,
                display_item = (object?)null,
                display_item_runtime_type = string.Empty,
                display_item_state = (object?)null,
                display_item_special_state = (object?)null,
                display_assignment = new { status = "not_applicable_text_sign" },
                sign_text = item.SignText ?? string.Empty,
                show_next_index = item.showNextIndex.Value,
                is_passable = item.isPassable()
            };
        }
        return new { status = "not_sign", runtime_type_supported = false };
    }

    private static object ReadSignDisplayAssignment(GameLocation location, Vector2 tile, Sign sign, Farmer player)
    {
        if (sign.GetType() != typeof(Sign))
        {
            return new { status = "unsupported_sign_runtime_type" };
        }
        if (SnapshotProfileContext.Current is not "full")
        {
            return new { status = "blocked_requires_full_profile" };
        }

        var previous = ReadDirectItemState(sign.displayItem.Value);
        var targetState = ReadDirectItemState(sign)!;
        var rows = player.Items
            .Select((candidate, slot) => new { candidate, slot })
            .Where(entry => entry.candidate is not null)
            .Select(entry =>
            {
                var source = entry.candidate!;
                var state = ReadDirectItemState(source)!;
                return new
                {
                    inventory_slot_index = entry.slot,
                    item_id = source.ItemId,
                    qualified_item_id = source.QualifiedItemId,
                    display_name = source.DisplayName,
                    stack = source.Stack,
                    quality = source.Quality,
                    source_runtime_type = source.GetType().FullName ?? source.GetType().Name,
                    source_state_status = state.StateStatus,
                    source_state_sha256 = state.StateSha256,
                    source_state_bytes = state.StateBytes,
                    expected_display_type = SignDisplayType(source),
                    expected_source_stack_after = source.Stack,
                    expected_display_stack = 1
                };
            })
            .ToArray();
        var previousQid = sign.displayItem.Value?.QualifiedItemId ?? string.Empty;
        var previousType = sign.displayItem.Value?.GetType().FullName ?? string.Empty;
        var previousHash = previous?.StateSha256 ?? string.Empty;
        var readyRows = rows.Count(row => string.Equals(row.source_state_status, "exact_live_direct_serialization", StringComparison.Ordinal));
        var fingerprintText = string.Join("|", new[]
        {
            location.NameOrUniqueName,
            ((int)tile.X).ToString(),
            ((int)tile.Y).ToString(),
            sign.GetType().FullName ?? string.Empty,
            sign.QualifiedItemId,
            targetState.StateStatus,
            targetState.StateSha256,
            sign.displayType.Value.ToString(),
            previousQid,
            previousType,
            previousHash,
            SignDisplayItemNativeContract,
            string.Join(";", rows.Select(row =>
                row.inventory_slot_index + ":" + row.qualified_item_id + ":" + row.stack + ":" +
                row.quality + ":" + row.source_runtime_type + ":" + row.source_state_status + ":" + row.source_state_sha256 + ":" +
                row.expected_display_type))
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintText))).ToLowerInvariant();
        return new
        {
            status = !string.Equals(targetState.StateStatus, "exact_live_direct_serialization", StringComparison.Ordinal)
                ? "target_sign_state_unavailable"
                : sign.displayItem.Value is not null && string.IsNullOrWhiteSpace(previousHash)
                ? "previous_payload_state_unavailable"
                : readyRows > 0
                    ? "ready"
                    : "serializable_current_item_required",
            target_location = location.NameOrUniqueName,
            target_tile_x = (int)tile.X,
            target_tile_y = (int)tile.Y,
            target_runtime_type = typeof(Sign).FullName,
            target_qualified_item_id = sign.QualifiedItemId,
            target_state_sha256 = targetState.StateSha256,
            target_projection_fingerprint = fingerprint,
            previous_display_item_qualified_item_id = previousQid,
            previous_display_item_runtime_type = previousType,
            previous_display_item_state_sha256 = previousHash,
            previous_display_type = sign.displayType.Value,
            replace_existing_display = sign.displayItem.Value is not null,
            inventory_rows = rows,
            native_contract = SignDisplayItemNativeContract
        };
    }

    private static int SignDisplayType(Item item) => item switch
    {
        Hat => 2,
        Ring => 4,
        Furniture => 5,
        StardewObject { bigCraftable.Value: true } => 3,
        _ => 1
    };

    private static DirectItemState? ReadDirectItemState(Item? item)
    {
        if (item is null)
        {
            return null;
        }
        try
        {
            using var stream = new MemoryStream();
            SaveSerializer.GetSerializer(item.GetType()).Serialize(stream, item);
            var bytes = stream.ToArray();
            return new DirectItemState(
                "exact_live_direct_serialization",
                item.GetType().FullName ?? item.GetType().Name,
                item.ItemId,
                item.QualifiedItemId,
                item.Stack,
                item.Quality,
                bytes.Length,
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                item.HasBeenInInventory,
                item.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        }
        catch (Exception ex)
        {
            return new DirectItemState(
                "serialization_failed:" + ex.GetType().Name,
                item.GetType().FullName ?? item.GetType().Name,
                item.ItemId,
                item.QualifiedItemId,
                item.Stack,
                item.Quality,
                0,
                string.Empty,
                item.HasBeenInInventory,
                item.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray());
        }
    }

    private sealed record DirectItemState(
        string StateStatus,
        string RuntimeType,
        string ItemId,
        string QualifiedItemId,
        int Stack,
        int Quality,
        int StateBytes,
        string StateSha256,
        bool HasBeenInInventory,
        string[] ContextTags)
    {
        public object ToProjection() => new
        {
            state_status = StateStatus,
            runtime_type = RuntimeType,
            item_id = ItemId,
            qualified_item_id = QualifiedItemId,
            stack = Stack,
            quality = Quality,
            state_bytes = StateBytes,
            state_sha256 = StateSha256,
            has_been_in_inventory = HasBeenInInventory,
            context_tags = ContextTags
        };
    }
}
