using StardewValley;
using System.Globalization;
using StardewValley.Constants;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

internal static partial class MiningMonsterDropResolver
{
    public static MiningDropCatalogProjection[] ReadSharedCatalogs(Farmer player)
    {
        var cosmeticEntries = BuildRandomCosmeticSelectionEntries();
        var naturalTrinkets = NaturalTrinketQualifiedItemIds();
        var hardMineTreasureEntries = BuildHardMineTreasureSelectionEntries(player, naturalTrinkets, out var hardMineTreasureCompleteness);
        return new[]
        {
            new MiningDropCatalogProjection
            {
                Key = RandomCosmeticCatalogKey,
                PossibleQualifiedItemIds = cosmeticEntries.Select(entry => entry.QualifiedItemId).ToArray(),
                SelectionProbabilityEntries = cosmeticEntries,
                Active = Game1.stats.DaysPlayed > 2,
                ItemIdentityCompleteness = "complete",
                SelectionProbabilityCompleteness = "complete_conditional_on_cosmetic_event_for_loaded_furniture_data",
                Source = "Utility.getRandomCosmeticItem/getRandomSingleTileFurniture"
            },
            new MiningDropCatalogProjection
            {
                Key = HardMineTreasureCatalogKey,
                PossibleQualifiedItemIds = hardMineTreasureEntries.Select(entry => entry.QualifiedItemId).ToArray(),
                SelectionProbabilityEntries = hardMineTreasureEntries,
                Active = true,
                ItemIdentityCompleteness = hardMineTreasureCompleteness,
                SelectionProbabilityCompleteness = hardMineTreasureCompleteness,
                Source = "MineShaft.getTreasureRoomItem; DataLoader.Trinkets"
            },
            new MiningDropCatalogProjection
            {
                Key = NaturalTrinketCatalogKey,
                PossibleQualifiedItemIds = naturalTrinkets,
                SelectionProbabilityEntries = UniformSelectionEntries(naturalTrinkets),
                Active = player.stats.Get("trinketSlots") != 0,
                ItemIdentityCompleteness = "complete_for_loaded_trinket_data",
                SelectionProbabilityCompleteness = naturalTrinkets.Length > 0 ? "complete_uniform_loaded_natural_trinkets" : "complete_empty_loaded_natural_trinkets",
                Source = "Trinket.TrySpawnTrinket/GetRandomTrinket; DataLoader.Trinkets"
            }
        };
    }

