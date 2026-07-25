namespace StardewAI.KnowledgeCompiler;

internal sealed class KnowledgeCompletenessLedgerBuilder
{
    public KnowledgeCompletenessLedger Build(
        IReadOnlyList<AssetCoverageRow> assets,
        SnapshotCoverageResult? snapshot,
        HandlerOperationIndex operationIndex,
        HandlerOperationCatalog operationCatalog,
        IReadOnlyList<HandlerSemanticSurface> semanticSurfaces,
        ExecutableRuleIndex executableRules,
        AuthoritativeDependencyGraph graph,
        MapTopologyIndex mapTopology,
        AccessConstraintIndex accessConstraints,
        GoalDependencyIndex goalDependencies)
    {
        var assetRows = assets.Select(row => new KnowledgeAssetCompleteness(
            row.AssetName,
            row.Classification,
            row.SemanticallyDecoded
                ? "authoritative_runtime_payload"
                : row.Classification == "runtime_map_projection_required"
                    ? "authoritative_binary_identity_runtime_projection_pending"
                    : row.BlocksDependencyCompleteness
                        ? "blocking_semantic_decode_missing"
                        : "authoritative_hash_inventory_nonlogic",
            row.RequiresRuntimeProjection,
            row.BlocksDependencyCompleteness,
            row.Evidence)).ToArray();

        KnowledgeFieldCompleteness[] fieldRows = snapshot?.Fields.Select(row => new KnowledgeFieldCompleteness(
            row.Path,
            row.Status,
            row.Coverage,
            row.Coverage == "readable_with_provenance"
                ? "authoritative_live_read"
                : row.Coverage == "contextually_unavailable"
                    ? "schema_bound_scene_observation_pending"
                    : "blocking",
            row.SourceKind,
            row.SourcePath,
            row.Adapter,
            row.Reason)).ToArray() ?? Array.Empty<KnowledgeFieldCompleteness>();

        var surfaceById = semanticSurfaces.ToDictionary(row => row.OperationRuleId);
        var ruleRows = operationCatalog.Rules.Select(row =>
        {
            var surface = surfaceById[row.RuleId];
            return new KnowledgeRuleCompleteness(
                row.RuleId,
                row.Identity,
                row.Families,
                row.Keys,
                surface.Roles,
                surface.RuntimeBoundaries.Count == 0
                    ? "authoritative_static_surface_closed"
                    : "authoritative_native_delegated_runtime_context_required",
                surface.RuntimeBoundaries,
                surface.ResultContract);
        }).ToArray();

        var blockers = new List<KnowledgeCompletenessIssue>();
        blockers.AddRange(assetRows.Where(row => row.Blocking)
            .Select(row => new KnowledgeCompletenessIssue(
                "asset_semantic_decode_missing",
                row.AssetName,
                row.Classification)));
        blockers.AddRange(fieldRows.Where(row => row.AuthorityStatus == "blocking")
            .Select(row => new KnowledgeCompletenessIssue(
                "required_field_not_transparent",
                row.Path,
                row.Coverage)));
        blockers.AddRange(operationIndex.UnresolvedMethodIdentities.Select(identity =>
            new KnowledgeCompletenessIssue("operation_method_unresolved", identity, string.Empty)));
        blockers.AddRange(executableRules.UnresolvedBindings.Select(binding =>
            new KnowledgeCompletenessIssue("executable_binding_unresolved", binding, string.Empty)));
        blockers.AddRange(operationCatalog.Rules.Where(row => row.DecodeFailures.Count > 0)
            .Select(row => new KnowledgeCompletenessIssue(
                "operation_il_decode_failure",
                row.Identity,
                string.Join(';', row.DecodeFailures))));
        blockers.AddRange(mapTopology.Issues.Where(row => row.Severity == "blocking")
            .Select(row => new KnowledgeCompletenessIssue(row.Code, row.Subject, row.Detail)));
        blockers.AddRange(accessConstraints.Issues.Where(row => row.Severity == "blocking")
            .Select(row => new KnowledgeCompletenessIssue(row.Code, row.Subject, row.Detail)));
        blockers.AddRange(goalDependencies.Issues.Where(row => row.Severity == "blocking")
            .Select(row => new KnowledgeCompletenessIssue(row.Code, row.Subject, row.Detail)));

        var pending = new List<KnowledgeCompletenessIssue>();
        pending.AddRange(assetRows.Where(row => row.RuntimeProjectionRequired)
            .Select(row => new KnowledgeCompletenessIssue(
                "map_runtime_projection_scene_proof_required",
                row.AssetName,
                row.Classification)));
        pending.AddRange(fieldRows.Where(row => row.AuthorityStatus == "schema_bound_scene_observation_pending")
            .Select(row => new KnowledgeCompletenessIssue(
                "field_scene_observation_required",
                row.Path,
                row.Reason ?? string.Empty)));
        pending.AddRange(ruleRows.Where(row => row.RuntimeBoundaries.Count > 0)
            .Select(row => new KnowledgeCompletenessIssue(
                "native_rule_runtime_context_required",
                row.Identity,
                string.Join(',', row.RuntimeBoundaries))));

        return new(
            assetRows,
            fieldRows,
            ruleRows,
            blockers,
            pending,
            new KnowledgeCompletenessCounts(
                assets.Count,
                assetRows.Count(row => row.AuthorityStatus == "authoritative_runtime_payload"),
                assetRows.Count(row => row.RuntimeProjectionRequired),
                fieldRows.Length,
                fieldRows.Count(row => row.AuthorityStatus == "authoritative_live_read"),
                fieldRows.Count(row => row.AuthorityStatus == "schema_bound_scene_observation_pending"),
                ruleRows.Length,
                ruleRows.Count(row => row.AuthorityStatus == "authoritative_static_surface_closed"),
                ruleRows.Count(row => row.AuthorityStatus == "authoritative_native_delegated_runtime_context_required"),
                executableRules.Conditions.Count,
                executableRules.Conditions.Sum(row => row.Clauses.Count),
                executableRules.Events.Count,
                executableRules.Events.Sum(row => row.Preconditions.Count),
                executableRules.Events.Sum(row => row.Commands.Count),
                executableRules.TriggerActions.Count,
                executableRules.TriggerActions.Sum(row => row.Actions.Count),
                executableRules.DataMethods.Count,
                mapTopology.Summary.MapCount,
                mapTopology.Summary.WarpCount,
                mapTopology.Summary.InteractionPropertyCount,
                mapTopology.Summary.BlockingIssueCount,
                accessConstraints.Summary.ShopCount,
                accessConstraints.Summary.ShopStockRowCount,
                accessConstraints.Summary.DoorWindowCount,
                accessConstraints.Summary.ShopEndpointCount,
                accessConstraints.Summary.NpcScheduleEntryCount,
                accessConstraints.Summary.NpcScheduleSegmentCount,
                accessConstraints.Summary.BlockingIssueCount,
                goalDependencies.Summary.BundleCount,
                goalDependencies.Summary.BundleIngredientCount,
                goalDependencies.Summary.CookingRecipeCount,
                goalDependencies.Summary.CraftingRecipeCount,
                goalDependencies.Summary.RecipeIngredientCount,
                goalDependencies.Summary.RecipeOutputCount,
                goalDependencies.Summary.GrandpaCriterionCount,
                goalDependencies.Summary.GrandpaMaximumScore,
                goalDependencies.Summary.BlockingIssueCount,
                graph.Nodes.Count,
                graph.Edges.Count));
    }
}

