using StardewValley;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static void ConfigureMaterialTransferFixtureContents(Chest chest, string profile)
    {
        if (profile != "sparse_source") return;
        var wood = chest.Items[0];
        chest.Items.Clear();
        for (var index = 0; index < 10; index++)
        {
            chest.Items.Add(index switch
            {
                5 => wood,
                9 => ItemRegistry.Create<StardewValley.Object>("(O)390", 3, quality: 0),
                _ => null!
            });
        }
    }

    private static bool MaterialTransferFixtureContentsMatch(Chest chest, string profile)
    {
        var slot = profile == "sparse_source" ? 5 : 0;
        if (chest.Items.Count != (profile == "sparse_source" ? 10 : 1) ||
            chest.Items[slot] is not { QualifiedItemId: "(O)388", Quality: 4, Stack: 11 })
            return false;
        return profile != "sparse_source" ||
            chest.Items[9] is { QualifiedItemId: "(O)390", Quality: 0, Stack: 3 } &&
            chest.Items.Where((_, index) => index != 5 && index != 9).All(item => item is null);
    }
}
