using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaDailySubgoalResolver
    {
        private readonly GrandpaDirectionDailyCandidateBinding binding;
        private readonly WorldModelProjector projector;
        private readonly GrandpaEvaluationGoalEvaluator evaluator;
        private readonly GrandpaTrainingSampleAdapter adapter;

        public GrandpaDailySubgoalResolver()
            : this(
                new GrandpaDirectionDailyCandidateBinding(),
                new WorldModelProjector(),
                new GrandpaEvaluationGoalEvaluator(),
                new GrandpaTrainingSampleAdapter())
        {
        }

        public GrandpaDailySubgoalResolver(
            GrandpaDirectionDailyCandidateBinding binding,
            WorldModelProjector projector,
            GrandpaEvaluationGoalEvaluator evaluator,
            GrandpaTrainingSampleAdapter adapter)
        {
            this.binding = binding;
            this.projector = projector;
            this.evaluator = evaluator;
            this.adapter = adapter;
        }

        public PlanningGoalResolution Resolve(
            SnapshotEnvelope snapshot,
            string requestedGoalId,
            PolicyEventCandidatePrediction[] broadRankedCandidates)
        {
            return ResolveWithBinding(
                snapshot,
                requestedGoalId,
                broadRankedCandidates).GoalResolution;
        }

        public GrandpaDailySubgoalResolution ResolveWithBinding(
            SnapshotEnvelope snapshot,
            string requestedGoalId,
            PolicyEventCandidatePrediction[] broadRankedCandidates)
        {
            var requested = (requestedGoalId ?? string.Empty).Trim();
            if (!GrandpaEvaluationGoalDefinition.IsMaximumScoreGoal(requested))
            {
                return Detailed(Result(
                    "not_applicable",
                    requested,
                    requested,
                    snapshot.StateHash,
                    "requested_goal_is_not_grandpa_max_score"));
            }

            var worldModel = projector.Project(
                snapshot,
                GrandpaEvaluationGoalDefinition.StrategicGoal,
                "strategic");
            var report = evaluator.Evaluate(worldModel);
            var sample = adapter.Build(worldModel, report);
            if (sample.Target.Complete)
            {
                return Detailed(Result(
                    "target_complete",
                    requested,
                    requested,
                    snapshot.StateHash,
                    "grandpa_max_score_already_complete"));
            }

            var candidates = broadRankedCandidates ??
                Array.Empty<PolicyEventCandidatePrediction>();
            var considered = new List<string>();
            foreach (var direction in sample.CandidateDirections.Where(
                         candidate =>
                             candidate.Known &&
                             !candidate.Blocked &&
                             candidate.PotentialPoints > 0))
            {
                considered.Add(direction.DirectionId);
                var catalog = GrandpaDirectionCatalog.Entries.FirstOrDefault(
                    entry => string.Equals(
                        entry.DirectionId,
                        direction.DirectionId,
                        StringComparison.Ordinal));
                if (catalog is null)
                {
                    continue;
                }

                if (!HasCandidateSurface(catalog, candidates))
                {
                    if (RequiresCropProductionTeacherHandoff(
                            snapshot,
                            direction.DirectionId))
                    {
                        return CropProductionTeacherHandoff(
                            snapshot,
                            requested,
                            catalog,
                            considered);
                    }

                    continue;
                }

                var bound = binding.Bind(
                    new GrandpaDirectionBindingRequest
                    {
                        StateHash = snapshot.StateHash,
                        DirectionId = direction.DirectionId,
                        RankedCandidates = candidates
                    },
                    snapshot);
                if (!string.Equals(
                        bound.BindingStatus,
                        "ready",
                        StringComparison.Ordinal) ||
                    bound.BoundCandidates.Length == 0)
                {
                    if (RequiresCropProductionTeacherHandoff(
                            snapshot,
                            direction.DirectionId))
                    {
                        return CropProductionTeacherHandoff(
                            snapshot,
                            requested,
                            catalog,
                            considered);
                    }

                    continue;
                }

                return Detailed(
                    new PlanningGoalResolution
                    {
                        Status = "resolved",
                        RequestedGoalId = requested,
                        EffectiveGoalId = catalog.EffectiveGoalId,
                        DirectionId = direction.DirectionId,
                        DemandFamily = catalog.DemandFamily,
                        Reason =
                            "current_snapshot_and_candidate_binding_ready",
                        SourceStateHash = snapshot.StateHash,
                        BoundCandidateIds = bound.BoundCandidates
                            .Select(candidate => candidate.CandidateId)
                            .Where(id => !string.IsNullOrWhiteSpace(id))
                            .Distinct(StringComparer.Ordinal)
                            .ToArray(),
                        BindingRuleId = bound.BindingRuleId,
                        ConsideredDirectionIds = considered.ToArray()
                    },
                    bound.BoundCandidates);
            }

            var unresolved = Result(
                "no_actionable_direction",
                requested,
                requested,
                snapshot.StateHash,
                "no_current_direction_has_exact_ready_candidate_binding");
            unresolved.ConsideredDirectionIds = considered.ToArray();
            return Detailed(unresolved);
        }

        public PolicyEventCandidatePrediction[] ApplyBindingProvenance(
            PolicyEventCandidatePrediction[] rankedCandidates,
            GrandpaDailySubgoalResolution resolution)
        {
            if (!string.Equals(
                    resolution.GoalResolution.Status,
                    "resolved",
                    StringComparison.Ordinal) ||
                resolution.BoundCandidates.Length == 0)
            {
                return rankedCandidates;
            }

            var boundById = resolution.BoundCandidates
                .Where(candidate =>
                    !string.IsNullOrWhiteSpace(candidate.CandidateId))
                .GroupBy(
                    candidate => candidate.CandidateId,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
            foreach (var candidate in rankedCandidates)
            {
                if (!boundById.TryGetValue(
                        candidate.CandidateId,
                        out var bound))
                {
                    continue;
                }

                var provenance = (bound.Parameters ??
                                  Array.Empty<SmallModelActionParameter>())
                    .Where(IsGrandpaBindingParameter)
                    .ToArray();
                candidate.Parameters = (candidate.Parameters ??
                                        Array.Empty<SmallModelActionParameter>())
                    .Where(parameter =>
                        !IsGrandpaBindingParameter(parameter))
                    .Concat(provenance)
                    .ToArray();
            }

            return rankedCandidates;
        }

        private static bool HasCandidateSurface(
            GrandpaDirectionCatalogEntry catalog,
            IEnumerable<PolicyEventCandidatePrediction> candidates)
        {
            return candidates.Any(candidate =>
                catalog.PermittedOptionIds.Contains(
                    candidate.OptionId,
                    StringComparer.Ordinal) &&
                catalog.PermittedCandidateKinds.Contains(
                    candidate.Kind,
                    StringComparer.Ordinal));
        }

        private static bool RequiresCropProductionTeacherHandoff(
            SnapshotEnvelope snapshot,
            string directionId)
        {
            if (!string.Equals(
                    directionId,
                    "earn_money",
                    StringComparison.Ordinal) ||
                !ReadableStatus(ReadStateFieldStatus(
                    snapshot,
                    "player",
                    "seed_inventory")) ||
                !ReadableStatus(ReadStateFieldStatus(
                    snapshot,
                    "farm",
                    "crops")))
            {
                return false;
            }

            var seeds = ReadStateFieldValue(
                snapshot,
                "player",
                "seed_inventory");
            var crops = ReadStateFieldValue(snapshot, "farm", "crops");
            if (!seeds.HasValue ||
                seeds.Value.ValueKind != JsonValueKind.Array ||
                !crops.HasValue ||
                crops.Value.ValueKind != JsonValueKind.Array ||
                crops.Value.GetArrayLength() != 0)
            {
                return false;
            }

            return seeds.Value.EnumerateArray().Any(seed =>
                seed.ValueKind == JsonValueKind.Object &&
                ReadInt(seed, "stack") > 0 &&
                ReadBool(seed, "crop_catalog_match") &&
                !string.IsNullOrWhiteSpace(
                    ReadString(seed, "qualified_item_id")));
        }

        private static GrandpaDailySubgoalResolution CropProductionTeacherHandoff(
            SnapshotEnvelope snapshot,
            string requestedGoalId,
            GrandpaDirectionCatalogEntry catalog,
            IReadOnlyCollection<string> consideredDirectionIds)
        {
            return Detailed(new PlanningGoalResolution
            {
                Status = "planning_handoff_required",
                RequestedGoalId = requestedGoalId,
                EffectiveGoalId = catalog.EffectiveGoalId,
                DirectionId = catalog.DirectionId,
                DemandFamily = catalog.DemandFamily,
                Reason = "crop_production_teacher_handoff_required",
                SourceStateHash = snapshot.StateHash,
                BindingRuleId = catalog.BindingRuleId,
                ConsideredDirectionIds = consideredDirectionIds.ToArray(),
                PlanningHandoff = new PlanningGoalHandoff
                {
                    HandoffKind = "crop_production_teacher",
                    RequiredSnapshotProfiles = new[] { "farm_layout" },
                    RequestedCapability =
                        "establish_initial_crop_production",
                    AutomaticDispatchRequired = true
                }
            });
        }

        private static PlanningGoalResolution Result(
            string status,
            string requestedGoalId,
            string effectiveGoalId,
            string stateHash,
            string reason)
        {
            return new PlanningGoalResolution
            {
                Status = status,
                RequestedGoalId = requestedGoalId,
                EffectiveGoalId = effectiveGoalId,
                SourceStateHash = stateHash,
                Reason = reason
            };
        }

        private static GrandpaDailySubgoalResolution Detailed(
            PlanningGoalResolution goalResolution,
            PolicyEventCandidatePrediction[]? boundCandidates = null)
        {
            return new GrandpaDailySubgoalResolution
            {
                GoalResolution = goalResolution,
                BoundCandidates = boundCandidates ??
                    Array.Empty<PolicyEventCandidatePrediction>()
            };
        }

        private static bool IsGrandpaBindingParameter(
            SmallModelActionParameter parameter)
        {
            return parameter.Name.StartsWith(
                "grandpa_",
                StringComparison.Ordinal);
        }

    }

    public sealed class GrandpaDailySubgoalResolution
    {
        public PlanningGoalResolution GoalResolution { get; set; } = new();

        public PolicyEventCandidatePrediction[] BoundCandidates { get; set; } =
            Array.Empty<PolicyEventCandidatePrediction>();
    }
}
