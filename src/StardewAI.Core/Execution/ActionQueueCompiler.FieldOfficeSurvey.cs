using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileAnswerFieldOfficeSurveyStep(SmallModelAction action)
    {
        var kind = ReadParameter(action, "survey_kind");
        var answer = ReadIntParameter(action, "survey_answer");
        if (string.IsNullOrWhiteSpace(kind) || !answer.HasValue)
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "answer_field_office_survey",
                "field-office-survey:" + kind + ":answer=" + answer.Value,
                "world_progress.island_field_office." +
                    (kind == "purple_flower" ? "plants_restored_left" : "plants_restored_right") +
                    "=true;collected_nut_key=" + ReadParameter(action, "expected_collected_nut_key") +
                    ";current_location.debris[(O)73].count=" + ReadParameter(action, "walnut_debris_count_after") +
                    ";world_progress.golden_walnuts.found=" + ReadParameter(action, "golden_walnuts_found_after") +
                    ";finale_ready=" + ReadParameter(action, "expected_finale_ready_after"),
                360)
        };
    }

    private static string[] ValidateFieldOfficeSurveyPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.answer_field_office_survey")
            return Array.Empty<string>();
        var reasons = new List<string>();
        var actionX = ReadIntParameter(action, "target_tile_x");
        var actionY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var answer = ReadIntParameter(action, "survey_answer");
        var minimum = ReadIntParameter(action, "survey_answer_minimum");
        var maximum = ReadIntParameter(action, "survey_answer_maximum");
        var kind = ReadParameter(action, "survey_kind");
        if (!actionX.HasValue || !actionY.HasValue || !standX.HasValue || !standY.HasValue ||
            Math.Abs(actionX.Value - standX.Value) + Math.Abs(actionY.Value - standY.Value) != 1 ||
            !answer.HasValue || !minimum.HasValue || !maximum.HasValue || answer < minimum || answer > maximum ||
            !FieldOfficeSurveySettlementContractMatches(action, kind) ||
            !FieldOfficeSurveyAnswerContractMatches(action, kind, answer.Value, minimum.Value, maximum.Value) ||
            !TryBoolParameter(action, "survey_plant_restored_before", out var restoredBefore) || restoredBefore ||
            !TryBoolParameter(action, "survey_plant_restored_after", out var restoredAfter) || !restoredAfter ||
            !TryBoolParameter(action, "survey_failed_today_before", out var failedBefore) || failedBefore ||
            !TryBoolParameter(action, "survey_failed_today_after", out var failedAfter) || failedAfter ||
            string.IsNullOrWhiteSpace(ReadParameter(action, "expected_collected_nut_key")) ||
            ReadParameter(action, "field_office_projection_status") != "exact_locked_base_1.6.15" ||
            ReadParameter(action, "native_contract") != "FieldOfficeSurvey_then_Survey_Yes_then_exact_Correct_response_then_native_plant_nut_debris_and_finale")
            return new[] { "field_office_survey_typed_projection_required" };

        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("field_office_survey_menu_must_be_clear");
        var location = ReadParameter(action, "target_location");
        if (!string.Equals(location, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
            reasons.Add("field_office_survey_target_location_mismatch");

        var office = ReadStateFieldValue(snapshot, "world_progress", "island_field_office");
        if (!office.HasValue || office.Value.ValueKind != JsonValueKind.Object ||
            ReadString(office.Value, "projection_status") != "exact_locked_base_1.6.15" ||
            ReadBool(office.Value, "is_current_location") != true ||
            ReadBool(office.Value, "north_cave_opened") != true ||
            ReadBool(office.Value, "professor_available") != true ||
            ReadBool(office.Value, "mutex_locked") == true ||
            ReadBool(office.Value, "menu_clear") != true ||
            ReadString(office.Value, "location_id") != location ||
            !FieldOfficeSurveyTileMatches(office.Value, actionX.Value, actionY.Value, ReadParameter(action, "field_office_survey_action_raw")) ||
            ReadBool(office.Value, "has_failed_survey_today") != false ||
            ReadBool(office.Value, "plants_restored_left") != ReadBoolParameter(action, "plants_restored_left_before") ||
            ReadBool(office.Value, "plants_restored_right") != ReadBoolParameter(action, "plants_restored_right_before") ||
            ReadBool(office.Value, "finale_received_or_pending") != ReadBoolParameter(action, "finale_received_or_pending_before") ||
            ReadInt(office.Value, "donated_piece_count") != ReadIntParameter(action, "donated_piece_count_before") ||
            ReadInt(office.Value, "golden_walnuts_found") != ReadIntParameter(action, "golden_walnuts_found_before") ||
            !TryFindFieldOfficeSurveyCandidate(office.Value, kind, answer.Value, out var candidate) ||
            !FieldOfficeSurveyCandidateMatches(action, candidate))
            reasons.Add("field_office_survey_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool FieldOfficeSurveyAnswerContractMatches(
        SmallModelAction action,
        string? kind,
        int answer,
        int minimum,
        int maximum) =>
        ReadParameter(action, "survey_prompt_question_key") == "Survey" &&
        ReadParameter(action, "survey_prompt_response_key") == "Yes" &&
        ReadParameter(action, "survey_answer_response_key") == "Correct" &&
        ((kind == "purple_flower" && answer == 22 && minimum == 18 && maximum == 24 &&
          ReadParameter(action, "survey_answer_question_key") == "PurpleFlowerSurvey" &&
          ReadParameter(action, "expected_collected_nut_key") == "IslandLeftPlantRestored") ||
         (kind == "purple_starfish" && answer == 18 && minimum == 11 && maximum == 18 &&
          ReadParameter(action, "survey_answer_question_key") == "PurpleStarfishSurvey" &&
          ReadParameter(action, "expected_collected_nut_key") == "IslandRightPlantRestored"));

    private static bool FieldOfficeSurveySettlementContractMatches(SmallModelAction action, string? kind)
    {
        var debrisBefore = ReadIntParameter(action, "walnut_debris_count_before");
        var debrisAfter = ReadIntParameter(action, "walnut_debris_count_after");
        var spawnCount = ReadIntParameter(action, "walnut_debris_spawn_count");
        var walnutsBefore = ReadIntParameter(action, "golden_walnuts_found_before");
        var walnutsAfter = ReadIntParameter(action, "golden_walnuts_found_after");
        var walnutsDelta = ReadIntParameter(action, "golden_walnuts_found_delta");
        var donatedCount = ReadIntParameter(action, "donated_piece_count_before");
        if (!debrisBefore.HasValue || !debrisAfter.HasValue || !spawnCount.HasValue ||
            !walnutsBefore.HasValue || !walnutsAfter.HasValue || !walnutsDelta.HasValue || !donatedCount.HasValue ||
            !TryBoolParameter(action, "collected_nut_before", out var collectedBefore) ||
            !TryBoolParameter(action, "expected_finale_ready_after", out var finaleReady) ||
            !TryBoolParameter(action, "expected_finale_trigger_after", out var finaleTrigger) ||
            !TryBoolParameter(action, "plants_restored_left_before", out var leftBefore) ||
            !TryBoolParameter(action, "plants_restored_right_before", out var rightBefore) ||
            !TryBoolParameter(action, "finale_received_or_pending_before", out var finaleReceived))
            return false;

        var expectedSpawn = walnutsBefore.Value < 130 ? 1 : 0;
        var expectedOutput = expectedSpawn == 1
            ? "native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found"
            : "none_at_130_walnuts_found";
        var expectedFinaleReady = donatedCount.Value == 11 &&
            (kind == "purple_flower" || leftBefore) &&
            (kind == "purple_starfish" || rightBefore);
        return debrisBefore.Value == 0 && debrisAfter.Value == 0 && !collectedBefore &&
            walnutsBefore.Value is >= 0 and <= 130 && spawnCount.Value == expectedSpawn &&
            walnutsDelta.Value == expectedSpawn && walnutsAfter.Value == walnutsBefore.Value + expectedSpawn &&
            ReadParameter(action, "output_delivery") == expectedOutput &&
            finaleReady == expectedFinaleReady && finaleTrigger == (expectedFinaleReady && !finaleReceived);
    }

    private static bool FieldOfficeSurveyCandidateMatches(SmallModelAction action, JsonElement candidate) =>
        ReadString(candidate, "action_status") == "ready" &&
        ReadInt(candidate, "answer_minimum") == ReadIntParameter(action, "survey_answer_minimum") &&
        ReadInt(candidate, "answer_maximum") == ReadIntParameter(action, "survey_answer_maximum") &&
        ReadString(candidate, "prompt_question_key") == ReadParameter(action, "survey_prompt_question_key") &&
        ReadString(candidate, "prompt_response_key") == ReadParameter(action, "survey_prompt_response_key") &&
        ReadString(candidate, "answer_question_key") == ReadParameter(action, "survey_answer_question_key") &&
        ReadString(candidate, "answer_response_key") == ReadParameter(action, "survey_answer_response_key") &&
        ReadBool(candidate, "plant_restored_before") == ReadBoolParameter(action, "survey_plant_restored_before") &&
        ReadBool(candidate, "plant_restored_after") == ReadBoolParameter(action, "survey_plant_restored_after") &&
        ReadBool(candidate, "failed_survey_today_before") == ReadBoolParameter(action, "survey_failed_today_before") &&
        ReadBool(candidate, "failed_survey_today_after") == ReadBoolParameter(action, "survey_failed_today_after") &&
        ReadString(candidate, "expected_collected_nut_key") == ReadParameter(action, "expected_collected_nut_key") &&
        ReadBool(candidate, "collected_nut_before") == ReadBoolParameter(action, "collected_nut_before") &&
        ReadInt(candidate, "walnut_debris_count_before") == ReadIntParameter(action, "walnut_debris_count_before") &&
        ReadInt(candidate, "walnut_debris_count_after") == ReadIntParameter(action, "walnut_debris_count_after") &&
        ReadInt(candidate, "walnut_debris_spawn_count") == ReadIntParameter(action, "walnut_debris_spawn_count") &&
        ReadInt(candidate, "golden_walnuts_found_before") == ReadIntParameter(action, "golden_walnuts_found_before") &&
        ReadInt(candidate, "golden_walnuts_found_after") == ReadIntParameter(action, "golden_walnuts_found_after") &&
        ReadInt(candidate, "golden_walnuts_found_delta") == ReadIntParameter(action, "golden_walnuts_found_delta") &&
        ReadString(candidate, "output_delivery") == ReadParameter(action, "output_delivery") &&
        ReadBool(candidate, "expected_finale_ready_after") == ReadBoolParameter(action, "expected_finale_ready_after") &&
        ReadBool(candidate, "expected_finale_trigger_after") == ReadBoolParameter(action, "expected_finale_trigger_after");

    private static bool FieldOfficeSurveyTileMatches(JsonElement office, int x, int y, string? raw) =>
        office.TryGetProperty("survey_action_tiles", out var tiles) && tiles.ValueKind == JsonValueKind.Array &&
        tiles.EnumerateArray().Any(tile => tile.ValueKind == JsonValueKind.Object &&
            ReadInt(tile, "tile_x") == x && ReadInt(tile, "tile_y") == y && ReadString(tile, "action_raw") == raw);

    private static bool TryFindFieldOfficeSurveyCandidate(
        JsonElement office,
        string? kind,
        int answer,
        out JsonElement candidate)
    {
        candidate = default;
        if (!office.TryGetProperty("survey_candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object && ReadString(row, "survey_kind") == kind &&
                ReadInt(row, "answer") == answer)
            {
                candidate = row;
                return true;
            }
        }
        return false;
    }
}
