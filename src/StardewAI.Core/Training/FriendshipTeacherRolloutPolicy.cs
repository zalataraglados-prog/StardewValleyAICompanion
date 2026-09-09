using System;
using System.Linq;

namespace StardewAI.Core.Training
{
    public enum FriendshipTeacherLabelDisposition
    {
        NotBuilt,
        Ready,
        DayExhausted,
        GoalSatisfied,
        Blocked
    }

    public enum FriendshipTeacherDayTransitionState
    {
        None,
        Requested,
        NativeSaveVerified,
        AuditVerified,
        Failed
    }

    public enum FriendshipTeacherRolloutPhase
    {
        BuildFreshTeacherLabel,
        ExecuteSelectedObjective,
        AdvanceBlockingStoryEvent,
        RecoverBlockingMenu,
        CloseDayAtNativeSaveBoundary,
        AuditNativeDayTransition,
        BoundedRunComplete,
        Complete,
        Blocked
    }

    public sealed class FriendshipTeacherRolloutObservation
    {
        public int CurrentTotalDays { get; set; }

        public int DeadlineTotalDaysExclusive { get; set; }

        public int QualifyingCount { get; set; }

        public int RequiredQualifyingCount { get; set; } = 10;

        public int CompletedObjectivesToday { get; set; }

        public int MaxObjectivesPerDay { get; set; } = 16;

        public int ConsecutiveNoProgressDays { get; set; }

        public int MaxConsecutiveNoProgressDays { get; set; } = 3;

        public int CompletedDayTransitions { get; set; }

        public int MaxDayTransitions { get; set; } = 1;

        public int CompletedStoryEventAdvances { get; set; }

        public int StopAfterStoryEventAdvances { get; set; }

        public bool ActiveMenuOpen { get; set; }

        public bool ActiveStoryEvent { get; set; }

        public string PendingCandidateId { get; set; } = string.Empty;

        public bool PendingObjectiveReceiptVerified { get; set; }

        public FriendshipTeacherLabelDisposition LabelDisposition { get; set; }

        public string LabelCandidateId { get; set; } = string.Empty;

        public FriendshipTeacherDayTransitionState DayTransitionState { get; set; }
    }

    public sealed class FriendshipTeacherRolloutDecision
    {
        public FriendshipTeacherRolloutPhase Phase { get; set; }

        public string CandidateId { get; set; } = string.Empty;

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Reason { get; set; } = string.Empty;
    }

    public sealed class FriendshipTeacherRolloutPolicy
    {
        private static readonly string[] ExactDayExhaustionReasons =
        {
            "current_social_itinerary_no_exact_nonqualifying_target",
            "current_social_itinerary_no_verified_first_visit"
        };