internal sealed record KnowledgeCompletenessLedger(
    IReadOnlyList<KnowledgeAssetCompleteness> Assets,
    IReadOnlyList<KnowledgeFieldCompleteness> RequiredFields,
    IReadOnlyList<KnowledgeRuleCompleteness> NativeRules,
    IReadOnlyList<KnowledgeCompletenessIssue> Blockers,
    IReadOnlyList<KnowledgeCompletenessIssue> ContextPending,
    KnowledgeCompletenessCounts Counts);

internal sealed record KnowledgeAssetCompleteness(
    string AssetName,
    string Classification,
    string AuthorityStatus,
    bool RuntimeProjectionRequired,
    bool Blocking,
    string Evidence);

internal sealed record KnowledgeFieldCompleteness(
    string Path,
    string Status,
    string Coverage,
    string AuthorityStatus,
    string? SourceKind,
    string? SourcePath,
    string? Adapter,
    string? Reason);

internal sealed record KnowledgeRuleCompleteness(
    int OperationRuleId,
    string Identity,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Keys,
    IReadOnlyList<string> Roles,
    string AuthorityStatus,
    IReadOnlyList<string> RuntimeBoundaries,
    string ResultContract);

internal sealed record KnowledgeCompletenessIssue(string Code, string Subject, string Detail);

internal sealed record KnowledgeCompletenessCounts(
    int AssetCount,
    int RuntimePayloadAssetCount,
    int RuntimeProjectionAssetCount,
    int RequiredFieldCount,
    int LiveReadableFieldCount,
    int ScenePendingFieldCount,
    int NativeRuleCount,
    int StaticClosedRuleCount,
    int NativeDelegatedContextRuleCount,
    int ConditionCount,
    int ConditionClauseCount,
    int EventCount,
    int EventPreconditionCount,
    int EventCommandCount,
    int TriggerActionEntryCount,
    int TriggerActionCount,
    int DataMethodReferenceCount,
    int MapCount,
    int MapWarpCount,
    int MapInteractionPropertyCount,
    int MapTopologyBlockingIssueCount,
    int ShopCount,
    int ShopStockRowCount,
    int DoorWindowCount,
    int ShopEndpointCount,
    int NpcScheduleEntryCount,
    int NpcScheduleSegmentCount,
    int AccessConstraintBlockingIssueCount,
    int BundleCount,
    int BundleIngredientCount,
    int CookingRecipeCount,
    int CraftingRecipeCount,
    int RecipeIngredientCount,
    int RecipeOutputCount,
    int GrandpaCriterionCount,
    int GrandpaMaximumScore,
    int GoalDependencyBlockingIssueCount,
    int GraphNodeCount,
    int GraphEdgeCount);
