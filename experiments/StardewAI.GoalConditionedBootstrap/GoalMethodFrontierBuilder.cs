using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class GoalMethodFrontierBuilder
{
    public static GoalMethodFrontierReport Build(
        string expansionPath,
        string dependencyExpansionPath,
        string isolatedTrainingAuthorizationPath,
        string requirementInventoryPath,
        string knowledgePath,
        string optionMatrixPath,
        string claimLedgerPath,
        string directionCatalogSourcePath)
    {
        var expansionFullPath = Path.GetFullPath(expansionPath);
        var dependencyFullPath = Path.GetFullPath(dependencyExpansionPath);
        var authorizationFullPath = Path.GetFullPath(
            isolatedTrainingAuthorizationPath);
        var requirementInventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var optionFullPath = Path.GetFullPath(optionMatrixPath);
        var ledgerFullPath = Path.GetFullPath(claimLedgerPath);
        var catalogSourceFullPath = Path.GetFullPath(directionCatalogSourcePath);
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        var requirementInventory = LoadRequirementInventory(
            requirementInventoryFullPath,
            knowledge.GoalId);
        var options = LoadOptions(optionFullPath);
        var isolatedTrainingAuthorization =
            LoadIsolatedTrainingAuthorization(
                authorizationFullPath,
                knowledge.GoalId,
                options);
        var claimLedger = LoadClaimLedger(ledgerFullPath);
        var verifiedDependencySourceIds = claimLedger.VerifiedSourceIds
            .Concat(requirementInventory.SourceEvidence.Select(source => source.SourceId))
            .ToHashSet(StringComparer.Ordinal);
        var overlays = LoadExpansionOverlay(expansionFullPath, knowledge.GoalId);
        var dependencyExpansions = LoadDependencyExpansions(dependencyFullPath, knowledge.GoalId);
        var catalogEntries = GrandpaDirectionCatalog.Entries;

        Require(File.Exists(catalogSourceFullPath),
            "Grandpa direction catalog source does not exist: " + catalogSourceFullPath);
        Require(catalogEntries.Length > 0, "Grandpa direction catalog is empty.");
        Require(catalogEntries.Select(value => value.DirectionId).Distinct(StringComparer.Ordinal).Count() == catalogEntries.Length,
            "Grandpa direction catalog contains duplicate direction IDs.");

        var catalogDirectionIds = catalogEntries
            .Select(value => value.DirectionId)
            .ToHashSet(StringComparer.Ordinal);
        var missingOverlays = catalogDirectionIds.Except(overlays.Keys).Order(StringComparer.Ordinal).ToArray();
        var unknownOverlays = overlays.Keys.Except(catalogDirectionIds).Order(StringComparer.Ordinal).ToArray();
        Require(missingOverlays.Length == 0,
            "Directions have no dependency expansion overlay: " + string.Join(",", missingOverlays));
        Require(unknownOverlays.Length == 0,
            "Expansion overlay contains unknown directions: " + string.Join(",", unknownOverlays));
        var unknownDependencyExpansions = dependencyExpansions.Keys
            .Except(catalogDirectionIds)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Require(unknownDependencyExpansions.Length == 0,
            "Dependency expansion contains unknown directions: " +
            string.Join(",", unknownDependencyExpansions));

        var criterionIds = knowledge.Criteria.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        var coveredCriteria = new HashSet<string>(StringComparer.Ordinal);
        var methodIds = new HashSet<string>(StringComparer.Ordinal);
        var methodRows = new List<GoalMethodFrontierMethod>();
        var nodes = new List<GoalMethodHypergraphNode>();
        var edges = new List<GoalMethodHypergraphEdge>();
        nodes.Add(new(knowledge.GoalId, "strategic_goal", new Dictionary<string, object?>
        {
            ["target_score"] = knowledge.TargetScore
        }));

        foreach (var criterion in knowledge.Criteria)
        {
            nodes.Add(new(criterion.Id, "goal_score_criterion", new Dictionary<string, object?>
            {
                ["points"] = criterion.Points,
                ["operation"] = criterion.Operation,
                ["target"] = criterion.Target
            }));
            edges.Add(new(criterion.Id, knowledge.GoalId, "contributes_goal_score"));
        }

        foreach (var direction in catalogEntries)
        {
            ValidateDirection(direction);
            var methodId = direction.BindingRuleId;
            Require(methodIds.Add(methodId), "Duplicate binding rule ID: " + methodId);
            var criteria = direction.CriterionIds.Distinct(StringComparer.Ordinal).ToArray();
            var permittedOptions = direction.PermittedOptionIds.Distinct(StringComparer.Ordinal).ToArray();
            var overlay = overlays[direction.DirectionId];
            dependencyExpansions.TryGetValue(direction.DirectionId, out var dependencyExpansion);
            Require(dependencyExpansion is null || overlay.UnexpandedRequirements.Length == 0,
                "Direction copies dependency blockers into both overlay and graph: " +
                direction.DirectionId);
            var unexpandedRequirements = dependencyExpansion?.Blockers ??
                overlay.UnexpandedRequirements;

            Require(criteria.Length == direction.CriterionIds.Length,
                "Direction contains duplicate criteria: " + direction.DirectionId);
            Require(criteria.Length > 0, "Direction has no criteria: " + direction.DirectionId);
            Require(permittedOptions.Length > 0, "Direction has no permitted options: " + direction.DirectionId);

            foreach (var criterionId in criteria)
            {
                Require(criterionIds.Contains(criterionId),
                    $"Direction {direction.DirectionId} has unknown criterion {criterionId}.");
                Require(coveredCriteria.Add(criterionId),
                    "Grandpa criterion is mapped by more than one direction: " + criterionId);
                edges.Add(new(methodId, criterionId, "achieves_criterion"));
            }
            foreach (var claimId in overlay.ClaimIds)
                Require(claimLedger.VerifiedClaimIds.Contains(claimId),
                    $"Direction {direction.DirectionId} references unverified claim {claimId}.");
            foreach (var optionId in permittedOptions)
                Require(options.ContainsKey(optionId),
                    $"Direction {direction.DirectionId} references unknown option {optionId}.");

            if (dependencyExpansion is not null)
            {
                ValidateDependencyExpansion(
                    dependencyExpansion,
                    methodId,
                    options,
                    verifiedDependencySourceIds,
                    nodes,
                    edges);
            }

            var policyDependencyOptions = dependencyExpansion?.Nodes
                .Where(node => node.Kind == "policy_option_transition")
                .Select(node => node.OptionId)
                .Where(optionId => !string.IsNullOrWhiteSpace(optionId))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            var deterministicDependencyOptions = dependencyExpansion?.Nodes
                .Where(node => node.Kind == "deterministic_transition")
                .Select(node => node.OptionId)
                .Where(optionId => !string.IsNullOrWhiteSpace(optionId))
                .Distinct(StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            var eligibleOptions = permittedOptions
                .Where(optionId => options[optionId].TrainingEligibility == "Eligible")
                .ToArray();
            var isolatedTeacherAuthorizedOptions = permittedOptions
                .Where(isolatedTrainingAuthorization.OptionIds.Contains)
                .ToArray();
            var eligibleDependencyOptions = policyDependencyOptions
                .Where(optionId => options[optionId].TrainingEligibility == "Eligible")
                .ToArray();
            var isolatedTeacherAuthorizedDependencyOptions =
                policyDependencyOptions
                    .Where(isolatedTrainingAuthorization.OptionIds.Contains)
                    .ToArray();
            var blockers = permittedOptions.Concat(policyDependencyOptions)
                .Distinct(StringComparer.Ordinal)
                .Select(optionId => options[optionId])
                .Where(option =>
                    option.TrainingEligibility != "Eligible" &&
                    !isolatedTrainingAuthorization.OptionIds.Contains(
                        option.OptionId))
                .Select(option => new GoalMethodOptionBlocker(
                    option.OptionId,
                    option.TrainingEligibility,
                    option.TrainingExclusionReasons))
                .Concat(deterministicDependencyOptions
                    .Select(optionId => options[optionId])
                    .Where(option => option.RuntimeStatus is not ("RuntimeVerified" or "LongDurationVerified"))
                    .Select(option => new GoalMethodOptionBlocker(
                        option.OptionId,
                        option.TrainingEligibility,
                        option.TrainingExclusionReasons
                            .Append("deterministic_transition_requires_runtime_verified_option")
                            .ToArray())))
                .GroupBy(blocker => blocker.OptionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            var dependencyComplete = dependencyExpansion is null ||
                dependencyExpansion.Status == "complete";
            var dependencyPolicyComplete =
                eligibleDependencyOptions.Length +
                    isolatedTeacherAuthorizedDependencyOptions.Length ==
                policyDependencyOptions.Length;
            var deterministicDependencyComplete = deterministicDependencyOptions.All(optionId =>
                options[optionId].RuntimeStatus is "RuntimeVerified" or "LongDurationVerified");
            var status = !direction.DirectBindingEnabled ||
                         eligibleOptions.Length +
                             isolatedTeacherAuthorizedOptions.Length == 0 ||
                         !dependencyPolicyComplete || !deterministicDependencyComplete
                ? "blocked_by_option_governance"
                : unexpandedRequirements.Length > 0 || !dependencyComplete
                    ? "pending_dependency_expansion"
                    : "executable_frontier";

            methodRows.Add(new GoalMethodFrontierMethod(
                methodId,
                direction.DirectionId,
                direction.Domain,
                direction.EffectiveGoalId,
                direction.DemandFamily,
                direction.DirectBindingEnabled,
                criteria,
                permittedOptions,
                eligibleOptions,
                isolatedTeacherAuthorizedOptions
                    .Concat(isolatedTeacherAuthorizedDependencyOptions)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                policyDependencyOptions,
                eligibleDependencyOptions,
                deterministicDependencyOptions,
                unexpandedRequirements,
                overlay.ClaimIds,
                blockers,
                status));
            nodes.Add(new(methodId, "goal_method", new Dictionary<string, object?>
            {
                ["direction_id"] = direction.DirectionId,
                ["domain"] = direction.Domain,
                ["effective_goal_id"] = direction.EffectiveGoalId,
                ["demand_family"] = direction.DemandFamily,
                ["direct_binding_enabled"] = direction.DirectBindingEnabled,
                ["status"] = status,
                ["claim_ids"] = overlay.ClaimIds,
                ["dependency_expansion_status"] = dependencyExpansion?.Status ?? "direct_option_leaf"
            }));
            foreach (var optionId in permittedOptions)
                edges.Add(new(optionId, methodId, "permitted_by_direction"));
            foreach (var requirement in unexpandedRequirements)
            {
                var blockerId = "unexpanded:" + StableId(methodId + ":" + requirement);
                nodes.Add(new(blockerId, "unexpanded_requirement", new Dictionary<string, object?>
                {
                    ["description"] = requirement
                }));
                edges.Add(new(blockerId, methodId, "blocks_method"));
            }
        }

        var missing = criterionIds.Except(coveredCriteria).Order(StringComparer.Ordinal).ToArray();
        Require(missing.Length == 0, "Criteria have no root method: " + string.Join(",", missing));

        AddRequirementInventoryGraph(
            requirementInventory,
            catalogEntries,
            nodes,
            edges);

        var referencedOptionIds = methodRows
            .SelectMany(method => method.PermittedOptionIds
                .Concat(method.DependencyOptionIds)
                .Concat(method.DeterministicDependencyOptionIds))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var option in options.Values.Where(option => referencedOptionIds.Contains(option.OptionId)))
        {
            nodes.Add(new(option.OptionId, "existing_high_level_option", new Dictionary<string, object?>
            {
                ["domain"] = option.Domain,
                ["training_eligibility"] = option.TrainingEligibility,
                ["runtime_status"] = option.RuntimeStatus,
                ["isolated_teacher_authorized"] =
                    isolatedTrainingAuthorization.OptionIds.Contains(
                        option.OptionId),
                ["exclusion_reasons"] = option.TrainingExclusionReasons
            }));
        }

        var criteriaRows = knowledge.Criteria.Select(criterion =>
        {
            var methods = methodRows
                .Where(method => method.CriterionIds.Contains(criterion.Id, StringComparer.Ordinal))
                .ToArray();
            var status = methods.Any(method => method.Status == "executable_frontier")
                ? "executable_frontier"
                : methods.Any(method => method.Status == "pending_dependency_expansion")
                    ? "pending_dependency_expansion"
                    : "blocked_by_option_governance";
            return new GoalCriterionFrontier(
                criterion.Id,
                criterion.Points,
                methods.Select(method => method.MethodId).ToArray(),
                status);
        }).ToArray();
        var breadthCoverage = BuildBreadthCoverage(
            criteriaRows,
            methodRows,
            overlays,
            dependencyExpansions,
            options,
            isolatedTrainingAuthorization.OptionIds);

        return new GoalMethodFrontierReport
        {
            Status = criteriaRows.All(value => value.Status == "executable_frontier") ? "complete" : "in_progress",
            GoalId = knowledge.GoalId,
            TargetScore = knowledge.TargetScore,
            KnowledgeSha256 = knowledge.Sha256,
            DirectionCatalogSha256 = ContentInventoryVerifier.HashFile(catalogSourceFullPath),
            ExpansionOverlaySha256 = ContentInventoryVerifier.HashFile(expansionFullPath),
            DependencyExpansionSha256 = ContentInventoryVerifier.HashFile(dependencyFullPath),
            IsolatedTrainingAuthorizationSha256 =
                ContentInventoryVerifier.HashFile(authorizationFullPath),
            RequirementInventorySha256 =
                ContentInventoryVerifier.HashFile(requirementInventoryFullPath),
            RequirementDenominatorGroupCount = requirementInventory.RequirementSets
                .Sum(set => set.RequiredGroupCount),
            RequirementRouteCoveredGroupCount = requirementInventory.RequirementSets
                .Sum(set => set.RouteCoveredGroupCount),
            UnresolvedAcquisitionRequirementCount =
                requirementInventory.UnresolvedAcquisitionRequirementIds.Length,
            ClaimLedgerSha256 = ContentInventoryVerifier.HashFile(ledgerFullPath),
            OptionMatrixSha256 = ContentInventoryVerifier.HashFile(optionFullPath),
            OptionCount = options.Count,
            TrainingEligibleOptionCount = options.Values.Count(value => value.TrainingEligibility == "Eligible"),
            IsolatedTeacherAuthorizedOptionIds =
                isolatedTrainingAuthorization.OptionIds
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
            RuntimeVerifiedOptionCount = options.Values.Count(value =>
                value.RuntimeStatus is "RuntimeVerified" or "LongDurationVerified"),
            CriterionCount = criteriaRows.Length,
            RootMethodCount = methodRows.Count,
            ExecutableCriterionCount = criteriaRows.Count(value => value.Status == "executable_frontier"),
            GovernanceBlockedCriterionCount = criteriaRows.Count(value => value.Status == "blocked_by_option_governance"),
            ExpansionPendingCriterionCount = criteriaRows.Count(value => value.Status == "pending_dependency_expansion"),
            BreadthCoverage = breadthCoverage,
            Criteria = criteriaRows,
            Methods = methodRows.ToArray(),
            Nodes = nodes.GroupBy(value => value.Id, StringComparer.Ordinal).Select(value => value.First()).ToArray(),
            Edges = edges.ToArray(),
            AdmissionPolicy =
                "This is a root frontier, not teacher data. Direct and policy-dependency options must be normally training-eligible or explicitly authorized only for deterministic teacher labels inside a run-local cloned training_singleplayer save whose source-tree hash remains unchanged. Production confirmation governance is unchanged. Deterministic compiler-owned transitions must be runtime-verified but never become policy labels. Only fully expanded, evidence-locked routes may supervise the learner."
        };
    }

    private static AuthoritativeRequirementInventoryReport LoadRequirementInventory(
        string path,
        string expectedGoalId)
    {
        var report = JsonSerializer.Deserialize<AuthoritativeRequirementInventoryReport>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Requirement inventory is null.");
        Require(report.SchemaVersion == "authoritative_goal_requirement_inventory.v1",
            "Unsupported authoritative requirement inventory schema.");
        Require(report.GoalId == expectedGoalId,
            "Requirement inventory goal does not match knowledge.");
        Require(report.DenominatorComplete,
            "Requirement inventory has an incomplete denominator.");
        Require(report.RequirementSets.Length == 4,
            "Requirement inventory must contain exactly four Stage 1 collection sets.");
        Require(report.RequirementSets.All(set =>
                set.DenominatorStatus == "complete" &&
                set.RequiredGroupCount == set.Groups.Length &&
                set.RouteCoveredGroupCount == set.Groups.Count(group => group.RouteCovered)),
            "Requirement inventory set counts do not reconcile.");
        Require(report.SourceEvidence.Length > 0,
            "Requirement inventory has no source evidence.");
        Require(report.SourceEvidence.Select(source => source.SourceId)
                .Distinct(StringComparer.Ordinal).Count() == report.SourceEvidence.Length,
            "Requirement inventory contains duplicate source evidence IDs.");
        foreach (var source in report.SourceEvidence)
        {
            Require(File.Exists(source.Path),
                "Requirement inventory source is missing: " + source.SourceId);
            Require(ContentInventoryVerifier.HashFile(source.Path) == source.Sha256,
                "Requirement inventory source hash drifted: " + source.SourceId);
        }
        return report;
    }

    private static void AddRequirementInventoryGraph(
        AuthoritativeRequirementInventoryReport inventory,
        IReadOnlyList<GrandpaDirectionCatalogEntry> catalogEntries,
        ICollection<GoalMethodHypergraphNode> nodes,
        ICollection<GoalMethodHypergraphEdge> edges)
    {
        var directionBySet = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["full_shipment"] = "complete_full_shipment",
            ["master_angler"] = "complete_master_angler",
            ["museum_collection"] = "complete_museum_collection",
            ["community_center_standard"] = "complete_community_center"
        };
        foreach (var set in inventory.RequirementSets)
        {
            Require(directionBySet.TryGetValue(set.RequirementSetId, out var directionId),
                "Requirement inventory contains an unknown set: " + set.RequirementSetId);
            var methodId = catalogEntries.Single(entry => entry.DirectionId == directionId).BindingRuleId;
            var setNodeId = "requirement_set:" + set.RequirementSetId;
            nodes.Add(new(setNodeId, "authoritative_requirement_set", new Dictionary<string, object?>
            {
                ["criterion_id"] = set.CriterionId,
                ["required_group_count"] = set.RequiredGroupCount,
                ["route_covered_group_count"] = set.RouteCoveredGroupCount,
                ["acquisition_routes_complete"] = set.AcquisitionRoutesComplete,
                ["transparent_state_path"] = set.TransparentStatePath
            }));
            edges.Add(new(setNodeId, methodId, "defines_method_denominator"));
            foreach (var group in set.Groups)
            {
                nodes.Add(new(group.RequirementId, "authoritative_requirement", new Dictionary<string, object?>
                {
                    ["selection_rule"] = group.SelectionRule,
                    ["required_alternative_count"] = group.RequiredAlternativeCount,
                    ["route_covered"] = group.RouteCovered,
                    ["transparent_completion_path"] = group.TransparentCompletionPath
                }));
                edges.Add(new(group.RequirementId, setNodeId, "belongs_to_requirement_set"));
            }
        }
    }

    private static IsolatedTrainingAuthorization LoadIsolatedTrainingAuthorization(
        string path,
        string expectedGoalId,
        IReadOnlyDictionary<string, OptionRow> options)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(
            RequiredString(root, "schema_version") ==
                "goal_conditioned_isolated_training_authorization.v1",
            "Unsupported isolated-training authorization schema.");
        Require(
            RequiredString(root, "goal_id") == expectedGoalId,
            "Isolated-training authorization goal does not match knowledge.");
        Require(
            RequiredString(root, "execution_mode") ==
                "training_singleplayer",
            "Isolated-training authorization must be single-player training only.");
        Require(
            RequiredString(root, "save_scope") ==
                "run_local_cloned_save_only",
            "Isolated-training authorization must require a run-local cloned save.");
        Require(
            RequiredString(root, "source_save_integrity") ==
                "sha256_tree_before_equals_after",
            "Isolated-training authorization must preserve the source save tree.");
        Require(
            root.GetProperty("formal_training_allowed").ValueKind ==
                JsonValueKind.False,
            "The frontier authorization cannot start formal training.");
        Require(
            root.GetProperty("production_governance_unchanged").ValueKind ==
                JsonValueKind.True,
            "The frontier authorization cannot weaken production governance.");

        var optionIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in root.GetProperty("options").EnumerateArray())
        {
            var optionId = RequiredString(row, "option_id");
            Require(
                optionIds.Add(optionId),
                "Duplicate isolated-training option authorization: " +
                    optionId);
            if (!options.TryGetValue(optionId, out var option))
            {
                throw new InvalidDataException(
                    "Isolated-training authorization references unknown option: " +
                    optionId);
            }
            Require(
                option.TrainingEligibility ==
                    RequiredString(row, "required_training_eligibility"),
                "Authorized option training eligibility drifted: " + optionId);
            Require(
                option.RuntimeStatus ==
                    RequiredString(row, "required_runtime_status"),
                "Authorized option runtime status drifted: " + optionId);
            var soleReason = RequiredString(
                row,
                "required_sole_exclusion_reason");
            Require(
                option.TrainingExclusionReasons.Length == 1 &&
                option.TrainingExclusionReasons[0] == soleReason,
                "Authorized option exclusion reasons drifted: " + optionId);
            Require(
                RequiredString(row, "authorization_scope") ==
                    "deterministic_teacher_label_on_isolated_clone_only",
                "Authorized option scope is too broad: " + optionId);
        }
        Require(
            optionIds.Count > 0,
            "Isolated-training authorization contains no options.");
        return new IsolatedTrainingAuthorization(optionIds);
    }

    private static Dictionary<string, DirectionExpansionOverlay> LoadExpansionOverlay(
        string path,
        string expectedGoalId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(RequiredString(root, "schema_version") == "goal_method_expansion_overlay.v1",
            "Unsupported goal-method expansion overlay schema.");
        Require(RequiredString(root, "goal_id") == expectedGoalId,
            "Goal-method expansion overlay goal does not match knowledge.");

        var result = new Dictionary<string, DirectionExpansionOverlay>(StringComparer.Ordinal);
        foreach (var value in root.GetProperty("directions").EnumerateArray())
        {
            var directionId = RequiredString(value, "direction_id");
            var overlay = new DirectionExpansionOverlay(
                directionId,
                StringArray(value, "unexpanded_requirements"),
                StringArray(value, "claim_ids"));
            Require(result.TryAdd(directionId, overlay),
                "Duplicate direction expansion overlay: " + directionId);
        }
        return result;
    }

    private static Dictionary<string, DirectionDependencyExpansion> LoadDependencyExpansions(
        string path,
        string expectedGoalId)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(RequiredString(root, "schema_version") == "goal_method_dependency_expansions.v1",
            "Unsupported goal-method dependency expansion schema.");
        Require(RequiredString(root, "goal_id") == expectedGoalId,
            "Goal-method dependency expansion goal does not match knowledge.");

        var result = new Dictionary<string, DirectionDependencyExpansion>(StringComparer.Ordinal);
        foreach (var value in root.GetProperty("directions").EnumerateArray())
        {
            var directionId = RequiredString(value, "direction_id");
            var nodes = value.GetProperty("nodes").EnumerateArray()
                .Select(node =>
                {
                    var attributes = JsonSerializer
                        .Deserialize<Dictionary<string, JsonElement>>(node.GetRawText())!
                        .Where(entry => entry.Key is not ("id" or "kind"))
                        .ToDictionary(
                            entry => entry.Key,
                            entry => (object?)entry.Value.Clone(),
                            StringComparer.Ordinal);
                    return new DirectionDependencyNode(
                        RequiredString(node, "id"),
                        RequiredString(node, "kind"),
                        OptionalString(node, "option_id"),
                        OptionalStringArray(node, "candidate_kinds"),
                        OptionalStringArray(node, "required_field_paths"),
                        attributes);
                })
                .ToArray();
            var edges = value.GetProperty("edges").EnumerateArray()
                .Select(edge => new DirectionDependencyEdge(
                    RequiredString(edge, "from"),
                    RequiredString(edge, "to"),
                    RequiredString(edge, "kind")))
                .ToArray();
            var expansion = new DirectionDependencyExpansion(
                directionId,
                RequiredString(value, "status"),
                RequiredString(value, "completion_node_id"),
                StringArray(value, "source_ids"),
                StringArray(value, "blockers"),
                nodes,
                edges);
            Require(result.TryAdd(directionId, expansion),
                "Duplicate direction dependency expansion: " + directionId);
        }
        return result;
    }

    private static void ValidateDependencyExpansion(
        DirectionDependencyExpansion expansion,
        string methodId,
        IReadOnlyDictionary<string, OptionRow> options,
        IReadOnlySet<string> verifiedSourceIds,
        ICollection<GoalMethodHypergraphNode> graphNodes,
        ICollection<GoalMethodHypergraphEdge> graphEdges)
    {
        Require(expansion.Status is "complete" or "in_progress",
            "Unsupported dependency expansion status: " + expansion.DirectionId);
        if (expansion.Status == "complete")
            Require(expansion.Blockers.Length == 0,
                "Complete dependency expansion still has blockers: " + expansion.DirectionId);
        else
            Require(expansion.Blockers.Length > 0,
                "In-progress dependency expansion has no blockers: " + expansion.DirectionId);
        Require(expansion.SourceIds.Length > 0,
            "Dependency expansion has no locked sources: " + expansion.DirectionId);
        foreach (var sourceId in expansion.SourceIds)
            Require(verifiedSourceIds.Contains(sourceId),
                $"Dependency expansion {expansion.DirectionId} references unverified source {sourceId}.");

        var allowedKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "terminal_fact",
            "state_fact",
            "state_gate",
            "policy_option_transition",
            "deterministic_transition",
            "recurrence_rule"
        };
        var localNodeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in expansion.Nodes)
        {
            Require(localNodeIds.Add(node.Id),
                "Duplicate dependency node ID: " + node.Id);
            Require(node.Id.StartsWith("dependency." + expansion.DirectionId + ".", StringComparison.Ordinal),
                "Dependency node is not namespaced to its direction: " + node.Id);
            Require(allowedKinds.Contains(node.Kind),
                $"Dependency node {node.Id} has unsupported kind {node.Kind}.");
            Require(node.RequiredFieldPaths.All(path => !string.IsNullOrWhiteSpace(path)),
                "Dependency node has an empty transparent field path: " + node.Id);

            if (node.Kind is "policy_option_transition" or "deterministic_transition" &&
                !string.IsNullOrWhiteSpace(node.OptionId))
            {
                Require(options.ContainsKey(node.OptionId),
                    $"Dependency node {node.Id} references unknown option {node.OptionId}.");
                Require(node.CandidateKinds.Length > 0,
                    "Option dependency node has no candidate kinds: " + node.Id);
            }
            if (node.Kind == "policy_option_transition")
            {
                Require(!string.IsNullOrWhiteSpace(node.OptionId),
                    "Policy dependency node has no option ID: " + node.Id);
            }

            var attributes = new Dictionary<string, object?>(node.Attributes, StringComparer.Ordinal)
            {
                ["direction_id"] = expansion.DirectionId
            };
            graphNodes.Add(new GoalMethodHypergraphNode(node.Id, node.Kind, attributes));
        }

        Require(localNodeIds.Contains(expansion.CompletionNodeId),
            "Dependency completion node is missing: " + expansion.CompletionNodeId);
        Require(expansion.Nodes.Single(node => node.Id == expansion.CompletionNodeId).Kind == "terminal_fact",
            "Dependency completion node is not a terminal fact: " + expansion.CompletionNodeId);
        foreach (var edge in expansion.Edges)
        {
            Require(localNodeIds.Contains(edge.From) && localNodeIds.Contains(edge.To),
                $"Dependency edge leaves its local graph: {edge.From}->{edge.To}.");
            graphEdges.Add(new GoalMethodHypergraphEdge(edge.From, edge.To, edge.Kind));
        }
        var reachesCompletion = new HashSet<string>(StringComparer.Ordinal)
        {
            expansion.CompletionNodeId
        };
        int previousCount;
        do
        {
            previousCount = reachesCompletion.Count;
            foreach (var edge in expansion.Edges.Where(edge => reachesCompletion.Contains(edge.To)))
                reachesCompletion.Add(edge.From);
        }
        while (reachesCompletion.Count > previousCount);
        Require(reachesCompletion.SetEquals(localNodeIds),
            "Dependency expansion has nodes with no route to completion: " +
            expansion.DirectionId + ":" +
            string.Join(",", localNodeIds.Except(reachesCompletion).Order(StringComparer.Ordinal)));
        graphEdges.Add(new GoalMethodHypergraphEdge(
            expansion.CompletionNodeId,
            methodId,
            "satisfies_method"));
    }

    private static void ValidateDirection(GrandpaDirectionCatalogEntry direction)
    {
        Require(!string.IsNullOrWhiteSpace(direction.DirectionId), "Direction ID is empty.");
        Require(!string.IsNullOrWhiteSpace(direction.Domain),
            "Direction domain is empty: " + direction.DirectionId);
        Require(!string.IsNullOrWhiteSpace(direction.Label),
            "Direction label is empty: " + direction.DirectionId);
        Require(!string.IsNullOrWhiteSpace(direction.FeedbackKey),
            "Direction feedback key is empty: " + direction.DirectionId);
        Require(!string.IsNullOrWhiteSpace(direction.EffectiveGoalId),
            "Direction effective goal is empty: " + direction.DirectionId);
        Require(!string.IsNullOrWhiteSpace(direction.DemandFamily),
            "Direction demand family is empty: " + direction.DirectionId);
        Require(!string.IsNullOrWhiteSpace(direction.BindingRuleId),
            "Direction binding rule is empty: " + direction.DirectionId);
    }

    private static Dictionary<string, OptionRow> LoadOptions(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(RequiredString(root, "schema_version") == "stardewai.option_governance_matrix.v3",
            "Current option matrix must be schema v3.");
        var result = root.GetProperty("options").EnumerateArray().Select(value => new OptionRow(
            RequiredString(value, "optionId"),
            RequiredString(value, "domain"),
            RequiredString(value, "trainingEligibility"),
            RequiredString(value, "runtimeStatus"),
            StringArray(value, "trainingExclusionReasons")))
            .ToDictionary(value => value.OptionId, StringComparer.Ordinal);
        Require(result.Count == root.GetProperty("option_count").GetInt32(), "Option matrix count mismatch.");
        return result;
    }

    private static ClaimLedgerIndex LoadClaimLedger(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var claims = root.GetProperty("claims").EnumerateArray()
            .Where(value => RequiredString(value, "verdict") is "verified" or "verified_scope_difference" or "native_verified_runtime_pending" or "strategy_hypothesis_only")
            .Select(value => RequiredString(value, "claim_id"))
            .ToHashSet(StringComparer.Ordinal);
        var sources = root.GetProperty("source_locks").EnumerateArray()
            .Select(value => RequiredString(value, "source_id"))
            .ToHashSet(StringComparer.Ordinal);
        return new ClaimLedgerIndex(claims, sources);
    }

    private static string[] StringArray(JsonElement value, string property) =>
        value.GetProperty(property).EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static string[] OptionalStringArray(JsonElement value, string property) =>
        value.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

    private static string OptionalString(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String
            ? item.GetString()?.Trim() ?? string.Empty
            : string.Empty;

    private static string RequiredString(JsonElement value, string property)
    {
        var result = value.GetProperty(property).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required frontier string is empty: " + property)
            : result;
    }

    private static string StableId(string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant()[..16];

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record DirectionExpansionOverlay(
        string DirectionId,
        string[] UnexpandedRequirements,
        string[] ClaimIds);

    private sealed record DirectionDependencyExpansion(
        string DirectionId,
        string Status,
        string CompletionNodeId,
        string[] SourceIds,
        string[] Blockers,
        DirectionDependencyNode[] Nodes,
        DirectionDependencyEdge[] Edges);

    private sealed record DirectionDependencyNode(
        string Id,
        string Kind,
        string OptionId,
        string[] CandidateKinds,
        string[] RequiredFieldPaths,
        Dictionary<string, object?> Attributes);

    private sealed record DirectionDependencyEdge(
        string From,
        string To,
        string Kind);

    private sealed record ClaimLedgerIndex(
        HashSet<string> VerifiedClaimIds,
        HashSet<string> VerifiedSourceIds);

    private sealed record IsolatedTrainingAuthorization(
        HashSet<string> OptionIds);

    private sealed record OptionRow(
        string OptionId,
        string Domain,
        string TrainingEligibility,
        string RuntimeStatus,
        string[] TrainingExclusionReasons);
}

public sealed class GoalMethodFrontierReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "goal_method_frontier_graph.v3";
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;
    [JsonPropertyName("target_score")]
    public int TargetScore { get; set; }
    [JsonPropertyName("knowledge_sha256")]
    public string KnowledgeSha256 { get; set; } = string.Empty;
    [JsonPropertyName("direction_catalog_sha256")]
    public string DirectionCatalogSha256 { get; set; } = string.Empty;
    [JsonPropertyName("expansion_overlay_sha256")]
    public string ExpansionOverlaySha256 { get; set; } = string.Empty;
    [JsonPropertyName("dependency_expansion_sha256")]
    public string DependencyExpansionSha256 { get; set; } = string.Empty;
    [JsonPropertyName("isolated_training_authorization_sha256")]
    public string IsolatedTrainingAuthorizationSha256 { get; set; } =
        string.Empty;
    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;
    [JsonPropertyName("requirement_denominator_group_count")]
    public int RequirementDenominatorGroupCount { get; set; }
    [JsonPropertyName("requirement_route_covered_group_count")]
    public int RequirementRouteCoveredGroupCount { get; set; }
    [JsonPropertyName("unresolved_acquisition_requirement_count")]
    public int UnresolvedAcquisitionRequirementCount { get; set; }
    [JsonPropertyName("claim_ledger_sha256")]
    public string ClaimLedgerSha256 { get; set; } = string.Empty;
    [JsonPropertyName("option_matrix_sha256")]
    public string OptionMatrixSha256 { get; set; } = string.Empty;
    [JsonPropertyName("option_count")]
    public int OptionCount { get; set; }
    [JsonPropertyName("training_eligible_option_count")]
    public int TrainingEligibleOptionCount { get; set; }
    [JsonPropertyName("isolated_teacher_authorized_option_ids")]
    public string[] IsolatedTeacherAuthorizedOptionIds { get; set; } =
        Array.Empty<string>();
    [JsonPropertyName("runtime_verified_option_count")]
    public int RuntimeVerifiedOptionCount { get; set; }
    [JsonPropertyName("criterion_count")]
    public int CriterionCount { get; set; }
    [JsonPropertyName("root_method_count")]
    public int RootMethodCount { get; set; }
    [JsonPropertyName("executable_criterion_count")]
    public int ExecutableCriterionCount { get; set; }
    [JsonPropertyName("governance_blocked_criterion_count")]
    public int GovernanceBlockedCriterionCount { get; set; }
    [JsonPropertyName("expansion_pending_criterion_count")]
    public int ExpansionPendingCriterionCount { get; set; }
    [JsonPropertyName("breadth_coverage")]
    public GoalMethodBreadthCoverage BreadthCoverage { get; set; } = new();
    [JsonPropertyName("criteria")]
    public GoalCriterionFrontier[] Criteria { get; set; } = Array.Empty<GoalCriterionFrontier>();
    [JsonPropertyName("methods")]
    public GoalMethodFrontierMethod[] Methods { get; set; } = Array.Empty<GoalMethodFrontierMethod>();
    [JsonPropertyName("nodes")]
    public GoalMethodHypergraphNode[] Nodes { get; set; } = Array.Empty<GoalMethodHypergraphNode>();
    [JsonPropertyName("edges")]
    public GoalMethodHypergraphEdge[] Edges { get; set; } = Array.Empty<GoalMethodHypergraphEdge>();
    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } = string.Empty;
}

