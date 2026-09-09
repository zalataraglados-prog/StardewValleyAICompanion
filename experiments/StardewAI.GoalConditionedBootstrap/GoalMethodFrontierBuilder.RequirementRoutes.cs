using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodFrontierBuilder
{
    private static readonly IReadOnlyDictionary<string, string> DirectionByRequirementSet =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["full_shipment"] = "complete_full_shipment",
            ["master_angler"] = "complete_master_angler",
            ["museum_collection"] = "complete_museum_collection",
            ["community_center_standard"] = "complete_community_center"
        };

    private static GoalMethodRequirementSetReadiness[] BuildRequirementSetReadiness(
        string directionId,
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRouteOptionLoweringReport acquisitionLowering)
    {
        var setId = DirectionByRequirementSet.SingleOrDefault(pair =>
            pair.Value == directionId).Key;
        if (string.IsNullOrEmpty(setId))
            return Array.Empty<GoalMethodRequirementSetReadiness>();

        var set = inventory.RequirementSets.Single(value =>
            value.RequirementSetId == setId);
        var lowering = acquisitionLowering.RequirementSets.Single(value =>
            value.RequirementSetId == setId);
        return new[]
        {
            new GoalMethodRequirementSetReadiness(
                setId,
                set.RequiredGroupCount,
                set.RouteCoveredGroupCount,
                lowering.RuntimeAdmittedGroupCount,
                lowering.TeacherAdmittedGroupCount,
                set.AcquisitionRoutesComplete &&
                lowering.TeacherAdmittedGroupCount == set.RequiredGroupCount)
        };
    }

    private static void AddRequirementInventoryGraph(
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRouteOptionLoweringReport acquisitionLowering,
        IReadOnlyList<GrandpaDirectionCatalogEntry> catalogEntries,
        ICollection<GoalMethodHypergraphNode> nodes,
        ICollection<GoalMethodHypergraphEdge> edges)
    {
        var loweringSets = acquisitionLowering.RequirementSets
            .ToDictionary(set => set.RequirementSetId, StringComparer.Ordinal);
        foreach (var set in inventory.RequirementSets)
        {
            Require(DirectionByRequirementSet.TryGetValue(
                    set.RequirementSetId,
                    out var directionId),
                "Requirement inventory contains an unknown set: " + set.RequirementSetId);
            var methodId = catalogEntries.Single(entry =>
                entry.DirectionId == directionId).BindingRuleId;
            var loweringSet = loweringSets[set.RequirementSetId];
            var setNodeId = "requirement_set:" + set.RequirementSetId;
            nodes.Add(new(setNodeId, "authoritative_requirement_set", new Dictionary<string, object?>
            {
                ["criterion_id"] = set.CriterionId,
                ["required_group_count"] = set.RequiredGroupCount,
                ["route_covered_group_count"] = set.RouteCoveredGroupCount,
                ["acquisition_routes_complete"] = set.AcquisitionRoutesComplete,
                ["runtime_admitted_group_count"] = loweringSet.RuntimeAdmittedGroupCount,
                ["teacher_admitted_group_count"] = loweringSet.TeacherAdmittedGroupCount,
                ["transparent_state_path"] = set.TransparentStatePath
            }));
            edges.Add(new(setNodeId, methodId, "defines_method_denominator"));
            foreach (var group in set.Groups)
                AddRequirementGroupGraph(setNodeId, group, loweringSet, nodes, edges);
        }
    }

    private static void AddRequirementGroupGraph(
        string setNodeId,
        GoalRequirementGroup group,
        AcquisitionRequirementSetLowering loweringSet,
        ICollection<GoalMethodHypergraphNode> nodes,
        ICollection<GoalMethodHypergraphEdge> edges)
    {
        var loweringGroup = loweringSet.Groups.Single(value =>
            value.RequirementId == group.RequirementId);
        nodes.Add(new(group.RequirementId, "authoritative_requirement", new Dictionary<string, object?>
        {
            ["selection_rule"] = group.SelectionRule,
            ["required_alternative_count"] = group.RequiredAlternativeCount,
            ["route_covered"] = group.RouteCovered,
            ["runtime_admission_ready"] = loweringGroup.RuntimeAdmissionReady,
            ["teacher_admission_ready"] = loweringGroup.TeacherAdmissionReady,
            ["transparent_completion_path"] = group.TransparentCompletionPath
        }));
        edges.Add(new(group.RequirementId, setNodeId, "belongs_to_requirement_set"));

        for (var index = 0; index < loweringGroup.Alternatives.Length; index++)
            AddRequirementAlternativeGraph(group.RequirementId, index,
                loweringGroup.Alternatives[index], nodes, edges);
    }

    private static void AddRequirementAlternativeGraph(
        string requirementId,
        int alternativeIndex,
        AcquisitionRequirementAlternativeLowering alternative,
        ICollection<GoalMethodHypergraphNode> nodes,
        ICollection<GoalMethodHypergraphEdge> edges)
    {
        var alternativeNodeId = requirementId + ":alternative:" +
            alternativeIndex.ToString("D2");
        nodes.Add(new(
            alternativeNodeId,
            "authoritative_requirement_alternative",
            new Dictionary<string, object?>
            {
                ["item_id"] = alternative.ItemId,
                ["qualified_item_id"] = alternative.QualifiedItemId,
                ["display_name"] = alternative.DisplayName,
                ["match_kind"] = alternative.MatchKind,
                ["amount"] = alternative.Amount,
                ["minimum_quality"] = alternative.MinimumQuality,
                ["runtime_admission_ready"] = alternative.RuntimeAdmissionReady,
                ["teacher_admission_ready"] = alternative.TeacherAdmissionReady
            }));
        edges.Add(new(alternativeNodeId, requirementId, "satisfies_requirement"));

        for (var index = 0; index < alternative.Routes.Length; index++)
            AddAcquisitionRouteGraph(alternativeNodeId, index,
                alternative.Routes[index], nodes, edges);
    }

    private static void AddAcquisitionRouteGraph(
        string alternativeNodeId,
        int routeIndex,
        AcquisitionRequirementRouteLowering route,
        ICollection<GoalMethodHypergraphNode> nodes,
        ICollection<GoalMethodHypergraphEdge> edges)
    {
        var routeNodeId = alternativeNodeId + ":route:" + routeIndex.ToString("D2");
        nodes.Add(new(
            routeNodeId,
            "authoritative_acquisition_route",
            new Dictionary<string, object?>
            {
                ["route_kind"] = route.RouteKind,
                ["source_id"] = route.SourceId,
                ["source_asset"] = route.SourceAsset,
                ["source_path"] = route.SourcePath,
                ["supervision_mode"] = route.SupervisionMode,
                ["uncertainty_mode"] = route.UncertaintyMode,
                ["endpoint_option_ids"] = route.EndpointOptionIds,
                ["supporting_option_ids"] = route.SupportingOptionIds,
                ["runtime_admission_ready"] = route.RuntimeAdmissionReady,
                ["teacher_admission_ready"] = route.TeacherAdmissionReady
            }));
        edges.Add(new(routeNodeId, alternativeNodeId, "acquires_alternative"));
        foreach (var optionId in route.EndpointOptionIds)
            edges.Add(new(optionId, routeNodeId, "lowers_acquisition_route"));
        foreach (var optionId in route.SupportingOptionIds)
            edges.Add(new(optionId, routeNodeId, "supports_acquisition_route"));
    }
}
