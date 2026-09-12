using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRouteOptionLoweringBuilder
{
    private static readonly HashSet<string> LoweringClasses = new(StringComparer.Ordinal)
    {
        "high_level_option", "primitive_gap", "missing_option"
    };

    private static readonly HashSet<string> SupervisionModes = new(StringComparer.Ordinal)
    {
        "policy_option", "deterministic_dependency", "isolated_teacher_option", "blocked_gap"
    };

    private static readonly HashSet<string> UncertaintyModes = new(StringComparer.Ordinal)
    {
        "deterministic_fresh_receipt", "native_outcome_domain_and_retry_bound"
    };

    public static AcquisitionRouteOptionLoweringReport Build(
        string requirementInventoryPath,
        string loweringCatalogPath,
        string optionMatrixPath,
        string isolatedTrainingAuthorizationPath)
    {
        var inventoryPath = Path.GetFullPath(requirementInventoryPath);
        var catalogPath = Path.GetFullPath(loweringCatalogPath);
        var matrixPath = Path.GetFullPath(optionMatrixPath);
        var authorizationPath = Path.GetFullPath(isolatedTrainingAuthorizationPath);
        var inventory = Read<AuthoritativeRequirementInventoryReport>(inventoryPath);
        var catalog = Read<AcquisitionRouteOptionLoweringCatalog>(catalogPath);

        Require(inventory.SchemaVersion == "authoritative_goal_requirement_inventory.v1",
            "Unsupported authoritative requirement inventory schema.");
        Require(inventory.DenominatorComplete && inventory.AcquisitionRoutesComplete,
            "Authoritative requirement inventory is incomplete.");
        Require(inventory.RequirementSets.Length == 4,
            "Acquisition lowering requires exactly four Stage 1 requirement sets.");
        Require(catalog.SchemaVersion == "acquisition_route_option_lowering_catalog.v1",
            "Unsupported acquisition route lowering catalog schema.");
        Require(catalog.GoalId == inventory.GoalId,
            "Acquisition route lowering catalog goal does not match the inventory.");
        Require(catalog.Scope == "terminal_acquisition_transition_only",
            "Acquisition route lowering catalog scope drifted.");

        foreach (var source in inventory.SourceEvidence)
        {
            Require(File.Exists(source.Path),
                "Requirement inventory source is missing: " + source.SourceId);
            Require(ContentInventoryVerifier.HashFile(source.Path) == source.Sha256,
                "Requirement inventory source hash drifted: " + source.SourceId);
        }

        var options = LoadOptions(matrixPath);
        var isolatedAuthorizations = LoadIsolatedAuthorizations(
            authorizationPath,
            inventory.GoalId,
            options);
        var usages = inventory.RequirementSets
            .SelectMany(set => set.Groups.SelectMany(group =>
                group.Alternatives.SelectMany(alternative =>
                    alternative.AcquisitionRoutes.Select(route =>
                        new RouteUsage(set.RequirementSetId, group.RequirementId, route)))))
            .ToArray();
        Require(usages.Length > 0, "Requirement inventory has no acquisition routes.");
        foreach (var usage in usages)
        {
            Require(!string.IsNullOrWhiteSpace(usage.Route.Kind),
                "Requirement inventory contains an empty route kind.");
            Require(!string.IsNullOrWhiteSpace(usage.Route.SourceId) &&
                    !string.IsNullOrWhiteSpace(usage.Route.SourceAsset) &&
                    !string.IsNullOrWhiteSpace(usage.Route.SourcePath),
                "Acquisition route source identity is incomplete: " + usage.Route.Kind);
        }

        Require(catalog.Routes.Select(row => row.RouteKind)
                .Distinct(StringComparer.Ordinal).Count() == catalog.Routes.Length,
            "Acquisition route lowering catalog contains duplicate route kinds.");
        var catalogRows = catalog.Routes.ToDictionary(row => row.RouteKind, StringComparer.Ordinal);
        foreach (var row in catalog.Routes)
            ValidateCatalogRow(row, options);

        var observedKinds = usages.Select(usage => usage.Route.Kind)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unknownKinds = observedKinds.Except(catalogRows.Keys, StringComparer.Ordinal).ToArray();
        var unobservedCatalogKinds = catalogRows.Keys.Except(observedKinds, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(unknownKinds.Length == 0,
            "Unknown authoritative acquisition route kinds fail closed: " +
            string.Join(",", unknownKinds));
        Require(unobservedCatalogKinds.Length == 0,
            "Lowering catalog contains route kinds absent from the authoritative inventory: " +
            string.Join(",", unobservedCatalogKinds));

        var routeRows = observedKinds.Select(kind =>
        {
            var row = catalogRows[kind];
            var matching = usages.Where(usage => usage.Route.Kind == kind).ToArray();
            return LowerRouteKind(row, matching, options, isolatedAuthorizations);
        }).ToArray();
        var routeRowsByKind = routeRows.ToDictionary(row => row.RouteKind, StringComparer.Ordinal);

        var requirementSets = inventory.RequirementSets.Select(set =>
        {
            var groups = set.Groups.Select(group =>
            {
                var alternatives = group.Alternatives.Select(alternative =>
                {
                    var routes = alternative.AcquisitionRoutes.Select(route =>
                    {
                        var lowering = routeRowsByKind[route.Kind];
                        return new AcquisitionRequirementRouteLowering(
                            route.Kind,
                            route.SourceId,
                            route.SourceAsset,
                            route.SourcePath,
                            lowering.SupervisionMode,
                            lowering.UncertaintyMode,
                            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
                            lowering.EndpointOptions.Select(option => option.OptionId).ToArray(),
                            lowering.SupportingOptions.Select(option => option.OptionId).ToArray(),
                            lowering.RuntimeAdmissionReady,
                            lowering.TeacherAdmissionReady);
                    }).ToArray();
                    return new AcquisitionRequirementAlternativeLowering(
                        alternative.ItemId,
                        alternative.QualifiedItemId,
                        alternative.DisplayName,
                        alternative.MatchKind,
                        alternative.Amount,
                        alternative.MinimumQuality,
                        routes.Any(route => route.RuntimeAdmissionReady),
                        routes.Any(route => route.TeacherAdmissionReady),
                        routes);
                }).ToArray();
                var runtimeAlternatives = alternatives.Count(alternative =>
                    alternative.RuntimeAdmissionReady);
                var teacherAlternatives = alternatives.Count(alternative =>
                    alternative.TeacherAdmissionReady);
                var blockedKinds = group.Alternatives
                    .SelectMany(alternative => alternative.AcquisitionRoutes)
                    .Select(route => route.Kind)
                    .Where(kind => !routeRowsByKind[kind].TeacherAdmissionReady)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray();
                return new AcquisitionRequirementGroupLowering(
                    group.RequirementId,
                    group.RequiredAlternativeCount,
                    runtimeAlternatives,
                    teacherAlternatives,
                    runtimeAlternatives >= group.RequiredAlternativeCount,
                    teacherAlternatives >= group.RequiredAlternativeCount,
                    blockedKinds,
                    alternatives);
            }).ToArray();
            return new AcquisitionRequirementSetLowering(
                set.RequirementSetId,
                set.RequiredGroupCount,
                groups.Count(group => group.RuntimeAdmissionReady),
                groups.Count(group => group.TeacherAdmissionReady),
                groups);
        }).ToArray();

        var dependencyAxisInventoryComplete = requirementSets
            .SelectMany(set => set.Groups)
            .SelectMany(group => group.Alternatives)
            .SelectMany(alternative => alternative.Routes)
            .All(route => StageOneCollectionRouteDependencyAxes.IsComplete(
                route.RequiredDownstreamDependencyAxes));
        Require(dependencyAxisInventoryComplete,
            "Acquisition route dependency-axis inventory is incomplete.");

        return new AcquisitionRouteOptionLoweringReport
        {
            Status = routeRows.All(row => row.TeacherAdmissionReady)
                ? "complete"
                : "classified_with_explicit_gaps",
            GoalId = inventory.GoalId,
            RequirementInventorySha256 = ContentInventoryVerifier.HashFile(inventoryPath),
            LoweringCatalogSha256 = ContentInventoryVerifier.HashFile(catalogPath),
            OptionMatrixSha256 = ContentInventoryVerifier.HashFile(matrixPath),
            IsolatedTrainingAuthorizationSha256 =
                ContentInventoryVerifier.HashFile(authorizationPath),
            RequirementSetCount = requirementSets.Length,
            RequirementGroupCount = requirementSets.Sum(set => set.RequiredGroupCount),
            RouteOccurrenceCount = usages.Length,
            DependencyAxisInventoryComplete = dependencyAxisInventoryComplete,
            RequiredDownstreamDependencyAxes =
                StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            ObservedRouteKindCount = observedKinds.Length,
            ClassifiedRouteKindCount = routeRows.Length,
            AdmittedRouteKindCount = routeRows.Count(row => row.TeacherAdmissionReady),
            BlockedRouteKindCount = routeRows.Count(row => !row.TeacherAdmissionReady),
            UnknownRouteKinds = unknownKinds,
            UnobservedCatalogRouteKinds = unobservedCatalogKinds,
            RouteKinds = routeRows,
            RequirementSets = requirementSets,
            AdmissionPolicy =
                "Every exact-version authoritative route kind must have exactly one typed catalog row. " +
                "Unknown and stale catalog kinds fail closed. This artifact lowers only the terminal " +
                "acquisition transition: live calendar, unlock, facility, resource, route-time, native " +
                "probability or retry bounds, inventory reservation, and fresh receipt checks remain " +
                "mandatory downstream. Deterministic dependencies never become policy labels."
        };
    }

    private static AcquisitionRouteKindLowering LowerRouteKind(
        AcquisitionRouteOptionLoweringCatalogRow row,
        RouteUsage[] usages,
        IReadOnlyDictionary<string, OptionGovernanceRow> options,
        HashSet<string> isolatedAuthorizations)
    {
        var endpoints = row.EndpointOptionIds.Select(optionId =>
            DescribeOption(options[optionId], isolatedAuthorizations)).ToArray();
        var supporting = row.SupportingOptionIds.Select(optionId =>
            DescribeOption(options[optionId], isolatedAuthorizations)).ToArray();
        var endpointRuntimeReady = endpoints.Length > 0 && endpoints.All(IsRuntimeReady);
        var runtimeReady = row.LoweringClass == "high_level_option" && endpointRuntimeReady;
        var normalTeacherReady = endpoints.Length > 0 && endpoints.All(option =>
            option.TrainingEligibility == "Eligible" &&
            option.PolicyTrainingCandidate &&
            option.TrainingExclusionReasons.Length == 0 &&
            IsRuntimeReady(option)) && row.LoweringClass == "high_level_option";
        var isolatedTeacherReady = endpoints.Length > 0 && endpoints.All(option =>
            (option.TrainingEligibility == "Eligible" || option.IsolatedTeacherAuthorized) &&
            IsRuntimeReady(option)) && row.LoweringClass == "high_level_option";
        var teacherReady = row.SupervisionMode switch
        {
            "policy_option" => normalTeacherReady,
            "deterministic_dependency" => runtimeReady,
            "isolated_teacher_option" => isolatedTeacherReady,
            "blocked_gap" => false,
            _ => false
        };
        var blockingReasons = new List<string>();
        if (row.LoweringClass == "missing_option")
            blockingReasons.Add(row.GapId);
        foreach (var option in endpoints.Where(option => !IsRuntimeReady(option)))
            blockingReasons.Add(option.OptionId + ":runtime_status=" + option.RuntimeStatus);
        if (!teacherReady && row.SupervisionMode is "policy_option" or "isolated_teacher_option")
        {
            foreach (var option in endpoints.Where(option =>
                         option.TrainingEligibility != "Eligible" &&
                         !option.IsolatedTeacherAuthorized))
            {
                blockingReasons.Add(option.OptionId + ":training_eligibility=" +
                    option.TrainingEligibility);
            }
        }
        if (row.LoweringClass == "primitive_gap")
            blockingReasons.Add(row.GapId);

        return new AcquisitionRouteKindLowering(
            row.RouteKind,
            row.LoweringClass,
            row.SupervisionMode,
            row.UncertaintyMode,
            StageOneCollectionRouteDependencyAxes.Required.ToArray(),
            usages.Length,
            usages.Select(usage => usage.RequirementId).Distinct(StringComparer.Ordinal).Count(),
            usages.Select(usage => usage.RequirementSetId).Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray(),
            endpoints,
            supporting,
            runtimeReady,
            teacherReady,
            row.SupervisionMode is "policy_option" or "isolated_teacher_option" && teacherReady,
            blockingReasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static void ValidateCatalogRow(
        AcquisitionRouteOptionLoweringCatalogRow row,
        IReadOnlyDictionary<string, OptionGovernanceRow> options)
    {
        Require(!string.IsNullOrWhiteSpace(row.RouteKind), "Catalog route kind is empty.");
        Require(LoweringClasses.Contains(row.LoweringClass),
            "Unknown lowering class: " + row.RouteKind + ":" + row.LoweringClass);
        Require(SupervisionModes.Contains(row.SupervisionMode),
            "Unknown supervision mode: " + row.RouteKind + ":" + row.SupervisionMode);
        Require(UncertaintyModes.Contains(row.UncertaintyMode),
            "Unknown uncertainty mode: " + row.RouteKind + ":" + row.UncertaintyMode);
        Require(row.EndpointOptionIds.Distinct(StringComparer.Ordinal).Count() ==
                row.EndpointOptionIds.Length,
            "Duplicate endpoint option ID: " + row.RouteKind);
        Require(row.SupportingOptionIds.Distinct(StringComparer.Ordinal).Count() ==
                row.SupportingOptionIds.Length,
            "Duplicate supporting option ID: " + row.RouteKind);
        Require(!row.EndpointOptionIds.Intersect(row.SupportingOptionIds, StringComparer.Ordinal).Any(),
            "Endpoint option is duplicated as support: " + row.RouteKind);
        foreach (var optionId in row.EndpointOptionIds.Concat(row.SupportingOptionIds))
            Require(options.ContainsKey(optionId),
                "Catalog references unknown option: " + row.RouteKind + ":" + optionId);

        if (row.LoweringClass == "missing_option")
        {
            Require(row.EndpointOptionIds.Length == 0 && row.SupervisionMode == "blocked_gap" &&
                    !string.IsNullOrWhiteSpace(row.GapId),
                "Missing-option row is not explicitly blocked: " + row.RouteKind);
        }
        else
        {
            Require(row.EndpointOptionIds.Length > 0,
                "Mapped route has no endpoint option: " + row.RouteKind);
        }
        if (row.LoweringClass == "primitive_gap")
        {
            Require(row.SupervisionMode == "blocked_gap" &&
                    row.EndpointOptionIds.All(id => id.StartsWith("executor.", StringComparison.Ordinal)) &&
                    !string.IsNullOrWhiteSpace(row.GapId),
                "Primitive-gap row must remain explicitly blocked: " + row.RouteKind);
        }
        if (row.LoweringClass == "high_level_option")
            Require(row.EndpointOptionIds.All(id => !id.StartsWith("executor.", StringComparison.Ordinal)),
                "High-level route points at a primitive option: " + row.RouteKind);
    }

    private static Dictionary<string, OptionGovernanceRow> LoadOptions(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(String(root, "schema_version") == "stardewai.option_governance_matrix.v3",
            "Current option matrix must be schema v3.");
        var rows = root.GetProperty("options").EnumerateArray().Select(value =>
            new OptionGovernanceRow(
                RequiredString(value, "optionId"),
                RequiredString(value, "trainingEligibility"),
                RequiredString(value, "runtimeStatus"),
                value.GetProperty("policyTrainingCandidate").GetBoolean(),
                StringArray(value, "trainingExclusionReasons")))
            .ToDictionary(value => value.OptionId, StringComparer.Ordinal);
        Require(rows.Count == root.GetProperty("option_count").GetInt32(),
            "Option matrix count mismatch.");
        return rows;
    }

    private static HashSet<string> LoadIsolatedAuthorizations(
        string path,
        string expectedGoalId,
        IReadOnlyDictionary<string, OptionGovernanceRow> options)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(String(root, "schema_version") ==
                "goal_conditioned_isolated_training_authorization.v1" &&
                String(root, "goal_id") == expectedGoalId &&
                String(root, "execution_mode") == "training_singleplayer" &&
                String(root, "save_scope") == "run_local_cloned_save_only" &&
                !root.GetProperty("formal_training_allowed").GetBoolean() &&
                root.GetProperty("production_governance_unchanged").GetBoolean(),
            "Isolated-training authorization scope drifted.");
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in root.GetProperty("options").EnumerateArray())
        {
            var optionId = RequiredString(value, "option_id");
            Require(result.Add(optionId), "Duplicate isolated authorization: " + optionId);
            if (!options.TryGetValue(optionId, out var option))
                throw new InvalidDataException(
                    "Isolated authorization references unknown option: " + optionId);
            Require(option.TrainingEligibility == RequiredString(value, "required_training_eligibility") &&
                    option.RuntimeStatus == RequiredString(value, "required_runtime_status"),
                "Isolated authorization option governance drifted: " + optionId);
            var soleReason = RequiredString(value, "required_sole_exclusion_reason");
            Require(option.TrainingExclusionReasons.SequenceEqual(new[] { soleReason }),
                "Isolated authorization exclusion reasons drifted: " + optionId);
        }
        return result;
    }

    private static AcquisitionLoweringOption DescribeOption(
        OptionGovernanceRow option,
        HashSet<string> isolatedAuthorizations) => new(
            option.OptionId,
            option.OptionId.StartsWith("executor.", StringComparison.Ordinal) ? "primitive" : "high_level",
            option.TrainingEligibility,
            option.RuntimeStatus,
            option.PolicyTrainingCandidate,
            isolatedAuthorizations.Contains(option.OptionId),
            option.TrainingExclusionReasons);

    private static bool IsRuntimeReady(AcquisitionLoweringOption option) =>
        option.RuntimeStatus is "RuntimeVerified" or "LongDurationVerified";

    private static T Read<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidDataException("JSON document is null: " + path);

    private static string RequiredString(JsonElement value, string property)
    {
        var result = String(value, property);
        Require(result.Length > 0, "Missing string property: " + property);
        return result;
    }

    private static string String(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString() ?? string.Empty
            : string.Empty;

    private static string[] StringArray(JsonElement value, string property) =>
        value.GetProperty(property).EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record RouteUsage(
        string RequirementSetId,
        string RequirementId,
        RequirementAcquisitionRoute Route);

    private sealed record OptionGovernanceRow(
        string OptionId,
        string TrainingEligibility,
        string RuntimeStatus,
        bool PolicyTrainingCandidate,
        string[] TrainingExclusionReasons);
}
