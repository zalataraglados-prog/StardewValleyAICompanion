using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool TryClickCraftingRecipe(CraftingPage page, string recipeName)
    {
        for (var pageIndex = 0; pageIndex < page.pagesOfCraftingRecipes.Count; pageIndex++)
        {
            foreach (var pair in page.pagesOfCraftingRecipes[pageIndex])
            {
                if (!string.Equals(pair.Value.name, recipeName, StringComparison.Ordinal))
                {
                    continue;
                }

                page.currentCraftingPage = pageIndex;
                page.receiveLeftClick(pair.Key.bounds.Center.X, pair.Key.bounds.Center.Y, playSound: false);
                return true;
            }
        }

        return false;
    }
}
