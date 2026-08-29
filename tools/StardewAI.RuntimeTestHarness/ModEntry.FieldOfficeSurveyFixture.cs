using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFieldOfficeSurveyFixture(TrainingExecutionRequest request)
    {
        if (request.FieldOfficeSurveyKind is not ("purple_flower" or "purple_starfish") ||
            !request.FieldOfficeGoldenWalnutsFoundBefore.HasValue ||
            request.FieldOfficeGoldenWalnutsFoundBefore is < 0 or > 130)
            return FieldOfficeSurveyFixtureBlocked(request, "field_office_survey_fixture_parameters_invalid");
        if (Game1.getLocationFromName("IslandFieldOffice") is not IslandFieldOffice office)
            return FieldOfficeSurveyFixtureBlocked(request, "field_office_survey_fixture_location_missing");
        if (Game1.eventUp || office.currentEvent is not null)
            return FieldOfficeSurveyFixtureBlocked(request, "field_office_survey_fixture_event_active");

        if (Game1.activeClickableMenu is not null)
            Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        if (office.safariGuyMutex.IsLockHeld())
            office.safariGuyMutex.ReleaseLock();
        while (office.piecesDonated.Count < IslandFieldOffice.totalPieces)
            office.piecesDonated.Add(false);
        for (var index = 0; index < IslandFieldOffice.totalPieces; index++)
            office.piecesDonated[index] = false;
        office.centerSkeletonRestored.Value = false;
        office.snakeRestored.Value = false;
        office.batRestored.Value = false;
        office.frogRestored.Value = false;
        office.plantsRestoredLeft.Value = false;
        office.plantsRestoredRight.Value = false;
        office.hasFailedSurveyToday.Value = false;
        office.uncollectedRewards.Clear();

        var finaleCase = request.FieldOfficeSurveyFixtureCase == "finale";
        if (finaleCase)
        {
            for (var index = 0; index < IslandFieldOffice.totalPieces; index++)
                office.piecesDonated[index] = true;
            office.centerSkeletonRestored.Value = true;
            office.snakeRestored.Value = true;
            office.batRestored.Value = true;
            office.frogRestored.Value = true;
        }
        if (request.FieldOfficeSurveyKind == "purple_starfish")
            office.plantsRestoredLeft.Value = true;
        if (request.FieldOfficeSurveyFixtureCase is "failed_today" or "day_reset")
            office.hasFailedSurveyToday.Value = true;

        foreach (var key in new[] { "IslandLeftPlantRestored", "IslandRightPlantRestored" })
        {
            if ((key == "IslandLeftPlantRestored" && office.plantsRestoredLeft.Value) ||
                (key == "IslandRightPlantRestored" && office.plantsRestoredRight.Value))
                continue;
            Game1.player.team.collectedNutTracker.Remove(key);
        }
        for (var index = office.debris.Count - 1; index >= 0; index--)
        {
            if (DebrisQualifiedItemId(office.debris[index]) == "(O)73")
                office.debris.RemoveAt(index);
        }
        Game1.netWorldState.Value.GoldenWalnutsFound = request.FieldOfficeGoldenWalnutsFoundBefore.Value;
        Game1.player.mailReceived.Add("islandNorthCaveOpened");
        Game1.player.mailReceived.Add("safariGuyIntro");
        Game1.player.mailReceived.Remove("fieldOfficeFinale");
        Game1.player.mailForTomorrow.Remove("fieldOfficeFinale");

        Game1.currentLocation = office;
        Game1.player.currentLocation = office;
        office.resetForPlayerEntry();
        var action = FieldOfficeFixtureSurveyTile(office);
        var stand = action.HasValue ? FieldOfficeFixtureStandTile(office, action.Value) : null;
        if (!action.HasValue || !stand.HasValue || office.getSafariGuy() is null)
            return FieldOfficeSurveyFixtureBlocked(request, "field_office_survey_fixture_native_endpoint_or_professor_missing");
        Game1.player.Position = stand.Value.ToVector2() * Game1.tileSize;
        Game1.player.forceCanMove();

        var targetRestored = request.FieldOfficeSurveyKind == "purple_flower"
            ? office.plantsRestoredLeft.Value
            : office.plantsRestoredRight.Value;
        var verified = ReferenceEquals(Game1.currentLocation, office) && Game1.player.TilePoint == stand.Value &&
            !targetRestored && office.hasFailedSurveyToday.Value ==
                (request.FieldOfficeSurveyFixtureCase is "failed_today" or "day_reset") &&
            CountFieldOfficeSurveyWalnutDebris(office) == 0 && !office.safariGuyMutex.IsLocked();
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_field_office_survey",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_field_office_survey_fixture_installed", "survey_kind=" + request.FieldOfficeSurveyKind, "fixture_case=" + request.FieldOfficeSurveyFixtureCase }
                : new[] { "field_office_survey_fixture_projection_mismatch" },
            RequestedEffect = "field_office.survey_fixture=ready",
            ObservedEffect = FieldOfficeSurveyObservedEffect(office),
            TargetLocation = office.NameOrUniqueName,
            TargetTileX = action.Value.X,
            TargetTileY = action.Value.Y,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "field_office_survey_fixture_projection_mismatch" }
        };
    }

    private static TrainingExecutionResult ExecuteFieldOfficeSurveyDayUpdate(TrainingExecutionRequest request)
    {
        if (Game1.getLocationFromName("IslandFieldOffice") is not IslandFieldOffice office ||
            !office.hasFailedSurveyToday.Value)
            return FieldOfficeSurveyFixtureBlocked(request, "field_office_survey_day_update_precondition_missing");
        office.DayUpdate(Game1.dayOfMonth);
        var verified = !office.hasFailedSurveyToday.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_field_office_survey_day_update",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_IslandFieldOffice_DayUpdate_cleared_failed_survey_today" }
                : new[] { "field_office_survey_day_update_reset_mismatch" },
            RequestedEffect = "world_progress.island_field_office.has_failed_survey_today=false",
            ObservedEffect = FieldOfficeSurveyObservedEffect(office),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "field_office_survey_day_update_reset_mismatch" }
        };
    }

    private static Point? FieldOfficeFixtureSurveyTile(IslandFieldOffice office)
    {
        var buildings = office.Map?.GetLayer("Buildings");
        if (buildings is null)
            return null;
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                if (office.doesTileHaveProperty(x, y, "Action", "Buildings") == "FieldOfficeSurvey")
                    return new Point(x, y);
            }
        }
        return null;
    }

    private static TrainingExecutionResult FieldOfficeSurveyFixtureBlocked(TrainingExecutionRequest request, string reason) =>
        BlockedWithPrimitive(
            request,
            request.OptionId == "debug.field_office_survey_day_update"
                ? "debug_field_office_survey_day_update"
                : "debug_setup_field_office_survey",
            "field_office.survey_fixture=ready",
            FieldOfficeSurveyObservedEffect(Game1.getLocationFromName("IslandFieldOffice") as IslandFieldOffice),
            reason);
}
