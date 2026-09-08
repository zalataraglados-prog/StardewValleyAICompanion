using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed partial class CurrentSocialDayItineraryPlanner
    {
        private const int FriendshipThreshold = 1975;
        private const int RequiredQualifyingCount = 10;

        public CurrentSocialDayItineraryPlan Plan(
            JsonElement snapshot,
            string timingCalibrationArtifactJson)
        {
            var frontier = new CurrentSocialContactFrontierProducer().Produce(
                snapshot,
                timingCalibrationArtifactJson);
            var partialFrontierAccepted =
                CanUseConservativePartialFrontier(frontier);
            if (!frontier.RankingAdmissionReady &&
                !partialFrontierAccepted)
            {
                return Blocked(
                    frontier.TotalDays,
                    frontier.RankingAdmissionBlockingReasons
                        .Concat(frontier.BlockingReasons)
                        .DefaultIfEmpty(
                            "current_social_frontier_not_ranking_ready")
                        .ToArray());
            }
            if (!TryReadInputs(
                    snapshot,
                    out var gameVersion,
                    out var totalDays,
                    out var startLocation,
                    out var startX,
                    out var startY,
                    out var startTime,
                    out var movementContext,
                    out var routeGraph,
                    out var routeDateEvidence,
                    out var friendshipProgress,
                    out var envelope,
                    out var inputReason))
            {
                return Blocked(totalDays, inputReason);
            }

            var timingLoad = new FutureRouteTimingCalibrationLoader().Load(
                timingCalibrationArtifactJson,
                movementContext,
                gameVersion,
                totalDays);
            if (timingLoad.Status !=
                    FutureRouteTimingCalibrationLoadStatus.Loaded ||
                timingLoad.Calibration is null)
            {
                return Blocked(totalDays, timingLoad.BlockingReasons);
            }
            if (!FutureRouteDateEvidenceProducer.TryCreateContext(
                    routeGraph,
                    routeDateEvidence,
                    totalDays,
                    out var routeContext,
                    out var routeContextBlocks))
            {
                return Blocked(totalDays, routeContextBlocks);
            }
            if (!TryReadPortfolio(
                    friendshipProgress,
                    out var qualifyingCount,
                    out var progressRows,
                    out var portfolioReason))
            {
                return Blocked(totalDays, portfolioReason);
            }

            var slotsNeeded = Math.Max(
                0,
                RequiredQualifyingCount - qualifyingCount);
            if (slotsNeeded == 0)
            {
                return new CurrentSocialDayItineraryPlan
                {
                    Status = "goal_already_satisfied",
                    TrainingLabelEligible = false,
                    PortfolioCoverageComplete = true,
                    TotalDays = totalDays,
                    QualifyingCountBefore = qualifyingCount,
                    PortfolioSlotsNeeded = 0,
                    PortfolioSlotsPlanned = 0,
                    RouteTimingEvidenceKind =
                        FutureRouteTravelTimingEvidenceKind.ExactDuration,
                    PlanningStartTime = startTime
                };
            }

            var talkTransitions = ReadTalkTransitions(envelope);
            var availableNames = frontier.Opportunities
                .Where(value => value.ExecutionReady &&
                    value.InteractionKind == "talk")
                .Select(value => value.NpcName)
                .Distinct(StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            var eligibleTargets = progressRows
                .Where(row => !row.Qualifies &&
                    availableNames.Contains(row.NpcName) &&
                    talkTransitions.TryGetValue(
                        row.NpcName,
                        out var transition) &&
                    transition.PointsBefore == row.Points &&
                    transition.Delta > 0 &&
                    transition.PointsAfter == row.Points + transition.Delta)
                .OrderByDescending(row => row.Points)
                .ThenBy(row => row.NpcName, StringComparer.Ordinal)
                .ToArray();
            var portfolio = eligibleTargets
                .Take(slotsNeeded)
                .Select(row => row.NpcName)
                .ToArray();
            if (portfolio.Length == 0)
            {
                return Blocked(
                    totalDays,
                    "current_social_itinerary_no_exact_nonqualifying_target");
            }

            var routeProducer = new FutureRouteDateEvidenceProducer();
            var verifier = new FutureSocialItineraryVerifier();
            var remaining = new HashSet<string>(
                portfolio,
                StringComparer.Ordinal);
            var steps = new List<CurrentSocialDayItineraryStep>();
            var currentLocation = startLocation;
            var currentX = startX;
            var currentY = startY;
            var currentTime = startTime;
            var timingKind =
                FutureRouteTravelTimingEvidenceKind.ExactDuration;
            while (remaining.Count > 0)
            {
                var feasible = new List<FeasibleVisit>();
                foreach (var opportunity in frontier.Opportunities.Where(value =>
                    value.InteractionKind == "talk" &&
                    remaining.Contains(value.NpcName)))
                {
                    var route = routeProducer.Produce(
                        routeContext,
                        new FutureRouteDateEvidenceRequest
                        {
                            TotalDays = totalDays,
                            StartLocation = currentLocation,
                            StartTileX = currentX,
                            StartTileY = currentY,
                            EarliestDepartureTime = currentTime,
                            TargetLocation = opportunity.LocationName,
                            TargetTileX = opportunity.NpcTileX,
                            TargetTileY = opportunity.NpcTileY
                        },
                        timingLoad.Calibration);
                    if (route.Status !=
                            FutureRouteDateEvidenceProductionStatus.Produced ||
                        route.Scenario is null)
                    {
                        continue;
                    }

                    var visit = BuildVisit(opportunity, totalDays);
                    var verification = verifier.Verify(
                        routeGraph,
                        route.Scenario,
                        new[] { visit });
                    if (verification.Status ==
                            FutureSocialItineraryVerificationStatus.Blocked ||
                        verification.Steps.Length != 1 ||
                        !verification.CompletionTime.HasValue)
                    {
                        continue;
                    }
                    feasible.Add(new FeasibleVisit(
                        opportunity,
                        verification.Steps[0],
                        verification.CompletionTime.Value,
                        talkTransitions[opportunity.NpcName]));
                }
                var selected = feasible
                    .OrderBy(value => ToMinutes(value.CompletionTime))
                    .ThenBy(value => value.Step.RouteWaitGameMinutes)
                    .ThenBy(value => value.Step.RouteConnectorCount)
                    .ThenByDescending(value => value.Transition.PointsBefore)
                    .ThenBy(value => value.Opportunity.OpportunityId,
                        StringComparer.Ordinal)
                    .FirstOrDefault();
                if (selected is null)
                    break;

                if (selected.Step.RouteTimingEvidenceKind ==
                    FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound)
                {
                    timingKind =
                        FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;
                }
                steps.Add(ToPlanStep(
                    steps.Count + 1,
                    selected,
                    FriendshipThreshold));
                remaining.Remove(selected.Opportunity.NpcName);
                currentLocation = selected.Step.LocationName;
                currentX = selected.Step.StandTileX;
                currentY = selected.Step.StandTileY;
                currentTime = selected.CompletionTime;
            }

            if (steps.Count == 0)
            {
                return Blocked(
                    totalDays,
                    "current_social_itinerary_no_verified_first_visit");
            }
            var limitations = new List<string>();
            if (remaining.Count > 0)
            {
                limitations.Add(
                    "current_social_itinerary_some_portfolio_targets_not_reachable_today");
            }
            if (partialFrontierAccepted)
            {
                limitations.Add(
                    "current_social_itinerary_unresolved_nonportfolio_npcs_excluded");
            }
            return new CurrentSocialDayItineraryPlan
            {
                Status = remaining.Count == 0 &&
                    portfolio.Length == slotsNeeded
                        ? "pass"
                        : "partial_verified_prefix",
                TrainingLabelEligible = true,
                PortfolioCoverageComplete = remaining.Count == 0 &&
                    portfolio.Length == slotsNeeded,
                TotalDays = totalDays,
                PlanningStartTime = startTime,
                QualifyingCountBefore = qualifyingCount,
                PortfolioSlotsNeeded = slotsNeeded,
                PortfolioSlotsPlanned = steps.Count,
                EligibleTargetCount = eligibleTargets.Length,
                PlannedVisitCount = steps.Count,
                CompletionTime = steps[^1].InteractionEndTime,
                RouteTimingEvidenceKind = timingKind,
                TargetPortfolioNpcNames = portfolio,
                OmittedTargetNpcNames = portfolio
                    .Where(remaining.Contains)
                    .ToArray(),
                Steps = steps.ToArray(),
                Limitations = limitations.ToArray()
            };
        }

        internal static bool CanUseConservativePartialFrontier(
            CurrentSocialContactFrontier frontier)
        {
            if (frontier is null || frontier.RankingAdmissionReady ||
                frontier.Opportunities.Any(value => !value.ExecutionReady))
            {
                return false;
            }
            var reasons = frontier.RankingAdmissionBlockingReasons
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return reasons.Length == 1 &&
                reasons[0] == "current_social_npc_coverage_incomplete";
        }

        private static CurrentSocialDayItineraryStep ToPlanStep(
            int sequence,
            FeasibleVisit selected,
            int threshold) => new()
        {
            Sequence = sequence,
            OpportunityId = selected.Opportunity.OpportunityId,
            NpcName = selected.Opportunity.NpcName,
            FriendshipPointsBefore = selected.Transition.PointsBefore,
            ExpectedFriendshipDelta = selected.Transition.Delta,
            ExpectedFriendshipPointsAfter = selected.Transition.PointsAfter,
            DeficitBefore = Math.Max(
                0,
                threshold - selected.Transition.PointsBefore),
            DeficitAfter = Math.Max(
                0,
                threshold - selected.Transition.PointsAfter),
            LocationName = selected.Step.LocationName,
            NpcTileX = selected.Step.TileX,
            NpcTileY = selected.Step.TileY,
            StandTileX = selected.Step.StandTileX,
            StandTileY = selected.Step.StandTileY,
            ArrivalTime = selected.Step.ArrivalTime,
            InteractionStartTime = selected.Step.InteractionStartTime,
            InteractionEndTime = selected.Step.InteractionEndTime,
            RouteConnectorCount = selected.Step.RouteConnectorCount,
            RouteWaitGameMinutes = selected.Step.RouteWaitGameMinutes,
            RouteTimingEvidenceKind = selected.Step.RouteTimingEvidenceKind
        };

        private sealed class FeasibleVisit
        {
            public FeasibleVisit(
                CurrentSocialContactOpportunity opportunity,
                FutureSocialItineraryStep step,
                int completionTime,
                TalkTransition transition)
            {
                Opportunity = opportunity;
                Step = step;
                CompletionTime = completionTime;
                Transition = transition;
            }

            public CurrentSocialContactOpportunity Opportunity { get; }

            public FutureSocialItineraryStep Step { get; }

            public int CompletionTime { get; }

            public TalkTransition Transition { get; }
        }

        private sealed class TalkTransition
        {
            public int PointsBefore { get; set; }

            public int Delta { get; set; }

            public int PointsAfter { get; set; }
        }

        private sealed class FriendshipProgressRow
        {
            public string NpcName { get; set; } = string.Empty;

            public int Points { get; set; }

            public bool Qualifies { get; set; }
        }
    }
}
