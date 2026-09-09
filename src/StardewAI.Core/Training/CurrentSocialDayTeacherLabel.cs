using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed class CurrentSocialDayTeacherLabel
    {
        public string SchemaVersion { get; set; } =
            "stardewai.current_social_day_teacher_label.v2";

        public string Status { get; set; } = "blocked";

        public bool TrainingLabelEligible { get; set; }

        public string SourceStateHash { get; set; } = string.Empty;

        public string DirectionId { get; set; } = "raise_friendships";

        public CurrentSocialDayItineraryPlan Itinerary { get; set; } = new();

        public PolicyEventCandidatePrediction? SelectedCandidate { get; set; }

        public GrandpaDirectionBindingResult DirectionBinding { get; set; } =
            new();

        public SmallModelPlanEnvelope? CompiledPlan { get; set; }

        public ActionQueueEnvelope? CompiledQueue { get; set; }

        public string[] DeferredVisitIds { get; set; } = Array.Empty<string>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "one_current_state_teacher_label_with_verified_ordered_itinerary_context";
    }

    public sealed class CurrentSocialDayTeacherLabelBuilder
    {
        public CurrentSocialDayTeacherLabel Build(
            JsonElement snapshot,
            string timingCalibrationArtifactJson)
        {
            var itinerary = new CurrentSocialDayItineraryPlanner().Plan(
                snapshot,
                timingCalibrationArtifactJson);
            if (!itinerary.TrainingLabelEligible ||
                itinerary.Steps.Length == 0)
            {
                return Blocked(
                    itinerary,
                    itinerary.BlockingReasons
                        .Concat(itinerary.Limitations)
                        .DefaultIfEmpty(
                            "current_social_itinerary_not_label_eligible")
                        .ToArray());
            }
            if (!JsonSnapshotEnvelopeAdapter.TryCreate(
                    snapshot,
                    out var envelope) ||
                string.IsNullOrWhiteSpace(envelope.StateHash))
            {
                return Blocked(
                    itinerary,
                    "current_social_teacher_snapshot_envelope_invalid");
            }

            var first = itinerary.Steps[0];
            var availability = new CandidateOptionAvailabilityEvaluator()
                .Evaluate(envelope, new[] { first.OptionId });
            var ranked = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "goal.grandpa_21");
            var matches = ranked.Where(candidate =>
                    candidate.Available &&
                    candidate.TimelineStatus == "ready_now" &&
                    string.Equals(
                        ReadParameter(candidate.Parameters, "npc_name") ??
                            ReadParameter(
                                candidate.Parameters,
                                "continuation.npc_name"),
                        first.NpcName,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                return Blocked(
                    itinerary,
                    matches.Length == 0
                        ? "current_social_teacher_first_ranked_candidate_missing"
                        : "current_social_teacher_first_ranked_candidate_ambiguous");
            }

            var binding = new GrandpaDirectionDailyCandidateBinding().Bind(
                new GrandpaDirectionBindingRequest
                {
                    StateHash = envelope.StateHash,
                    DirectionId = "raise_friendships",
                    RankedCandidates = matches
                },
                envelope);
            if (binding.BindingStatus != "ready" ||
                binding.BoundCandidates.Length != 1)
            {
                return new CurrentSocialDayTeacherLabel
                {
                    SourceStateHash = envelope.StateHash,
                    Itinerary = itinerary,
                    SelectedCandidate = matches[0],
                    DirectionBinding = binding,
                    DeferredVisitIds = itinerary.Steps
                        .Skip(1)
                        .Select(value => value.OpportunityId)
                        .ToArray(),
                    BlockingReasons = binding.BlockReasons
                        .DefaultIfEmpty(
                            "current_social_teacher_direction_binding_failed")
                        .ToArray()
                };
            }

            var boundCandidate = binding.BoundCandidates[0];
            var compiledPlan = new DailyPlanCompiler().Compile(
                new[] { boundCandidate },
                envelope.StateHash,
                goalId: "goal.grandpa_21",
                maxCandidates: 1);
            var compiledQueue = new ActionQueueCompiler().Compile(
                compiledPlan,
                envelope);
            var compilationReasons = compiledPlan.CandidateAudit
                .Where(audit => audit.Decision != "accepted")
                .SelectMany(audit => audit.Reasons)
                .Concat(compiledQueue.CompilerDiagnostics)
                .Concat(compiledQueue.Items.SelectMany(item =>
                    item.BlockingReasons.Concat(item.MissingStateFactors)))
                .Where(reason => !string.IsNullOrWhiteSpace(reason))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (compiledPlan.Steps.Length == 0 ||
                compiledQueue.Status != "pending" ||
                compiledQueue.Items.Length == 0 ||
                compiledQueue.Items.Any(item => item.Status != "pending") ||
                compilationReasons.Length > 0)
            {
                return new CurrentSocialDayTeacherLabel
                {
                    SourceStateHash = envelope.StateHash,
                    Itinerary = itinerary,
                    SelectedCandidate = boundCandidate,
                    DirectionBinding = binding,
                    CompiledPlan = compiledPlan,
                    CompiledQueue = compiledQueue,
                    DeferredVisitIds = itinerary.Steps
                        .Skip(1)
                        .Select(value => value.OpportunityId)
                        .ToArray(),
                    BlockingReasons = compilationReasons
                        .DefaultIfEmpty(
                            "current_social_teacher_action_queue_not_dispatchable")
                        .ToArray()
                };
            }

            return new CurrentSocialDayTeacherLabel
            {
                Status = "ready",
                TrainingLabelEligible = true,
                SourceStateHash = envelope.StateHash,
                Itinerary = itinerary,
                SelectedCandidate = boundCandidate,
                DirectionBinding = binding,
                CompiledPlan = compiledPlan,
                CompiledQueue = compiledQueue,
                DeferredVisitIds = itinerary.Steps
                    .Skip(1)
                    .Select(value => value.OpportunityId)
                    .ToArray()
            };
        }

        private static CurrentSocialDayTeacherLabel Blocked(
            CurrentSocialDayItineraryPlan itinerary,
            string reason) => Blocked(itinerary, new[] { reason });

        private static CurrentSocialDayTeacherLabel Blocked(
            CurrentSocialDayItineraryPlan itinerary,
            string[] reasons) => new()
        {
            SourceStateHash = string.Empty,
            Itinerary = itinerary,
            BlockingReasons = reasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}
