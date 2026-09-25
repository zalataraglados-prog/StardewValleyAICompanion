using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static object[] ReadMachineOutputAuthoritativeRouteSources(
        GameLocation location,
        Vector2 tile,
        StardewValley.Object machine)
    {
        return ReadSolarPanelOutputAuthoritativeRouteSources(machine)
            .Concat(ReadWildTreeTapperOutputAuthoritativeRouteSources(
                location,
                tile,
                machine))
            .ToArray();
    }

    private static object[]
        ReadWildTreeTapperOutputAuthoritativeRouteSources(
            GameLocation location,
            Vector2 tile,
            StardewValley.Object machine)
    {
        var output = machine.heldObject.Value;
        if (!machine.readyForHarvest.Value ||
            !machine.IsTapper() ||
            output is null ||
            !location.terrainFeatures.TryGetValue(tile, out var feature) ||
            feature is not Tree tree ||
            tree.GetType() != typeof(Tree) ||
            !tree.tapped.Value)
        {
            return Array.Empty<object>();
        }

        var data = tree.GetData();
        if (data?.TapItems is null)
            return Array.Empty<object>();

        var expectedItemId = UnqualifiedMachineOutputObjectId(
            output.QualifiedItemId);
        var sources = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var rowIndex = 0;
            rowIndex < data.TapItems.Count;
            rowIndex++)
        {
            var row = data.TapItems[rowIndex];
            var sourcePrefix = "wild_tree:" + tree.treeType.Value +
                ":" + rowIndex;
            AddWildTreeTapperSource(
                sources,
                seen,
                sourcePrefix,
                row.ItemId,
                expectedItemId);
            if (row.RandomItemId is null)
                continue;
            for (var randomIndex = 0;
                randomIndex < row.RandomItemId.Count;
                randomIndex++)
            {
                AddWildTreeTapperSource(
                    sources,
                    seen,
                    sourcePrefix + ":random:" + randomIndex,
                    row.RandomItemId[randomIndex],
                    expectedItemId);
            }
        }
        return sources.ToArray();
    }

    private static void AddWildTreeTapperSource(
        ICollection<object> sources,
        ISet<string> seen,
        string sourceId,
        string? rawItemIds,
        string expectedItemId)
    {
        if (string.IsNullOrWhiteSpace(rawItemIds))
            return;

        foreach (var itemId in rawItemIds.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries)
                 .Select(UnqualifiedMachineOutputObjectId)
                 .Where(itemId => string.Equals(
                     itemId,
                     expectedItemId,
                     StringComparison.Ordinal))
                 .Distinct(StringComparer.Ordinal))
        {
            if (!seen.Add(sourceId + "|" + itemId))
                continue;
            sources.Add(new
            {
                route_kind = "native_wild_tree_tapper_output",
                source_id = sourceId,
                qualified_item_id = "(O)" + itemId
            });
        }
    }

    private static string UnqualifiedMachineOutputObjectId(
        string? itemId) =>
        itemId?.StartsWith("(O)", StringComparison.Ordinal) == true
            ? itemId[3..]
            : itemId ?? string.Empty;
}
