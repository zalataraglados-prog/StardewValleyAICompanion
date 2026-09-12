using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

internal static class NativeCalendarConstraintNormalizer
{
    private static readonly HashSet<string> DynamicPredicateNames = new(StringComparer.Ordinal)
    {
        "DAYS_PLAYED",
        "IS_FESTIVAL_DAY",
        "IS_ISLAND_NORTH_BRIDGE_FIXED",
        "IS_PASSIVE_FESTIVAL_OPEN",
        "PLAYER_HAS_MAIL",
        "PLAYER_HAS_ITEM",
        "PLAYER_LOCATION_NAME",
        "PLAYER_SPECIAL_ORDER_ACTIVE",
        "PLAYER_SPECIAL_ORDER_RULE_ACTIVE",
        "PLAYER_STAT",
        "RANDOM",
        "SYNCED_CHOICE",
        "SYNCED_RANDOM"
    };

    public static string[] AllSeasons =>
        new[] { "spring", "summer", "fall", "winter" };

    public static MasterAnglerTimeWindow[] AllDay =>
        new[] { new MasterAnglerTimeWindow { StartTime = 600, EndTime = 2600 } };

    public static string[] AllWeatherModes =>
        new[] { "sun", "rain", "storm", "green_rain" };

    public static MasterAnglerCalendarConstraint Normalize(
        IEnumerable<string> baseSeasons,
        IEnumerable<MasterAnglerTimeWindow> baseTimeWindows,
        IEnumerable<string> baseWeatherModes,
        string explicitSeason,
        params string[] conditions)
    {
        var minimumYear = 1;
        int? maximumYear = null;
        var seasons = new HashSet<string>(
            baseSeasons.Select(NormalizeSeason),
            StringComparer.Ordinal);
        var timeWindows = baseTimeWindows
            .Select(window => new MasterAnglerTimeWindow
            {
                StartTime = window.StartTime,
                EndTime = window.EndTime
            })
            .ToList();
        var weather = new HashSet<string>(
            baseWeatherModes.Select(NormalizeWeatherMode),
            StringComparer.Ordinal);
        var dynamicConditions = new List<string>();
        var unparsed = new List<string>();

        if (!string.IsNullOrWhiteSpace(explicitSeason))
            seasons.IntersectWith(new[] { NormalizeSeason(explicitSeason) });

        foreach (var clause in conditions.SelectMany(SplitClauses))
        {
            var tokens = clause.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length >= 3 && tokens[0] == "LOCATION_SEASON" &&
                tokens[1] == "Here")
            {
                seasons.IntersectWith(tokens.Skip(2).Select(NormalizeSeason));
            }
            else if (tokens.Length >= 2 && tokens[0] == "SEASON")
            {
                seasons.IntersectWith(tokens.Skip(1).Select(NormalizeSeason));
            }
            else if (TryParseYear(tokens, ref minimumYear, ref maximumYear))
            {
            }
            else if (TryParseTime(tokens, out var requiredTime))
            {
                timeWindows = IntersectTimeWindows(timeWindows, requiredTime);
            }
            else if (tokens.Length >= 3 && tokens[0] == "WEATHER" &&
                tokens[1] == "Here")
            {
                weather.IntersectWith(tokens.Skip(2).Select(NormalizeWeatherMode));
            }
            else if (tokens.Length > 0 && DynamicPredicateNames.Contains(
                         tokens[0].StartsWith('!')
                             ? tokens[0][1..]
                             : tokens[0]))
            {
                dynamicConditions.Add(clause);
            }
            else
            {
                unparsed.Add(clause);
            }
        }

        return new MasterAnglerCalendarConstraint
        {
            ParseStatus = unparsed.Count == 0
                ? "complete"
                : "unparsed_dynamic_condition",
            MinimumYear = minimumYear,
            MaximumYear = maximumYear,
            Seasons = seasons.OrderBy(SeasonOrder).ToArray(),
            TimeWindows = timeWindows.OrderBy(window => window.StartTime).ToArray(),
            WeatherModes = weather.Order(StringComparer.Ordinal).ToArray(),
            DynamicConditions = dynamicConditions.Distinct(StringComparer.Ordinal).ToArray(),
            UnparsedConditions = unparsed.Distinct(StringComparer.Ordinal).ToArray(),
            StaticCalendarPossible =
                (!maximumYear.HasValue || minimumYear <= maximumYear.Value) &&
                seasons.Count > 0 &&
                timeWindows.Count > 0 &&
                weather.Count > 0
        };
    }

    public static string[] FishWeatherModes(string raw) => raw.ToLowerInvariant() switch
    {
        "sunny" => new[] { "sun" },
        "rainy" => new[] { "rain", "storm", "green_rain" },
        _ => AllWeatherModes
    };

    private static IEnumerable<string> SplitClauses(string condition) =>
        (condition ?? string.Empty).Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool TryParseYear(
        string[] tokens,
        ref int minimumYear,
        ref int? maximumYear)
    {
        if (tokens.Length is not (2 or 3) || tokens[0] != "YEAR" ||
            !int.TryParse(tokens[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsedMinimum) ||
            (tokens.Length == 3 &&
             !int.TryParse(tokens[2], NumberStyles.Integer,
                 CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        minimumYear = Math.Max(minimumYear, parsedMinimum);
        if (tokens.Length == 3)
        {
            var parsedMaximum = int.Parse(tokens[2], CultureInfo.InvariantCulture);
            maximumYear = maximumYear.HasValue
                ? Math.Min(maximumYear.Value, parsedMaximum)
                : parsedMaximum;
        }
        return true;
    }

    private static bool TryParseTime(
        string[] tokens,
        out MasterAnglerTimeWindow required)
    {
        required = new MasterAnglerTimeWindow();
        if (tokens.Length is not (2 or 3) || tokens[0] != "TIME" ||
            !int.TryParse(tokens[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var start) ||
            (tokens.Length == 3 &&
             !int.TryParse(tokens[2], NumberStyles.Integer,
                 CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }

        required.StartTime = start;
        required.EndTime = tokens.Length == 3
            ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
            : int.MaxValue;
        return true;
    }

    private static List<MasterAnglerTimeWindow> IntersectTimeWindows(
        IEnumerable<MasterAnglerTimeWindow> existing,
        MasterAnglerTimeWindow required) => existing
        .Select(window => new MasterAnglerTimeWindow
        {
            StartTime = Math.Max(window.StartTime, required.StartTime),
            EndTime = Math.Min(window.EndTime, required.EndTime)
        })
        .Where(window => window.StartTime < window.EndTime)
        .ToList();

    private static string NormalizeWeatherMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "greenrain" => "green_rain",
            var normalized => normalized
        };

    private static string NormalizeSeason(string value) => value.ToLowerInvariant();

    private static int SeasonOrder(string season) => season switch
    {
        "spring" => 0,
        "summer" => 1,
        "fall" => 2,
        "winter" => 3,
        _ => 4
    };
}
