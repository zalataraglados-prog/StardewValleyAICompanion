using System;

namespace StardewAI.Core.Training
{
    public sealed class CurrentSocialContactOpportunity
    {
        public string OpportunityId { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string InteractionKind { get; set; } = string.Empty;

        public bool ExecutionReady { get; set; }

        public string ExecutionReadiness { get; set; } = string.Empty;

        public int ScheduleEntryOrdinal { get; set; }

        public string SelectedScheduleKey { get; set; } = string.Empty;

        public string LocationName { get; set; } = string.Empty;

        public int NpcTileX { get; set; }

        public int NpcTileY { get; set; }

        public int StandTileX { get; set; }

        public int StandTileY { get; set; }

        public int EarliestInteractionTime { get; set; }

        public int WindowEndTimeExclusive { get; set; }

        public int NpcPresentFromTime { get; set; }

        public int NpcPresentUntilTimeExclusive { get; set; }

        public int InteractionEligibleFromTime { get; set; }

        public int InteractionEligibleUntilTimeExclusive { get; set; }

        public int RouteConnectorCount { get; set; }

        public int RouteWaitGameMinutes { get; set; }

        public FutureRouteTravelTimingEvidenceKind RouteTimingEvidenceKind { get; set; }

        public string TimingEvidenceId { get; set; } = string.Empty;

        public string TimingScope { get; set; } =
            "movement_and_route_connector_travel_upper_bound_only";

        public string[] TimingPreconditions { get; set; } =
            Array.Empty<string>();

        public int? GiftSlotIndex { get; set; }

        public string GiftItemId { get; set; } = string.Empty;

        public string GiftQualifiedItemId { get; set; } = string.Empty;

        public int? GiftQuality { get; set; }

        public int? GiftStackBefore { get; set; }

        public string GiftTaste { get; set; } = string.Empty;

        public int? ExpectedFriendshipDelta { get; set; }
    }

    public sealed class CurrentSocialNpcCoverage
    {
        public string NpcName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string ScheduleProjectionStatus { get; set; } = string.Empty;

        public int StableWindowCount { get; set; }

        public int TalkOpportunityCount { get; set; }

        public int GiftSlotOpportunityCount { get; set; }

        public int ExcludedWindowCount { get; set; }

        public string[] ExclusionReasons { get; set; } = Array.Empty<string>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class CurrentSocialDynamicTrackingDirective
    {
        public string NpcName { get; set; } = string.Empty;

        public string ObservedLocationName { get; set; } = string.Empty;

        public int ObservedTileX { get; set; }

        public int ObservedTileY { get; set; }

        public int ObservedAtTime { get; set; }

        public string[] CandidateFamilies { get; set; } = Array.Empty<string>();

        public string TargetBindingMode { get; set; } =
            "live_npc_identity_rebind_each_snapshot";

        public string ReplanPolicy { get; set; } =
            "refresh_after_each_route_connector_and_before_interaction";

        public string EligibilityPolicy { get; set; } =
            "recheck_native_social_queries_and_daily_limits_on_each_snapshot";

        public string EvidenceKind { get; set; } = string.Empty;
    }

    public sealed class CurrentSocialDynamicTrackingIntent
    {
        public string IntentId { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string OptionId { get; set; } = string.Empty;

        public string CandidateId { get; set; } = string.Empty;

        public int? GiftSlotIndex { get; set; }

        public string GiftQualifiedItemId { get; set; } = string.Empty;

        public string TargetBindingMode { get; set; } = string.Empty;

        public string ReplanPolicy { get; set; } = string.Empty;

        public string CompileChain { get; set; } =
            "social_candidate_builder_to_daily_plan_compiler_to_existing_native_executor";
    }

    public sealed class CurrentSocialContactFrontier
    {
        public string SchemaVersion { get; set; } =
            "stardewai.current_social_contact_frontier.v3";

        public string Status { get; set; } = "blocked";

        public bool RankingAdmissionReady { get; set; }

        public int TotalDays { get; set; }

        public int GameTime { get; set; }

        public int CatalogRowCount { get; set; }

        public int UniqueNpcNameCount { get; set; }

        public string[] DuplicateNpcNames { get; set; } = Array.Empty<string>();

        public int ExactScheduleProjectionCount { get; set; }

        public int FullyResolvedNpcCount { get; set; }

        public int BlockedNpcCount { get; set; }

        public int TalkOpportunityCount { get; set; }

        public int GiftSlotOpportunityCount { get; set; }

        public int DynamicTrackingDirectiveCount { get; set; }

        public int DynamicTrackingIntentCount { get; set; }

        public string TimingEvidenceId { get; set; } = string.Empty;

        public CurrentSocialContactOpportunity[] Opportunities { get; set; } =
            Array.Empty<CurrentSocialContactOpportunity>();

        public CurrentSocialDynamicTrackingDirective[] DynamicTrackingDirectives { get; set; } =
            Array.Empty<CurrentSocialDynamicTrackingDirective>();

        public CurrentSocialDynamicTrackingIntent[] DynamicTrackingIntents { get; set; } =
            Array.Empty<CurrentSocialDynamicTrackingIntent>();

        public CurrentSocialNpcCoverage[] NpcCoverage { get; set; } =
            Array.Empty<CurrentSocialNpcCoverage>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string[] RankingAdmissionBlockingReasons { get; set; } =
            Array.Empty<string>();

        public string Scope { get; set; } =
            "current_snapshot_native_loaded_schedule_contact_opportunities";
    }
}
