using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class AcquisitionCalendarConditionEvaluator
{
    private static readonly HashSet<string> PredicateNames = new(
        StringComparer.Ordinal)
    {
        "DAYS_PLAYED",
        "IS_FESTIVAL_DAY",
        "IS_PASSIVE_FESTIVAL_OPEN"
    };

    private readonly AcquisitionCalendarSnapshotState state;

    public AcquisitionCalendarConditionEvaluator(
        AcquisitionCalendarSnapshotState state)
    {
        this.state = state;
    }

    public AcquisitionCalendarConditionEvaluation Evaluate(string condition)
    {
        var tokens = Tokens(condition);
        if (tokens.Length == 0)
        {
            return Blocked(
                condition,
                string.Empty,
                false,
                "empty_calendar_condition");
        }

        var negated = tokens[0].StartsWith('!');
        var name = negated ? tokens[0][1..] : tokens[0];
        if (!PredicateNames.Contains(name))
        {
            return Blocked(
                condition,
                name,
                negated,
                "condition_not_owned_by_calendar_axis");
        }
        if (!state.Available)
            return Blocked(condition, name, negated, state.BlockingReason);

        return name switch
        {
            "DAYS_PLAYED" => EvaluateDaysPlayed(condition, tokens, negated),
            "IS_FESTIVAL_DAY" => EvaluateFestivalDay(
                condition, tokens, negated),
            "IS_PASSIVE_FESTIVAL_OPEN" => EvaluatePassiveFestivalOpen(
                condition, tokens, negated),
            _ => Blocked(
                condition, name, negated, "unsupported_calendar_predicate")
        };
    }

    private AcquisitionCalendarConditionEvaluation EvaluateDaysPlayed(
        string condition,
        string[] tokens,
        bool negated)
    {
        if (tokens.Length < 2 ||
            !int.TryParse(tokens[1], NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var minimum) ||
            (tokens.Length > 2 && !int.TryParse(tokens[2],
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out _)))
        {
            return Blocked(condition, "DAYS_PLAYED", negated,
                "invalid_days_played_arguments");
        }
        var maximum = tokens.Length > 2
            ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
            : int.MaxValue;
        var daysPlayed = (long)state.DaysPlayed;
        return Resolved(
            condition,
            "DAYS_PLAYED",
            negated,
            daysPlayed >= minimum && daysPlayed <= maximum,
            new[]
            {
                "world_progress.game_state_query_calendar_state.days_played"
            });
    }

    private AcquisitionCalendarConditionEvaluation EvaluateFestivalDay(
        string condition,
        string[] tokens,
        bool negated)
    {
        var context = tokens.Length > 1 ? tokens[1] : "any";
        if (tokens.Length > 2 && !int.TryParse(tokens[2],
                NumberStyles.Integer, CultureInfo.InvariantCulture,
                out _))
        {
            return Blocked(condition, "IS_FESTIVAL_DAY", negated,
                "invalid_is_festival_day_arguments");
        }
        if (!context.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            return Blocked(condition, "IS_FESTIVAL_DAY", negated,
                "festival_location_context_unavailable:" + context);
        }

        var offset = tokens.Length > 2
            ? int.Parse(tokens[2], CultureInfo.InvariantCulture)
            : 0;
        var dayInCycle = unchecked(state.CurrentTotalDay + offset) % 112;
        var seasonIndex = dayInCycle / 28;
        var dayOfMonth = dayInCycle % 28 + 1;
        var season = seasonIndex switch
        {
            0 => "spring",
            1 => "summer",
            2 => "fall",
            3 => "winter",
            _ => seasonIndex.ToString(CultureInfo.InvariantCulture)
        };
        var dateKey = season + dayOfMonth.ToString(
            CultureInfo.InvariantCulture);
        return Resolved(
            condition,
            "IS_FESTIVAL_DAY",
            negated,
            state.FestivalDateKeys.Contains(dateKey),
            new[]
            {
                "world_progress.game_state_query_calendar_state." +
                "current_total_day",
                "world_progress.game_state_query_calendar_state." +
                "festival_date_keys[" + dateKey + "]"
            });
    }

    private AcquisitionCalendarConditionEvaluation EvaluatePassiveFestivalOpen(
        string condition,
        string[] tokens,
        bool negated)
    {
        if (tokens.Length < 2)
        {
            return Blocked(condition, "IS_PASSIVE_FESTIVAL_OPEN", negated,
                "invalid_is_passive_festival_open_arguments");
        }
        var festivalId = tokens[1];
        var native = state.ActivePassiveFestivalIds.Contains(festivalId) &&
            state.PassiveFestivalStartTimes.TryGetValue(
                festivalId, out var startTime) &&
            state.TimeOfDay >= startTime;
        return Resolved(
            condition,
            "IS_PASSIVE_FESTIVAL_OPEN",
            negated,
            native,
            new[]
            {
                "world_progress.game_state_query_calendar_state." +
                "time_of_day",
                "world_progress.game_state_query_calendar_state." +
                "active_passive_festival_ids[" + festivalId + "]",
                "world_progress.game_state_query_calendar_state." +
                "passive_festivals[" + festivalId + "].start_time"
            });
    }

    private static AcquisitionCalendarConditionEvaluation Resolved(
        string condition,
        string name,
        bool negated,
        bool native,
        string[] evidencePaths)
    {
        var matches = negated ? !native : native;
        return new AcquisitionCalendarConditionEvaluation(
            condition,
            name,
            negated,
            matches ? "resolved_match" : "resolved_miss",
            native,
            matches,
            evidencePaths,
            null);
    }

    private static AcquisitionCalendarConditionEvaluation Blocked(
        string condition,
        string name,
        bool negated,
        string reason) => new(
            condition,
            name,
            negated,
            "blocked",
            null,
            null,
            Array.Empty<string>(),
            reason);

    private static string[] Tokens(string condition) =>
        (condition ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
}
