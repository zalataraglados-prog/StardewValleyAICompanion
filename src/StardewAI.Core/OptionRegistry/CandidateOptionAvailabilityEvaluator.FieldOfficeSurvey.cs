using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] FieldOfficeSurveyCandidates(SnapshotEnvelope snapshot)
    {
        var office = ReadStateFieldValue(snapshot, "world_progress", "island_field_office");
        if (!office.HasValue || office.Value.ValueKind != JsonValueKind.Object ||
            !office.Value.TryGetProperty("survey_candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var officeRow = office.Value;
        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var targetLocation = ReadString(officeRow, "location_id");
        if (ReadBool(officeRow, "is_current_location") != true)
            return FieldOfficeSurveyRouteCandidates(snapshot, officeRow, candidates, currentLocation, targetLocation);

        var endpoint = ReadFieldOfficeSurveyTiles(officeRow)
            .Select(tile => new { tile, stand = FindBestStandTile(snapshot, tile.X, tile.Y) })
            .Where(value => value.stand is not null)
            .OrderBy(value => Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - value.stand!.X) +
                Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - value.stand!.Y))
            .FirstOrDefault();

        return candidates.EnumerateArray()
            .Where(candidate => candidate.ValueKind == JsonValueKind.Object)
            .Select(candidate =>
            {
                var reasons = new List<string>();
                var status = ReadString(candidate, "action_status");
                if (status != "ready")
                    reasons.Add(string.IsNullOrWhiteSpace(status) ? "field_office_survey_projection_unavailable" : status);
                if (ReadString(officeRow, "projection_status") != "exact_locked_base_1.6.15")
                    reasons.Add("field_office_projection_not_locked");
                if (endpoint is null)
                    reasons.Add("field_office_no_reachable_survey_stand");
                if (!FieldOfficeSurveyCandidateIsTyped(candidate))
                    reasons.Add("field_office_survey_typed_projection_invalid");

                return new EventCandidate
                {
                    CandidateId = "field-office-survey:" + ReadString(candidate, "survey_kind"),
                    Kind = "answer_field_office_survey",
                    Available = reasons.Count == 0,
                    AllowedNow = reasons.Count == 0,
                    LocationId = targetLocation,
                    TileX = endpoint?.tile.X,
                    TileY = endpoint?.tile.Y,
                    EstimatedTicks = 360,
                    AvailabilityClass = "transparent_native_field_office_survey",
                    ExpectedEffect = FieldOfficeSurveyExpectedEffect(candidate),
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = endpoint is null
                        ? Array.Empty<SmallModelActionParameter>()
                        : FieldOfficeSurveyParameters(officeRow, candidate, endpoint.tile, endpoint.stand!)
                };
            })
            .ToArray();
    }

    private EventCandidate[] FieldOfficeSurveyRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement office,
        JsonElement candidates,
        string currentLocation,
        string targetLocation)
    {
        if (ReadString(office, "projection_status") != "exact_locked_base_1.6.15" ||
            ReadBool(office, "north_cave_opened") != true ||
            ReadString(office, "location_id") != targetLocation)
            return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, targetLocation,
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(value => value.Kind == "route_connector_tile").ToArray());
        if (route?.FirstActionCandidate is null)
            return Array.Empty<EventCandidate>();

        return candidates.EnumerateArray()
            .Where(candidate => candidate.ValueKind == JsonValueKind.Object &&
                ReadString(candidate, "action_status") == "route_to_field_office_required" &&
                FieldOfficeSurveyCandidateIsTyped(candidate))
            .Select(candidate => CloneCandidate(
                route.FirstActionCandidate,
                candidateId: "field-office-survey-route:" + ReadString(candidate, "survey_kind") + ":" + currentLocation,
                expectedEffect: route.FirstActionCandidate.ExpectedEffect + ";field_office_survey=" + ReadString(candidate, "survey_kind"),
                parameters: route.FirstActionCandidate.Parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "island.field_office_survey"),
                    Parameter("continuation.survey_kind", ReadString(candidate, "survey_kind")),
                    Parameter("continuation.answer", ReadInt(candidate, "answer").ToString(CultureInfo.InvariantCulture))
                }).ToArray(),
                availabilityClass: "field_office_survey_rolling_route"))
            .ToArray();
    }

    private static SmallModelActionParameter[] FieldOfficeSurveyParameters(
        JsonElement office,
        JsonElement candidate,
        FieldOfficeSurveyTile tile,
        CandidateTile stand) => new[]
    {
        Parameter("target_location", ReadString(office, "location_id")),
        Parameter("target_tile_x", tile.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", tile.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
        Parameter("field_office_survey_action_raw", tile.ActionRaw),
        Parameter("survey_kind", ReadString(candidate, "survey_kind")),
        Parameter("survey_answer", ReadInt(candidate, "answer").ToString(CultureInfo.InvariantCulture)),
        Parameter("survey_answer_minimum", ReadInt(candidate, "answer_minimum").ToString(CultureInfo.InvariantCulture)),
        Parameter("survey_answer_maximum", ReadInt(candidate, "answer_maximum").ToString(CultureInfo.InvariantCulture)),
        Parameter("survey_prompt_question_key", ReadString(candidate, "prompt_question_key")),
        Parameter("survey_prompt_response_key", ReadString(candidate, "prompt_response_key")),
        Parameter("survey_answer_question_key", ReadString(candidate, "answer_question_key")),
        Parameter("survey_answer_response_key", ReadString(candidate, "answer_response_key")),
        Parameter("survey_plant_restored_before", FieldOfficeBoolText(candidate, "plant_restored_before")),
        Parameter("survey_plant_restored_after", FieldOfficeBoolText(candidate, "plant_restored_after")),
        Parameter("survey_failed_today_before", FieldOfficeBoolText(candidate, "failed_survey_today_before")),
        Parameter("survey_failed_today_after", FieldOfficeBoolText(candidate, "failed_survey_today_after")),
        Parameter("expected_collected_nut_key", ReadString(candidate, "expected_collected_nut_key")),
        Parameter("collected_nut_before", FieldOfficeBoolText(candidate, "collected_nut_before")),
        Parameter("walnut_debris_count_before", ReadInt(candidate, "walnut_debris_count_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("walnut_debris_count_after", ReadInt(candidate, "walnut_debris_count_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("walnut_debris_spawn_count", ReadInt(candidate, "walnut_debris_spawn_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("golden_walnuts_found_after", ReadInt(candidate, "golden_walnuts_found_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("golden_walnuts_found_delta", ReadInt(candidate, "golden_walnuts_found_delta").ToString(CultureInfo.InvariantCulture)),
        Parameter("output_delivery", ReadString(candidate, "output_delivery")),
        Parameter("expected_finale_ready_after", FieldOfficeBoolText(candidate, "expected_finale_ready_after")),
        Parameter("expected_finale_trigger_after", FieldOfficeBoolText(candidate, "expected_finale_trigger_after")),
        Parameter("plants_restored_left_before", FieldOfficeBoolText(office, "plants_restored_left")),
        Parameter("plants_restored_right_before", FieldOfficeBoolText(office, "plants_restored_right")),
        Parameter("finale_received_or_pending_before", FieldOfficeBoolText(office, "finale_received_or_pending")),
        Parameter("donated_piece_count_before", ReadInt(office, "donated_piece_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("golden_walnuts_found_before", ReadInt(office, "golden_walnuts_found").ToString(CultureInfo.InvariantCulture)),
        Parameter("field_office_projection_status", ReadString(office, "projection_status")),
        Parameter("native_contract", "FieldOfficeSurvey_then_Survey_Yes_then_exact_Correct_response_then_native_plant_nut_debris_and_finale"),
        Parameter("max_movement_tiles", "512")
    };

    private static bool FieldOfficeSurveyCandidateIsTyped(JsonElement candidate)
    {
        var kind = ReadString(candidate, "survey_kind");
        var answer = ReadInt(candidate, "answer");
        var minimum = ReadInt(candidate, "answer_minimum");
        var maximum = ReadInt(candidate, "answer_maximum");
        var spawnCount = ReadInt(candidate, "walnut_debris_spawn_count");
        var walnutsBefore = ReadInt(candidate, "golden_walnuts_found_before");
        var walnutDelta = ReadInt(candidate, "golden_walnuts_found_delta");
        return kind is "purple_flower" or "purple_starfish" && answer >= minimum && answer <= maximum &&
            ((kind == "purple_flower" && answer == 22 && minimum == 18 && maximum == 24 &&
              ReadString(candidate, "answer_question_key") == "PurpleFlowerSurvey") ||
             (kind == "purple_starfish" && answer == 18 && minimum == 11 && maximum == 18 &&
              ReadString(candidate, "answer_question_key") == "PurpleStarfishSurvey")) &&
            ReadString(candidate, "prompt_question_key") == "Survey" &&
            ReadString(candidate, "prompt_response_key") == "Yes" &&
            ReadString(candidate, "answer_response_key") == "Correct" &&
            ReadBool(candidate, "plant_restored_before") == false &&
            ReadBool(candidate, "plant_restored_after") == true &&
            ReadBool(candidate, "failed_survey_today_before") == false &&
            ReadBool(candidate, "failed_survey_today_after") == false &&
            ReadBool(candidate, "collected_nut_before") == false &&
            ReadInt(candidate, "walnut_debris_count_before") == 0 &&
            ReadInt(candidate, "walnut_debris_count_after") == 0 &&
            walnutsBefore is >= 0 and <= 130 && spawnCount == (walnutsBefore < 130 ? 1 : 0) &&
            walnutDelta == spawnCount && ReadInt(candidate, "golden_walnuts_found_after") == walnutsBefore + walnutDelta &&
            ReadString(candidate, "output_delivery") == (spawnCount == 1
                ? "native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found"
                : "none_at_130_walnuts_found");
    }

    private static string FieldOfficeSurveyExpectedEffect(JsonElement candidate) =>
        "field_office_survey=" + ReadString(candidate, "survey_kind") + ":answer=" + ReadInt(candidate, "answer") +
        ":restored=true;collected_nut_key=" + ReadString(candidate, "expected_collected_nut_key") +
        ";walnut_debris_spawn_count=" + ReadInt(candidate, "walnut_debris_spawn_count") +
        ";golden_walnuts_found_after=" + ReadInt(candidate, "golden_walnuts_found_after") +
        ";finale_ready=" + FieldOfficeBoolText(candidate, "expected_finale_ready_after");

    private static FieldOfficeSurveyTile[] ReadFieldOfficeSurveyTiles(JsonElement office)
    {
        if (!office.TryGetProperty("survey_action_tiles", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<FieldOfficeSurveyTile>();
        return rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => new FieldOfficeSurveyTile(ReadInt(row, "tile_x"), ReadInt(row, "tile_y"), ReadString(row, "action_raw")))
            .ToArray();
    }

    private sealed record FieldOfficeSurveyTile(int X, int Y, string ActionRaw);
}
