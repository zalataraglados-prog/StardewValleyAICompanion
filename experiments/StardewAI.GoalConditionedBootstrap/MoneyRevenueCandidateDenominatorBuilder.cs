using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    public static MoneyRevenueCandidateDenominatorCatalog Build(
        string snapshotPath,
        string dependencyExpansionPath)
    {
        var snapshotFullPath = Path.GetFullPath(snapshotPath);
        var dependencyFullPath = Path.GetFullPath(dependencyExpansionPath);
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotFullPath,
            "Money Revenue candidate-denominator snapshot");
        var dependencyText = CurrentTeacherFrontierSupport.ReadArtifactText(
            dependencyFullPath);
        using var dependency = JsonDocument.Parse(dependencyText);
        var direction = ReadDirection(dependency.RootElement);
        var optionIds = direction.Nodes
            .Select(node => node.OptionId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            optionIds,
            includeExecutorCalibrationOptions: true);
        return BuildCatalog(
            snapshot,
            snapshotFullPath,
            CurrentTeacherFrontierSupport.HashFile(snapshotFullPath),
            dependencyFullPath,
            CurrentTeacherFrontierSupport.HashFile(dependencyFullPath),
            direction,
            availability);
    }

    internal static MoneyRevenueCandidateDenominatorCatalog BuildCatalog(
        SnapshotEnvelope snapshot,
        string snapshotPath,
        string snapshotSha256,
        string dependencyExpansionPath,
        string dependencyExpansionSha256,
        RevenueDirectionDefinition direction,
        OptionAvailabilityEnvelope availability)
    {
        Require(!string.IsNullOrWhiteSpace(snapshot.StateHash),
            "Revenue candidate snapshot state_hash is missing.");
        Require(string.Equals(
                availability.StateHash,
                snapshot.StateHash,
                StringComparison.Ordinal),
            "Revenue candidate availability does not bind the source snapshot.");
        Require(direction.Nodes.Length > 0,
            "Revenue dependency expansion has no actionable nodes.");

        var nodes = direction.Nodes
            .Select(node => BuildNode(node, availability, snapshot))
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var comparisonIds = nodes
            .Where(node => node.PolicyChoice)
            .SelectMany(node => node.Candidates)
            .Where(candidate => candidate.ComparisonEligible)
            .Select(candidate => candidate.CatalogCandidateId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var blockers = new List<string>();
        foreach (var node in nodes.Where(node => !node.EnumerationComplete))
        {
            blockers.Add("revenue_node_enumeration_incomplete:" + node.NodeId);
        }
        if (comparisonIds.Length < 2)
            blockers.Add("fewer_than_two_current_comparison_candidates");
        if (!string.Equals(direction.Status, "complete", StringComparison.Ordinal))
        {
            blockers.Add("revenue_dependency_expansion_not_complete:" +
                direction.Status);
        }
        blockers.AddRange(direction.Blockers.Select(blocker =>
            "revenue_dependency_expansion_blocker:" + blocker));
        blockers.Add("verified_rollout_artifact_binding_not_supplied");

        var nodeCoverageComplete = nodes.All(node => node.EnumerationComplete);
        var currentDenominatorAuthoritative = nodeCoverageComplete;
        var currentComparisonReady = currentDenominatorAuthoritative &&
            comparisonIds.Length >= 2;
        var longHorizonAuthoritative = currentDenominatorAuthoritative &&
            string.Equals(direction.Status, "complete", StringComparison.Ordinal) &&
            direction.Blockers.Length == 0;
        var catalog = new MoneyRevenueCandidateDenominatorCatalog
        {
            Status = currentDenominatorAuthoritative
                ? "current_snapshot_denominator_authoritative"
                : "blocked_incomplete_current_snapshot_denominator",
            SourceStateHash = snapshot.StateHash,
            SnapshotPath = snapshotPath,
            SnapshotSha256 = snapshotSha256,
            DependencyExpansionPath = dependencyExpansionPath,
            DependencyExpansionSha256 = dependencyExpansionSha256,
            DependencyExpansionStatus = direction.Status,
            DependencyExpansionBlockers = direction.Blockers,
            NodeCoverageComplete = nodeCoverageComplete,
            CurrentSnapshotDenominatorAuthoritative =
                currentDenominatorAuthoritative,
            CurrentComparisonDenominatorReady = currentComparisonReady,
            LongHorizonDenominatorAuthoritative = longHorizonAuthoritative,
            ProductionTeacherPreferenceLabelEligible = false,
            FormalProductTrainingAuthorized = false,
            PolicyNodeCount = nodes.Count(node => node.PolicyChoice),
            DeterministicSupportNodeCount = nodes.Count(node =>
                node.DeterministicSupport),
            EnumeratedCandidateCount = nodes.Sum(node =>
                node.EnumeratedCandidateCount),
            ComparisonCandidateCount = comparisonIds.Length,
            ComparisonCandidateIds = comparisonIds,
            Nodes = nodes,
            BlockingReasons = blockers
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };
        catalog.CatalogSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .ArtifactSha256(catalog);
        return catalog;
    }

    private static MoneyRevenueCandidateNodeCoverage BuildNode(
        RevenueActionNodeDefinition definition,
        OptionAvailabilityEnvelope availability,
        SnapshotEnvelope snapshot)
    {
        var matches = availability.Options
            .Where(option => string.Equals(
                option.OptionId,
                definition.OptionId,
                StringComparison.Ordinal))
            .ToArray();
        var reasons = new List<string>();
        if (matches.Length != 1)
        {
            reasons.Add("option_evaluation_count_not_one:" + matches.Length);
            return EmptyNode(definition, matches.Length, reasons);
        }

        var option = matches[0];
        var candidates = option.EventCandidates
            .Select(candidate => BuildEntry(definition, candidate))
            .Concat(option.EconomicCandidates.Select(candidate =>
                BuildEntry(definition, candidate)))
            .OrderBy(candidate => candidate.CatalogCandidateId,
                StringComparer.Ordinal)
            .ToArray();
        if (option.MissingStateFactors.Length > 0)
            reasons.Add("required_state_factors_missing");
        foreach (var path in definition.RequiredFieldPaths.Where(path =>
                     !RequiredFieldAvailable(snapshot, path)))
        {
            reasons.Add("required_field_path_unavailable:" + path);
        }
        if (candidates.Any(candidate =>
                string.IsNullOrWhiteSpace(candidate.CandidateId)))
            reasons.Add("candidate_id_missing");
        if (candidates.GroupBy(
                candidate => candidate.CatalogCandidateId,
                StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
            reasons.Add("catalog_candidate_id_not_unique");
        if (candidates.Any(candidate =>
                candidate.SemanticKindBinding == "unbound"))
            reasons.Add("candidate_kind_not_bound_to_dependency_node");

        return new MoneyRevenueCandidateNodeCoverage
        {
            NodeId = definition.NodeId,
            NodeKind = definition.NodeKind,
            OptionId = definition.OptionId,
            PolicyChoice = definition.PolicyChoice,
            DeterministicSupport = definition.DeterministicSupport,
            DeclaredCandidateKinds = definition.CandidateKinds,
            RequiredFieldPaths = definition.RequiredFieldPaths,
            OptionEvaluationCount = 1,
            OptionStatus = option.Status,
            OptionAvailable = option.Available,
            ReadEligible = option.ReadEligible,
            MissingStateFactors = option.MissingStateFactors,
            OptionBlockingReasons = option.BlockingReasons,
            EnumerationComplete = reasons.Count == 0,
            EnumeratedCandidateCount = candidates.Length,
            ComparisonCandidateCount = candidates.Count(candidate =>
                candidate.ComparisonEligible),
            Candidates = candidates,
            BlockingReasons = reasons.ToArray()
        };
    }

    private static bool RequiredFieldAvailable(
        SnapshotEnvelope snapshot,
        string path)
    {
        var separator = path.IndexOf('.');
        if (separator <= 0 || separator == path.Length - 1)
            return false;
        var domain = path[..separator];
        var field = path[(separator + 1)..];
        if (!snapshot.State.TryGetValue(domain, out var domainState) ||
            domainState.ValueKind != JsonValueKind.Object ||
            !domainState.TryGetProperty(field, out var fieldState) ||
            fieldState.ValueKind != JsonValueKind.Object ||
            !fieldState.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            !(string.Equals(
                  status.GetString(),
                  "available",
                  StringComparison.OrdinalIgnoreCase) ||
              string.Equals(
                  status.GetString(),
                  "derived",
                  StringComparison.OrdinalIgnoreCase)) ||
            !fieldState.TryGetProperty("value", out var value))
        {
            return false;
        }

        return value.ValueKind is not JsonValueKind.Null and
            not JsonValueKind.Undefined;
    }

    private static MoneyRevenueCandidateNodeCoverage EmptyNode(
        RevenueActionNodeDefinition definition,
        int optionEvaluationCount,
        IEnumerable<string> reasons) => new()
        {
            NodeId = definition.NodeId,
            NodeKind = definition.NodeKind,
            OptionId = definition.OptionId,
            PolicyChoice = definition.PolicyChoice,
            DeterministicSupport = definition.DeterministicSupport,
            DeclaredCandidateKinds = definition.CandidateKinds,
            RequiredFieldPaths = definition.RequiredFieldPaths,
            OptionEvaluationCount = optionEvaluationCount,
            EnumerationComplete = false,
            BlockingReasons = reasons.ToArray()
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