        public FriendshipTeacherRolloutDecision Decide(
            FriendshipTeacherRolloutObservation observation)
        {
            var inputReasons = Validate(observation);
            if (inputReasons.Length > 0)
                return Blocked(inputReasons);

            if (observation.DayTransitionState ==
                FriendshipTeacherDayTransitionState.Failed)
            {
                return Blocked("friendship_teacher_day_transition_failed");
            }
            if (observation.DayTransitionState ==
                FriendshipTeacherDayTransitionState.NativeSaveVerified)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.AuditNativeDayTransition,
                    "native_save_boundary_requires_friendship_audit");
            }
            if (observation.ActiveStoryEvent)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.AdvanceBlockingStoryEvent,
                    "blocking_story_event_requires_native_event_executor");
            }
            if (observation.StopAfterStoryEventAdvances > 0 &&
                observation.CompletedStoryEventAdvances >=
                    observation.StopAfterStoryEventAdvances)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.BoundedRunComplete,
                    "configured_story_event_evidence_target_reached");
            }
            if (observation.QualifyingCount >=
                observation.RequiredQualifyingCount)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.Complete,
                    "friendship_portfolio_goal_satisfied");
            }
            if (observation.CurrentTotalDays >=
                observation.DeadlineTotalDaysExclusive)
            {
                return Blocked("friendship_teacher_deadline_reached");
            }
            if (observation.ConsecutiveNoProgressDays >=
                observation.MaxConsecutiveNoProgressDays)
            {
                return Blocked(
                    "friendship_teacher_no_progress_day_limit_reached");
            }
            if (observation.ActiveMenuOpen)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.RecoverBlockingMenu,
                    "blocking_menu_requires_mechanical_recovery");
            }
            if (observation.CompletedDayTransitions >=
                observation.MaxDayTransitions)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.BoundedRunComplete,
                    "configured_rollout_transition_limit_reached");
            }
            if (!string.IsNullOrWhiteSpace(
                    observation.PendingCandidateId))
            {
                return observation.PendingObjectiveReceiptVerified
                    ? Decision(
                        FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel,
                        "verified_objective_requires_fresh_replan")
                    : Decision(
                        FriendshipTeacherRolloutPhase.ExecuteSelectedObjective,
                        "selected_teacher_objective_pending_native_receipt",
                        observation.PendingCandidateId);
            }

            if (observation.DayTransitionState ==
                FriendshipTeacherDayTransitionState.Requested)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.CloseDayAtNativeSaveBoundary,
                    "current_day_teacher_work_complete");
            }
            if (observation.DayTransitionState ==
                FriendshipTeacherDayTransitionState.AuditVerified)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel,
                    "verified_new_day_requires_fresh_teacher_label");
            }

            if (observation.LabelDisposition ==
                FriendshipTeacherLabelDisposition.GoalSatisfied)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.Complete,
                    "teacher_label_reports_goal_satisfied");
            }
            if (observation.LabelDisposition ==
                    FriendshipTeacherLabelDisposition.DayExhausted ||
                observation.CompletedObjectivesToday >=
                    observation.MaxObjectivesPerDay)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.CloseDayAtNativeSaveBoundary,
                    observation.LabelDisposition ==
                        FriendshipTeacherLabelDisposition.DayExhausted
                            ? "evidence_complete_current_day_candidates_exhausted"
                            : "per_day_objective_safety_limit_reached");
            }
            if (observation.LabelDisposition ==
                FriendshipTeacherLabelDisposition.Blocked)
            {
                return Blocked("friendship_teacher_label_blocked");
            }
            if (observation.LabelDisposition ==
                FriendshipTeacherLabelDisposition.Ready)
            {
                return Decision(
                    FriendshipTeacherRolloutPhase.ExecuteSelectedObjective,
                    "fresh_teacher_candidate_ready",
                    observation.LabelCandidateId);
            }
            return Decision(
                FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel,
                "teacher_label_not_built_for_current_state");
        }

        public FriendshipTeacherLabelDisposition Classify(
            CurrentSocialDayTeacherLabel label)
        {
            if (label is null)
                return FriendshipTeacherLabelDisposition.Blocked;
            if (string.Equals(
                    label.Itinerary.Status,
                    "goal_already_satisfied",
                    StringComparison.Ordinal))
            {
                return FriendshipTeacherLabelDisposition.GoalSatisfied;
            }
            if (string.Equals(label.Status, "ready", StringComparison.Ordinal) &&
                label.TrainingLabelEligible &&
                label.SelectedCandidate is not null &&
                !string.IsNullOrWhiteSpace(
                    label.SelectedCandidate.CandidateId) &&
                label.CompiledQueue is not null &&
                string.Equals(
                    label.CompiledQueue.Status,
                    "pending",
                    StringComparison.Ordinal))
            {
                return FriendshipTeacherLabelDisposition.Ready;
            }
            var reasons = label.BlockingReasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return reasons.Length > 0 && reasons.All(reason =>
                    ExactDayExhaustionReasons.Contains(
                        reason,
                        StringComparer.Ordinal))
                ? FriendshipTeacherLabelDisposition.DayExhausted
                : FriendshipTeacherLabelDisposition.Blocked;
        }

        private static string[] Validate(
            FriendshipTeacherRolloutObservation observation)
        {
            if (observation is null)
                return new[] { "friendship_teacher_observation_required" };
            var reasons = new System.Collections.Generic.List<string>();
            if (observation.CurrentTotalDays < 0 ||
                observation.DeadlineTotalDaysExclusive <= 0)
            {
                reasons.Add("friendship_teacher_day_bounds_invalid");
            }
            if (observation.CompletedStoryEventAdvances < 0 ||
                observation.StopAfterStoryEventAdvances < 0)
            {
                reasons.Add("friendship_teacher_story_event_bounds_invalid");
            }
            if (observation.RequiredQualifyingCount <= 0 ||
                observation.QualifyingCount < 0)
            {
                reasons.Add("friendship_teacher_portfolio_counts_invalid");
            }
            if (observation.CompletedObjectivesToday < 0 ||
                observation.MaxObjectivesPerDay <= 0)
            {
                reasons.Add("friendship_teacher_objective_counts_invalid");
            }
            if (observation.ConsecutiveNoProgressDays < 0 ||
                observation.MaxConsecutiveNoProgressDays <= 0)
            {
                reasons.Add("friendship_teacher_no_progress_limits_invalid");
            }
            if (observation.CompletedDayTransitions < 0 ||
                observation.MaxDayTransitions <= 0)
            {
                reasons.Add("friendship_teacher_transition_limits_invalid");
            }
            if (observation.PendingObjectiveReceiptVerified &&
                string.IsNullOrWhiteSpace(observation.PendingCandidateId))
            {
                reasons.Add(
                    "friendship_teacher_verified_receipt_candidate_missing");
            }
            if (observation.LabelDisposition ==
                    FriendshipTeacherLabelDisposition.Ready &&
                string.IsNullOrWhiteSpace(observation.LabelCandidateId))
            {
                reasons.Add("friendship_teacher_ready_candidate_missing");
            }
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static FriendshipTeacherRolloutDecision Decision(
            FriendshipTeacherRolloutPhase phase,
            string reason,
            string candidateId = "") => new()
        {
            Phase = phase,
            Reason = reason,
            CandidateId = candidateId
        };

        private static FriendshipTeacherRolloutDecision Blocked(
            params string[] reasons) => new()
        {
            Phase = FriendshipTeacherRolloutPhase.Blocked,
            Reason = reasons.FirstOrDefault() ?? string.Empty,
            BlockingReasons = reasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}
