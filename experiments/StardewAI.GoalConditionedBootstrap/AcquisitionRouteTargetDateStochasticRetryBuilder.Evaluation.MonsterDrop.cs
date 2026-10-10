namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateStochasticRetryBuilder
{
    private static AcquisitionRouteTargetDateStochasticRetry
        EvaluateMonsterDrop(
            AcquisitionRouteTargetDateProcessing route,
            AcquisitionRouteCalendarResolution staticRoute,
            AcquisitionCurrentRouteCandidateIndex candidates)
    {
        if (!candidates.TryFind(
                staticRoute.RouteKind,
                staticRoute.SourceId,
                staticRoute.QualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_stochastic_probability_evidence",
                false,
                null,
                "source_bound_guaranteed_output_evidence_missing",
                null,
                null,
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                blockingReasons);
        }

        var guaranteed = matches.FirstOrDefault(candidate =>
            candidate.SourceMatchStatus == "guaranteed_monster_drop" &&
            candidate.TargetDropChancePreview == 1d &&
            candidate.TargetDropProbabilityStatus ==
                "guaranteed_from_live_projection");
        if (guaranteed is null)
        {
            return Result(
                route,
                staticRoute.UncertaintyMode,
                "blocked_stochastic_probability_evidence",
                false,
                null,
                "source_bound_guaranteed_output_evidence_missing",
                null,
                null,
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[]
                {
                    matches.Length == 0
                        ? "matching_monster_drop_candidate_not_present"
                        : "matching_monster_drop_not_guaranteed_by_live_projection"
                });
        }

        return Result(
            route,
            staticRoute.UncertaintyMode,
            "resolved_source_bound_guaranteed_output",
            true,
            true,
            "source_bound_guaranteed_output",
            1d,
            1,
            false,
            true,
            new[]
            {
                "state.mining.monsters.value[].authoritative_route_sources",
                "candidate.parameters[source_match_status]",
                "candidate.parameters[target_drop_chance_preview]",
                "candidate.parameters[target_drop_probability_status]",
                "candidate:mining.reach_depth"
            },
            Array.Empty<string>(),
            Array.Empty<string>(),
            baselineAttemptCount: 1);
    }
}
