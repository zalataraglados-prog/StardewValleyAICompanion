using SObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class MiningReadAdapter
{
    private static object[] ReadMiningObjectAuthoritativeRouteSources(
        SObject obj,
        MiningStoneDropProjection? drops)
    {
        if (obj.ItemId != "95" ||
            drops is null ||
            !string.Equals(
                drops.RuleBranch,
                "game_location_break_stone_direct_node",
                StringComparison.Ordinal) ||
            !drops.GuaranteedDropQualifiedItemIds.Contains(
                "(O)909",
                StringComparer.Ordinal))
        {
            return Array.Empty<object>();
        }

        return new object[]
        {
            new
            {
                route_kind = "native_radioactive_ore_node",
                source_id = "GameLocation.breakStone",
                qualified_item_id = "(O)909"
            }
        };
    }
}
