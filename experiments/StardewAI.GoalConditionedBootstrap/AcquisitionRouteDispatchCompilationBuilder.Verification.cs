using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    internal static PolicyEventCandidatePrediction[]
        RebuildVerifiedCurrentCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger ledger,
            string goalId,
            AcquisitionRouteTargetDateUnlock requirement,
            string[] endpointOptionIds,
            IEnumerable<PolicyEventCandidatePrediction> rankedCandidates,
            out string[] blockingReasons)
    {
        var allowed = endpointOptionIds.ToHashSet(StringComparer.Ordinal);
        var supplied = rankedCandidates
            .Where(candidate => allowed.Contains(candidate.OptionId))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        var evaluationCandidates = RebuildEvaluationCandidates(
            snapshot,
            requirement,
            endpointOptionIds,
            supplied,
            out var intentReasons);
        var rebuilt = intentReasons.Length > 0
            ? Array.Empty<PolicyEventCandidatePrediction>()
            : new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    new CandidateOptionAvailabilityEvaluator().Evaluate(
                        snapshot,
                        evaluationCandidates,
                        includeExecutorCalibrationOptions: true,
                        commitmentLedger: ledger),
                    goalId)
                .Where(candidate => allowed.Contains(candidate.OptionId))
                .OrderBy(candidate => candidate.CandidateId,
                    StringComparer.Ordinal)
                .ToArray();
        var reasons = new List<string>(intentReasons);
        if (supplied.GroupBy(candidate => candidate.CandidateId,
                StringComparer.Ordinal).Any(group => group.Count() != 1) ||
            rebuilt.GroupBy(candidate => candidate.CandidateId,
                StringComparer.Ordinal).Any(group => group.Count() != 1))
        {
            reasons.Add("route_dispatch_endpoint_candidate_identity_ambiguous");
        }

        var suppliedById = supplied
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);
        var rebuiltById = rebuilt
            .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                StringComparer.Ordinal);
        if (suppliedById.Keys.Except(rebuiltById.Keys, StringComparer.Ordinal)
            .Any())
        {
            reasons.Add("ranking_endpoint_candidate_not_rebuilt");
        }
        if (rebuiltById.Keys.Except(suppliedById.Keys, StringComparer.Ordinal)
            .Any())
        {
            reasons.Add("rebuilt_endpoint_candidate_missing_from_ranking");
        }
        if (suppliedById.Keys.Intersect(rebuiltById.Keys,
                StringComparer.Ordinal).Any(id =>
                !CandidateEvidenceMatches(
                    suppliedById[id],
                    rebuiltById[id])))
        {
            reasons.Add("ranking_endpoint_candidate_evidence_mismatch");
        }

        blockingReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return blockingReasons.Length == 0
            ? rebuilt
            : Array.Empty<PolicyEventCandidatePrediction>();
    }

    private static OptionAvailabilityCandidate[] RebuildEvaluationCandidates(
        SnapshotEnvelope snapshot,
        AcquisitionRouteTargetDateUnlock requirement,
        string[] endpointOptionIds,
        PolicyEventCandidatePrediction[] supplied,
        out string[] blockingReasons)
    {
        if (requirement.RouteKind is not
            ("native_location_fish_spawn" or
             "native_mine_fishing_override" or
             "recipe_output"))
        {
            blockingReasons = Array.Empty<string>();
            return endpointOptionIds
                .Select(optionId => new OptionAvailabilityCandidate
                {
                    OptionId = optionId
                })
                .ToArray();
        }

        if (requirement.RouteKind == "recipe_output")
        {
            const string prefix = "cooking_recipe:";
            var recipeName = requirement.SourceId.StartsWith(
                    prefix,
                    StringComparison.Ordinal)
                ? requirement.SourceId[prefix.Length..]
                : string.Empty;
            var cookingReason = requirement.RequirementSetId;
            if (string.IsNullOrWhiteSpace(recipeName) ||
                string.IsNullOrWhiteSpace(cookingReason))
            {
                blockingReasons = new[]
                {
                    "route_dispatch_verified_recipe_intent_missing_or_ambiguous"
                };
                return Array.Empty<OptionAvailabilityCandidate>();
            }
            blockingReasons = Array.Empty<string>();
            return new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "crafting.cook_recipe",
                    ExplicitConfirmationGranted = true,
                    Parameters = new[]
                    {
                        Parameter("recipe_name", recipeName),
                        Parameter("craft_count", "1"),
                        Parameter("cooking_reason", cookingReason)
                    }
                }
            };
        }

        var intents = supplied
            .Select(candidate =>
            {
                var valid = MasterAnglerCurrentCandidateMatcher.TryMatch(
                    snapshot,
                    candidate,
                    out var match,
                    out _);
                return new { Candidate = candidate, Match = match, Valid = valid };
            })
            .Where(value => value.Valid &&
                string.Equals(
                    value.Match.Intent.TargetQualifiedItemId,
                    requirement.QualifiedItemId,
                    StringComparison.Ordinal) &&
                ((requirement.RouteKind == "native_location_fish_spawn" &&
                  value.Match.Intent.SourceKind == "location_rule" &&
                  requirement.SourceId == "location_fish:" +
                    value.Match.Intent.SourceKey) ||
                 (requirement.RouteKind == "native_mine_fishing_override" &&
                  value.Match.Intent.SourceKind == "mine_override" &&
                  requirement.SourceId == value.Match.Intent.SourceKey)))
            .GroupBy(value =>
                value.Match.Intent.SourceKind + "|" +
                value.Match.Intent.SourceKey + "|" +
                value.Match.Intent.TargetQualifiedItemId,
                StringComparer.Ordinal)
            .ToArray();
        if (intents.Length != 1)
        {
            blockingReasons = new[]
            {
                "route_dispatch_verified_bound_intent_missing_or_ambiguous"
            };
            return Array.Empty<OptionAvailabilityCandidate>();
        }

        blockingReasons = Array.Empty<string>();
        var source = intents[0]
            .OrderBy(value => value.Candidate.CandidateId,
                StringComparer.Ordinal)
            .First()
            .Candidate;
        return new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = source.OptionId,
                Parameters = source.Parameters
            }
        };
    }

    private static bool CandidateEvidenceMatches(
        PolicyEventCandidatePrediction supplied,
        PolicyEventCandidatePrediction rebuilt) =>
        string.Equals(
            System.Text.Json.JsonSerializer.Serialize(
                NeutralizeLearnerSignals(supplied, supplied.CandidateId),
                JsonDefaults.Options),
            System.Text.Json.JsonSerializer.Serialize(
                NeutralizeLearnerSignals(rebuilt, rebuilt.CandidateId),
                JsonDefaults.Options),
            StringComparison.Ordinal);
}
