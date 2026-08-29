using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool FieldOfficeSurveyRequestIsTyped(TrainingExecutionRequest request, bool intentionallyWrong)
    {
        if (request.OptionId != (intentionallyWrong
                ? "debug.answer_field_office_survey_wrong"
                : "executor.answer_field_office_survey") ||
            !request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            request.FieldOfficeSurveyActionRaw != "FieldOfficeSurvey" ||
            request.FieldOfficeSurveyPromptQuestionKey != "Survey" ||
            request.FieldOfficeSurveyPromptResponseKey != "Yes" ||
            !request.FieldOfficeSurveyAnswer.HasValue || !request.FieldOfficeSurveyAnswerMinimum.HasValue ||
            !request.FieldOfficeSurveyAnswerMaximum.HasValue ||
            request.FieldOfficeSurveyAnswer < request.FieldOfficeSurveyAnswerMinimum ||
            request.FieldOfficeSurveyAnswer > request.FieldOfficeSurveyAnswerMaximum ||
            !request.FieldOfficeSurveyPlantRestoredBefore.HasValue ||
            !request.FieldOfficeSurveyPlantRestoredAfter.HasValue ||
            !request.FieldOfficeSurveyFailedTodayBefore.HasValue ||
            !request.FieldOfficeSurveyFailedTodayAfter.HasValue ||
            !request.FieldOfficeSurveyWalnutDebrisCountBefore.HasValue ||
            !request.FieldOfficeSurveyWalnutDebrisCountAfter.HasValue ||
            !request.FieldOfficeSurveyWalnutDebrisSpawnCount.HasValue ||
            !request.FieldOfficeSurveyGoldenWalnutsFoundAfter.HasValue ||
            !request.FieldOfficeSurveyGoldenWalnutsFoundDelta.HasValue ||
            request.FieldOfficeSurveyWalnutDebrisCountBefore != 0 ||
            request.FieldOfficeSurveyWalnutDebrisCountAfter != request.FieldOfficeSurveyWalnutDebrisCountBefore ||
            !request.FieldOfficeCollectedNutBefore.HasValue ||
            !request.FieldOfficeFinaleReadyAfter.HasValue ||
            !request.FieldOfficeSurveyExpectedFinaleTriggerAfter.HasValue ||
            !request.FieldOfficePlantsRestoredLeftBefore.HasValue ||
            !request.FieldOfficePlantsRestoredRightBefore.HasValue ||
            !request.FieldOfficeFinaleReceivedBefore.HasValue ||
            !request.FieldOfficeGoldenWalnutsFoundBefore.HasValue ||
            request.FieldOfficeSurveyGoldenWalnutsFoundAfter !=
                request.FieldOfficeGoldenWalnutsFoundBefore + request.FieldOfficeSurveyGoldenWalnutsFoundDelta ||
            request.FieldOfficeSurveyGoldenWalnutsFoundDelta != request.FieldOfficeSurveyWalnutDebrisSpawnCount ||
            !request.FieldOfficeSurveyDonatedPieceCountBefore.HasValue ||
            string.IsNullOrWhiteSpace(request.FieldOfficeCollectedNutKey) ||
            string.IsNullOrWhiteSpace(request.FieldOfficeSurveyOutputDelivery) ||
            request.FieldOfficeProjectionStatus != "exact_locked_base_1.6.15" ||
            request.NativeContract != FieldOfficeSurveyNativeContract)
            return false;

        var canonical = request.FieldOfficeSurveyKind switch
        {
            "purple_flower" => request.FieldOfficeSurveyAnswerQuestionKey == "PurpleFlowerSurvey" &&
                request.FieldOfficeSurveyAnswerMinimum == 18 && request.FieldOfficeSurveyAnswerMaximum == 24 &&
                request.FieldOfficeCollectedNutKey == "IslandLeftPlantRestored",
            "purple_starfish" => request.FieldOfficeSurveyAnswerQuestionKey == "PurpleStarfishSurvey" &&
                request.FieldOfficeSurveyAnswerMinimum == 11 && request.FieldOfficeSurveyAnswerMaximum == 18 &&
                request.FieldOfficeCollectedNutKey == "IslandRightPlantRestored",
            _ => false
        };
        if (!canonical || request.FieldOfficeSurveyPlantRestoredBefore != false || request.FieldOfficeSurveyFailedTodayBefore != false)
            return false;

        var correctAnswer = request.FieldOfficeSurveyKind == "purple_flower" ? 22 : 18;
        var expectedSpawn = intentionallyWrong ? 0 : request.FieldOfficeGoldenWalnutsFoundBefore < 130 ? 1 : 0;
        var expectedOutput = intentionallyWrong
            ? "none_wrong_answer"
            : expectedSpawn == 1
                ? "native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found"
                : "none_at_130_walnuts_found";
        var expectedFinaleReady = !intentionallyWrong && request.FieldOfficeSurveyDonatedPieceCountBefore == 11 &&
            (request.FieldOfficeSurveyKind == "purple_flower" || request.FieldOfficePlantsRestoredLeftBefore == true) &&
            (request.FieldOfficeSurveyKind == "purple_starfish" || request.FieldOfficePlantsRestoredRightBefore == true);
        if (request.FieldOfficeGoldenWalnutsFoundBefore is < 0 or > 130 ||
            request.FieldOfficeCollectedNutBefore != false ||
            request.FieldOfficeSurveyWalnutDebrisSpawnCount != expectedSpawn ||
            request.FieldOfficeSurveyGoldenWalnutsFoundDelta != expectedSpawn ||
            request.FieldOfficeSurveyOutputDelivery != expectedOutput ||
            request.FieldOfficeFinaleReadyAfter != expectedFinaleReady ||
            request.FieldOfficeSurveyExpectedFinaleTriggerAfter !=
                (expectedFinaleReady && request.FieldOfficeFinaleReceivedBefore == false))
            return false;

        return intentionallyWrong
            ? request.FieldOfficeSurveyAnswer != correctAnswer &&
              request.FieldOfficeSurveyAnswerResponseKey == "Wrong" &&
              request.FieldOfficeSurveyPlantRestoredAfter == false &&
              request.FieldOfficeSurveyFailedTodayAfter == true &&
              request.FieldOfficeFinaleReadyAfter == false &&
              request.FieldOfficeSurveyExpectedFinaleTriggerAfter == false
            : request.FieldOfficeSurveyAnswer == correctAnswer &&
              request.FieldOfficeSurveyAnswerResponseKey == "Correct" &&
              request.FieldOfficeSurveyPlantRestoredAfter == true &&
              request.FieldOfficeSurveyFailedTodayAfter == false;
    }

    private static bool FieldOfficeSurveyLiveProjectionMatches(
        TrainingExecutionRequest request,
        IslandFieldOffice office,
        bool intentionallyWrong)
    {
        var isLeft = request.FieldOfficeSurveyKind == "purple_flower";
        var targetRestored = isLeft ? office.plantsRestoredLeft.Value : office.plantsRestoredRight.Value;
        var correctAnswer = isLeft ? 22 : 18;
        return !targetRestored && !office.hasFailedSurveyToday.Value &&
            office.plantsRestoredLeft.Value == request.FieldOfficePlantsRestoredLeftBefore &&
            office.plantsRestoredRight.Value == request.FieldOfficePlantsRestoredRightBefore &&
            office.piecesDonated.Count(value => value) == request.FieldOfficeSurveyDonatedPieceCountBefore &&
            Game1.netWorldState.Value.GoldenWalnutsFound == request.FieldOfficeGoldenWalnutsFoundBefore &&
            Game1.player.hasOrWillReceiveMail("fieldOfficeFinale") == request.FieldOfficeFinaleReceivedBefore &&
            Game1.player.team.collectedNutTracker.Contains(request.FieldOfficeCollectedNutKey) == request.FieldOfficeCollectedNutBefore &&
            CountFieldOfficeSurveyWalnutDebris(office) == request.FieldOfficeSurveyWalnutDebrisCountBefore &&
            (intentionallyWrong
                ? request.FieldOfficeSurveyAnswer != correctAnswer
                : request.FieldOfficeSurveyAnswer == correctAnswer);
    }

    private static bool FieldOfficeSurveyPostconditionsMatch(ActiveFieldOfficeSurvey active)
    {
        var request = active.Pending.Request;
        var isLeft = request.FieldOfficeSurveyKind == "purple_flower";
        var targetRestored = isLeft ? active.Office.plantsRestoredLeft.Value : active.Office.plantsRestoredRight.Value;
        var otherRestored = isLeft ? active.Office.plantsRestoredRight.Value : active.Office.plantsRestoredLeft.Value;
        var otherBefore = isLeft ? request.FieldOfficePlantsRestoredRightBefore : request.FieldOfficePlantsRestoredLeftBefore;
        var collectedAfter = Game1.player.team.collectedNutTracker.Contains(request.FieldOfficeCollectedNutKey);
        var allPieces = active.Office.piecesDonated.All(value => value);
        var finaleReady = allPieces && active.Office.plantsRestoredLeft.Value && active.Office.plantsRestoredRight.Value;
        var finaleTriggered = Game1.eventUp || active.Office.currentEvent is not null ||
            Game1.player.hasOrWillReceiveMail("fieldOfficeFinale");
        return targetRestored == request.FieldOfficeSurveyPlantRestoredAfter &&
            otherRestored == otherBefore &&
            active.Office.hasFailedSurveyToday.Value == request.FieldOfficeSurveyFailedTodayAfter &&
            CountFieldOfficeSurveyWalnutDebris(active.Office) == request.FieldOfficeSurveyWalnutDebrisCountAfter &&
            active.WalnutDebrisSpawnObservedCount == request.FieldOfficeSurveyWalnutDebrisSpawnCount &&
            Game1.netWorldState.Value.GoldenWalnutsFound == request.FieldOfficeSurveyGoldenWalnutsFoundAfter &&
            finaleReady == request.FieldOfficeFinaleReadyAfter &&
            finaleTriggered == request.FieldOfficeSurveyExpectedFinaleTriggerAfter &&
            (active.IntentionallyWrong
                ? collectedAfter == request.FieldOfficeCollectedNutBefore
                : collectedAfter);
    }

    private static int CountFieldOfficeSurveyWalnutDebris(IslandFieldOffice office) =>
        office.debris.Count(debris => DebrisQualifiedItemId(debris) == "(O)73");

    private static string FieldOfficeSurveyRequestedEffect(TrainingExecutionRequest request, bool intentionallyWrong) =>
        "field_office_survey=" + request.FieldOfficeSurveyKind +
        ":answer=" + request.FieldOfficeSurveyAnswer +
        ";plant_restored=" + (!intentionallyWrong).ToString().ToLowerInvariant() +
        ";failed_today=" + intentionallyWrong.ToString().ToLowerInvariant() +
        ";walnut_debris_spawn_count=" + request.FieldOfficeSurveyWalnutDebrisSpawnCount +
        ";golden_walnuts_found_after=" + request.FieldOfficeSurveyGoldenWalnutsFoundAfter;

    private static string FieldOfficeSurveyObservedEffect(IslandFieldOffice? office) =>
        "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
        ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
        ";left=" + (office?.plantsRestoredLeft.Value.ToString() ?? "unavailable") +
        ";right=" + (office?.plantsRestoredRight.Value.ToString() ?? "unavailable") +
        ";failed_today=" + (office?.hasFailedSurveyToday.Value.ToString() ?? "unavailable") +
        ";walnut_debris=" + (office is null ? "unavailable" : CountFieldOfficeSurveyWalnutDebris(office).ToString()) +
        ";golden_walnuts_found=" + Game1.netWorldState.Value.GoldenWalnutsFound;

    private static SimulatedFactChange[] FieldOfficeSurveyChangedFacts(
        TrainingExecutionRequest request,
        IslandFieldOffice office,
        bool intentionallyWrong)
    {
        var plantPath = request.FieldOfficeSurveyKind == "purple_flower"
            ? "world_progress.island_field_office.plants_restored_left"
            : "world_progress.island_field_office.plants_restored_right";
        var result = new List<SimulatedFactChange>
        {
            new()
            {
                Path = plantPath,
                Before = request.FieldOfficeSurveyPlantRestoredBefore.ToString()!,
                After = request.FieldOfficeSurveyPlantRestoredAfter.ToString()!
            },
            new()
            {
                Path = "world_progress.island_field_office.has_failed_survey_today",
                Before = request.FieldOfficeSurveyFailedTodayBefore.ToString()!,
                After = office.hasFailedSurveyToday.Value.ToString()
            },
            new()
            {
                Path = "current_location.debris.count[(O)73]",
                Before = request.FieldOfficeSurveyWalnutDebrisCountBefore.ToString()!,
                After = CountFieldOfficeSurveyWalnutDebris(office).ToString()
            },
            new()
            {
                Path = "world_progress.golden_walnuts.found",
                Before = request.FieldOfficeGoldenWalnutsFoundBefore.ToString()!,
                After = Game1.netWorldState.Value.GoldenWalnutsFound.ToString()
            }
        };
        if (!intentionallyWrong)
        {
            result.Add(new SimulatedFactChange
            {
                Path = "player.team.collected_nut_tracker[" + request.FieldOfficeCollectedNutKey + "]",
                Before = request.FieldOfficeCollectedNutBefore.ToString()!,
                After = "True"
            });
        }
        return result.ToArray();
    }
}
