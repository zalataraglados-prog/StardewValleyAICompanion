namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteCalendarResolutionBuilder
{
    internal static CalendarSourceResolution
        ResolveCommunityCenterRewardWindow(
            string qualifiedItemId,
            AcquisitionRequirementRouteLowering route,
            AcquisitionCurrentRouteCandidateIndex? candidates,
            int deadlineTotalDayExclusive)
    {
        const string sourcePrefix = "bundle:";
        const string sourceSuffix = ":reward";
        if (route.SourceAsset != "Data/Bundles" ||
            !route.SourceId.StartsWith(sourcePrefix, StringComparison.Ordinal) ||
            !route.SourceId.EndsWith(sourceSuffix, StringComparison.Ordinal))
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_community_center_reward_source_invalid",
                "community_center_reward_authoritative_source_identity_invalid");
        }

        var bundleDataKey = route.SourceId[
            sourcePrefix.Length..^sourceSuffix.Length];
        if (string.IsNullOrWhiteSpace(bundleDataKey) ||
            route.SourcePath != "payload." + bundleDataKey + "[reward]")
        {
            return BlockLiveSourceCalendar(
                "blocked_authoritative_community_center_reward_source_invalid",
                "community_center_reward_authoritative_source_path_mismatch");
        }
        if (candidates is null)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_community_center_reward_candidate_required",
                "current_community_center_reward_candidate_evidence_required");
        }
        if (!candidates.TryFind(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var matches,
                out var blockingReasons))
        {
            return new CalendarSourceResolution(
                "blocked_current_community_center_reward_candidate_evidence",
                string.Empty,
                Array.Empty<AuthoritativeCalendarSourceWindow>(),
                blockingReasons);
        }
        if (matches.Length != 1)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_community_center_reward_candidate_match",
                matches.Length == 0
                    ? "matching_current_community_center_reward_candidate_not_present"
                    : "matching_current_community_center_reward_candidate_ambiguous");
        }

        var candidate = matches[0];
        if (candidate.OptionId != "community_center.donate_bundle_items" ||
            candidate.LocationId != "CommunityCenter" ||
            !candidate.TargetTileX.HasValue ||
            !candidate.TargetTileY.HasValue ||
            !candidate.StandTileX.HasValue ||
            !candidate.StandTileY.HasValue ||
            candidate.EstimatedTicks <= 0 ||
            candidate.EnergyCost != 0)
        {
            return BlockLiveSourceCalendar(
                "blocked_current_community_center_reward_candidate_invalid",
                "current_community_center_reward_candidate_typed_projection_invalid");
        }

        Require(deadlineTotalDayExclusive > 0,
            "Community Center reward calendar deadline must be positive.");
        return new CalendarSourceResolution(
            ResolvedStatus,
            "current_native_community_center_reward_candidate_calendar",
            new[]
            {
                new AuthoritativeCalendarSourceWindow
                {
                    SourceKind = "community_center_bundle_reward",
                    SourceKey = route.SourceId,
                    LocationId = candidate.LocationId,
                    RuleId = bundleDataKey,
                    FirstTotalDay = 0,
                    LastTotalDay = deadlineTotalDayExclusive - 1,
                    TimeWindows = NativeCalendarConstraintNormalizer.AllDay,
                    WeatherModes =
                        NativeCalendarConstraintNormalizer.AllWeatherModes,
                    RequiresLocationAccessEvidence = true,
                    RequiresExistingLiveCandidateMatch = true,
                    StochasticOutcome = false
                }
            },
            Array.Empty<string>());
    }
}
