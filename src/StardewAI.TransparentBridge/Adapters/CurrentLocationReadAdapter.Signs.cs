using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object ReadSignState(StardewValley.Object item)
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
                display_item_special_state = sign.displayItem.Value is null
                    ? null
                    : FarmReadAdapter.ReadItemSpecialState(sign.displayItem.Value),
                sign_text = string.Empty,
                show_next_index = sign.showNextIndex.Value,
                is_passable = sign.isPassable()
            };
        }
        if (item.IsTextSign())
        {
            return new
            {
                status = item.GetType() == typeof(StardewValley.Object) ? "available" : "custom_runtime_type_blocked",
                placement_kind = "text_sign",
                runtime_type_supported = item.GetType() == typeof(StardewValley.Object),
                native_runtime_type = item.GetType().FullName,
                display_type = 0,
                display_item = (object?)null,
                display_item_runtime_type = string.Empty,
                display_item_special_state = (object?)null,
                sign_text = item.SignText ?? string.Empty,
                show_next_index = item.showNextIndex.Value,
                is_passable = item.isPassable()
            };
        }
        return new { status = "not_sign", runtime_type_supported = false };
    }
}
