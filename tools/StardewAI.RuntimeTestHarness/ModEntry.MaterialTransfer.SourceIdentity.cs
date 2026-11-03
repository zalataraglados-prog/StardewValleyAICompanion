using StardewAI.Contracts.Execution;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static int MaterialSourceReferenceIndex(IList<Item?> items, Item original)
    {
        var found = -1;
        for (var index = 0; index < items.Count; index++)
        {
            if (!ReferenceEquals(items[index], original)) continue;
            if (found >= 0) return -2;
            found = index;
        }
        return found;
    }

    private static int MaterialSourceReferenceStack(IList<Item?> items, Item original, MaterialTransferIntent intent)
    {
        var index = MaterialSourceReferenceIndex(items, original);
        if (index == -1) return 0;
        return index >= 0 && MatchesMaterialTransferItem(items[index], intent) ? items[index]!.Stack : -1;
    }
}
