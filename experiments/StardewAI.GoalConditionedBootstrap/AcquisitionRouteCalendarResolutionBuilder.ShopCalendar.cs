using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    private static bool TryBuildShopCalendarProjection(
        IEnumerable<string?> conditions,
        out ShopCalendarProjection projection,
        out string[] errors)
    {
        var predicates = new List<ShopDatePredicate>();
        var dynamicConditions = new List<string>();
        var timeWindows = NativeCalendarConstraintNormalizer.AllDay.ToList();
        var failures = new List<string>();
        foreach (var clause in conditions
                     .Where(condition => !string.IsNullOrWhiteSpace(condition))
                     .SelectMany(condition => condition!.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries)))
        {
            var tokens = clause.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
            if (tokens.Length == 0)
                continue;
            var negated = tokens[0].StartsWith('!');
            var key = negated ? tokens[0][1..] : tokens[0];
            var values = tokens.Skip(1).ToArray();
            if (key == "TIME")
            {
                if (!TryApplyShopTimePredicate(
                        timeWindows, values, negated, out timeWindows))
                    failures.Add("unparsed_native_condition:" + clause);
            }
            else if (key is "SEASON" or "YEAR" or "DAY_OF_WEEK" or
                     "DAY_OF_MONTH")
            {
                if (ShopDatePredicate.TryCreate(key, negated, values,
                        out var predicate))
                    predicates.Add(predicate);
                else
                    failures.Add("unparsed_native_condition:" + clause);
            }
            else
            {
                dynamicConditions.Add(clause);
            }
        }

        projection = new ShopCalendarProjection(
            predicates.ToArray(),
            timeWindows.ToArray(),
            dynamicConditions.Distinct(StringComparer.Ordinal).ToArray());
        errors = failures.ToArray();
        return errors.Length == 0 && timeWindows.Count > 0;
    }

    private static bool TryApplyShopTimePredicate(
        IReadOnlyList<MasterAnglerTimeWindow> existing,
        string[] values,
        bool negated,
        out List<MasterAnglerTimeWindow> result)
    {
        result = new List<MasterAnglerTimeWindow>();
        if (values.Length is not (1 or 2) ||
            !int.TryParse(values[0], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var start) ||
            (values.Length == 2 &&
             !int.TryParse(values[1], NumberStyles.Integer,
                 CultureInfo.InvariantCulture, out _)))
        {
            return false;
        }
        var end = values.Length == 2
            ? NextGameTime(int.Parse(values[1], CultureInfo.InvariantCulture))
            : int.MaxValue;
        foreach (var window in existing)
        {
            if (!negated)
            {
                AddTimeIntersection(result, window,
                    new MasterAnglerTimeWindow { StartTime = start, EndTime = end });
            }
            else
            {
                AddTimeIntersection(result, window,
                    new MasterAnglerTimeWindow { StartTime = int.MinValue, EndTime = start });
                AddTimeIntersection(result, window,
                    new MasterAnglerTimeWindow { StartTime = end, EndTime = int.MaxValue });
            }
        }
        return true;
    }

    private static int NextGameTime(int time)
    {
        var hour = time / 100;
        var minute = time % 100 + 10;
        if (minute >= 60)
        {
            hour += minute / 60;
            minute %= 60;
        }
        return hour * 100 + minute;
    }

    private static void AddTimeIntersection(
        ICollection<MasterAnglerTimeWindow> result,
        MasterAnglerTimeWindow left,
        MasterAnglerTimeWindow right)
    {
        var start = Math.Max(left.StartTime, right.StartTime);
        var end = Math.Min(left.EndTime, right.EndTime);
        if (start < end)
        {
            result.Add(new MasterAnglerTimeWindow
            {
                StartTime = start,
                EndTime = end
            });
        }
    }

    private static AuthoritativeCalendarSourceWindow[] ExpandShopWindows(
        string shopId,
        int rowIndex,
        string ruleId,
        ShopCalendarProjection calendar,
        int deadlineTotalDayExclusive,
        bool stochastic)
    {
        var dates = Enumerable.Range(0, deadlineTotalDayExclusive)
            .Select(ShopDate.FromTotalDay)
            .Where(date => calendar.Predicates.All(predicate => predicate.Matches(date)))
            .ToArray();
        var windows = new List<AuthoritativeCalendarSourceWindow>();
        for (var index = 0; index < dates.Length; index++)
        {
            var first = dates[index];
            var last = first;
            while (index + 1 < dates.Length &&
                   dates[index + 1].TotalDay == last.TotalDay + 1 &&
                   dates[index + 1].Year == first.Year &&
                   dates[index + 1].Season == first.Season)
            {
                last = dates[++index];
            }
            windows.Add(new AuthoritativeCalendarSourceWindow
            {
                SourceKind = "shop_stock_rule",
                SourceKey = shopId + ":" + rowIndex,
                RuleId = ruleId,
                Year = first.Year,
                Season = first.Season,
                FirstTotalDay = first.TotalDay,
                LastTotalDay = last.TotalDay,
                TimeWindows = calendar.TimeWindows,
                WeatherModes = NativeCalendarConstraintNormalizer.AllWeatherModes,
                DynamicConditions = calendar.DynamicConditions,
                RequiresLocationAccessEvidence = true,
                RequiresExistingLiveCandidateMatch = true,
                StochasticOutcome = stochastic,
                RequiredLocationCapability = "shop_access"
            });
        }
        return windows.ToArray();
    }

    private sealed record ShopCalendarProjection(
        ShopDatePredicate[] Predicates,
        MasterAnglerTimeWindow[] TimeWindows,
        string[] DynamicConditions);

    private sealed record ShopDate(
        int TotalDay,
        int Year,
        string Season,
        int DayOfMonth,
        string DayOfWeek)
    {
        public static ShopDate FromTotalDay(int totalDay)
        {
            var dayInYear = totalDay % 112;
            var dayOfMonth = dayInYear % 28 + 1;
            var dayNames = new[]
            {
                "Sunday", "Monday", "Tuesday", "Wednesday",
                "Thursday", "Friday", "Saturday"
            };
            return new ShopDate(
                totalDay,
                totalDay / 112 + 1,
                NativeCalendarConstraintNormalizer.AllSeasons[dayInYear / 28],
                dayOfMonth,
                dayNames[dayOfMonth % 7]);
        }
    }

    private sealed record ShopDatePredicate(
        string Key,
        bool Negated,
        string[] Values)
    {
        public bool Matches(ShopDate date)
        {
            var matched = Key switch
            {
                "SEASON" => Values.Contains(date.Season,
                    StringComparer.OrdinalIgnoreCase),
                "DAY_OF_WEEK" => Values.Contains(date.DayOfWeek,
                    StringComparer.OrdinalIgnoreCase),
                "DAY_OF_MONTH" => Values.Any(value =>
                    value.Equals("even", StringComparison.OrdinalIgnoreCase)
                        ? date.DayOfMonth % 2 == 0
                        : value.Equals("odd", StringComparison.OrdinalIgnoreCase)
                            ? date.DayOfMonth % 2 == 1
                            : int.Parse(value, CultureInfo.InvariantCulture) ==
                              date.DayOfMonth),
                "YEAR" => date.Year >= int.Parse(Values[0],
                    CultureInfo.InvariantCulture) &&
                    (Values.Length == 1 || date.Year <= int.Parse(
                        Values[1], CultureInfo.InvariantCulture)),
                _ => false
            };
            return Negated ? !matched : matched;
        }

        public static bool TryCreate(
            string key,
            bool negated,
            string[] values,
            out ShopDatePredicate predicate)
        {
            predicate = new ShopDatePredicate(key, negated, values);
            return key switch
            {
                "SEASON" => values.Length > 0 && values.All(value =>
                    NativeCalendarConstraintNormalizer.AllSeasons.Contains(
                        value, StringComparer.OrdinalIgnoreCase)),
                "DAY_OF_WEEK" => values.Length > 0 && values.All(value =>
                    new[]
                    {
                        "Monday", "Tuesday", "Wednesday", "Thursday",
                        "Friday", "Saturday", "Sunday"
                    }.Contains(value, StringComparer.OrdinalIgnoreCase)),
                "DAY_OF_MONTH" => values.Length > 0 && values.All(value =>
                    value.Equals("even", StringComparison.OrdinalIgnoreCase) ||
                    value.Equals("odd", StringComparison.OrdinalIgnoreCase) ||
                    int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var day) &&
                    day is >= 1 and <= 28),
                "YEAR" => values.Length is 1 or 2 && values.All(value =>
                    int.TryParse(value, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var year) && year >= 1) &&
                    (values.Length == 1 || int.Parse(values[0],
                        CultureInfo.InvariantCulture) <= int.Parse(values[1],
                        CultureInfo.InvariantCulture)),
                _ => false
            };
        }
    }
}