public sealed record GoalCriterionFrontier(
    [property: JsonPropertyName("criterion_id")] string CriterionId,
    [property: JsonPropertyName("points")] int Points,
    [property: JsonPropertyName("method_ids")] string[] MethodIds,
    [property: JsonPropertyName("status")] string Status);

public sealed record GoalMethodFrontierMethod(
    [property: JsonPropertyName("method_id")] string MethodId,
    [property: JsonPropertyName("direction_id")] string DirectionId,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("effective_goal_id")] string EffectiveGoalId,
    [property: JsonPropertyName("demand_family")] string DemandFamily,
    [property: JsonPropertyName("direct_binding_enabled")] bool DirectBindingEnabled,
    [property: JsonPropertyName("criterion_ids")] string[] CriterionIds,
    [property: JsonPropertyName("permitted_option_ids")] string[] PermittedOptionIds,
    [property: JsonPropertyName("eligible_option_ids")] string[] EligibleOptionIds,
    [property: JsonPropertyName("isolated_teacher_authorized_option_ids")] string[] IsolatedTeacherAuthorizedOptionIds,
    [property: JsonPropertyName("dependency_option_ids")] string[] DependencyOptionIds,
    [property: JsonPropertyName("eligible_dependency_option_ids")] string[] EligibleDependencyOptionIds,
    [property: JsonPropertyName("deterministic_dependency_option_ids")] string[] DeterministicDependencyOptionIds,
    [property: JsonPropertyName("unexpanded_requirements")] string[] UnexpandedRequirements,
    [property: JsonPropertyName("claim_ids")] string[] ClaimIds,
    [property: JsonPropertyName("option_blockers")] GoalMethodOptionBlocker[] OptionBlockers,
    [property: JsonPropertyName("status")] string Status);

public sealed record GoalMethodOptionBlocker(
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("training_eligibility")] string TrainingEligibility,
    [property: JsonPropertyName("exclusion_reasons")] string[] ExclusionReasons);

public sealed record GoalMethodHypergraphNode(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("attributes")] Dictionary<string, object?> Attributes);

public sealed record GoalMethodHypergraphEdge(
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("kind")] string Kind);
