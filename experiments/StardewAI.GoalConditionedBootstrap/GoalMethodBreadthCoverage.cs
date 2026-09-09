using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodFrontierBuilder
{
    private static GoalMethodBreadthCoverage BuildBreadthCoverage(
        GoalCriterionFrontier[] criteria,
        IReadOnlyCollection<GoalMethodFrontierMethod> methods,
        IReadOnlyDictionary<string, DirectionExpansionOverlay> overlays,
        IReadOnlyDictionary<string, DirectionDependencyExpansion> dependencyExpansions,
        IReadOnlyDictionary<string, OptionRow> options,
        IReadOnlySet<string> isolatedTeacherAuthorizedOptionIds)
    {
        var methodById = methods.ToDictionary(value => value.MethodId, StringComparer.Ordinal);
        var criterionRows = criteria.Select(criterion =>
        {
            Require(criterion.MethodIds.Length == 1,
                "Breadth classification requires exactly one root method for criterion " +
                criterion.CriterionId + ".");
            var method = methodById[criterion.MethodIds[0]];
            dependencyExpansions.TryGetValue(method.DirectionId, out var dependencyExpansion);
            var routeClass = method.Status == "executable_frontier"
                ? "executable_route"
                : method.Status == "blocked_by_option_governance"
                    ? "governance_blocked"
                    : dependencyExpansion is null
                        ? "missing_dependency_graph"
                        : "dependency_graph_pending";
            var blockerIds = new List<string>();
            if (routeClass == "governance_blocked")
            {
                blockerIds.AddRange(method.OptionBlockers.Select(blocker =>
                    "blocker.option_governance." + StableId(blocker.OptionId)));
            }
            if (routeClass == "missing_dependency_graph")
                blockerIds.Add("blocker.missing_dependency_graph." + method.DirectionId);
            if (routeClass == "dependency_graph_pending")
                blockerIds.Add("blocker.dependency_expansion." + method.DirectionId);
            return new GoalCriterionBreadthClassification(
                criterion.CriterionId,
                criterion.Points,
                method.DirectionId,
                method.MethodId,
                routeClass,
                blockerIds.Distinct(StringComparer.Ordinal).ToArray());
        }).ToArray();

        var blockerClusters = new List<GoalMethodTypedBlockerCluster>();
        foreach (var method in methods.OrderBy(value => value.DirectionId, StringComparer.Ordinal))
        {
            dependencyExpansions.TryGetValue(method.DirectionId, out var dependencyExpansion);
            var criterionIds = method.CriterionIds.Order(StringComparer.Ordinal).ToArray();
            if (method.Status == "pending_dependency_expansion" && dependencyExpansion is null)
            {
                blockerClusters.Add(new GoalMethodTypedBlockerCluster(
                    "blocker.missing_dependency_graph." + method.DirectionId,
                    "missing_dependency_graph",
                    new[] { method.DirectionId },
                    criterionIds,
                    Array.Empty<string>(),
                    overlays[method.DirectionId].UnexpandedRequirements));
            }
            else if (method.Status == "pending_dependency_expansion" &&
                     dependencyExpansion is not null)
            {
                var details = dependencyExpansion.Blockers.Length > 0
                    ? dependencyExpansion.Blockers
                    : overlays[method.DirectionId].UnexpandedRequirements;
                blockerClusters.Add(new GoalMethodTypedBlockerCluster(
                    "blocker.dependency_expansion." + method.DirectionId,
                    "dependency_expansion_pending",
                    new[] { method.DirectionId },
                    criterionIds,
                    Array.Empty<string>(),
                    details));
            }
        }

        foreach (var group in methods
                     .Where(method => method.Status == "blocked_by_option_governance")
                     .SelectMany(method => method.OptionBlockers.Select(blocker => (method, blocker)))
                     .GroupBy(value => value.blocker.OptionId, StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            blockerClusters.Add(new GoalMethodTypedBlockerCluster(
                "blocker.option_governance." + StableId(group.Key),
                "option_governance",
                group.Select(value => value.method.DirectionId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                group.SelectMany(value => value.method.CriterionIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                new[] { group.Key },
                group.SelectMany(value => value.blocker.ExclusionReasons)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
        }

        var sharedOptionDependencies = dependencyExpansions.Values
            .SelectMany(expansion => expansion.Nodes
                .Where(node => node.Kind is "policy_option_transition" or "deterministic_transition")
                .Where(node => !string.IsNullOrWhiteSpace(node.OptionId))
                .Select(node => (expansion.DirectionId, node.Kind, node.OptionId)))
            .GroupBy(value => (value.Kind, value.OptionId))
            .Select(group =>
            {
                var directionIds = group.Select(value => value.DirectionId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                var option = options[group.Key.OptionId];
                var readiness = group.Key.Kind == "policy_option_transition"
                    ? (option.TrainingEligibility == "Eligible" ||
                       isolatedTeacherAuthorizedOptionIds.Contains(option.OptionId))
                        ? "ready"
                        : "blocked_by_option_governance"
                    : option.RuntimeStatus is "RuntimeVerified" or "LongDurationVerified"
                        ? "ready"
                        : "blocked_by_runtime_verification";
                return new GoalMethodSharedDependencyCluster(
                    "dependency.shared." + group.Key.Kind + "." + StableId(group.Key.OptionId),
                    group.Key.Kind,
                    group.Key.OptionId,
                    group.Key.OptionId,
                    directionIds,
                    methods.Where(method => directionIds.Contains(method.DirectionId, StringComparer.Ordinal))
                        .SelectMany(method => method.CriterionIds)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    readiness);
            })
            .Where(value => value.DirectionIds.Length > 1)
            .OrderBy(value => value.DependencyKind, StringComparer.Ordinal)
            .ThenBy(value => value.OptionId, StringComparer.Ordinal)
            .ToArray();
        var sharedDependencyFamilies = dependencyExpansions.Values
            .SelectMany(expansion => expansion.Nodes.Select(node =>
                (expansion.DirectionId, FamilyId: SharedDependencyFamily(node))))
            .Where(value => !string.IsNullOrWhiteSpace(value.FamilyId))
            .GroupBy(value => value.FamilyId, StringComparer.Ordinal)
            .Select(group =>
            {
                var directionIds = group.Select(value => value.DirectionId)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                return new GoalMethodSharedDependencyCluster(
                    "dependency.shared.family." + StableId(group.Key),
                    "shared_dependency_family",
                    group.Key,
                    string.Empty,
                    directionIds,
                    methods.Where(method => directionIds.Contains(method.DirectionId, StringComparer.Ordinal))
                        .SelectMany(method => method.CriterionIds)
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                    "expansion_pending");
            })
            .Where(value => value.DirectionIds.Length > 1)
            .ToArray();
        var sharedDependencies = sharedOptionDependencies
            .Concat(sharedDependencyFamilies)
            .OrderBy(value => value.DependencyKind, StringComparer.Ordinal)
            .ThenBy(value => value.DependencyId, StringComparer.Ordinal)
            .ToArray();

        Require(criterionRows.Length == criteria.Length,
            "Breadth classification did not cover every criterion.");
        Require(criterionRows.Select(value => value.CriterionId)
                .Distinct(StringComparer.Ordinal).Count() == criteria.Length,
            "Breadth classification contains duplicate criteria.");
        Require(criterionRows.All(value =>
                (value.BlockerIds.Length == 0) ==
                (value.RouteClass == "executable_route")),
            "Breadth classification blocker assignment is inconsistent.");

        return new GoalMethodBreadthCoverage
        {
            ClassifiedCriterionCount = criterionRows.Length,
            AllCriteriaClassified = criterionRows.Length == criteria.Length,
            ExecutableRouteCriterionCount = criterionRows.Count(value => value.RouteClass == "executable_route"),
            DependencyGraphPendingCriterionCount = criterionRows.Count(value => value.RouteClass == "dependency_graph_pending"),
            MissingDependencyGraphCriterionCount = criterionRows.Count(value => value.RouteClass == "missing_dependency_graph"),
            GovernanceBlockedCriterionCount = criterionRows.Count(value => value.RouteClass == "governance_blocked"),
            Criteria = criterionRows,
            BlockerClusters = blockerClusters.ToArray(),
            SharedDependencyClusters = sharedDependencies
        };
    }

    private static string SharedDependencyFamily(DirectionDependencyNode node)
    {
        if (!node.Attributes.TryGetValue("shared_dependency_family", out var value) ||
            value is not JsonElement element ||
            element.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }
        return element.GetString()?.Trim() ?? string.Empty;
    }
}

public sealed class GoalMethodBreadthCoverage
{
    [JsonPropertyName("classified_criterion_count")]
    public int ClassifiedCriterionCount { get; set; }
    [JsonPropertyName("all_criteria_classified")]
    public bool AllCriteriaClassified { get; set; }
    [JsonPropertyName("executable_route_criterion_count")]
    public int ExecutableRouteCriterionCount { get; set; }
    [JsonPropertyName("dependency_graph_pending_criterion_count")]
    public int DependencyGraphPendingCriterionCount { get; set; }
    [JsonPropertyName("missing_dependency_graph_criterion_count")]
    public int MissingDependencyGraphCriterionCount { get; set; }
    [JsonPropertyName("governance_blocked_criterion_count")]
    public int GovernanceBlockedCriterionCount { get; set; }
    [JsonPropertyName("criteria")]
    public GoalCriterionBreadthClassification[] Criteria { get; set; } =
        Array.Empty<GoalCriterionBreadthClassification>();
    [JsonPropertyName("blocker_clusters")]
    public GoalMethodTypedBlockerCluster[] BlockerClusters { get; set; } =
        Array.Empty<GoalMethodTypedBlockerCluster>();
    [JsonPropertyName("shared_dependency_clusters")]
    public GoalMethodSharedDependencyCluster[] SharedDependencyClusters { get; set; } =
        Array.Empty<GoalMethodSharedDependencyCluster>();
}

public sealed record GoalCriterionBreadthClassification(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("direction_id")] string DirectionId,
    [property: JsonPropertyName("method_id")] string MethodId,
    [property: JsonPropertyName("route_class")] string RouteClass,
    [property: JsonPropertyName("blocker_ids")] string[] BlockerIds);

public sealed record GoalMethodTypedBlockerCluster(
    [property: JsonPropertyName("blocker_id")] string BlockerId,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("direction_ids")] string[] DirectionIds,
    [property: JsonPropertyName("criterion_ids")] string[] CriterionIds,
    [property: JsonPropertyName("option_ids")] string[] OptionIds,
    [property: JsonPropertyName("details")] string[] Details);

public sealed record GoalMethodSharedDependencyCluster(
    [property: JsonPropertyName("cluster_id")] string ClusterId,
    [property: JsonPropertyName("dependency_kind")] string DependencyKind,
    [property: JsonPropertyName("dependency_id")] string DependencyId,
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("direction_ids")] string[] DirectionIds,
    [property: JsonPropertyName("criterion_ids")] string[] CriterionIds,
    [property: JsonPropertyName("readiness")] string Readiness);
