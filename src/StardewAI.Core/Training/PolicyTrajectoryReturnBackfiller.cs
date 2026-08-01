using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

internal sealed class PolicyTrajectoryReturnBackfiller
{
    public void Backfill(
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> trajectories,
        IReadOnlyList<PolicyHorizonObservationEnvelope> observations)
    {
        var dayClosures = ObservationKeys(observations, PolicyHorizonKinds.Day, DayKey);
        var seasonClosures = ObservationKeys(observations, PolicyHorizonKinds.Season, SeasonKey);
        var yearClosures = ObservationKeys(observations, PolicyHorizonKinds.Year, YearKey);

        foreach (var saveGroup in trajectories.GroupBy(row => row.Context.SaveId, StringComparer.Ordinal))
        {
            var rows = saveGroup.OrderBy(TrajectoryOrder).ToArray();
            var grandpaObservation = observations
                .Where(observation =>
                    observation.Closed &&
                    string.Equals(observation.SaveId, saveGroup.Key, StringComparison.Ordinal) &&
                    string.Equals(observation.Horizon, PolicyHorizonKinds.Grandpa21, StringComparison.Ordinal) &&
                    observation.GrandpaScore.HasValue)
                .OrderBy(ObservationOrder)
                .FirstOrDefault();

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var laterRows = rows.Skip(index + 1).ToArray();
                var dayClosed = laterRows.Any(later =>
                    PolicyTrajectoryDatasetValidator.DateOrdinal(later.Context) >
                    PolicyTrajectoryDatasetValidator.DateOrdinal(row.Context)) ||
                    dayClosures.Contains(DayKey(row.Context));
                var seasonClosed = laterRows.Any(later =>
                    later.Context.Year > row.Context.Year ||
                    later.Context.Year == row.Context.Year &&
                    PolicyTrajectoryDatasetValidator.SeasonOrdinal(later.Context.Season) >
                    PolicyTrajectoryDatasetValidator.SeasonOrdinal(row.Context.Season)) ||
                    seasonClosures.Contains(SeasonKey(row.Context));
                var yearClosed = laterRows.Any(later => later.Context.Year > row.Context.Year) ||
                    yearClosures.Contains(YearKey(row.Context));

                row.Returns.Day = dayClosed
                    ? SumFrom(rows, index, candidate => SameDay(candidate.Context, row.Context))
                    : null;
                row.Returns.DayStatus = dayClosed ? "observed_closed" : "pending";
                row.Returns.Season = seasonClosed
                    ? SumFrom(rows, index, candidate => SameSeason(candidate.Context, row.Context))
                    : null;
                row.Returns.SeasonStatus = seasonClosed ? "observed_closed" : "pending";
                row.Returns.Year = yearClosed
                    ? SumFrom(rows, index, candidate => candidate.Context.Year == row.Context.Year)
                    : null;
                row.Returns.YearStatus = yearClosed ? "observed_closed" : "pending";

                var grandpaObservedAfterDecision = grandpaObservation is not null &&
                    AtOrBefore(row.Context, grandpaObservation);
                row.Returns.Grandpa21 = grandpaObservedAfterDecision
                    ? grandpaObservation!.GrandpaScore!.Value >= 21 ? 1d : 0d
                    : null;
                row.Returns.Grandpa21Status = grandpaObservedAfterDecision
                    ? "terminal_evaluation_observed"
                    : "pending";

                var completeCount = new[]
                {
                    row.Returns.Day.HasValue,
                    row.Returns.Season.HasValue,
                    row.Returns.Year.HasValue,
                    row.Returns.Grandpa21.HasValue
                }.Count(value => value);
                row.Returns.LongHorizonStatus = completeCount switch
                {
                    0 => "pending",
                    4 => "complete",
                    _ => "partial_observed"
                };
            }
        }
    }

    private static HashSet<string> ObservationKeys(
        IReadOnlyList<PolicyHorizonObservationEnvelope> observations,
        string horizon,
        Func<PolicyHorizonObservationEnvelope, string> keySelector) =>
        observations
            .Where(observation => observation.Closed && string.Equals(observation.Horizon, horizon, StringComparison.Ordinal))
            .Select(keySelector)
            .ToHashSet(StringComparer.Ordinal);

    private static double SumFrom(
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows,
        int start,
        Func<PolicyDecisionTrajectoryEnvelope, bool> inHorizon)
    {
        var sum = 0d;
        for (var index = start; index < rows.Count && inHorizon(rows[index]); index++)
            sum += rows[index].Returns.Immediate;
        return sum;
    }

    private static bool SameDay(PolicyTrajectoryContext left, PolicyTrajectoryContext right) =>
        SameSeason(left, right) && left.Day == right.Day;

    private static bool SameSeason(PolicyTrajectoryContext left, PolicyTrajectoryContext right) =>
        left.Year == right.Year && string.Equals(left.Season, right.Season, StringComparison.Ordinal);

    private static string DayKey(PolicyTrajectoryContext context) =>
        context.SaveId + ":" + context.Year + ":" + context.Season + ":" + context.Day;

    private static string DayKey(PolicyHorizonObservationEnvelope observation) =>
        observation.SaveId + ":" + observation.Year + ":" + observation.Season + ":" + observation.Day;

    private static string SeasonKey(PolicyTrajectoryContext context) =>
        context.SaveId + ":" + context.Year + ":" + context.Season;

    private static string SeasonKey(PolicyHorizonObservationEnvelope observation) =>
        observation.SaveId + ":" + observation.Year + ":" + observation.Season;

    private static string YearKey(PolicyTrajectoryContext context) => context.SaveId + ":" + context.Year;

    private static string YearKey(PolicyHorizonObservationEnvelope observation) => observation.SaveId + ":" + observation.Year;

    private static string TrajectoryOrder(PolicyDecisionTrajectoryEnvelope row) =>
        PolicyTrajectoryDatasetValidator.DateOrdinal(row.Context).ToString("D8") + ":" +
        row.Context.Time.ToString("D4") + ":" + row.TrajectoryId;

    private static string ObservationOrder(PolicyHorizonObservationEnvelope observation) =>
        PolicyTrajectoryDatasetValidator.DateOrdinal(observation).ToString("D8") + ":" +
        observation.Time.ToString("D4") + ":" + observation.ObservationId;

    private static bool AtOrBefore(
        PolicyTrajectoryContext context,
        PolicyHorizonObservationEnvelope observation)
    {
        var contextDate = PolicyTrajectoryDatasetValidator.DateOrdinal(context);
        var observationDate = PolicyTrajectoryDatasetValidator.DateOrdinal(observation);
        return contextDate < observationDate ||
            contextDate == observationDate && context.Time <= observation.Time;
    }
}
