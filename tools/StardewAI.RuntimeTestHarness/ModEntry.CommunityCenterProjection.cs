using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool CommunityCenterOutcomeProjectionMatches(
        CommunityCenter communityCenter,
        TrainingExecutionRequest request,
        out string failure)
    {
        failure = string.Empty;
        if (!request.BundleId.HasValue || !request.BundleAreaId.HasValue || !request.ExpectedBundleCompleteAfter.HasValue ||
            !request.ExpectedBundleCompletedCountAfter.HasValue)
        {
            failure = "required_request_fields_missing";
            return false;
        }
        int[] requestedNewNoteAreas;
        try
        {
            requestedNewNoteAreas = JsonSerializer.Deserialize<int[]>(request.NewlyAppearingNoteAreaIdsJson) ?? Array.Empty<int>();
        }
        catch (JsonException)
        {
            failure = "new_note_area_json_invalid";
            return false;
        }

        var bitsAfter = communityCenter.bundles.Pairs.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        if (!bitsAfter.TryGetValue(request.BundleId.Value, out var targetBits) ||
            !request.BundleIngredientIndex.HasValue || request.BundleIngredientIndex.Value < 0 || request.BundleIngredientIndex.Value >= targetBits.Length)
        {
            failure = "target_bundle_or_ingredient_missing";
            return false;
        }
        if (!Game1.netWorldState.Value.BundleData.TryGetValue(request.BundleDataKey, out var rawBundleData))
        {
            failure = "target_bundle_data_missing";
            return false;
        }
        var rawFields = rawBundleData.Split('/');
        if (rawFields.Length < Bundle.FieldCount)
        {
            failure = "target_bundle_data_invalid";
            return false;
        }
        var ingredientCount = ArgUtility.SplitBySpace(rawFields[Bundle.IngredientsIndex]).Length / 3;
        targetBits[request.BundleIngredientIndex.Value] = true;
        if (request.ExpectedBundleCompleteAfter.Value)
        {
            Array.Fill(targetBits, true);
        }
        var completedAfter = targetBits.Take(ingredientCount).Count(value => value);
        var completeBundleCountAfter = bitsAfter.Count(pair => pair.Value.All(value => value));
        var areaBundleIds = RuntimeCommunityCenterAreaBundleIds(request.BundleAreaId.Value);
        var areaWouldBeComplete = areaBundleIds.Length > 0 && areaBundleIds.All(id =>
            bitsAfter.TryGetValue(id, out var bits) && bits.All(value => value));
        var areaCompleteBefore = communityCenter.areasComplete[request.BundleAreaId.Value];
        var completesArea = !areaCompleteBefore && areaWouldBeComplete;
        var allAreasCompleteAfter = Enumerable.Range(0, communityCenter.areasComplete.Count)
            .All(area => communityCenter.areasComplete[area] || area == request.BundleAreaId.Value && areaWouldBeComplete);
        var projectedNewNoteAreas = request.ExpectedBundleCompleteAfter.Value
            ? Enumerable.Range(0, Math.Min(6, communityCenter.areasComplete.Count))
                .Where(area => !communityCenter.isJunimoNoteAtArea(area))
                .Where(area => RuntimeCommunityCenterNoteShouldAppear(bitsAfter, area, completeBundleCountAfter))
                .ToArray()
            : Array.Empty<int>();
        var rewardBefore = communityCenter.bundleRewards.TryGetValue(request.BundleId.Value, out var rewardAvailable) && rewardAvailable;
        var mailBefore = HasPendingCommunityCenterMail(Game1.player, request.AreaCompletionMailId);
        var bulletinBefore = HasPendingCommunityCenterMail(Game1.player, "ccBulletinThankYou");
        var checks = new[]
        {
            (Name: "completed_count", Match: completedAfter == request.ExpectedBundleCompletedCountAfter),
            (Name: "reward", Match: (rewardBefore || request.ExpectedBundleCompleteAfter.Value) == request.ExpectedBundleRewardAvailableAfter),
            (Name: "complete_bundle_count", Match: completeBundleCountAfter == request.ExpectedCompleteBundleCountAfter),
            (Name: "completes_area", Match: completesArea == request.CompletesArea),
            (Name: "area_complete", Match: (areaCompleteBefore || areaWouldBeComplete) == request.ExpectedAreaCompleteAfter),
            (Name: "area_mail", Match: (mailBefore || completesArea) == request.ExpectedAreaCompletionMailPendingAfter),
            (Name: "bulletin_mail", Match: (bulletinBefore || completesArea && request.BundleAreaId.Value == 5) == request.ExpectedBulletinThankYouPendingAfter),
            (Name: "all_areas", Match: allAreasCompleteAfter == request.ExpectedAllAreasCompleteAfter),
            (Name: "new_notes", Match: projectedNewNoteAreas.SequenceEqual(requestedNewNoteAreas))
        };
        failure = string.Join(",", checks.Where(check => !check.Match).Select(check => check.Name)) +
            ":projected_notes=" + JsonSerializer.Serialize(projectedNewNoteAreas) +
            ":requested_notes=" + JsonSerializer.Serialize(requestedNewNoteAreas);
        return checks.All(check => check.Match);
    }

    private static int[] RuntimeCommunityCenterAreaBundleIds(int areaId)
    {
        return Game1.netWorldState.Value.BundleData.Keys
            .Select(key => key.Split('/'))
            .Where(parts => parts.Length >= 2 && CommunityCenter.getAreaNumberFromName(parts[0]) == areaId)
            .Select(parts => int.TryParse(parts[1], out var id) ? id : -1)
            .Where(id => id >= 0)
            .Distinct()
            .ToArray();
    }

    private static bool RuntimeCommunityCenterNoteShouldAppear(
        IReadOnlyDictionary<int, bool[]> bitsAfter,
        int areaId,
        int completeBundleCountAfter)
    {
        var areaBundles = RuntimeCommunityCenterAreaBundleIds(areaId);
        if (areaBundles.Length == 0 || areaBundles.All(id => bitsAfter.TryGetValue(id, out var bits) && bits.All(value => value)))
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

    private static int CommunityCenterCompleteBundleCount(CommunityCenter communityCenter) =>
        communityCenter.bundles.Pairs.Count(pair => pair.Value.All(value => value));

    private static bool HasPendingCommunityCenterMail(Farmer farmer, string mailId)
    {
        return farmer.mailForTomorrow.Any(value =>
            string.Equals(value, mailId, StringComparison.Ordinal) ||
            value.StartsWith(mailId + "%&NL&%", StringComparison.Ordinal));
    }
}
