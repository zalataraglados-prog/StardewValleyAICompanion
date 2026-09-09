using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerOpportunityCatalogBuilder
{
    private static MasterAnglerCalendarConstraint NormalizeCalendar(
        MasterAnglerFishDataConstraint fishConstraint,
        string spawnSeason,
        string condition,
        bool ignoreFishDataRequirements)
    {
        var minimumYear = 1;
        int? maximumYear = null;
        var seasons = new HashSet<string>(
            ignoreFishDataRequirements || fishConstraint.Seasons.Length == 0
                ? new[] { "spring", "summer", "fall", "winter" }
                : fishConstraint.Seasons,
            StringComparer.OrdinalIgnoreCase);
        var timeWindows = (ignoreFishDataRequirements || fishConstraint.TimeWindows.Length == 0
                ? new[] { new MasterAnglerTimeWindow { StartTime = 600, EndTime = 2600 } }
                : fishConstraint.TimeWindows)
            .ToList();
        var weather = new HashSet<string>(
            ignoreFishDataRequirements
                ? AllWeatherModes()
                : WeatherModes(fishConstraint.Weather),
            StringComparer.Ordinal);
        var dynamicConditions = new List<string>();
        var unparsed = new List<string>();

        if (spawnSeason.Length > 0)
            seasons.IntersectWith(new[] { spawnSeason });

        foreach (var clause in condition.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokens = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length >= 3 && tokens[0] == "LOCATION_SEASON" && tokens[1] == "Here")
            {
                seasons.IntersectWith(tokens.Skip(2));
            }
            else if (tokens.Length >= 2 && tokens[0] == "SEASON")
            {
                seasons.IntersectWith(tokens.Skip(1));
            }
            else if (tokens.Length is 2 or 3 && tokens[0] == "YEAR" &&
                     int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minYear) &&
                     (tokens.Length == 2 ||
                      int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                minimumYear = Math.Max(minimumYear, minYear);
                if (tokens.Length == 3)
                {
                    var maxYear = int.Parse(tokens[2], CultureInfo.InvariantCulture);
                    maximumYear = maximumYear.HasValue
                        ? Math.Min(maximumYear.Value, maxYear)
                        : maxYear;
                }
            }
            else if (tokens.Length is 2 or 3 && tokens[0] == "TIME" &&
                     int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) &&
                     (tokens.Length == 2 ||
                      int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                var end = tokens.Length == 3
                    ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
                    : int.MaxValue;
                timeWindows = IntersectTimeWindows(
                    timeWindows,
                    new MasterAnglerTimeWindow { StartTime = start, EndTime = end });
            }
            else if (tokens.Length >= 3 && tokens[0] == "WEATHER" && tokens[1] == "Here")
            {
                weather.IntersectWith(tokens.Skip(2).Select(NormalizeWeatherMode));
            }
            else if (tokens.Length > 0 &&
                     (tokens[0] is "IS_FESTIVAL_DAY" or "!IS_FESTIVAL_DAY" or
                         "IS_PASSIVE_FESTIVAL_OPEN" or "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE" or "RANDOM"))
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
            ParseStatus = unparsed.Count == 0 ? "complete" : "unparsed_dynamic_condition",
            MinimumYear = minimumYear,
            MaximumYear = maximumYear,
            Seasons = seasons.OrderBy(SeasonOrder).ToArray(),
            TimeWindows = timeWindows.OrderBy(value => value.StartTime).ToArray(),
            WeatherModes = weather.Order(StringComparer.Ordinal).ToArray(),
            DynamicConditions = dynamicConditions.ToArray(),
            UnparsedConditions = unparsed.ToArray(),
            StaticCalendarPossible = (!maximumYear.HasValue || minimumYear <= maximumYear.Value) &&
                                     seasons.Count > 0 && timeWindows.Count > 0 && weather.Count > 0
        };
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

    private static string[] WeatherModes(string raw) => raw.ToLowerInvariant() switch
    {
        "sunny" => new[] { "sun" },
        "rainy" => new[] { "rain", "storm", "green_rain" },
        _ => AllWeatherModes()
    };

    private static string[] AllWeatherModes() => new[] { "sun", "rain", "storm", "green_rain" };

    private static string NormalizeWeatherMode(string value) => value.ToLowerInvariant() switch
    {
        "greenrain" => "green_rain",
        var normalized => normalized
    };

    private static int SeasonOrder(string season) => season.ToLowerInvariant() switch
    {
        "spring" => 0,
        "summer" => 1,
        "fall" => 2,
        "winter" => 3,
        _ => 4
    };
}
