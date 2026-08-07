using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static CommunityCenterDonationProjection ProjectCommunityCenterDonation(
        NetWorldState world,
        CommunityCenter communityCenter,
        int areaId,
        int bundleId,
        int ingredientIndex,
        int ingredientCount,
        int requiredSlots,
        int completedCount)
    {
        var completesBundle = completedCount + 1 >= requiredSlots;
        var bitsAfter = world.Bundles.Pairs.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray());
        if (bitsAfter.TryGetValue(bundleId, out var targetBits) && ingredientIndex >= 0 && ingredientIndex < targetBits.Length)
        {
            targetBits[ingredientIndex] = true;
            if (completesBundle)
            {
                Array.Fill(targetBits, true);
            }
        }

        var completedAfter = bitsAfter.TryGetValue(bundleId, out targetBits)
            ? targetBits.Take(ingredientCount).Count(value => value)
            : completedCount + 1;
        var completeBundleCountAfter = bitsAfter.Count(pair => pair.Value.All(value => value));
        var areaBundleIds = CommunityCenterAreaBundleIds(world, areaId);
        var areaWouldBeComplete = areaBundleIds.Length > 0 &&
            areaBundleIds.All(id => bitsAfter.TryGetValue(id, out var bits) && bits.All(value => value));
        var areaCompleteBefore = areaId >= 0 && areaId < communityCenter.areasComplete.Count && communityCenter.areasComplete[areaId];
        var completesArea = !areaCompleteBefore && areaWouldBeComplete;
        var areaMailId = CommunityCenterAreaCompletionMailId(areaId);
        var newlyAppearingNotes = completesBundle
            ? Enumerable.Range(0, Math.Min(6, communityCenter.areasComplete.Count))
                .Where(candidateArea => !communityCenter.isJunimoNoteAtArea(candidateArea))
                .Where(candidateArea => ProjectedCommunityCenterNoteShouldAppear(world, bitsAfter, candidateArea, completeBundleCountAfter))
                .ToArray()
            : Array.Empty<int>();
        var allAreasCompleteAfter = Enumerable.Range(0, communityCenter.areasComplete.Count)
            .All(candidateArea => communityCenter.areasComplete[candidateArea] || candidateArea == areaId && areaWouldBeComplete);

        return new CommunityCenterDonationProjection
        {
            CompletedIngredientCountAfter = completedAfter,
            CompletesBundle = completesBundle,
            ExpectedBundleRewardAvailableAfter = completesBundle ||
                communityCenter.bundleRewards.TryGetValue(bundleId, out var rewardAvailable) && rewardAvailable,
            ExpectedCompleteBundleCountAfter = completeBundleCountAfter,
            CompletesArea = completesArea,
            ExpectedAreaCompleteAfter = areaCompleteBefore || areaWouldBeComplete,
            ExpectedAreaCompletionMailPendingAfter = !string.IsNullOrWhiteSpace(areaMailId) &&
                (HasPendingMail(Game1.player, areaMailId) || completesArea),
            ExpectedBulletinThankYouPendingAfter = HasPendingMail(Game1.player, "ccBulletinThankYou") || completesArea && areaId == 5,
            ExpectedAllAreasCompleteAfter = allAreasCompleteAfter,
            NewlyAppearingNoteAreaIds = newlyAppearingNotes
        };
    }

    private static int[] CommunityCenterAreaBundleIds(NetWorldState world, int areaId)
    {
        return world.BundleData.Keys
            .Select(key => key.Split('/'))
            .Where(parts => parts.Length >= 2 && CommunityCenter.getAreaNumberFromName(parts[0]) == areaId)
            .Select(parts => int.TryParse(parts[1], out var id) ? id : -1)
            .Where(id => id >= 0)
            .Distinct()
            .ToArray();
    }

    private static bool ProjectedCommunityCenterNoteShouldAppear(
        NetWorldState world,
        IReadOnlyDictionary<int, bool[]> bitsAfter,
        int areaId,
        int completeBundleCountAfter)
    {
        var areaBundles = CommunityCenterAreaBundleIds(world, areaId);
        var allAreaBundlesComplete = areaBundles.Length > 0 && areaBundles.All(id =>
            bitsAfter.TryGetValue(id, out var bits) && bits.All(value => value));
        if (areaId < 0 || allAreaBundlesComplete)
        {
            return false;
        }
        return areaId switch
        {
            1 => true,
            0 or 2 => completeBundleCountAfter > 0,
            3 => completeBundleCountAfter > 1,
            5 => completeBundleCountAfter > 2,
            4 => completeBundleCountAfter > 3,
            _ => false
        };
    }

    private static string CommunityCenterAreaCompletionMailId(int areaId)
    {
        return areaId switch
        {
            0 => "ccPantry",
            1 => "ccCraftsRoom",
            2 => "ccFishTank",
            3 => "ccBoilerRoom",
            4 => "ccVault",
            5 => "ccBulletin",
            _ => string.Empty
        };
    }

    private static Microsoft.Xna.Framework.Point? CommunityCenterInteractionTile(
        CommunityCenter communityCenter,
        int areaId,
        Microsoft.Xna.Framework.Point? noteTile)
    {
        if (areaId != 5)
        {
            return noteTile;
        }
        var buildings = communityCenter.Map?.GetLayer("Buildings");
        if (buildings is null)
        {
            return null;
        }
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                if (communityCenter.getTileIndexAt(x, y, "Buildings") == 1799)
                {
                    return new Microsoft.Xna.Framework.Point(x, y);
                }
            }
        }
        return null;
    }

    private sealed class CommunityCenterDonationProjection
    {
        public int CompletedIngredientCountAfter { get; init; }
        public bool CompletesBundle { get; init; }
        public bool ExpectedBundleRewardAvailableAfter { get; init; }
        public int ExpectedCompleteBundleCountAfter { get; init; }
        public bool CompletesArea { get; init; }
        public bool ExpectedAreaCompleteAfter { get; init; }
        public bool ExpectedAreaCompletionMailPendingAfter { get; init; }
        public bool ExpectedBulletinThankYouPendingAfter { get; init; }
        public bool ExpectedAllAreasCompleteAfter { get; init; }
        public int[] NewlyAppearingNoteAreaIds { get; init; } = Array.Empty<int>();
    }
}
