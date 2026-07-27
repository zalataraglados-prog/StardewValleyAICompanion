using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static readonly string[]
        MushroomLogOutputQualifiedItemIds =
        {
            "(O)257",
            "(O)281",
            "(O)404",
            "(O)420",
            "(O)422"
        };

    private static MushroomLogTreeRow[]
        ReadMushroomLogNearbyTrees(
            StardewValley.Object machine,
            GameLocation location)
    {
        var rows = new List<MushroomLogTreeRow>();
        var machineX = (int)machine.TileLocation.X;
        var machineY = (int)machine.TileLocation.Y;
        for (var x = machineX - MushroomLogTreeRadius;
            x < machineX + MushroomLogTreeRadius + 1;
            x++)
        {
            for (var y = machineY - MushroomLogTreeRadius;
                y < machineY + MushroomLogTreeRadius + 1;
                y++)
            {
                if (location.terrainFeatures
                        .GetValueOrDefault(
                            new Vector2(x, y)) is not Tree tree)
                {
                    continue;
                }

                rows.Add(
                    new MushroomLogTreeRow(
                        x,
                        y,
                        tree.treeType.Value,
                        tree.growthStage.Value,
                        tree.growthStage.Value >= 5,
                        tree.hasMoss.Value));
            }
        }

        return rows.ToArray();
    }

    private static MushroomLogItemProbability[]
        ReadMushroomLogItemDistribution(
            IReadOnlyCollection<MushroomLogTreeRow>
                nearbyTrees,
            int genericPoolEntryCount,
            int totalPoolEntryCount)
    {
        var weights = MushroomLogOutputQualifiedItemIds
            .ToDictionary(
                id => id,
                _ => 0.0,
                StringComparer.Ordinal);
        foreach (var tree in nearbyTrees.Where(
                     row => row.Mature))
        {
            AddMushroomLogPoolEntryWeights(
                weights,
                tree.TreeType,
                1);
        }
        AddMushroomLogPoolEntryWeights(
            weights,
            treeType: null,
            genericPoolEntryCount);

        return MushroomLogOutputQualifiedItemIds
            .Where(id => weights[id] > 0)
            .Select(id =>
            {
                var item = ItemRegistry.Create(id);
                return new MushroomLogItemProbability(
                    id,
                    SummarizeItem(item),
                    item.salePrice(),
                    weights[id] /
                        totalPoolEntryCount);
            })
            .ToArray();
    }

    private static void AddMushroomLogPoolEntryWeights(
        IDictionary<string, double> weights,
        string? treeType,
        int entryCount)
    {
        if (entryCount <= 0)
        {
            return;
        }

        switch (treeType)
        {
            case "2":
                weights["(O)422"] +=
                    entryCount * 0.1;
                weights["(O)420"] +=
                    entryCount * 0.9;
                return;
            case "1":
                weights["(O)257"] += entryCount;
                return;
            case "3":
                weights["(O)281"] += entryCount;
                return;
            case "13":
                weights["(O)422"] += entryCount;
                return;
            default:
                weights["(O)422"] +=
                    entryCount * 0.05;
                weights["(O)420"] +=
                    entryCount * 0.1425;
                weights["(O)404"] +=
                    entryCount * 0.8075;
                return;
        }
    }

    private static MushroomLogAmountProbability[]
        ReadMushroomLogAmountDistribution(
            int allTreeCount)
    {
        var halfTreeCount = allTreeCount / 2;
        var probabilities =
            new Dictionary<int, double>();
        for (var multiplier = 1;
            multiplier <= 2;
            multiplier++)
        {
            var amount = Math.Clamp(
                multiplier * halfTreeCount,
                1,
                5);
            probabilities[amount] =
                probabilities.GetValueOrDefault(amount) +
                0.5;
        }

        return probabilities
            .OrderBy(row => row.Key)
            .Select(row =>
                new MushroomLogAmountProbability(
                    row.Key,
                    row.Value))
            .ToArray();
    }

    private static MushroomLogQualityProbability[]
        ReadMushroomLogQualityDistribution(
            double successChance)
    {
        var failureChance = 1 - successChance;
        return new[]
        {
            new MushroomLogQualityProbability(
                0,
                failureChance),
            new MushroomLogQualityProbability(
                1,
                successChance * failureChance),
            new MushroomLogQualityProbability(
                2,
                successChance * successChance *
                    failureChance),
            new MushroomLogQualityProbability(
                4,
                successChance * successChance *
                    successChance)
        }.Where(row => row.Probability > 0)
            .ToArray();
    }

    private sealed record MushroomLogTreeRow(
        int TileX,
        int TileY,
        string TreeType,
        int GrowthStage,
        bool Mature,
        bool HasMoss)
    {
        public object ToSnapshot()
        {
            return new
            {
                tile_x = TileX,
                tile_y = TileY,
                tree_type = TreeType,
                growth_stage = GrowthStage,
                mature = Mature,
                has_moss = HasMoss,
                contributes_tree_specific_pool_entry =
                    Mature
            };
        }
    }

    private sealed record MushroomLogItemProbability(
        string QualifiedItemId,
        object? Item,
        int SalePrice,
        double Probability)
    {
        public object ToSnapshot()
        {
            return new
            {
                qualified_item_id = QualifiedItemId,
                item = Item,
                sale_price = SalePrice,
                probability = Probability
            };
        }
    }

    private sealed record MushroomLogAmountProbability(
        int Amount,
        double Probability)
    {
        public object ToSnapshot()
        {
            return new
            {
                amount = Amount,
                probability = Probability
            };
        }
    }

    private sealed record MushroomLogQualityProbability(
        int Quality,
        double Probability)
    {
        public object ToSnapshot()
        {
            return new
            {
                quality = Quality,
                probability = Probability
            };
        }
    }
}
