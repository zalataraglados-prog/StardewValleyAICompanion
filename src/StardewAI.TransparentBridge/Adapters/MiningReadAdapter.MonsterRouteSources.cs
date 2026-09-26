using StardewValley.Monsters;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter
{
    private static object[] ReadMonsterAuthoritativeRouteSources(
        Monster monster,
        MiningMonsterDropProjection drops)
    {
        const string source = "GameLocation.monsterDrop/Data/Monsters";
        return drops.DropProbabilityRules
            .Where(rule => string.Equals(
                rule.Source,
                source,
                StringComparison.Ordinal))
            .SelectMany(rule => rule.QualifiedItemIds)
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(itemId => itemId, StringComparer.Ordinal)
            .Select(itemId => (object)new
            {
                route_kind = "native_monster_drop_table",
                source_id = "monster:" + monster.Name,
                qualified_item_id = itemId
            })
            .ToArray();
    }
}
