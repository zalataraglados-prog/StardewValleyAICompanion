using System.Globalization;
using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerTargetDateIntentBuilder
{
    private const string GingerIslandPrerequisiteControlParameter =
        "master_angler_ginger_island_prerequisite_control_only";

    private static MasterAnglerTargetDateIntent?
        BuildGingerIslandPrerequisiteControlCandidate(
            MasterAnglerStageOneSpeciesWindow species,
            AuthoritativeCalendarSourceWindow window,
            MasterAnglerStageOneWindowIndex index,
            JsonElement state,
            string indexPath,
            string indexHash,
            int currentTotalDay,
            int currentTime,
            string farmTypeKey,
            IReadOnlyDictionary<string,
                FutureLocationRouteDateEvidenceProduction> routeByLocation,
            string timingCalibrationPath,
            string timingCalibrationSha256)
    {
        if (window.SourceKind == "crab_pot")
            return null;

        var targetLocation = RuntimeTargetLocation(
            window.LocationId,
            farmTypeKey);
        if (!TryReadLockedGingerIslandTarget(
                state,
                targetLocation,
                out var accessStatus) ||
            !routeByLocation.TryGetValue(targetLocation, out var route) ||
            route.Status == FutureRouteDateEvidenceProductionStatus.Produced)
        {
            return null;
        }

        var selectedTime = window.TimeWindows
            .Where(value => currentTime < value.EndTime)
            .Select(value => new
            {
                Start = Math.Max(currentTime, value.StartTime),
                End = value.EndTime
            })
            .Where(value => value.Start < value.End)
            .OrderBy(value => value.End)
            .ThenBy(value => value.Start)
            .FirstOrDefault();
        if (selectedTime is null)
            return null;

        var parameters = new[]
        {
            Parameter("master_angler_target_qualified_item_id", species.QualifiedItemId),
            Parameter("master_angler_target_location", targetLocation),
            Parameter("master_angler_source_kind", window.SourceKind),
            Parameter("master_angler_source_key", window.SourceKey),
            Parameter("master_angler_window_first_total_day", window.FirstTotalDay),
            Parameter("master_angler_window_last_total_day", window.LastTotalDay),
            Parameter("master_angler_target_total_day", currentTotalDay),
            Parameter("master_angler_effective_start_time", selectedTime.Start),
            Parameter("master_angler_last_cast_time_exclusive", selectedTime.End),
            Parameter("master_angler_stage_one_deadline_total_day_exclusive",
                index.DeadlineTotalDayExclusive),
            Parameter("master_angler_window_index_path", indexPath),
            Parameter("master_angler_window_index_sha256", indexHash),
            Parameter("master_angler_validation_status",
                "authoritative_stage_one_window_match"),
            Parameter("master_angler_runtime_terminal_validation_required", "false"),
            Parameter(MasterAnglerRouteTimingValidator.CalibrationPathParameter,
                timingCalibrationPath),
            Parameter(MasterAnglerRouteTimingValidator.CalibrationSha256Parameter,
                timingCalibrationSha256),
            Parameter(MasterAnglerRouteTimingValidator.ValidationRequiredParameter,
                "false"),
            Parameter(GingerIslandPrerequisiteControlParameter, "true"),
            Parameter("master_angler_ginger_island_access_status", accessStatus)
        };

        return new MasterAnglerTargetDateIntent
        {
            IntentId = "master-angler-ginger-island-prerequisite:" +
                currentTotalDay.ToString(CultureInfo.InvariantCulture) + ":" +
                species.QualifiedItemId + ":" + window.SourceKey,
            TargetQualifiedItemId = species.QualifiedItemId,
            DisplayName = species.DisplayName,
            TargetLocation = targetLocation,
            SourceKind = window.SourceKind,
            SourceKey = window.SourceKey,
            FirstTotalDay = window.FirstTotalDay,
            LastTotalDay = window.LastTotalDay,
            EffectiveStartTime = selectedTime.Start,
            LastCastTimeExclusive = selectedTime.End,
            RouteEdgeCount = 0,
            RouteGuaranteedArrivalTime = 0,
            RouteTimingStatus = "ginger_island_prerequisite_control_only",
            RouteTimingEvidenceId = string.Empty,
            DeadlineSlackDays =
                index.DeadlineTotalDayExclusive - currentTotalDay,
            RuntimeTerminalValidationRequired = false,
            Parameters = parameters
        };
    }

    private static bool TryReadLockedGingerIslandTarget(
        JsonElement state,
        string targetLocation,
        out string accessStatus)
    {
        accessStatus = string.Empty;
        var field = targetLocation switch
        {
            "IslandSouth" => "island_south_accessible",
            "IslandNorth" => "island_north_accessible",
            "IslandWest" => "island_west_accessible",
            "IslandSouthEast" => "island_south_east_accessible",
            "IslandSouthEastCave" => "island_south_east_cave_accessible",
            _ => string.Empty
        };
        if (field.Length == 0)
            return false;

        var access = RequiredFieldValue(
            state,
            "locations",
            "ginger_island_access");
        if (access.ValueKind != JsonValueKind.Object ||
            !access.TryGetProperty("schema_version", out var schema) ||
            schema.GetString() != "ginger_island_access.v1" ||
            !access.TryGetProperty("projection_status", out var projection) ||
            projection.GetString() !=
                "complete_locked_base_1.6.15_native_contract" ||
            !access.TryGetProperty("status", out var status) ||
            string.IsNullOrWhiteSpace(status.GetString()) ||
            !access.TryGetProperty("target_access", out var targetAccess) ||
            !targetAccess.TryGetProperty(field, out var accessible) ||
            accessible.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            accessible.GetBoolean())
        {
            return false;
        }

        accessStatus = status.GetString()!;
        return true;
    }
}
