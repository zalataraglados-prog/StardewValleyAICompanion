using System.Globalization;
using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static CalendarSourceResolution ResolveLocationWindows(
        AcquisitionRequirementRouteLowering route,
        JsonElement locations,
        int deadlineTotalDayExclusive)
    {
        if (!TryParseLocationSource(route, out var locationId, out var rowIndex) ||
            !locations.TryGetProperty(locationId, out var location) ||
            !location.TryGetProperty(LocationRowProperty(route.RouteKind), out var rows) ||
            rows.ValueKind != JsonValueKind.Array ||
            rowIndex < 0 ||
            rowIndex >= rows.GetArrayLength())
        {
            return new CalendarSourceResolution(
                "blocked_authoritative_location_row_not_found",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                new[] { "exact_location_route_source_identity_not_found" });
        }

        var row = rows[rowIndex];
        if (!TryReadSeason(row, out var explicitSeason))
        {
            return new CalendarSourceResolution(
                "blocked_unparsed_location_season",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                new[] { "location_row_season_value_is_not_supported" });
        }
        var calendar = NativeCalendarConstraintNormalizer.Normalize(
            NativeCalendarConstraintNormalizer.AllSeasons,
            NativeCalendarConstraintNormalizer.AllDay,
            NativeCalendarConstraintNormalizer.AllWeatherModes,
            explicitSeason,
            ReadString(row, "Condition"),
            ReadString(row, "PerItemCondition"));
        if (calendar.ParseStatus != "complete")
        {
            return new CalendarSourceResolution(
                "blocked_unparsed_location_calendar_condition",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                calendar.UnparsedConditions.Select(condition =>
                    "unparsed_native_condition:" + condition).ToArray());
        }
        if (!calendar.StaticCalendarPossible)
        {
            return new CalendarSourceResolution(
                "blocked_static_location_calendar_impossible",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                new[] { "location_calendar_has_no_static_window" });
        }

        var sourceKey = locationId + ":" + rowIndex;
        var windows = ExpandLocationWindows(
            route.RouteKind,
            sourceKey,
            locationId,
            ReadString(row, "Id"),
            row,
            calendar,
            deadlineTotalDayExclusive);
        return windows.Length > 0
            ? new CalendarSourceResolution(
                ResolvedStatus,
                LocationEvidenceClass(route.RouteKind),
                windows,
                Array.Empty<string>())
            : new CalendarSourceResolution(
                "blocked_location_calendar_after_deadline",
                string.Empty,
                windows,
                new[] { "location_calendar_has_no_window_before_deadline" });
    }

    private static AuthoritativeCalendarSourceWindow[] ExpandLocationWindows(
        string routeKind,
        string sourceKey,
        string locationId,
        string ruleId,
        JsonElement row,
        MasterAnglerCalendarConstraint calendar,
        int deadlineTotalDayExclusive)
    {
        var result = new List<AuthoritativeCalendarSourceWindow>();
        var deadlineYear = ((deadlineTotalDayExclusive - 1) / 112) + 1;
        for (var year = Math.Max(1, calendar.MinimumYear);
             year <= deadlineYear;
             year++)
        {
            if (calendar.MaximumYear.HasValue && year > calendar.MaximumYear.Value)
                continue;
            foreach (var season in calendar.Seasons)
            {
                var first = TotalDay(year, season, 1);
                var last = Math.Min(
                    TotalDay(year, season, 28),
                    deadlineTotalDayExclusive - 1);
                if (first >= deadlineTotalDayExclusive || last < first)
                    continue;
                result.Add(new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = LocationSourceKind(routeKind),
                    SourceKey = sourceKey,
                    LocationId = locationId,
                    RuleId = ruleId,
                    Year = year,
                    Season = season,
                    FirstTotalDay = first,
                    LastTotalDay = last,
                    TimeWindows = calendar.TimeWindows,
                    WeatherModes = calendar.WeatherModes,
                    DynamicConditions = calendar.DynamicConditions,
                    MinimumFishingLevel = routeKind == "native_location_fish_spawn"
                        ? ReadInt(row, "MinFishingLevel")
                        : 0,
                    RequireMagicBait = routeKind == "native_location_fish_spawn" &&
                        ReadBool(row, "RequireMagicBait"),
                    TrainingRodAllowed = routeKind == "native_location_fish_spawn"
                        ? ReadNullableBool(row, "CanUseTrainingRod")
                        : null,
                    RequiresLocationAccessEvidence = true,
                    RequiresRouteAndFishableTileEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = true
                });
            }
        }
        return result
            .OrderBy(window => window.LastTotalDay)
            .ThenBy(window => window.FirstTotalDay)
            .ThenBy(window => window.SourceKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryParseLocationSource(
        AcquisitionRequirementRouteLowering route,
        out string locationId,
        out int rowIndex)
    {
        locationId = string.Empty;
        rowIndex = -1;
        var prefix = route.RouteKind == "native_location_fish_spawn"
            ? "location_fish:"
            : "location:";
        if (!route.SourceId.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var source = route.SourceId[prefix.Length..];
        var separator = source.LastIndexOf(':');
        return separator > 0 &&
            int.TryParse(source[(separator + 1)..], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out rowIndex) &&
            (locationId = source[..separator]).Length > 0;
    }

    private static bool TryReadSeason(JsonElement row, out string season)
    {
        season = string.Empty;
        if (!row.TryGetProperty("Season", out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            season = value.GetString()?.ToLowerInvariant() ?? string.Empty;
            return season.Length == 0 ||
                NativeCalendarConstraintNormalizer.AllSeasons.Contains(
                    season,
                    StringComparer.Ordinal);
        }
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var index))
            return false;
        season = index switch
        {
            0 => "spring",
            1 => "summer",
            2 => "fall",
            3 => "winter",
            _ => string.Empty
        };
        return season.Length > 0;
    }

    private static int TotalDay(int year, string season, int dayOfMonth)
    {
        var seasonIndex = Array.FindIndex(
            NativeCalendarConstraintNormalizer.AllSeasons,
            value => value == season);
        if (seasonIndex < 0 || dayOfMonth is < 1 or > 28)
            throw new InvalidDataException("Invalid normalized location calendar date.");
        return (year - 1) * 112 + seasonIndex * 28 + dayOfMonth - 1;
    }

    private static string LocationRowProperty(string routeKind) => routeKind switch
    {
        "native_location_artifact_spot" => "ArtifactSpots",
        "native_location_fish_spawn" => "Fish",
        "native_location_forage_spawn" => "Forage",
        _ => string.Empty
    };

    private static string LocationSourceKind(string routeKind) => routeKind switch
    {
        "native_location_artifact_spot" => "location_artifact_spot_rule",
        "native_location_fish_spawn" => "location_nonfish_fishing_rule",
        "native_location_forage_spawn" => "location_forage_rule",
        _ => string.Empty
    };

    private static string LocationEvidenceClass(string routeKind) => routeKind switch
    {
        "native_location_artifact_spot" => "runtime_location_artifact_spot_window",
        "native_location_fish_spawn" => "runtime_location_nonfish_fishing_window",
        "native_location_forage_spawn" => "runtime_location_forage_window",
        _ => string.Empty
    };

    private static string ReadString(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : 0;

    private static bool ReadBool(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private static bool? ReadNullableBool(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
