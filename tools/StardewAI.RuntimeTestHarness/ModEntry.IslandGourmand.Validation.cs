using System.Reflection;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static readonly FieldInfo? GourmandMutexField =
        typeof(IslandFarmCave).GetField(
            "gourmandMutex",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static bool GourmandRequestIsTyped(TrainingExecutionRequest request)
    {
        if (request.OptionId != "executor.advance_gourmand_request" ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            request.GourmandAction != "Gourmand" ||
            request.GourmandSchemaVersion != "island_gourmand_progress.v1" ||
            request.GourmandProjectionStatus != "exact_native_active_request_projected" ||
            request.NativeContract != GourmandRuntimeNativeContract ||
            !request.GourmandRequestIndexBefore.HasValue ||
            request.GourmandRequestIndexBefore is < 0 or > 2 ||
            !request.GourmandRequestIndexAfter.HasValue ||
            !request.GourmandReadyMatchingCropCount.HasValue ||
            request.GourmandReadyMatchingCropCount < 1 ||
            !request.GourmandTalkedBefore.HasValue ||
            request.GourmandTalkedAfter != true ||
            !request.GourmandWalnutDebrisCountBefore.HasValue ||
            request.GourmandWalnutDebrisCountBefore != 0 ||
            !request.GourmandWalnutDebrisCountAfter.HasValue ||
            request.GourmandWalnutDebrisCountAfter != 0 ||
            !request.GourmandExpectedWalnutDebrisSpawnCount.HasValue ||
            !request.GourmandGoldenWalnutsFoundBefore.HasValue ||
            request.GourmandGoldenWalnutsFoundBefore is < 0 or > 130 ||
            !request.GourmandGoldenWalnutsFoundDelta.HasValue ||
            !request.GourmandGoldenWalnutsFoundAfter.HasValue ||
            request.GourmandGoldenWalnutsFoundAfter !=
                request.GourmandGoldenWalnutsFoundBefore + request.GourmandGoldenWalnutsFoundDelta ||
            !GourmandCropMatchesRequest(
                request.GourmandRequestIndexBefore.Value,
                request.GourmandRequiredCropItemId) ||
            request.GourmandRequiredCropQualifiedItemId !=
                "(O)" + request.GourmandRequiredCropItemId)
            return false;

        var completing = request.GourmandInteractionStage == "complete_request";
        if (!completing && request.GourmandInteractionStage != "introduce_request")
            return false;
        var expectedDelta = completing
            ? Math.Min(5, 130 - request.GourmandGoldenWalnutsFoundBefore.Value)
            : 0;
        return request.GourmandRequestIndexAfter ==
                request.GourmandRequestIndexBefore + (completing ? 1 : 0) &&
            request.GourmandTalkedBefore == completing &&
            request.GourmandExpectedWalnutDebrisSpawnCount == (completing ? 5 : 0) &&
            request.GourmandGoldenWalnutsFoundDelta == expectedDelta &&
            request.GourmandQuestionKey == (completing ? "Gourmand" : string.Empty) &&
            request.GourmandResponseKey == (completing ? "Yes" : string.Empty) &&
            request.GourmandExpectedCollectedNutKey ==
                (completing ? "IslandGourmand" + request.GourmandRequestIndexAfter : string.Empty) &&
            request.GourmandOutputDelivery ==
                (completing ? "native_five_debris_then_magnet_pickup" : "none_introduction_only");
    }

    private static bool GourmandLiveProjectionMatches(
        TrainingExecutionRequest request,
        IslandFarmCave cave)
    {
        var mutex = GourmandMutex(cave);
        return mutex is not null && !mutex.IsLocked() && !cave.triggeredGourmand &&
            cave.gourmandRequestsFulfilled.Value == request.GourmandRequestIndexBefore &&
            Game1.MasterPlayer.hasOrWillReceiveMail("Island_UpgradeHouse") &&
            Game1.MasterPlayer.hasOrWillReceiveMail("talkedToGourmand") == request.GourmandTalkedBefore &&
            Game1.netWorldState.Value.GoldenWalnutsFound == request.GourmandGoldenWalnutsFoundBefore &&
            CountGourmandWalnutDebris(cave) == request.GourmandWalnutDebrisCountBefore &&
            CountReadyGourmandCrops(request.GourmandRequiredCropItemId) ==
                request.GourmandReadyMatchingCropCount &&
            (string.IsNullOrWhiteSpace(request.GourmandExpectedCollectedNutKey) ||
             !Game1.player.team.collectedNutTracker.Contains(request.GourmandExpectedCollectedNutKey));
    }

    private static bool GourmandPostconditionsMatch(ActiveGourmandRequest active)
    {
        var request = active.Pending.Request;
        var stable = Game1.activeClickableMenu is null && !Game1.dialogueUp && !Game1.eventUp &&
            Game1.currentLocation?.currentEvent is null && Game1.locationRequest is null;
        if (!stable || !Game1.MasterPlayer.hasOrWillReceiveMail("talkedToGourmand") ||
            active.Cave.triggeredGourmand ||
            active.Cave.gourmandRequestsFulfilled.Value != request.GourmandRequestIndexAfter ||
            CountGourmandWalnutDebris(active.Cave) != request.GourmandWalnutDebrisCountAfter ||
            active.ObservedWalnutDebris.Count != request.GourmandExpectedWalnutDebrisSpawnCount ||
            Game1.netWorldState.Value.GoldenWalnutsFound != request.GourmandGoldenWalnutsFoundAfter)
            return false;

        if (!active.Completing)
            return !active.YesAnswered &&
                string.IsNullOrWhiteSpace(request.GourmandExpectedCollectedNutKey);

        var mutex = GourmandMutex(active.Cave);
        return active.YesAnswered && active.SawNativeEvent &&
            ReferenceEquals(Game1.currentLocation, active.Cave) &&
            mutex is not null && !mutex.IsLocked() &&
            Game1.player.team.collectedNutTracker.Contains(request.GourmandExpectedCollectedNutKey);
    }

    private static NetMutex? GourmandMutex(IslandFarmCave cave) =>
        GourmandMutexField?.GetValue(cave) as NetMutex;

    private static int CountReadyGourmandCrops(string itemId)
    {
        if (Game1.getLocationFromName("IslandWest") is not IslandWest west)
            return -1;
        return west.terrainFeatures.Pairs.Count(pair =>
            pair.Value is HoeDirt { crop: not null } dirt &&
            dirt.crop.indexOfHarvest.Value == itemId &&
            dirt.crop.currentPhase.Value >= dirt.crop.phaseDays.Count - 1 &&
            (!dirt.crop.fullyGrown.Value || dirt.crop.dayOfCurrentPhase.Value <= 0));
    }

    private static int CountGourmandWalnutDebris(IslandFarmCave cave) =>
        cave.debris.Count(debris => DebrisQualifiedItemId(debris) == "(O)73");

    private static void CaptureGourmandWalnutDebris(ActiveGourmandRequest active)
    {
        foreach (var debris in active.Cave.debris)
        {
            if (!active.DebrisBefore.Contains(debris) && DebrisQualifiedItemId(debris) == "(O)73")
                active.ObservedWalnutDebris.Add(debris);
        }
    }

    private static bool GourmandCropMatchesRequest(int requestIndex, string itemId) =>
        (requestIndex == 0 && itemId == "254") ||
        (requestIndex == 1 && itemId == "262") ||
        (requestIndex == 2 && itemId == "248");

    private static string GourmandRequestedEffect(TrainingExecutionRequest request) =>
        "gourmand_stage=" + request.GourmandInteractionStage +
        ";request_index_after=" + request.GourmandRequestIndexAfter +
        ";collected_nut_key=" + request.GourmandExpectedCollectedNutKey +
        ";walnut_debris_spawn_count=" + request.GourmandExpectedWalnutDebrisSpawnCount +
        ";golden_walnuts_found_after=" + request.GourmandGoldenWalnutsFoundAfter;

    private static string GourmandObservedEffect(IslandFarmCave? cave) =>
        "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";request_index=" + (cave?.gourmandRequestsFulfilled.Value.ToString() ?? "unavailable") +
        ";talked=" + Game1.MasterPlayer.hasOrWillReceiveMail("talkedToGourmand").ToString().ToLowerInvariant() +
        ";walnut_debris=" + (cave is null ? "unavailable" : CountGourmandWalnutDebris(cave).ToString()) +
        ";golden_walnuts_found=" + Game1.netWorldState.Value.GoldenWalnutsFound;

    private static SimulatedFactChange[] GourmandChangedFacts(
        TrainingExecutionRequest request,
        IslandFarmCave cave)
    {
        var result = new List<SimulatedFactChange>
        {
            new()
            {
                Path = "world_progress.island_gourmand.talked_to_gourmand_received_or_pending",
                Before = request.GourmandTalkedBefore.ToString()!,
                After = Game1.MasterPlayer.hasOrWillReceiveMail("talkedToGourmand").ToString()
            },
            new()
            {
                Path = "world_progress.island_gourmand.requests_fulfilled",
                Before = request.GourmandRequestIndexBefore.ToString()!,
                After = cave.gourmandRequestsFulfilled.Value.ToString()
            },
            new()
            {
                Path = "world_progress.island_gourmand.walnut_debris_count",
                Before = request.GourmandWalnutDebrisCountBefore.ToString()!,
                After = CountGourmandWalnutDebris(cave).ToString()
            },
            new()
            {
                Path = "world_progress.golden_walnuts.found",
                Before = request.GourmandGoldenWalnutsFoundBefore.ToString()!,
                After = Game1.netWorldState.Value.GoldenWalnutsFound.ToString()
            }
        };
        if (!string.IsNullOrWhiteSpace(request.GourmandExpectedCollectedNutKey))
        {
            result.Add(new SimulatedFactChange
            {
                Path = "player.team.collected_nut_tracker[" + request.GourmandExpectedCollectedNutKey + "]",
                Before = "False",
                After = "True"
            });
        }
        return result.ToArray();
    }
}
