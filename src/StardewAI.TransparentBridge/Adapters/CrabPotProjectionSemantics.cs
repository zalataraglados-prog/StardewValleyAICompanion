using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

internal static class CrabPotProjectionSemantics
{
    public static bool IsFishCollectionEligible(
        Item? output,
        IDictionary<string, string> fishData)
    {
        if (output is null || !fishData.ContainsKey(output.ItemId))
            return false;

        var metadata = ItemRegistry.GetMetadata(output.QualifiedItemId);
        var parsedData = metadata.GetParsedData();
        return metadata.Exists() &&
            !ItemContextTagManager.HasBaseTag(
                metadata.QualifiedItemId,
                "trash_item") &&
            metadata.QualifiedItemId != "(O)167" &&
            (parsedData?.ObjectType == "Fish" ||
             metadata.QualifiedItemId == "(O)372");
    }
}
