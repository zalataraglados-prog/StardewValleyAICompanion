using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueAlternativeDenominatorBuilder
{
    public static MoneyRevenueAlternativeDenominatorCatalog Build(
        string sourceCandidateCatalogPath)
    {
        var fullPath = Path.GetFullPath(sourceCandidateCatalogPath);
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCandidateDenominatorCatalog>(
                fullPath,
                "Money Revenue source candidate catalog");
        var rebuilt = MoneyRevenueCandidateDenominatorBuilder.Build(
            persisted.SnapshotPath,
            persisted.DependencyExpansionPath);
        Require(EqualJson(persisted, rebuilt),
            "Money Revenue source candidate catalog did not equal its source rebuild.");
        using var dependency = JsonDocument.Parse(
            CurrentTeacherFrontierSupport.ReadArtifactText(
                rebuilt.DependencyExpansionPath));
        var direction = MoneyRevenueCandidateDenominatorBuilder.ReadDirection(
            dependency.RootElement);
        return BuildCatalog(
            fullPath,
            CurrentTeacherFrontierSupport.HashFile(fullPath),
            rebuilt,
            direction);
    }

    internal static MoneyRevenueAlternativeDenominatorCatalog BuildCatalog(
        string sourceCandidateCatalogPath,
        string sourceCandidateCatalogFileSha256,
        MoneyRevenueCandidateDenominatorCatalog source,
        MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition
            direction)
    {
        var currentBlockers = new List<string>();
        if (!source.CurrentSnapshotDenominatorAuthoritative ||
            !source.CurrentComparisonDenominatorReady ||
            !source.NodeCoverageComplete)
        {
            currentBlockers.Add(
                "source_candidate_denominator_not_authoritative");
        }
        var milestoneNodeId = SettledMilestoneNodeId(direction);
        var sourceCandidates = SourceCandidates(source);
        var alternatives = new List<MoneyRevenueAlternativeSkeleton>();
        foreach (var sourceCandidate in sourceCandidates)
        {
            var paths = FindPaths(
                direction,
                sourceCandidate.Node.NodeId,
                milestoneNodeId);
            if (paths.Length == 0)
            {
                currentBlockers.Add(
                    "source_candidate_has_no_settlement_path:" +
                    sourceCandidate.Candidate.CatalogCandidateId);
                continue;
            }
            foreach (var path in paths)
            {
                var skeleton = BuildSkeleton(
                    source,
                    direction,
                    sourceCandidate.Node,
                    sourceCandidate.Candidate,
                    path,
                    currentBlockers);
                if (skeleton is not null)
                    alternatives.Add(skeleton);
            }
        }

        var ordered = alternatives
            .OrderBy(row => row.AlternativeId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Select(row => row.AlternativeId)
            .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            currentBlockers.Add("alternative_id_not_unique");
        }
        if (sourceCandidates.Any(sourceCandidate => !ordered.Any(row =>
                row.SourceCatalogCandidateId ==
                    sourceCandidate.Candidate.CatalogCandidateId)))
        {
            currentBlockers.Add("source_candidate_not_expanded");
        }
        if (ordered.Length < 2)
            currentBlockers.Add("fewer_than_two_revenue_alternatives");

        var skeletonComplete = currentBlockers.Count == 0;
        var incompleteConcreteIdentityCount = ordered.Count(row =>
            !row.ConcreteAlternativeIdentityComplete);
        var currentAuthoritative = skeletonComplete &&
            incompleteConcreteIdentityCount == 0;
        var longHorizonAuthoritative = currentAuthoritative &&
            source.LongHorizonDenominatorAuthoritative;
        var blockers = currentBlockers
            .Concat(incompleteConcreteIdentityCount > 0
                ? new[]
                {
                    "fresh_terminal_candidate_rebind_required:" +
                    incompleteConcreteIdentityCount
                }
                : Array.Empty<string>())
            .Concat(source.DependencyExpansionBlockers.Select(blocker =>
                "long_horizon_dependency_blocker:" + blocker))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var catalog = new MoneyRevenueAlternativeDenominatorCatalog
        {
            Status = currentAuthoritative
                ? "current_alternative_denominator_authoritative"
                : skeletonComplete
                    ? "alternative_skeleton_complete_pending_terminal_rebind"
                    : "blocked_incomplete_alternative_skeleton",
            SourceStateHash = source.SourceStateHash,
            SourceCandidateCatalogPath = sourceCandidateCatalogPath,
            SourceCandidateCatalogFileSha256 =
                sourceCandidateCatalogFileSha256,
            SourceCandidateCatalogSha256 = source.CatalogSha256,
            DependencyExpansionSha256 = source.DependencyExpansionSha256,
            SettledMilestoneNodeId = milestoneNodeId,
            SourceCandidateDenominatorAuthoritative =
                source.CurrentSnapshotDenominatorAuthoritative,
            AlternativeSkeletonDenominatorComplete = skeletonComplete,
            CurrentAlternativeDenominatorAuthoritative =
                currentAuthoritative,
            LongHorizonDenominatorAuthoritative =
                longHorizonAuthoritative,
            ProductionTeacherPreferenceLabelEligible = false,
            FormalProductTrainingAuthorized = false,
            SourceCandidateCount = sourceCandidates.Length,
            AlternativeCount = ordered.Length,
            AlternativeIds = ordered.Select(row => row.AlternativeId).ToArray(),
            Alternatives = ordered,
            BlockingReasons = blockers
        };
        catalog.CatalogSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                catalog);
        return catalog;
    }

    private static SourceCandidate[] SourceCandidates(
        MoneyRevenueCandidateDenominatorCatalog source)
    {
        var rows = source.Nodes
            .Where(node => node.PolicyChoice)
            .SelectMany(node => node.Candidates
                .Where(candidate => candidate.ComparisonEligible)
                .Select(candidate => new SourceCandidate(node, candidate)))
            .OrderBy(row => row.Candidate.CatalogCandidateId,
                StringComparer.Ordinal)
            .ToArray();
        Require(rows.Length == source.ComparisonCandidateCount &&
                rows.Select(row => row.Candidate.CatalogCandidateId)
                    .SequenceEqual(
                        source.ComparisonCandidateIds,
                        StringComparer.Ordinal),
            "Money Revenue source candidate IDs drifted.");
        return rows;
    }

    private static bool EqualJson(object left, object right) =>
        JsonSerializer.Serialize(left, JsonDefaults.Options) ==
        JsonSerializer.Serialize(right, JsonDefaults.Options);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record SourceCandidate(
        MoneyRevenueCandidateNodeCoverage Node,
        MoneyRevenueCandidateCatalogEntry Candidate);
}
