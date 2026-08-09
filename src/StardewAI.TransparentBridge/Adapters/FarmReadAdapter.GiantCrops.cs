using System.Text.Json.Serialization;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    internal static GiantCropOutputProjection[] ReadGuaranteedGiantCropOutputs(GiantCrop giantCrop)
    {
        if (!GiantCrop.TryGetData(giantCrop.Id, out var data) || data?.HarvestItems is null)
        {
            return Array.Empty<GiantCropOutputProjection>();
        }

        return data.HarvestItems
            .Where(drop =>
                drop.Chance >= 1f &&
                string.IsNullOrWhiteSpace(drop.Condition) &&
                drop.ForShavingEnchantment != true &&
                string.IsNullOrWhiteSpace(drop.PerItemCondition) &&
                (drop.RandomItemId is null || drop.RandomItemId.Count == 0) &&
                (drop.StackModifiers is null || drop.StackModifiers.Count == 0) &&
                !string.IsNullOrWhiteSpace(drop.ItemId))
            .Select(TryReadGuaranteedGiantCropOutput)
            .Where(output => output is not null)
            .Select(output => output!)
            .ToArray();
    }

    private static GiantCropOutputProjection? TryReadGuaranteedGiantCropOutput(
        StardewValley.GameData.GiantCrops.GiantCropHarvestItemData drop)
    {
        try
        {
            var qualifiedItemId = ItemRegistry.QualifyItemId(drop.ItemId);
            if (string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                return null;
            }

            var item = ItemRegistry.Create(qualifiedItemId);
            return new GiantCropOutputProjection(
                qualifiedItemId,
                item.GetContextTags().OrderBy(tag => tag, StringComparer.Ordinal).ToArray(),
                Math.Max(1, drop.MinStack),
                Math.Max(Math.Max(1, drop.MinStack), drop.MaxStack));
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record GiantCropOutputProjection(
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("context_tags")] string[] ContextTags,
    [property: JsonPropertyName("quantity_min")] int QuantityMin,
    [property: JsonPropertyName("quantity_max")] int QuantityMax);