    private static MiningDropCatalogEntryProjection[] BuildRandomCosmeticSelectionEntries()
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        AddSelectionWeight(weights, "(F)1369", 0.2d * 0.05d, validateFurnitureFallback: true);
        var furnitureRemainderBranch = 0.2d * 0.95d / 3d;
        var singleTileBranch = furnitureRemainderBranch / 3d;
        for (var id = 0; id < 30; id += 3)
        {
            AddSelectionWeight(weights, "(F)" + id, singleTileBranch / 10d, validateFurnitureFallback: true);
        }
        for (var id = 1362; id < 1370; id++)
        {
            AddSelectionWeight(weights, "(F)" + id, furnitureRemainderBranch / 8d, validateFurnitureFallback: true);
        }
        for (var id = 1376; id < 1391; id++)
        {
            AddSelectionWeight(weights, "(F)" + id, singleTileBranch / 15d, validateFurnitureFallback: true);
            AddSelectionWeight(weights, "(F)" + id, furnitureRemainderBranch / 15d, validateFurnitureFallback: true);
        }
        for (var id = 1391; id <= 1401; id += 2)
        {
            AddSelectionWeight(weights, "(F)" + id, singleTileBranch / 6d, validateFurnitureFallback: true);
        }
        var hats = new[] { 45, 46, 47, 49, 52, 53, 54, 55, 57, 58, 59, 62, 63, 68, 69, 70, 84, 85, 87, 88, 89, 90 };
        foreach (var id in hats)
        {
            AddSelectionWeight(weights, "(H)" + id, 0.2d / hats.Length, validateFurnitureFallback: false);
        }
        var excludedShirts = new HashSet<int> { 1127, 1129, 1130, 1132, 1133, 1136, 1152, 1176, 1177, 1201, 1202 };
        var shirts = Enumerable.Range(1112, 1291 - 1112).Where(id => !excludedShirts.Contains(id)).ToArray();
        foreach (var id in shirts)
        {
            AddSelectionWeight(weights, "(S)" + id, 0.6d / shirts.Length, validateFurnitureFallback: false);
        }
        return weights
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MiningDropCatalogEntryProjection
            {
                QualifiedItemId = pair.Key,
                ConditionalSelectionChance = pair.Value,
                ConditionalExpectedQuantity = 1d,
                ProbabilityStatus = "exact_decompiled_weight_with_loaded_furniture_fallback"
            })
            .ToArray();
    }

    private static void AddSelectionWeight(
        Dictionary<string, double> weights,
        string qualifiedItemId,
        double chance,
        bool validateFurnitureFallback)
    {
        var resolvedId = qualifiedItemId;
        if (validateFurnitureFallback)
        {
            var data = ItemRegistry.GetDataOrErrorItem(qualifiedItemId);
            if (data.IsErrorItem || data.InternalName.Contains("Error", StringComparison.Ordinal))
            {
                resolvedId = "(F)1369";
            }
        }
        weights[resolvedId] = weights.GetValueOrDefault(resolvedId) + chance;
    }

    private static MiningDropCatalogEntryProjection[] UniformSelectionEntries(string[] qualifiedItemIds)
    {
        if (qualifiedItemIds.Length == 0)
        {
            return Array.Empty<MiningDropCatalogEntryProjection>();
        }
        var chance = 1d / qualifiedItemIds.Length;
        return qualifiedItemIds.Select(id => new MiningDropCatalogEntryProjection
        {
            QualifiedItemId = id,
            ConditionalSelectionChance = chance,
            ConditionalExpectedQuantity = 1d,
            ProbabilityStatus = "exact_uniform_loaded_catalog"
        }).ToArray();
    }

    private static string[] NaturalTrinketQualifiedItemIds()
    {
        return DataLoader.Trinkets(Game1.content)
            .Where(pair => pair.Value.DropsNaturally)
            .Select(pair => "(TR)" + pair.Key)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static MiningDropCatalogEntryProjection[] BuildHardMineTreasureSelectionEntries(
        Farmer player,
        string[] naturalTrinkets,
        out string completeness)
    {
        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        var crackerRollChance = player.stats.Get(StatKeys.Mastery(0)) != 0 ? 0.02d : 0d;
        var trinketRollChance = player.stats.Get("trinketSlots") != 0 ? 0.045d : 0d;
        AddSelectionWeight(weights, "(O)GoldenAnimalCracker", crackerRollChance, validateFurnitureFallback: false);

        var afterCrackerMass = 1d - crackerRollChance;
        var trinketMass = afterCrackerMass * trinketRollChance;
        if (trinketRollChance > 0d && naturalTrinkets.Length == 0)
        {
            completeness = "partial_active_trinket_gate_without_loaded_natural_entries";
        }
        else
        {
            foreach (var trinket in naturalTrinkets)
            {
                AddSelectionWeight(weights, trinket, trinketMass / naturalTrinkets.Length, validateFurnitureFallback: false);
            }
            completeness = "complete_decompiled_current_player_gate_tree";
        }

        var caseMass = afterCrackerMass * (1d - trinketRollChance) / 26d;
        AddSelectionWeight(weights, "(O)288", caseMass, false);
        AddSelectionWeight(weights, "(O)287", caseMass, false);
        if (Game1.MasterPlayer.hasOrWillReceiveMail("volcanoShortcutUnlocked"))
        {
            AddSelectionWeight(weights, "(O)848", caseMass * 0.66d, false);
            AddSelectionWeight(weights, "(O)275", caseMass * 0.34d, false);
        }
        else
        {
            AddSelectionWeight(weights, "(O)275", caseMass, false);
        }
        AddSelectionWeight(weights, "(O)773", caseMass, false);
        AddSelectionWeight(weights, "(O)749", caseMass, false);
        AddSelectionWeight(weights, "(O)688", caseMass, false);
        AddSelectionWeight(weights, "(O)681", caseMass, false);
        for (var id = 628; id < 634; id++)
        {
            AddSelectionWeight(weights, "(O)" + id, caseMass / 6d, false);
        }
        AddSelectionWeight(weights, "(O)645", caseMass, false);
        AddSelectionWeight(weights, "(O)621", caseMass, false);
        AddSelectionWeight(weights, "(O)802", caseMass * 0.33d, false);
        for (var id = 472; id < 499; id++)
        {
            AddSelectionWeight(weights, "(O)" + id, caseMass * 0.67d / 27d, false);
        }
        AddSelectionWeight(weights, "(O)286", caseMass, false);
        AddSelectionWeight(weights, "(O)265", caseMass * 0.5d, false);
        AddSelectionWeight(weights, "(O)437", caseMass * 0.5d, false);
        AddSelectionWeight(weights, "(O)439", caseMass, false);
        AddSelectionWeight(weights, "(O)349", caseMass * 0.67d, false);
        AddSelectionWeight(weights, "(O)226", caseMass * 0.33d * 0.5d, false);
        AddSelectionWeight(weights, "(O)732", caseMass * 0.33d * 0.5d, false);
        AddSelectionWeight(weights, "(O)337", caseMass, false);
        for (var id = 235; id < 245; id++)
        {
            AddSelectionWeight(weights, "(O)" + id, caseMass * 0.67d / 10d, false);
        }
        AddSelectionWeight(weights, "(O)226", caseMass * 0.33d * 0.5d, false);
        AddSelectionWeight(weights, "(O)732", caseMass * 0.33d * 0.5d, false);
        AddSelectionWeight(weights, "(O)74", caseMass, false);
        AddSelectionWeight(weights, "(BC)21", caseMass, false);
        AddSelectionWeight(weights, "(BC)25", caseMass, false);
        AddSelectionWeight(weights, "(BC)165", caseMass, false);
        AddSelectionWeight(weights, "(H)38", caseMass * 0.5d, false);
        AddSelectionWeight(weights, "(H)37", caseMass * 0.5d, false);
        if (player.mailReceived.Contains("sawQiPlane"))
        {
            AddSelectionWeight(
                weights,
                player.stats.Get(StatKeys.Mastery(2)) != 0 ? "(O)GoldenMysteryBox" : "(O)MysteryBox",
                caseMass,
                false);
        }
        else
        {
            AddSelectionWeight(weights, "(O)749", caseMass, false);
        }
        AddSelectionWeight(weights, "(H)65", caseMass, false);
        AddSelectionWeight(weights, "(BC)272", caseMass, false);
        AddSelectionWeight(weights, "(H)83", caseMass, false);

        return weights
            .Where(pair => pair.Value > 0d)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new MiningDropCatalogEntryProjection
            {
                QualifiedItemId = pair.Key,
                ConditionalSelectionChance = pair.Value,
                ConditionalExpectedQuantity = HardMineTreasureConditionalExpectedQuantity(pair.Key),
                ProbabilityStatus = "exact_decompiled_hard_mine_treasure_tree"
            })
            .ToArray();
    }

    private static double HardMineTreasureConditionalExpectedQuantity(string qualifiedItemId)
    {
        if (qualifiedItemId.StartsWith("(TR)", StringComparison.Ordinal))
        {
            return 1d;
        }
        if (qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) &&
            int.TryParse(qualifiedItemId.AsSpan(3), NumberStyles.Integer, CultureInfo.InvariantCulture, out var objectId))
        {
            if (objectId is >= 628 and <= 633 || objectId is 74 or 265 or 437 or 439)
            {
                return 1d;
            }
            if (objectId is >= 472 and <= 498)
            {
                return 12.5d;
            }
            if (objectId is >= 235 and <= 244 || objectId is 226 or 275 or 688 or 732)
            {
                return 5d;
            }
            return objectId switch
            {
                288 => 5d,
                287 => 10d,
                848 => 15d,
                773 => 3d,
                749 => 6.25d,
                681 => 2d,
                645 => 1.5d,
                621 => 4d,
                802 => 15d,
                286 => 15d,
                349 => 3d,
                337 => 2.5d,
                _ => 1d
            };
        }
        return qualifiedItemId is "(O)MysteryBox" or "(O)GoldenMysteryBox" ? 5d : 1d;
    }

}
