using StardewValley;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private static object ReadDonateColorContext(Item? item)
    {
        var coloredObject = item as ColoredObject;
        var parentItemId = coloredObject?.preservedParentSheetIndex.Value;
        var parentTags = string.IsNullOrWhiteSpace(parentItemId)
            ? Array.Empty<string>()
            : ItemContextTagManager.GetBaseContextTags(parentItemId)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();
        var projectionStatus = coloredObject is null
            ? "not_applicable_not_colored_object"
            : string.IsNullOrWhiteSpace(parentItemId)
                ? "not_applicable_no_preserved_parent"
                : "exact_native_preserved_parent_base_context_tags";

        return new
        {
            is_colored_object = coloredObject is not null,
            preserved_parent_item_id = parentItemId,
            preserved_parent_base_context_tags = parentTags,
            projection_status = projectionStatus
        };
    }
}
