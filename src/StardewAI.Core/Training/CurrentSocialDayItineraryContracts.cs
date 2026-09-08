using System;

namespace StardewAI.Core.Training
{
    public sealed class CurrentSocialDayItineraryStep
    {
        public int Sequence { get; set; }

        public string OpportunityId { get; set; } = string.Empty;

        public string OptionId { get; set; } = "social.talk_npc";

        public string NpcName { get; set; } = string.Empty;

        public int FriendshipPointsBefore { get; set; }

        public int ExpectedFriendshipDelta { get; set; }

        public int ExpectedFriendshipPointsAfter { get; set; }

        public int DeficitBefore { get; set; }

        public int DeficitAfter { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int NpcTileX { get; set; }

        public int NpcTileY { get; set; }

        public int StandTileX { get; set; }

        public int StandTileY { get; set; }

        public int ArrivalTime { get; set; }

        public int InteractionStartTime { get; set; }

        public int InteractionEndTime { get; set; }

        public int RouteConnectorCount { get; set; }

        public int RouteWaitGameMinutes { get; set; }

        public FutureRouteTravelTimingEvidenceKind RouteTimingEvidenceKind { get; set; }

        public string CompileChain { get; set; } =
            "social_candidate_builder_to_one_connector_daily_plan_to_existing_native_executor";

        public string ReplanPolicy { get; set; } =
            "refresh_snapshot_after_each_connector_and_completed_interaction";
    }

    public sealed class CurrentSocialDayItineraryPlan
    {
        public string SchemaVersion { get; set; } =
            "stardewai.current_social_day_itinerary.v2";

        public string Status { get; set; } = "blocked";

        public bool TrainingLabelEligible { get; set; }

        public bool PortfolioCoverageComplete { get; set; }

        public string PlannerPolicyId { get; set; } =
            "grandpa_friendship_baseline.nearest_threshold_then_earliest_finish.v2";

        public int TotalDays { get; set; }

        public int PlanningStartTime { get; set; }

        public int QualifyingCountBefore { get; set; }

        public int RequiredQualifyingCount { get; set; } = 10;

        public int PortfolioSlotsNeeded { get; set; }

        public int PortfolioSlotsPlanned { get; set; }

        public int EligibleTargetCount { get; set; }

        public int PlannedVisitCount { get; set; }

        public int? CompletionTime { get; set; }

        public FutureRouteTravelTimingEvidenceKind RouteTimingEvidenceKind { get; set; }

        public string[] TargetPortfolioNpcNames { get; set; } =
            Array.Empty<string>();

        public string[] OmittedTargetNpcNames { get; set; } =
            Array.Empty<string>();

        public CurrentSocialDayItineraryStep[] Steps { get; set; } =
            Array.Empty<CurrentSocialDayItineraryStep>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string[] Limitations { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "current_state_current_date_verified_baseline_order_not_global_optimum_or_multi_day_deadline_proof";
    }
}
