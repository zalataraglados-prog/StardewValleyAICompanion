using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentStaticCompilabilityInventoryBuilder
{
    private static readonly HashSet<string> ReadyCompilerStatuses =
        new(StringComparer.Ordinal)
        {
            "StepCompilerDeclared",
            "ParameterCompilerDeclared",
            "StepAndParameterCompilerDeclared"
        };

    private static readonly HashSet<string> ReadyRuntimeBindings =
        new(StringComparer.Ordinal)
        {
            "internal_execution_pipeline",
            "product_executor"
        };

    public static FullShipmentStaticCompilabilityInventoryReport Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string actionReconciliationPath,
        string supportTerminalCoveragePath)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var reconciliationFullPath = Path.GetFullPath(actionReconciliationPath);
        var supportCoverageFullPath = Path.GetFullPath(
            supportTerminalCoveragePath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryFullPath,
            "Full Shipment authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringFullPath,
            "Full Shipment acquisition lowering");
        var supportCoverage = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionTerminalCoverageReport>(
            supportCoverageFullPath,
            "Supporting-transition terminal coverage");
        var options = ReadActionReconciliation(reconciliationFullPath);

        Require(inventory.SchemaVersion ==
                    "authoritative_goal_requirement_inventory.v1" &&
                inventory.DenominatorComplete &&
                inventory.AcquisitionRoutesComplete,
            "Full Shipment authority inventory is incomplete.");
        Require(lowering.SchemaVersion ==
                    "acquisition_route_option_lowering.v1" &&
                lowering.Status == "complete" &&
                lowering.RequirementInventorySha256 ==
                    CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
            "Full Shipment acquisition lowering is stale or incomplete.");
        ValidateLoweringSummary(lowering);
        ValidateSupportCoverage(supportCoverage);

        var inventorySet = SingleFullShipmentSet(inventory.RequirementSets);
        var loweringSet = SingleFullShipmentSet(lowering.RequirementSets);
        Require(inventorySet.RequiredGroupCount == 154 &&
                inventorySet.Groups.Length == 154 &&
                inventorySet.RouteCoveredGroupCount == 154 &&
                inventorySet.AcquisitionRoutesComplete &&
                inventory.UnresolvedAcquisitionRequirementIds.Length == 0 &&
                inventorySet.Groups.Select(group => group.RequirementId)
                    .Distinct(StringComparer.Ordinal).Count() == 154 &&
                inventorySet.Groups.All(group =>
                    group.SelectionRule == "all_required" &&
                    group.RequiredAlternativeCount == 1 &&
                    group.RouteCovered &&
                    group.Alternatives.Length == 1 &&
                    group.Alternatives[0].AcquisitionRoutes.Length > 0) &&
                inventorySet.Groups.Select(group =>
                        group.Alternatives[0].QualifiedItemId)
                    .Distinct(StringComparer.Ordinal).Count() == 154,
            "Full Shipment denominator is not the exact authoritative 154 groups.");
        Require(loweringSet.RequiredGroupCount == 154 &&
                loweringSet.Groups.Length == 154 &&
                loweringSet.RuntimeAdmittedGroupCount == 154 &&
                loweringSet.TeacherAdmittedGroupCount == 154,
            "Full Shipment lowering does not admit all 154 groups.");

        var coveredSupportKinds = supportCoverage.CoveredSupportTransitionKinds
            .ToHashSet(StringComparer.Ordinal);
        var routes = BuildRoutes(
            inventorySet,
            loweringSet,
            options,
            coveredSupportKinds);
        var optionRows = BuildOptionRows(routes, options);
        var sourceRows = routes
            .GroupBy(route => route.RouteKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var contract = AcquisitionRouteDispatchCompilationBuilder
                    .DescribeAuthoritativeSourceContract(group.Key);
                return new FullShipmentSourceContractInventoryRow(
                    group.Key,
                    contract?.EvidenceMode ?? string.Empty,
                    contract?.EvidenceFields ?? Array.Empty<string>(),
                    group.Count(),
                    group.Select(route => route.SourceId)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    contract is not null && group.All(route =>
                        route.SourceIdentityReady));
            })
            .ToArray();

        var sourceComplete = routes.All(route => route.SourceIdentityReady);
        var endpointsComplete = routes.All(route => route.EndpointOptionsReady);
        var supportingComplete = routes.All(route =>
            route.SupportingOptionsReady);
        var lineageComplete = supportCoverage.TerminalLineageCoverageComplete &&
            routes.All(route => route.InlineSupportLineageReady);
        var groupReady = routes
            .GroupBy(route => route.RequirementId, StringComparer.Ordinal)
            .Count(group => group.Any(route => route.StaticCompilationReady)) ==
                inventorySet.RequiredGroupCount;
        var staticComplete = sourceComplete && endpointsComplete &&
            supportingComplete && lineageComplete && groupReady &&
            optionRows.All(row => row.StaticCompilationReady);
        var runtimeSampleStrata = BuildRuntimeSampleStrata(routes);
        var blockers = routes
            .SelectMany(route => route.BlockingReasons.Select(reason =>
                route.RouteOccurrenceId + ":" + reason))
            .Concat(optionRows.Where(row => !row.StaticCompilationReady)
                .Select(row => row.OptionId + ":option_not_static_compilation_ready"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new FullShipmentStaticCompilabilityInventoryReport
        {
            Status = staticComplete
                ? "complete_static_compilability_inventory"
                : "blocked_incomplete_static_compilability_inventory",
            RequirementInventorySha256 =
                CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
            AcquisitionLoweringSha256 =
                CurrentTeacherFrontierSupport.HashFile(loweringFullPath),
            ActionReconciliationSha256 =
                CurrentTeacherFrontierSupport.HashFile(reconciliationFullPath),
            SupportTerminalCoverageSha256 =
                CurrentTeacherFrontierSupport.HashFile(supportCoverageFullPath),
            RequiredGroupCount = inventorySet.RequiredGroupCount,
            RouteOccurrenceCount = routes.Length,
            RouteKindCount = sourceRows.Length,
            AuthoritativeSourceIdentityCount = routes
                .Select(route => route.RouteKind + "|" + route.SourceId)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            ReferencedOptionCount = optionRows.Length,
            SourceIdentityContractComplete = sourceComplete,
            EndpointOptionCompilationComplete = endpointsComplete,
            SupportingOptionCompilationComplete = supportingComplete,
            SupportTerminalLineageComplete = lineageComplete,
            AllRequirementGroupsHaveCompilableRoute = groupReady,
            StaticCompilabilityComplete = staticComplete,
            FreshSaveRecurrenceEvidenceComplete = false,
            RuntimeSampleStratumCount = runtimeSampleStrata.Length,
            RuntimeSampleEvidenceComplete = false,
            FullRecurrenceRequiredForTraining = false,
            FullRecurrenceRetainedForAcceptance = true,
            FormalProductTrainingAuthorized = false,
            SourceContracts = sourceRows,
            Options = optionRows,
            Routes = routes,
            RuntimeSampleStrata = runtimeSampleStrata,
            BlockingReasons = blockers,
            RemainingEvidenceGaps = new[]
            {
                "stratified_full_shipment_runtime_samples_not_supplied"
            }
        };
    }

    internal static FullShipmentRuntimeSampleStratum[] BuildRuntimeSampleStrata(
        IReadOnlyCollection<FullShipmentRouteCompilabilityRow> routes) =>
        routes
            .GroupBy(RuntimeSampleSignature, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select((group, index) =>
            {
                var rows = group
                    .OrderBy(row => row.RequirementId, StringComparer.Ordinal)
                    .ThenBy(row => row.QualifiedItemId, StringComparer.Ordinal)
                    .ThenBy(row => row.SourceId, StringComparer.Ordinal)
                    .ToArray();
                var sample = rows[0];
                return new FullShipmentRuntimeSampleStratum(
                    "full_shipment.runtime_sample." +
                        (index + 1).ToString("D2") + "." + sample.RouteKind,
                    sample.RouteKind,
                    sample.SourceEvidenceMode,
                    sample.EndpointOptionIds,
                    sample.SupportingOptionIds,
                    sample.InlineSupportTransitionKinds,
                    rows.Length,
                    sample.RouteOccurrenceId,
                    sample.RequirementId,
                    sample.QualifiedItemId);
            })
            .ToArray();

    private static string RuntimeSampleSignature(
        FullShipmentRouteCompilabilityRow route) =>
        route.RouteKind + "|" + route.SourceEvidenceMode + "|" +
        string.Join(",", route.EndpointOptionIds.Order(StringComparer.Ordinal)) +
        "|" +
        string.Join(",", route.SupportingOptionIds.Order(StringComparer.Ordinal)) +
        "|" +
        string.Join(",", route.InlineSupportTransitionKinds.Order(
            StringComparer.Ordinal));

    private static FullShipmentRouteCompilabilityRow[] BuildRoutes(
        GoalRequirementSet inventorySet,
        AcquisitionRequirementSetLowering loweringSet,
        IReadOnlyDictionary<string, ActionReconciliationOption> options,
        IReadOnlySet<string> coveredSupportKinds)
    {
        var result = new List<FullShipmentRouteCompilabilityRow>();
        for (var groupIndex = 0; groupIndex < inventorySet.Groups.Length;
             groupIndex++)
        {
            var inventoryGroup = inventorySet.Groups[groupIndex];
            var loweringGroup = loweringSet.Groups[groupIndex];
            Require(inventoryGroup.RequirementId == loweringGroup.RequirementId &&
                    inventoryGroup.RequiredAlternativeCount ==
                        loweringGroup.RequiredAlternativeCount &&
                    inventoryGroup.Alternatives.Length ==
                        loweringGroup.Alternatives.Length,
                "Full Shipment lowering group identity drifted: " +
                inventoryGroup.RequirementId);
            for (var alternativeIndex = 0;
                 alternativeIndex < inventoryGroup.Alternatives.Length;
                 alternativeIndex++)
            {
                var inventoryAlternative =
                    inventoryGroup.Alternatives[alternativeIndex];
                var loweringAlternative =
                    loweringGroup.Alternatives[alternativeIndex];
                Require(inventoryAlternative.ItemId ==
                            loweringAlternative.ItemId &&
                        inventoryAlternative.QualifiedItemId ==
                            loweringAlternative.QualifiedItemId &&
                        inventoryAlternative.AcquisitionRoutes.Length ==
                            loweringAlternative.Routes.Length,
                    "Full Shipment lowering alternative identity drifted: " +
                    inventoryGroup.RequirementId);
                for (var routeIndex = 0;
                     routeIndex < inventoryAlternative.AcquisitionRoutes.Length;
                     routeIndex++)
                {
                    var inventoryRoute =
                        inventoryAlternative.AcquisitionRoutes[routeIndex];
                    var route = loweringAlternative.Routes[routeIndex];
                    Require(inventoryRoute.Kind == route.RouteKind &&
                            inventoryRoute.SourceId == route.SourceId &&
                            inventoryRoute.SourceAsset == route.SourceAsset &&
                            inventoryRoute.SourcePath == route.SourcePath,
                        "Full Shipment lowering route identity drifted: " +
                        inventoryGroup.RequirementId);
                    result.Add(BuildRoute(
                        inventoryGroup.RequirementId,
                        alternativeIndex,
                        routeIndex,
                        loweringAlternative.QualifiedItemId,
                        route,
                        options,
                        coveredSupportKinds));
                }
            }
        }
        return result.ToArray();
    }

    private static FullShipmentRouteCompilabilityRow BuildRoute(
        string requirementId,
        int alternativeIndex,
        int routeIndex,
        string qualifiedItemId,
        AcquisitionRequirementRouteLowering route,
        IReadOnlyDictionary<string, ActionReconciliationOption> options,
        IReadOnlySet<string> coveredSupportKinds)
    {
        var sourceReady = AcquisitionRouteDispatchCompilationBuilder
            .CanRepresentAuthoritativeSource(
                route.RouteKind,
                route.SourceId,
                qualifiedItemId,
                out var sourceContract,
                out var sourceReason);
        var endpointsReady = route.EndpointOptionIds.Length > 0 &&
            route.EndpointOptionIds.Distinct(StringComparer.Ordinal).Count() ==
                route.EndpointOptionIds.Length &&
            route.EndpointOptionIds.All(optionId =>
                options.TryGetValue(optionId, out var option) &&
                option.StaticCompilationReady);
        var supportingReady =
            route.SupportingOptionIds.Distinct(StringComparer.Ordinal).Count() ==
                route.SupportingOptionIds.Length &&
            route.SupportingOptionIds.All(optionId =>
                options.TryGetValue(optionId, out var option) &&
                option.StaticCompilationReady);
        var inlineKinds = InlineSupportKinds(route.RouteKind);
        var inlineReady = inlineKinds.All(coveredSupportKinds.Contains);
        var blockers = new List<string>();
        if (!sourceReady)
            blockers.Add(sourceReason);
        if (!endpointsReady)
            blockers.Add("endpoint_option_not_static_compilation_ready");
        if (!supportingReady)
            blockers.Add("supporting_option_not_static_compilation_ready");
        if (!inlineReady)
            blockers.Add("inline_support_terminal_lineage_missing");
        if (!route.RuntimeAdmissionReady || !route.TeacherAdmissionReady)
            blockers.Add("route_lowering_not_admitted");

        return new FullShipmentRouteCompilabilityRow(
            string.Join(
                ":",
                "full_shipment",
                requirementId,
                alternativeIndex,
                routeIndex),
            requirementId,
            qualifiedItemId,
            route.RouteKind,
            route.SourceId,
            sourceContract?.EvidenceMode ?? string.Empty,
            route.EndpointOptionIds.ToArray(),
            route.SupportingOptionIds.ToArray(),
            inlineKinds,
            sourceReady,
            endpointsReady,
            supportingReady,
            inlineReady,
            sourceReady && endpointsReady && supportingReady && inlineReady &&
                route.RuntimeAdmissionReady && route.TeacherAdmissionReady,
            blockers.ToArray());
    }

    private static FullShipmentOptionCompilabilityRow[] BuildOptionRows(
        IReadOnlyCollection<FullShipmentRouteCompilabilityRow> routes,
        IReadOnlyDictionary<string, ActionReconciliationOption> options)
    {
        var optionIds = routes
            .SelectMany(route => route.EndpointOptionIds.Concat(
                route.SupportingOptionIds))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return optionIds.Select(optionId =>
        {
            var option = options.GetValueOrDefault(optionId) ??
                ActionReconciliationOption.Missing(optionId);
            var endpointCount = routes.Count(route =>
                route.EndpointOptionIds.Contains(optionId, StringComparer.Ordinal));
            var supportingCount = routes.Count(route =>
                route.SupportingOptionIds.Contains(optionId, StringComparer.Ordinal));
            var roles = new List<string>();
            if (endpointCount > 0)
                roles.Add("endpoint");
            if (supportingCount > 0)
                roles.Add("supporting");
            return new FullShipmentOptionCompilabilityRow(
                optionId,
                roles.ToArray(),
                endpointCount,
                supportingCount,
                option.RegistrationStatus,
                option.ReadStatus,
                option.CandidateStatus,
                option.CompilerStatus,
                option.RuntimeStatus,
                option.RuntimeBinding,
                option.StaticCompilationReady);
        }).ToArray();
    }

    private static string[] InlineSupportKinds(string routeKind) =>
        routeKind switch
        {
            "harvests_as" => new[]
            {
                AcquisitionRouteSupportingTransitionKinds.CropPlanting
            },
            "machine_output" or
            "native_machine_flavored_output" or
            "native_machine_item_query_output" => new[]
            {
                AcquisitionRouteSupportingTransitionKinds.MachineInputLoad,
                AcquisitionRouteSupportingTransitionKinds
                    .MachineInputMaterialTransfer,
                AcquisitionRouteSupportingTransitionKinds.MachineInputPurchase,
                AcquisitionRouteSupportingTransitionKinds
                    .MachineCapacityEstablishment
            },
            _ => Array.Empty<string>()
        };

    private static Dictionary<string, ActionReconciliationOption>
        ReadActionReconciliation(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(ReadString(root, "schema_version") ==
                    "stardewai.action_implementation_reconciliation.v1",
            "Unsupported action reconciliation schema.");
        var options = root.GetProperty("options").EnumerateArray()
            .Select(value => new ActionReconciliationOption(
                ReadRequiredString(value, "optionId"),
                ReadRequiredString(value, "registrationStatus"),
                ReadRequiredString(value, "readStatus"),
                ReadRequiredString(value, "candidateStatus"),
                ReadRequiredString(value, "compilerStatus"),
                ReadRequiredString(value, "runtimeStatus"),
                ReadRequiredString(value, "runtimeBinding")))
            .ToArray();
        Require(options.Select(option => option.OptionId)
                    .Distinct(StringComparer.Ordinal).Count() == options.Length &&
                options.Length == root.GetProperty("registered_option_count")
                    .GetInt32(),
            "Action reconciliation option inventory drifted.");
        return options.ToDictionary(option => option.OptionId,
            StringComparer.Ordinal);
    }

    private static void ValidateSupportCoverage(
        AcquisitionRouteSupportingTransitionTerminalCoverageReport report)
    {
        var rebuilt = AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
            .BuildReport(report.Rows);
        Require(report.SchemaVersion ==
                    "acquisition_route_supporting_transition_terminal_coverage.v1" &&
                report.TerminalLineageCoverageComplete &&
                !report.FormalProductTrainingAuthorized &&
                rebuilt.TerminalLineageCoverageComplete &&
                rebuilt.CoveredSupportTransitionKinds.SequenceEqual(
                    AcquisitionRouteSupportingTransitionKinds.All,
                    StringComparer.Ordinal) &&
                rebuilt.BlockingReasons.Length == 0,
            "Supporting-transition terminal lineage is incomplete.");
    }

    private static void ValidateLoweringSummary(
        AcquisitionRouteOptionLoweringReport report)
    {
        var groups = report.RequirementSets
            .SelectMany(set => set.Groups)
            .ToArray();
        var routes = groups
            .SelectMany(group => group.Alternatives)
            .SelectMany(alternative => alternative.Routes)
            .ToArray();
        var routeKinds = routes
            .Select(route => route.RouteKind)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Require(report.RequirementSetCount == report.RequirementSets.Length &&
                report.RequirementSets.Select(set => set.RequirementSetId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    report.RequirementSets.Length &&
                report.RequirementGroupCount == groups.Length &&
                report.RouteOccurrenceCount == routes.Length &&
                report.DependencyAxisInventoryComplete &&
                StageOneCollectionRouteDependencyAxes.IsComplete(
                    report.RequiredDownstreamDependencyAxes) &&
                routes.All(route =>
                    StageOneCollectionRouteDependencyAxes.IsComplete(
                        route.RequiredDownstreamDependencyAxes)) &&
                report.ObservedRouteKindCount == routeKinds.Length &&
                report.ClassifiedRouteKindCount == routeKinds.Length &&
                report.AdmittedRouteKindCount == routeKinds.Length &&
                report.BlockedRouteKindCount == 0 &&
                report.UnknownRouteKinds.Length == 0 &&
                routeKinds.All(routeKind =>
                    AcquisitionRouteDispatchCompilationBuilder
                        .DescribeAuthoritativeSourceContract(routeKind) is not
                        null),
            "Acquisition lowering summary or dependency-axis inventory drifted.");
    }

    private static GoalRequirementSet SingleFullShipmentSet(
        IEnumerable<GoalRequirementSet> sets) => sets.Single(set =>
            set.RequirementSetId == "full_shipment");

    private static AcquisitionRequirementSetLowering SingleFullShipmentSet(
        IEnumerable<AcquisitionRequirementSetLowering> sets) => sets.Single(
            set => set.RequirementSetId == "full_shipment");

    private static string ReadRequiredString(JsonElement source, string name)
    {
        var value = ReadString(source, name);
        Require(!string.IsNullOrWhiteSpace(value),
            "Action reconciliation field is empty: " + name);
        return value;
    }

    private static string ReadString(JsonElement source, string name) =>
        source.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record ActionReconciliationOption(
        string OptionId,
        string RegistrationStatus,
        string ReadStatus,
        string CandidateStatus,
        string CompilerStatus,
        string RuntimeStatus,
        string RuntimeBinding)
    {
        public bool StaticCompilationReady =>
            RegistrationStatus == "Registered" &&
            ReadStatus == "RequiredFactContractDeclared" &&
            CandidateStatus == "Declared" &&
            ReadyCompilerStatuses.Contains(CompilerStatus) &&
            RuntimeStatus == "RuntimeVerified" &&
            ReadyRuntimeBindings.Contains(RuntimeBinding);

        public static ActionReconciliationOption Missing(string optionId) =>
            new(
                optionId,
                "Missing",
                "Missing",
                "Missing",
                "Missing",
                "Missing",
                "Missing");
    }
}
