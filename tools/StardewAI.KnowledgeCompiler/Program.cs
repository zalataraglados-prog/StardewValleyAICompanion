using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.KnowledgeCompiler;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var options = Parse(args);
            if (options.ContainsKey("validate-snapshot-schema-only"))
                return ValidateSnapshotSchemaOnly(options);
            if (options.ContainsKey("update-current-snapshot-lock"))
                return UpdateCurrentSnapshotLock(options);

            var exportRoot = Required(options, "export-root");
            var outputRoot = Required(options, "output");
            options.TryGetValue("content-root", out var contentRoot);
            options.TryGetValue("snapshot-schema", out var snapshotPath);
            options.TryGetValue("game-assembly", out var gameAssembly);
            options.TryGetValue("game-data-assembly", out var gameDataAssembly);
            options.TryGetValue("decompile-root", out var decompileRoot);
            options.TryGetValue("action-denominator-freeze", out var actionDenominatorFreezePath);

            Directory.CreateDirectory(outputRoot);
            var validator = new KnowledgeSourceValidator(exportRoot, contentRoot);
            var manifest = validator.LoadManifest();
            var issues = validator.Validate(manifest).ToList();
            var coverage = validator.BuildCoverage(manifest);
            var runtimeSemanticSummary = validator.LoadRuntimeSemantics(manifest);
            using var payloads = new DisposablePayloadMap(validator.LoadPayloads(manifest));
            var mapTopology = new MapTopologyIndexBuilder().Build(payloads.Items);
            foreach (var issue in mapTopology.Issues.Where(row => row.Severity == "blocking"))
                issues.Add(new("blocking", issue.Code, issue.Subject, issue.Detail));
            Write(outputRoot, "map-topology-index.json", new
            {
                schema_version = "stardewai.map_topology_index.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority = "runtime-projected xTile maps interpreted with exact Linux 1.6.15 GameLocation.updateWarps, doesTileHaveProperty, and isTilePassable rules",
                semantic_limit = "Static base-map topology excludes dynamic buildings, furniture, placed objects, characters, events, map mutations, and location-specific collision overrides.",
                summary = mapTopology.Summary,
                issues = mapTopology.Issues,
                non_map_assets = mapTopology.NonMapAssets,
                maps = mapTopology.Maps
            });

            Write(outputRoot, "asset-coverage.json", new
            {
                schema_version = "stardewai.knowledge_asset_coverage.v1",
                total = coverage.Count,
                semantic_decoded = coverage.Count(row => row.SemanticallyDecoded),
                runtime_projection_required = coverage.Count(row => row.RequiresRuntimeProjection),
                dependency_blocking = coverage.Count(row => row.BlocksDependencyCompleteness),
                classifications = coverage.GroupBy(row => row.Classification).OrderByDescending(group => group.Count())
                    .ToDictionary(group => group.Key, group => group.Count()),
                assets = coverage
            });

            Write(outputRoot, "runtime-semantic-coverage.json", new
            {
                schema_version = "stardewai.runtime_semantic_coverage.v1",
                authority = "native 1.6.15 runtime registries and parsers",
                runtimeSemanticSummary.SourceFile,
                runtimeSemanticSummary.Sha256,
                runtimeSemanticSummary.HandlerCount,
                runtimeSemanticSummary.HandlerFamilies,
                runtimeSemanticSummary.ParsedConditionCount,
                runtimeSemanticSummary.ConditionParseErrorCount,
                runtimeSemanticSummary.ParsedEventCount,
                runtimeSemanticSummary.UnresolvedEventPreconditionCount,
                runtimeSemanticSummary.UnresolvedEventCommandCount,
                runtimeSemanticSummary.ParsedTriggerActionCount,
                runtimeSemanticSummary.UnresolvedTriggerActionCount
            });

            var registry = new OptionRegistry();
            var requiredFactors = registry.All
                .SelectMany(row => row.RequiredStateFactors)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            SnapshotCoverageResult? snapshotCoverage = null;
            if (!string.IsNullOrWhiteSpace(snapshotPath))
            {
                snapshotCoverage = new SnapshotSchemaJoiner().Join(Path.GetFullPath(snapshotPath), requiredFactors);
                if (!string.Equals(snapshotCoverage.GameVersion, manifest.GameVersion, StringComparison.Ordinal))
                {
                    issues.Add(new(
                        "blocking",
                        "snapshot_game_version_mismatch",
                        "live_snapshot",
                        $"snapshot={snapshotCoverage.GameVersion};export={manifest.GameVersion}"));
                }

                foreach (var field in snapshotCoverage.Fields)
                {
                    if (SnapshotCoverageBlocksTraining(field.Coverage))
                    {
                        issues.Add(new("blocking", "required_state_factor_not_transparent", field.Path,
                            $"status={field.Status};coverage={field.Coverage};reason={field.Reason}"));
                    }
                }
            }
            else
            {
                issues.Add(new("warning", "live_snapshot_schema_not_supplied", "option_field_matrix",
                    "required state factors were registered but not joined to a full live snapshot"));
            }

            var optionRows = registry.All.OrderBy(row => row.OptionId, StringComparer.Ordinal).Select(row => new
            {
                row.OptionId,
                row.Domain,
                row.BehaviorCategory,
                row.CompilerResponsibility,
                row.TrainingRole,
                row.SemanticKind,
                row.ParameterSchema,
                row.RequiredFactPolicy,
                row.RegistrationStatus,
                row.ReadStatus,
                row.CandidateStatus,
                row.CompilerStatus,
                row.HarnessDispatchSupported,
                row.ProductExecutorSupported,
                row.InternalExecutionPipelineSupported,
                row.BeforeVerifierStatus,
                row.AfterVerifierStatus,
                row.ProductIntegrationStatus,
                row.PolicyTrainingCandidate,
                row.ReadTrainingGate,
                row.CandidateTrainingGate,
                row.CompilerTrainingGate,
                row.RuntimeTrainingGate,
                row.OutputTrainingGate,
                row.ReadEvidenceIds,
                row.CandidateEvidenceIds,
                row.CompilerEvidenceIds,
                row.RuntimeEvidenceIds,
                row.OutputEvidenceIds,
                row.TrainingExclusionReasons,
                row.TrainingEvidenceScope,
                row.RiskClass,
                row.Irreversibility,
                row.ConfirmationPolicy,
                row.HostPolicy,
                row.OwnershipPolicy,
                row.ModAdapterPolicy,
                row.CompilerBinding,
                row.BeforeVerifierBinding,
                row.AfterVerifierBinding,
                row.RuntimeEvidenceId,
                row.RuntimeStatus,
                row.TrainingEligibility,
                row.AutonomousCandidatePolicy,
                row.ProductStatus,
                row.RequiredStateFactors,
                row.SafetyConstraints,
                runtime_field_verification = snapshotCoverage is null
                    ? null
                    : row.RequiredStateFactors.Select(snapshotCoverage.GetRequired).ToArray()
            }).ToArray();

            var nativeActionSurfaces = new NativeActionSurfaceCatalogBuilder().Build(decompileRoot);
            var nativeActionBranches = new NativeActionBranchCatalogBuilder().Build(
                decompileRoot,
                nativeActionSurfaces);
            var nativeMapInteractions = new NativeMapInteractionCoverageBuilder().Build(
                mapTopology,
                nativeActionBranches);
            var branchCoveredSurfaceIds = nativeActionBranches.Branches
                .Select(row => row.SurfaceId)
                .ToHashSet(StringComparer.Ordinal);
            var blockingNativeSurfaces = nativeActionSurfaces.Surfaces
                .Where(row => row.SemanticCoverageStatus is
                    "semantic_action_missing_registration" or
                    "unclassified" ||
                    row.SemanticCoverageStatus == "requires_branch_decompilation" &&
                    !branchCoveredSurfaceIds.Contains(row.SurfaceId))
                .ToArray();
            var blockingNativeBranches = nativeActionBranches.Branches
                .Where(row => row.CoverageStatus is
                    "semantic_action_missing_registration" or
                    "requires_semantic_review")
                .ToArray();
            var blockingNativeMapInteractions = nativeMapInteractions.Interactions
                .Where(row => row.SemanticCoverageStatus == "requires_semantic_review")
                .ToArray();
            var missingSemanticActionIds = nativeActionSurfaces.Surfaces
                .Where(row => row.SemanticCoverageStatus == "semantic_action_missing_registration")
                .SelectMany(row => row.MappedOptionIds)
                .Concat(nativeActionBranches.Branches
                    .Where(row => row.CoverageStatus == "semantic_action_missing_registration")
                    .SelectMany(row => row.MappedActionIds))
                .Where(id => !OptionCapabilityRegistrySource.TryGet(id, out _))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var cataloguedBlockedActionIds = nativeActionSurfaces.Surfaces
                .Where(row => row.SemanticCoverageStatus == "mapped_to_catalogued_blocked_action")
                .SelectMany(row => row.MappedOptionIds)
                .Concat(nativeActionBranches.Branches
                    .Where(row => row.CoverageStatus == "mapped_to_semantic_action")
                    .SelectMany(row => row.MappedActionIds))
                .Where(id => PendingSemanticActionCatalog.TryGet(id, out _))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var pendingCatalogWithoutSurface = PendingSemanticActionCatalog.All
                .Select(row => row.ActionId)
                .Except(cataloguedBlockedActionIds, StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            if (nativeActionSurfaces.SourceStatus != "native_decompile_scanned")
            {
                issues.Add(new(
                    "blocking",
                    "native_action_surface_source_not_scanned",
                    "native-action-surface-inventory",
                    nativeActionSurfaces.SourceStatus));
            }
            else if (blockingNativeSurfaces.Length > 0)
            {
                issues.Add(new(
                    "blocking",
                    "native_action_surfaces_not_semantically_closed",
                    "native-action-surface-inventory",
                    $"blocking={blockingNativeSurfaces.Length};missing_actions={missingSemanticActionIds.Length};total={nativeActionSurfaces.Surfaces.Count}"));
            }
            if (nativeActionBranches.SourceStatus != "native_branch_syntax_scanned")
            {
                issues.Add(new(
                    "blocking",
                    "native_action_branch_source_not_scanned",
                    "native-action-branch-inventory",
                    $"{nativeActionBranches.SourceStatus};missing_surfaces={nativeActionBranches.MissingSurfaceIds.Count}"));
            }
            else if (blockingNativeBranches.Length > 0)
            {
                issues.Add(new(
                    "blocking",
                    "native_action_branches_not_semantically_closed",
                    "native-action-branch-inventory",
                    $"blocking={blockingNativeBranches.Length};missing_actions={missingSemanticActionIds.Length};total={nativeActionBranches.Branches.Count}"));
            }
            if (blockingNativeMapInteractions.Length > 0)
            {
                issues.Add(new(
                    "blocking",
                    "native_map_interactions_not_semantically_closed",
                    "native-map-interaction-coverage",
                    $"blocking={blockingNativeMapInteractions.Length};total={nativeMapInteractions.Interactions.Count}"));
            }
            if (pendingCatalogWithoutSurface.Length > 0)
            {
                issues.Add(new(
                    "blocking",
                    "pending_semantic_action_without_native_surface",
                    "semantic-action-catalog",
                    string.Join(",", pendingCatalogWithoutSurface)));
            }

            Write(outputRoot, "native-action-surface-inventory.json", new
            {
                schema_version = "stardewai.native_action_surface_inventory.v1",
                authority = "locked Stardew Valley 1.6.15 decompile; source inventory is not the semantic action denominator",
                source_status = nativeActionSurfaces.SourceStatus,
                decompile_root = nativeActionSurfaces.DecompileRoot,
                surface_count = nativeActionSurfaces.Surfaces.Count,
                mapped_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "mapped_to_registered_option"),
                catalogued_blocked_surface_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "mapped_to_catalogued_blocked_action"),
                catalogued_blocked_action_count = cataloguedBlockedActionIds.Length,
                catalogued_blocked_action_ids = cataloguedBlockedActionIds,
                missing_registration_surface_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "semantic_action_missing_registration"),
                missing_semantic_action_count = missingSemanticActionIds.Length,
                missing_semantic_action_ids = missingSemanticActionIds,
                classified_non_semantic_surface_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "classified_non_semantic_surface"),
                branch_decompilation_required_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "requires_branch_decompilation"),
                branch_inventory_generated_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "requires_branch_decompilation" &&
                    branchCoveredSurfaceIds.Contains(row.SurfaceId)),
                branch_inventory_missing_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "requires_branch_decompilation" &&
                    !branchCoveredSurfaceIds.Contains(row.SurfaceId)),
                unclassified_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "unclassified"),
                surfaces = nativeActionSurfaces.Surfaces
            });
            Write(outputRoot, "native-action-branch-inventory.json", new
            {
                schema_version = "stardewai.native_action_branch_inventory.v1",
                authority = "locked Stardew Valley 1.6.15 decompile parsed as C# syntax; branch evidence is not an implementation claim",
                source_status = nativeActionBranches.SourceStatus,
                broad_surface_count = nativeActionSurfaces.Surfaces.Count(row =>
                    row.SemanticCoverageStatus == "requires_branch_decompilation"),
                covered_surface_count = branchCoveredSurfaceIds.Count,
                missing_surface_count = nativeActionBranches.MissingSurfaceIds.Count,
                missing_surface_ids = nativeActionBranches.MissingSurfaceIds,
                branch_count = nativeActionBranches.Branches.Count,
                mapped_branch_count = nativeActionBranches.Branches.Count(row =>
                    row.CoverageStatus == "mapped_to_semantic_action"),
                classified_non_semantic_branch_count = nativeActionBranches.Branches.Count(row =>
                    row.CoverageStatus == "classified_non_semantic_branch"),
                semantic_review_required_count = nativeActionBranches.Branches.Count(row =>
                    row.CoverageStatus == "requires_semantic_review"),
                missing_registration_branch_count = nativeActionBranches.Branches.Count(row =>
                    row.CoverageStatus == "semantic_action_missing_registration"),
                branches = nativeActionBranches.Branches
            });
            Write(outputRoot, "native-map-interaction-coverage.json", new
            {
                schema_version = "stardewai.native_map_interaction_coverage.v1",
                authority = "runtime-projected effective Action/TouchAction properties joined to locked native 1.6.15 branch evidence",
                interaction_token_count = nativeMapInteractions.Interactions.Count,
                occurrence_count = nativeMapInteractions.Interactions.Sum(row => row.OccurrenceCount),
                mapped_token_count = nativeMapInteractions.Interactions.Count(row =>
                    row.SemanticCoverageStatus == "mapped_to_semantic_action"),
                classified_non_semantic_token_count = nativeMapInteractions.Interactions.Count(row =>
                    row.SemanticCoverageStatus == "classified_non_semantic"),
                semantic_review_required_count = blockingNativeMapInteractions.Length,
                interactions = nativeMapInteractions.Interactions
            });

            var semanticActionRows = optionRows.Select(row =>
            {
                var ownership = OptionImplementationCatalog.GetRequired(row.OptionId);
                return new
                {
                    action_id = row.OptionId,
                    domain = row.Domain,
                    semantic_kind = row.SemanticKind.ToString(),
                    primary_engine_id = ownership.PrimaryEngineId,
                    catalog_status = "registered_option_spec",
                    block_reason = string.Empty,
                    native_runtime_types = Array.Empty<string>()
                };
            }).Concat(PendingSemanticActionCatalog.All.Select(row => new
            {
                action_id = row.ActionId,
                domain = row.Domain,
                semantic_kind = row.SemanticKind,
                primary_engine_id = row.PrimaryEngineId,
                catalog_status = row.CatalogStatus,
                block_reason = row.BlockReason,
                native_runtime_types = row.NativeRuntimeTypes
            })).OrderBy(row => row.action_id, StringComparer.Ordinal).ToArray();
            var nativeDenominatorSourceClosed = blockingNativeSurfaces.Length == 0 &&
                blockingNativeBranches.Length == 0 &&
                blockingNativeMapInteractions.Length == 0 &&
                missingSemanticActionIds.Length == 0 &&
                pendingCatalogWithoutSurface.Length == 0;
            var denominatorFingerprint = ActionDenominatorFingerprintBuilder.Build(
                manifest.GameVersion,
                nativeActionSurfaces,
                nativeActionBranches,
                nativeMapInteractions,
                semanticActionRows.Select(row => row.action_id));
            var denominatorFreeze = ActionDenominatorFingerprintBuilder.VerifyApproval(
                denominatorFingerprint,
                actionDenominatorFreezePath);
            if (!string.IsNullOrWhiteSpace(actionDenominatorFreezePath) &&
                denominatorFreeze.Status != "frozen")
            {
                issues.Add(new(
                    "blocking",
                    "native_action_denominator_freeze_mismatch",
                    "native-action-denominator-freeze",
                    denominatorFreeze.Status + ";" +
                    string.Join(",", denominatorFreeze.MismatchReasons)));
            }
            Write(outputRoot, "native-action-denominator-fingerprint.json", new
            {
                schema_version = "stardewai.native_action_denominator_fingerprint.v1",
                authority = "canonical identity digest over locked native surfaces, branches, effective map tokens, and semantic action IDs",
                game_version = denominatorFingerprint.GameVersion,
                fingerprint_sha256 = denominatorFingerprint.Sha256,
                surface_count = denominatorFingerprint.SurfaceCount,
                branch_count = denominatorFingerprint.BranchCount,
                map_token_count = denominatorFingerprint.MapTokenCount,
                semantic_action_count = denominatorFingerprint.SemanticActionCount,
                source_denominator_closed = nativeDenominatorSourceClosed,
                freeze_status = denominatorFreeze.Status,
                approval_path = denominatorFreeze.ApprovalPath,
                mismatch_reasons = denominatorFreeze.MismatchReasons
            });
            Write(outputRoot, "semantic-action-catalog.json", new
            {
                schema_version = "stardewai.semantic_action_catalog.v1",
                denominator_status = nativeDenominatorSourceClosed &&
                    denominatorFreeze.Status == "frozen"
                    ? "native_action_denominator_frozen"
                    : nativeDenominatorSourceClosed
                        ? "provisional_native_surface_denominator_closed"
                    : "provisional_native_surface_denominator_open",
                denominator_fingerprint_sha256 = denominatorFingerprint.Sha256,
                action_count = semanticActionRows.Length,
                registered_option_spec_count = optionRows.Length,
                catalogued_blocked_count = PendingSemanticActionCatalog.All.Count,
                uncatalogued_native_action_count = missingSemanticActionIds.Length,
                pending_catalog_without_surface_count = pendingCatalogWithoutSurface.Length,
                pending_catalog_without_surface_ids = pendingCatalogWithoutSurface,
                actions = semanticActionRows
            });

            var implementationRows = optionRows.Select(row =>
            {
                var binding = OptionImplementationCatalog.GetRequired(row.OptionId);
                return new
                {
                    row.OptionId,
                    row.Domain,
                    binding.PrimaryEngineId,
                    binding.AdapterId,
                    binding.CandidateBinding,
                    binding.CompilerBinding,
                    binding.RuntimeBinding,
                    binding.VerifierBinding,
                    binding.EvidenceBinding,
                    row.RegistrationStatus,
                    row.ReadStatus,
                    row.CandidateStatus,
                    row.CompilerStatus,
                    row.ProductExecutorSupported,
                    row.RuntimeStatus,
                    row.ReadTrainingGate,
                    row.CandidateTrainingGate,
                    row.CompilerTrainingGate,
                    row.RuntimeTrainingGate,
                    row.OutputTrainingGate
                };
            }).ToArray();
            var registeredIds = optionRows.Select(row => row.OptionId).ToHashSet(StringComparer.Ordinal);
            var orphanCompilerIds = ActionQueueCompiler.StepCompilerOptionIds
                .Concat(ActionQueueCompiler.ParameterCompilerOptionIds)
                .Where(id => !registeredIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            var orphanRuntimeIds = RuntimeTestHarnessDispatchCatalog.OptionIds
                .Concat(ProductExecutorCapabilityCatalog.OptionIds)
                .Where(id => !registeredIds.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            Write(outputRoot, "action-implementation-reconciliation.json", new
            {
                schema_version = "stardewai.action_implementation_reconciliation.v1",
                authority_policy = "Every registered semantic action has exactly one primary engine. Harness support is not product execution.",
                registered_option_count = implementationRows.Length,
                primary_engine_count = implementationRows
                    .Select(row => row.PrimaryEngineId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                orphan_compiler_count = orphanCompilerIds.Length,
                orphan_compiler_ids = orphanCompilerIds,
                orphan_runtime_count = orphanRuntimeIds.Length,
                orphan_runtime_ids = orphanRuntimeIds,
                engines = implementationRows
                    .GroupBy(row => row.PrimaryEngineId, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
                options = implementationRows
            });

            var fiveGateClosedCount = optionRows.Count(row =>
                row.ReadTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                row.CandidateTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                row.CompilerTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                row.RuntimeTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified &&
                row.OutputTrainingGate == TrainingEvidenceGateStatus.RuntimeVerified);
            Write(outputRoot, "action-progress-dashboard.json", new
            {
                schema_version = "stardewai.action_progress_dashboard.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                semantic_denominator_status = nativeDenominatorSourceClosed &&
                    denominatorFreeze.Status == "frozen"
                    ? "native_action_denominator_frozen"
                    : nativeDenominatorSourceClosed
                        ? "native_surfaces_classified_semantic_denominator_pending_freeze"
                    : "not_frozen_native_surfaces_or_registrations_pending",
                native_action_denominator_fingerprint_sha256 = denominatorFingerprint.Sha256,
                registered_option_count = optionRows.Length,
                semantic_action_catalog_count = semanticActionRows.Length,
                catalogued_blocked_action_count = PendingSemanticActionCatalog.All.Count,
                high_level_option_count = optionRows.Count(row =>
                    !row.OptionId.StartsWith("executor.", StringComparison.Ordinal)),
                primitive_option_count = optionRows.Count(row =>
                    row.OptionId.StartsWith("executor.", StringComparison.Ordinal)),
                native_surface_count = nativeActionSurfaces.Surfaces.Count,
                native_surface_blocking_count = blockingNativeSurfaces.Length,
                native_branch_count = nativeActionBranches.Branches.Count,
                native_branch_blocking_count = blockingNativeBranches.Length,
                native_map_interaction_token_count = nativeMapInteractions.Interactions.Count,
                native_map_interaction_blocking_count = blockingNativeMapInteractions.Length,
                missing_semantic_action_registration_count = missingSemanticActionIds.Length,
                compiler_bound_count = implementationRows.Count(row => row.CompilerBinding != "unbound"),
                harness_dispatch_count = optionRows.Count(row => row.HarnessDispatchSupported),
                product_executor_count = optionRows.Count(row => row.ProductExecutorSupported),
                five_gate_evidence_closed_count = fiveGateClosedCount,
                training_allowlist_count = OptionCapabilityRegistrySource.TrainingAllowlist.Count,
                warning = "Registered count is not the whole-game denominator. Native surfaces are source evidence, not semantic actions."
            });

            Write(outputRoot, "option-field-matrix.json", new
            {
                schema_version = "stardewai.option_field_matrix.v2",
                option_count = optionRows.Length,
                distinct_required_state_factor_count = requiredFactors.Length,
                live_snapshot = snapshotCoverage is null ? null : new
                {
                    snapshotCoverage.SchemaVersion,
                    snapshotCoverage.BridgeVersion,
                    snapshotCoverage.GameVersion,
                    snapshotCoverage.StateHash,
                    snapshotCoverage.Completeness,
                    field_count = snapshotCoverage.Fields.Count,
                    readable_count = snapshotCoverage.Fields.Count(row => row.Coverage == "readable_with_provenance"),
                    contextually_unavailable_count = snapshotCoverage.Fields.Count(row => row.Coverage == "contextually_unavailable"),
                    stale_count = snapshotCoverage.Fields.Count(row => row.Coverage == "stale"),
                    blocking_count = snapshotCoverage.Fields.Count(row => row.Coverage is "missing_from_snapshot_schema" or "not_a_field_envelope" or "readable_missing_provenance" or "adapter_error" or "invalid_status")
                },
                required_state_factors = snapshotCoverage?.Fields,
                options = optionRows
            });
            Write(outputRoot, "option-governance-matrix.json", new
            {
                schema_version = "stardewai.option_governance_matrix.v3",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority_policy = "Unknown governance fails registry initialization. Runtime status is evidence-scoped and cannot be inferred from compiler or Harness registration.",
                option_count = optionRows.Length,
                goal_template_count = optionRows.Count(row => row.SemanticKind == OptionSemanticKind.GoalTemplate),
                composite_option_count = optionRows.Count(row => row.SemanticKind == OptionSemanticKind.CompositeOptionSpec),
                primitive_option_count = optionRows.Count(row => row.SemanticKind == OptionSemanticKind.PrimitiveOptionSpec),
                training_eligible_count = optionRows.Count(row => row.TrainingEligibility == OptionTrainingEligibility.Eligible),
                runtime_verified_count = optionRows.Count(row =>
                    row.RuntimeStatus == OptionRuntimeStatus.RuntimeVerified ||
                    row.RuntimeStatus == OptionRuntimeStatus.LongDurationVerified),
                options = optionRows.Select(row => new
                {
                    row.OptionId,
                    row.Domain,
                    row.SemanticKind,
                    row.ParameterSchema,
                    row.RequiredFactPolicy,
                    row.RegistrationStatus,
                    row.ReadStatus,
                    row.CandidateStatus,
                    row.CompilerStatus,
                    row.HarnessDispatchSupported,
                    row.ProductExecutorSupported,
                    row.InternalExecutionPipelineSupported,
                    row.BeforeVerifierStatus,
                    row.AfterVerifierStatus,
                    row.ProductIntegrationStatus,
                    row.PolicyTrainingCandidate,
                    row.ReadTrainingGate,
                    row.CandidateTrainingGate,
                    row.CompilerTrainingGate,
                    row.RuntimeTrainingGate,
                    row.OutputTrainingGate,
                    row.ReadEvidenceIds,
                    row.CandidateEvidenceIds,
                    row.CompilerEvidenceIds,
                    row.RuntimeEvidenceIds,
                    row.OutputEvidenceIds,
                    row.TrainingExclusionReasons,
                    row.TrainingEvidenceScope,
                    row.RiskClass,
                    row.Irreversibility,
                    row.ConfirmationPolicy,
                    row.HostPolicy,
                    row.OwnershipPolicy,
                    row.ModAdapterPolicy,
                    row.CompilerBinding,
                    row.BeforeVerifierBinding,
                    row.AfterVerifierBinding,
                    row.RuntimeEvidenceId,
                    row.RuntimeStatus,
                    row.TrainingEligibility,
                    row.AutonomousCandidatePolicy,
                    row.ProductStatus
                }).ToArray()
            });

            Write(outputRoot, "training-admission-manifest.json", new
            {
                schema_version = "stardewai.training_admission_manifest.v1",
                capability_schema_version = OptionCapabilityRegistrySource.SchemaVersion,
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority_policy = "Policy training admits only model-facing options whose read, candidate, compile, runtime, and output gates have explicit runtime evidence. Every excluded option carries typed reasons.",
                option_count = optionRows.Length,
                eligible_count = OptionCapabilityRegistrySource.TrainingAllowlist.Count,
                allowlist = OptionCapabilityRegistrySource.TrainingAllowlist,
                excluded_count = optionRows.Count(row => row.TrainingEligibility != OptionTrainingEligibility.Eligible),
                options = optionRows.Select(row => new
                {
                    row.OptionId,
                    row.PolicyTrainingCandidate,
                    row.ReadTrainingGate,
                    row.CandidateTrainingGate,
                    row.CompilerTrainingGate,
                    row.RuntimeTrainingGate,
                    row.OutputTrainingGate,
                    row.ReadEvidenceIds,
                    row.CandidateEvidenceIds,
                    row.CompilerEvidenceIds,
                    row.RuntimeEvidenceIds,
                    row.OutputEvidenceIds,
                    row.RuntimeStatus,
                    row.TrainingEligibility,
                    row.TrainingExclusionReasons,
                    row.TrainingEvidenceScope
                }).ToArray()
            });

            var questActionCoverage = new QuestActionCoverageBuilder().Build(decompileRoot);
            if (questActionCoverage.SourceStatus != "native_decompile_scanned")
            {
                issues.Add(new(
                    "warning",
                    "quest_action_native_source_not_scanned",
                    "quest-action-coverage-matrix",
                    questActionCoverage.SourceStatus));
            }
            foreach (var runtimeType in questActionCoverage.UncataloguedOrdinaryRuntimeTypes)
            {
                issues.Add(new(
                    "blocking",
                    "ordinary_quest_runtime_type_missing_from_action_catalog",
                    runtimeType,
                    "native decompile contains a quest subclass with no declared action stages"));
            }
            foreach (var runtimeType in questActionCoverage.UncataloguedSpecialOrderObjectiveRuntimeTypes)
            {
                issues.Add(new(
                    "blocking",
                    "special_order_objective_missing_from_action_catalog",
                    runtimeType,
                    "native decompile contains an objective subclass with no declared action stages"));
            }
            Write(outputRoot, "quest-action-coverage-matrix.json", new
            {
                schema_version = "stardewai.quest_action_coverage.v1",
                authority = "Stardew Valley 1.6.15 native decompile joined to typed quest candidate bindings",
                source_status = questActionCoverage.SourceStatus,
                quest_source_directory = questActionCoverage.QuestSourceDirectory,
                objective_source_directory = questActionCoverage.ObjectiveSourceDirectory,
                discovered_ordinary_runtime_types = questActionCoverage.DiscoveredOrdinaryRuntimeTypes,
                discovered_special_order_objective_runtime_types = questActionCoverage.DiscoveredSpecialOrderObjectiveRuntimeTypes,
                uncatalogued_ordinary_runtime_types = questActionCoverage.UncataloguedOrdinaryRuntimeTypes,
                uncatalogued_special_order_objective_runtime_types = questActionCoverage.UncataloguedSpecialOrderObjectiveRuntimeTypes,
                catalog_ordinary_types_missing_from_source = questActionCoverage.CatalogOrdinaryTypesMissingFromSource,
                catalog_special_order_types_missing_from_source = questActionCoverage.CatalogSpecialOrderTypesMissingFromSource,
                stage_count = QuestActionCoverageCatalog.All.Count,
                bound_stage_count = QuestActionCoverageCatalog.All.Count(row => row.BindingStatus == QuestActionCoverageCatalog.Bound),
                blocked_stage_count = QuestActionCoverageCatalog.All.Count(row => row.BindingStatus == QuestActionCoverageCatalog.Blocked),
                native_observation_only_stage_count = QuestActionCoverageCatalog.All.Count(row => row.BindingStatus == QuestActionCoverageCatalog.NativeObservationOnly),
                stages = QuestActionCoverageCatalog.All
            });

            var downstreamRows = registry.All
                .OrderBy(row => row.OptionId, StringComparer.Ordinal)
                .Select(row =>
                {
                    var stepCompilerRegistered = ActionQueueCompiler.HasStepCompiler(row.OptionId);
                    var parameterCompilerRegistered = ActionQueueCompiler.HasParameterCompiler(row.OptionId);
                    var harnessDispatchSupported = row.HarnessDispatchSupported;
                    var productExecutorSupported = row.ProductExecutorSupported;
                    var runtimeBindingMode = productExecutorSupported
                        ? "product_executor"
                        : harnessDispatchSupported
                            ? "runtime_test_harness_only"
                            : row.OptionId == "recovery.stabilize_day"
                            ? "compiler_parameter_execution_option_id"
                            : row.OptionId == "farm.process_machines"
                                ? "daily_candidate_to_runtime_primitive"
                                : "not_directly_executable_at_this_stage";
                    var downstreamStatus =
                        row.CompilerResponsibility == CompilerResponsibilities.FullActionExpansion && !stepCompilerRegistered
                            ? "blocking_missing_step_compiler"
                            : productExecutorSupported
                                ? "product_executor_declared"
                                : harnessDispatchSupported
                                    ? "blocked_product_executor_not_integrated"
                                    : "candidate_or_strategy_stage";
                    return new
                    {
                        row.OptionId,
                        row.Domain,
                        row.CompilerResponsibility,
                        row.TrainingRole,
                        step_compiler_registered = stepCompilerRegistered,
                        parameter_compiler_registered = parameterCompilerRegistered,
                        harness_dispatch_supported = harnessDispatchSupported,
                        product_executor_supported = productExecutorSupported,
                        row.RuntimeStatus,
                        row.TrainingEligibility,
                        runtime_binding_mode = runtimeBindingMode,
                        downstream_status = downstreamStatus
                    };
                })
                .ToArray();
            var downstreamBlockers = downstreamRows
                .Where(row => row.downstream_status.StartsWith("blocking_", StringComparison.Ordinal))
                .Select(row => new
                {
                    severity = "blocking",
                    code = row.downstream_status,
                    subject = row.OptionId,
                    detail = $"compiler={row.step_compiler_registered};harness={row.harness_dispatch_supported};product={row.product_executor_supported};binding={row.runtime_binding_mode}"
                })
                .ToArray();
            var dailyCandidateRows = DailyPlanCandidateCapabilityCatalog.All
                .OrderBy(row => row.Kind, StringComparer.Ordinal)
                .Select(row => new
                {
                    candidate_kind = row.Kind,
                    daily_plan_compilable = row.Compilable,
                    implementation_block_reason = row.BlockReason
                })
                .ToArray();
            var dailyCandidateImplementationBlockers = dailyCandidateRows
                .Where(row => !row.daily_plan_compilable)
                .Select(row => new
                {
                    severity = "implementation_blocking",
                    code = row.implementation_block_reason,
                    subject = row.candidate_kind,
                    detail = "Candidate generation is retained for planning visibility, but DailyPlanCompiler must not emit an executable plan until the native executor exists."
                })
                .ToArray();
            foreach (var blocker in downstreamBlockers)
            {
                issues.Add(new("blocking", blocker.code, blocker.subject, blocker.detail));
            }
            Write(outputRoot, "downstream-capability-matrix.json", new
            {
                schema_version = "stardewai.downstream_capability_matrix.v2",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority_policy = "Readable fields, candidate generation, daily-plan compilation, action compilation, and runtime dispatch are distinct stages. A missing later stage remains explicit and cannot be replaced by a success no-op.",
                option_count = downstreamRows.Length,
                step_compiler_count = ActionQueueCompiler.StepCompilerOptionIds.Count,
                parameter_compiler_count = ActionQueueCompiler.ParameterCompilerOptionIds.Count,
                harness_dispatch_count = RuntimeTestHarnessDispatchCatalog.OptionIds.Count,
                product_executor_count = ProductExecutorCapabilityCatalog.OptionIds.Count,
                training_allowlist_count = OptionCapabilityRegistrySource.TrainingAllowlist.Count,
                blocker_count = downstreamBlockers.Length,
                blockers = downstreamBlockers,
                options = downstreamRows,
                daily_candidate_kind_count = dailyCandidateRows.Length,
                daily_candidate_compilable_count = dailyCandidateRows.Count(row => row.daily_plan_compilable),
                daily_candidate_implementation_blocker_count = dailyCandidateImplementationBlockers.Length,
                daily_candidate_implementation_blockers = dailyCandidateImplementationBlockers,
                daily_candidate_kinds = dailyCandidateRows
            });

            var graph = new RuntimeDependencyGraphBuilder().Build(payloads.Items);
            Write(outputRoot, "runtime-dependency-graph.json", graph);
            var runtimeSemanticsPath = Path.Combine(exportRoot, manifest.RuntimeSemanticsFile!);
            var progressionDependencies = new ProgressionDependencyIndexBuilder().Build(
                payloads.Items,
                runtimeSemanticsPath);
            foreach (var issue in progressionDependencies.Issues.Where(row => row.Severity == "blocking"))
                issues.Add(new("blocking", issue.Code, issue.Subject, issue.Detail));
            Write(outputRoot, "progression-dependency-index.json", new
            {
                schema_version = "stardewai.progression_dependency_index.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority = "runtime-loaded Data/mail and Data/TriggerActions joined to native 1.6.15 trigger, condition, and event parser output",
                semantic_limit = "References are emitted only for argument roles verified against native handlers. Unclassified event commands remain losslessly preserved with exact handler identity and tokens.",
                summary = progressionDependencies.Summary,
                issues = progressionDependencies.Issues,
                references = progressionDependencies.References,
                mail = progressionDependencies.Mail,
                trigger_actions = progressionDependencies.TriggerActions,
                events = progressionDependencies.Events,
                conditions = progressionDependencies.Conditions
            });
            var accessConstraints = new AccessConstraintIndexBuilder().Build(
                payloads.Items,
                mapTopology,
                progressionDependencies.Conditions);
            foreach (var issue in accessConstraints.Issues.Where(row => row.Severity == "blocking"))
                issues.Add(new("blocking", issue.Code, issue.Subject, issue.Detail));
            Write(outputRoot, "access-constraint-index.json", new
            {
                schema_version = "stardewai.access_constraint_index.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority = "runtime-loaded shops and NPC schedules joined to exact static map actions and native-parsed game-state conditions",
                semantic_limit = "NPC schedule selection and owner presence remain runtime-context decisions. Door windows and static interaction tiles are exact base-map records, not proof that a dynamic map mutation leaves them unchanged.",
                summary = accessConstraints.Summary,
                issues = accessConstraints.Issues,
                shops = accessConstraints.Shops,
                door_windows = accessConstraints.DoorWindows,
                shop_endpoints = accessConstraints.ShopEndpoints,
                npc_schedules = accessConstraints.NpcSchedules
            });

            var assemblyEvidence = new List<AssemblyEvidenceIndex>();
            if (!string.IsNullOrWhiteSpace(gameAssembly) || !string.IsNullOrWhiteSpace(gameDataAssembly))
            {
                if (string.IsNullOrWhiteSpace(decompileRoot))
                    throw new ArgumentException("--decompile-root is required when an assembly evidence input is supplied.");

                var indexer = new AssemblyEvidenceIndexer();
                if (!string.IsNullOrWhiteSpace(gameAssembly))
                {
                    assemblyEvidence.Add(indexer.Build(
                        gameAssembly,
                        Path.Combine(Path.GetFullPath(decompileRoot), "StardewValley")));
                }
                if (!string.IsNullOrWhiteSpace(gameDataAssembly))
                {
                    assemblyEvidence.Add(indexer.Build(
                        gameDataAssembly,
                        Path.Combine(Path.GetFullPath(decompileRoot), "StardewValley.GameData")));
                }

                Write(outputRoot, "decompiled-assembly-evidence.json", new
                {
                    schema_version = "stardewai.decompiled_assembly_evidence.v1",
                    generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                    evidence_limit = "Hashes and metadata prove exhaustive binary/source indexing. Rule semantics still require method-level review records.",
                    assembly_count = assemblyEvidence.Count,
                    source_file_count = assemblyEvidence.Sum(row => row.SourceFiles.Count),
                    type_count = assemblyEvidence.Sum(row => row.Types.Count),
                    method_count = assemblyEvidence.Sum(row => row.Types.Sum(type => type.Methods.Count)),
                    invalid_il_body_count = assemblyEvidence.Sum(row => row.Types.Sum(type => type.Methods.Count(method => method.BodyStatus == "invalid_il_body"))),
                    assemblies = assemblyEvidence
                });
            }
            else
            {
                issues.Add(new("warning", "decompiled_assembly_evidence_not_supplied", "decompile", "assembly and source inventories were not indexed"));
            }

            var goalDependencies = new GoalDependencyIndexBuilder().Build(
                payloads.Items,
                assemblyEvidence,
                snapshotPath);
            foreach (var issue in goalDependencies.Issues.Where(row => row.Severity == "blocking"))
                issues.Add(new("blocking", issue.Code, issue.Subject, issue.Detail));
            Write(outputRoot, "goal-dependency-index.json", new
            {
                schema_version = "stardewai.goal_dependency_index.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority = "runtime-loaded bundle, recipe, and achievement data joined to exact-platform Grandpa scoring IL/source identity and transparent live score inputs",
                semantic_limit = "Recipe and bundle grammars are decoded according to native 1.6.15 consumers. Runtime availability and route feasibility remain contextual planning inputs.",
                summary = goalDependencies.Summary,
                issues = goalDependencies.Issues,
                bundles = goalDependencies.Bundles,
                recipes = goalDependencies.Recipes,
                grandpa_goal = goalDependencies.GrandpaGoal
            });

            var runtimeRuleEvidence = new RuntimeRuleEvidenceBuilder().Build(payloads.Items, assemblyEvidence);
            Write(outputRoot, "runtime-rule-evidence.json", new
            {
                schema_version = "stardewai.runtime_rule_evidence.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority = "runtime-loaded payload identity joined to exact installed assembly IL",
                semantic_limit = "Indexed references and hashes are not interpreted rule semantics.",
                condition_count = runtimeRuleEvidence.Conditions.Count,
                method_reference_count = runtimeRuleEvidence.MethodReferences.Count,
                unresolved_method_reference_count = runtimeRuleEvidence.MethodReferences.Count(row => row.ResolutionStatus == "unresolved"),
                ambiguous_method_reference_count = runtimeRuleEvidence.MethodReferences.Count(row => row.ResolutionStatus == "ambiguous_overload_requires_signature_binding"),
                event_script_count = runtimeRuleEvidence.EventScripts.Count,
                conditions = runtimeRuleEvidence.Conditions,
                method_references = runtimeRuleEvidence.MethodReferences,
                event_scripts = runtimeRuleEvidence.EventScripts
            });

            RuntimeAssemblyIdentityValidation? runtimeAssemblyIdentity = null;
            HandlerOperationIndex? handlerOperationIndex = null;
            HandlerOperationCatalog? handlerOperationCatalog = null;
            ExecutableRuleIndex? executableRuleIndex = null;
            IReadOnlyList<HandlerSemanticSurface>? semanticSurfaces = null;
            KnowledgeCompletenessLedger? completenessLedger = null;
            var indexedAssemblyPaths = new[] { gameAssembly, gameDataAssembly }
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => Path.GetFullPath(path!))
                .ToArray();
            if (indexedAssemblyPaths.Length > 0)
            {
                runtimeAssemblyIdentity = new RuntimeAssemblyIdentityValidator().Validate(
                    runtimeSemanticsPath,
                    assemblyEvidence);
                Write(outputRoot, "runtime-assembly-identity.json", new
                {
                    schema_version = "stardewai.runtime_assembly_identity.v1",
                    generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                    status = runtimeAssemblyIdentity.IsCompatible ? "exact_match" : "blocked",
                    handler_assembly_reference_count = runtimeAssemblyIdentity.HandlerAssemblyReferences.Count,
                    declared_runtime_assembly_count = runtimeAssemblyIdentity.DeclaredRuntimeAssemblies.Count,
                    supplied_assembly_count = runtimeAssemblyIdentity.SuppliedAssemblies.Count,
                    mismatch_count = runtimeAssemblyIdentity.Mismatches.Count,
                    handler_assembly_references = runtimeAssemblyIdentity.HandlerAssemblyReferences,
                    declared_runtime_assemblies = runtimeAssemblyIdentity.DeclaredRuntimeAssemblies,
                    supplied_assemblies = runtimeAssemblyIdentity.SuppliedAssemblies,
                    mismatches = runtimeAssemblyIdentity.Mismatches
                });

                if (!runtimeAssemblyIdentity.IsCompatible)
                {
                    issues.Add(new(
                        "blocking",
                        "runtime_semantics_assembly_identity_mismatch",
                        "runtime-assembly-identity",
                        string.Join(';', runtimeAssemblyIdentity.Mismatches.Take(10)
                            .Select(row => $"{row.AssemblyName}:{row.RuntimeModuleVersionId}:{row.Reason}"))));
                }
                else
                {
                    handlerOperationIndex = new AssemblyOperationIndexer().Build(
                        runtimeSemanticsPath,
                        assemblyEvidence,
                        indexedAssemblyPaths,
                        runtimeRuleEvidence.MethodReferences);
                    var decodeFailureCount = handlerOperationIndex.Rules.Sum(row => row.DecodeFailures.Count);
                    if (handlerOperationIndex.UnresolvedMethodIdentities.Count > 0)
                    {
                        issues.Add(new("blocking", "handler_operation_method_unresolved", "handler-operation-rules",
                            string.Join(',', handlerOperationIndex.UnresolvedMethodIdentities.Take(20))));
                    }
                    if (decodeFailureCount > 0)
                    {
                        issues.Add(new("blocking", "handler_operation_il_decode_failure", "handler-operation-rules",
                            $"count={decodeFailureCount}"));
                    }
                    handlerOperationCatalog = new HandlerOperationCatalogBuilder().Build(handlerOperationIndex);
                    Write(outputRoot, "handler-operation-rules.json", new
                    {
                        schema_version = "stardewai.handler_operation_rules.v2",
                        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                        authority = "exact installed assembly IL, rooted at handlers used by native-parsed runtime data",
                        semantic_limit = "Static operation closure identifies dependencies and mutation surfaces; branch conditions and runtime virtual targets still require semantic/runtime evidence.",
                        rule_count = handlerOperationIndex.Rules.Count,
                        unresolved_method_count = handlerOperationIndex.UnresolvedMethodIdentities.Count,
                        decode_failure_count = decodeFailureCount,
                        complete_static_closure_count = handlerOperationIndex.Rules.Count(row => row.Completeness == "complete_static_operation_closure"),
                        dynamic_dispatch_boundary_count = handlerOperationIndex.Rules.Count(row => row.Completeness == "static_operations_with_dynamic_dispatch_boundary"),
                        reflection_boundary_count = handlerOperationIndex.Rules.Count(row => row.Completeness == "static_operations_with_reflection_boundary"),
                        unresolved_methods = handlerOperationIndex.UnresolvedMethodIdentities,
                        operation_catalogs = handlerOperationCatalog.OperationCatalogs,
                        rules = handlerOperationCatalog.Rules
                    });

                    executableRuleIndex = new ExecutableRuleIndexBuilder().Build(
                        runtimeSemanticsPath,
                        assemblyEvidence,
                        handlerOperationCatalog,
                        runtimeRuleEvidence.MethodReferences);
                    if (executableRuleIndex.UnresolvedBindings.Count > 0)
                    {
                        issues.Add(new(
                            "blocking",
                            "executable_rule_binding_unresolved",
                            "executable-rule-index",
                            string.Join(';', executableRuleIndex.UnresolvedBindings.Take(20))));
                    }
                    Write(outputRoot, "executable-rule-index.json", new
                    {
                        schema_version = "stardewai.executable_rule_index.v1",
                        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                        authority = "runtime-native parser output bound to exact-platform transitive IL operation rules",
                        semantic_limit = "Bindings and operation surfaces are exhaustive for exported conditions, events, and data method references; runtime values and virtual targets remain context-bound.",
                        condition_count = executableRuleIndex.Conditions.Count,
                        condition_clause_count = executableRuleIndex.Conditions.Sum(row => row.Clauses.Count),
                        event_count = executableRuleIndex.Events.Count,
                        event_precondition_count = executableRuleIndex.Events.Sum(row => row.Preconditions.Count),
                        event_command_count = executableRuleIndex.Events.Sum(row => row.Commands.Count),
                        trigger_action_entry_count = executableRuleIndex.TriggerActions.Count,
                        trigger_action_count = executableRuleIndex.TriggerActions.Sum(row => row.Actions.Count),
                        data_method_reference_count = executableRuleIndex.DataMethods.Count,
                        unresolved_binding_count = executableRuleIndex.UnresolvedBindings.Count,
                        unresolved_bindings = executableRuleIndex.UnresolvedBindings,
                        conditions = executableRuleIndex.Conditions,
                        events = executableRuleIndex.Events,
                        trigger_actions = executableRuleIndex.TriggerActions,
                        data_methods = executableRuleIndex.DataMethods
                    });

                    semanticSurfaces = new SemanticSurfaceBuilder().Build(handlerOperationCatalog);
                    Write(outputRoot, "handler-semantic-surfaces.json", new
                    {
                        schema_version = "stardewai.handler_semantic_surfaces.v1",
                        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                        authority = "normalized may-read, may-write, external side-effect, and runtime-boundary projection of exact-platform transitive IL operations",
                        interpretation = "IDs resolve through handler-operation-rules.json operation_catalogs. May-read/write sets are sound static surfaces, not proof that every branch executes.",
                        surface_count = semanticSurfaces.Count,
                        predicate_surface_count = semanticSurfaces.Count(row => row.Roles.Contains("predicate")),
                        command_surface_count = semanticSurfaces.Count(row => row.Roles.Contains("command")),
                        data_method_surface_count = semanticSurfaces.Count(row => row.Roles.Contains("data_method")),
                        runtime_boundary_surface_count = semanticSurfaces.Count(row => row.RuntimeBoundaries.Count > 0),
                        random_surface_count = semanticSurfaces.Count(row => row.RandomSourceIds.Count > 0),
                        surfaces = semanticSurfaces
                    });

                    var authoritativeGraph = new AuthoritativeDependencyGraphBuilder().Build(
                        graph,
                        handlerOperationCatalog,
                        semanticSurfaces,
                        executableRuleIndex,
                        mapTopology,
                        accessConstraints,
                        goalDependencies);
                    Write(outputRoot, "authoritative-dependency-graph.json", new
                    {
                        schema_version = "stardewai.authoritative_dependency_graph.v1",
                        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                        authority = "runtime-loaded content plus native parser bindings plus exact-platform IL operation identities",
                        semantic_limit = "Native-delegated rules preserve exact executable authority. Runtime-context classifications are not statically guessed.",
                        node_count = authoritativeGraph.Nodes.Count,
                        edge_count = authoritativeGraph.Edges.Count,
                        node_kinds = authoritativeGraph.NodeKinds,
                        edge_kinds = authoritativeGraph.EdgeKinds,
                        operation_catalog_source = "handler-operation-rules.json",
                        nodes = authoritativeGraph.Nodes,
                        edges = authoritativeGraph.Edges
                    });

                    completenessLedger = new KnowledgeCompletenessLedgerBuilder().Build(
                        coverage,
                        snapshotCoverage,
                        handlerOperationIndex,
                        handlerOperationCatalog,
                        semanticSurfaces,
                        executableRuleIndex,
                        authoritativeGraph,
                        mapTopology,
                        accessConstraints,
                        goalDependencies);
                    Write(outputRoot, "knowledge-completeness-ledger.json", new
                    {
                        schema_version = "stardewai.knowledge_completeness_ledger.v1",
                        generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                        authority_policy = "Identity completeness, runtime executability, and predictive semantic closure are tracked separately. Native delegation is never reported as static prediction.",
                        identity_graph_status = completenessLedger.Blockers.Count == 0
                            ? "complete"
                            : "blocked",
                        predictive_semantic_status = completenessLedger.ContextPending.Count == 0
                            ? "closed"
                            : "context_evidence_pending",
                        blocker_count = completenessLedger.Blockers.Count,
                        context_pending_count = completenessLedger.ContextPending.Count,
                        counts = completenessLedger.Counts,
                        blockers = completenessLedger.Blockers,
                        context_pending = completenessLedger.ContextPending,
                        assets = completenessLedger.Assets,
                        required_fields = completenessLedger.RequiredFields,
                        native_rules = completenessLedger.NativeRules
                    });
                }
            }

            Write(outputRoot, "wiki-verification-registry.json", new
            {
                schema_version = "stardewai.wiki_verification_registry.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                authority_policy = "secondary corroboration only; never creates or overrides runtime fields, values, or executable rules",
                source_count = WikiVerificationCatalog.Sources.Count,
                sources = WikiVerificationCatalog.Sources
            });

            Write(outputRoot, "source-validation.json", new
            {
                schema_version = "stardewai.knowledge_source_validation.v1",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                manifest = new
                {
                    manifest.GameVersion,
                    manifest.SmapiVersion,
                    manifest.Locale,
                    manifest.Status,
                    content_file_count = manifest.ContentFiles.Count,
                    manifest.ExpectedExports,
                    manifest.SuccessfulExports,
                    manifest.FailedExports
                },
                content_root_rehashed = !string.IsNullOrWhiteSpace(contentRoot),
                live_snapshot_schema_joined = snapshotCoverage is not null,
                runtime_assembly_identity_checked = runtimeAssemblyIdentity is not null,
                runtime_assembly_identity_exact = runtimeAssemblyIdentity?.IsCompatible,
                blocking_issue_count = issues.Count(row => row.Severity == "blocking"),
                warning_count = issues.Count(row => row.Severity == "warning"),
                issues
            });

            var sourceHash = HashFile(Path.Combine(exportRoot, "manifest.json"));
            Write(outputRoot, "build-manifest.json", new
            {
                schema_version = "stardewai.knowledge_build_manifest.v1",
                status = issues.Any(row => row.Severity == "blocking")
                    ? "blocked"
                    : completenessLedger is not null && completenessLedger.Blockers.Count == 0
                        ? "complete_authoritative_identity_graph_stage"
                        : assemblyEvidence.Count > 0 && snapshotCoverage is not null
                            ? "complete_evidence_index_stage"
                        : "complete_source_stage",
                generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
                game_version = manifest.GameVersion,
                source_manifest_sha256 = sourceHash,
                runtime_assembly_identity_status = runtimeAssemblyIdentity is null
                    ? "not_checked"
                    : runtimeAssemblyIdentity.IsCompatible ? "exact_match" : "blocked",
                outputs = Directory.EnumerateFiles(outputRoot, "*.json").Select(path => new
                {
                    file = Path.GetFileName(path),
                    bytes = new FileInfo(path).Length,
                    sha256 = HashFile(path)
                }).OrderBy(row => row.file, StringComparer.Ordinal).ToArray(),
                next_required_stages = new[]
                {
                    "semantically classify branch predicates, context-dependent virtual targets, and formula outputs from the bound operation surfaces",
                    "attach wiki verification records without promoting wiki above runtime or decompiled evidence"
                }
            });

            Console.WriteLine($"Knowledge source stage: game={manifest.GameVersion}; exports={manifest.SuccessfulExports}/{manifest.ExpectedExports}; blocking={issues.Count(row => row.Severity == "blocking")}; output={outputRoot}");
            return issues.Any(row => row.Severity == "blocking") ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int ValidateSnapshotSchemaOnly(
        IReadOnlyDictionary<string, string> options)
    {
        var snapshotPath = Required(options, "validate-snapshot-schema-only");
        var outputRoot = Required(options, "output");
        Directory.CreateDirectory(outputRoot);

        var registry = new OptionRegistry();
        var requiredFactors = registry.All
            .SelectMany(row => row.RequiredStateFactors)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var coverage = new SnapshotSchemaJoiner().Join(
            snapshotPath,
            requiredFactors);
        var blocking = coverage.Fields
            .Where(field => SnapshotCoverageBlocksTraining(field.Coverage))
            .ToArray();
        Write(outputRoot, "snapshot-schema-validation.json", new
        {
            schema_version = "stardewai.snapshot_schema_validation.v1",
            generated_at_utc = DateTimeOffset.UtcNow.ToString("O"),
            snapshot_path = snapshotPath,
            snapshot_sha256 = HashFile(snapshotPath),
            coverage.SchemaVersion,
            coverage.BridgeVersion,
            coverage.GameVersion,
            coverage.StateHash,
            coverage.Completeness,
            registered_option_count = registry.All.Count,
            required_state_factor_count = requiredFactors.Length,
            readable_with_provenance_count = coverage.Fields.Count(
                field => field.Coverage == "readable_with_provenance"),
            contextual_or_stale_count = coverage.Fields.Count(
                field => field.Coverage is "contextually_unavailable" or "stale"),
            blocking_count = blocking.Length,
            blocking_fields = blocking,
            fields = coverage.Fields
        });
        Console.WriteLine(
            $"Snapshot schema validation: game={coverage.GameVersion}; " +
            $"required={requiredFactors.Length}; blocking={blocking.Length}; " +
            $"snapshot={snapshotPath}");
        return blocking.Length == 0 ? 0 : 2;
    }

    private static bool SnapshotCoverageBlocksTraining(string coverage) =>
        coverage is "missing_from_snapshot_schema" or
            "not_a_field_envelope" or
            "readable_missing_provenance" or
            "adapter_error" or
            "invalid_status";

    private static int UpdateCurrentSnapshotLock(
        IReadOnlyDictionary<string, string> options)
    {
        var lockPath = Required(options, "update-current-snapshot-lock");
        var outputPath = Required(options, "output");
        var root = JsonNode.Parse(File.ReadAllText(lockPath)) as JsonObject ??
            throw new InvalidDataException($"Knowledge artifact lock is not a JSON object: {lockPath}");
        if (root["schema_version"]?.GetValue<string>() != "stardewai.knowledge_artifact_lock.v1")
            throw new InvalidDataException($"Unexpected knowledge artifact lock schema: {lockPath}");

        root["current_snapshot"] = new JsonObject
        {
            ["relative_path"] = RequiredValue(options, "snapshot-relative-path"),
            ["bytes"] = RequiredNonNegativeLong(options, "snapshot-bytes"),
            ["sha256"] = RequiredSha256(options, "snapshot-sha256"),
            ["metadata_relative_path"] = RequiredValue(options, "metadata-relative-path"),
            ["metadata_bytes"] = RequiredNonNegativeLong(options, "metadata-bytes"),
            ["metadata_sha256"] = RequiredSha256(options, "metadata-sha256"),
            ["required_state_factor_count"] = RequiredNonNegativeLong(options, "required-count"),
            ["readable_with_provenance_count"] = RequiredNonNegativeLong(options, "readable-count"),
            ["contextual_or_stale_count"] = RequiredNonNegativeLong(options, "contextual-count"),
            ["blocking_count"] = RequiredNonNegativeLong(options, "blocking-count")
        };

        var json = JsonSerializer.Serialize(root, JsonOptions.Write) + "\n";
        File.WriteAllText(outputPath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return 0;
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index++)
        {
            if (!args[index].StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length)
                throw new ArgumentException($"Expected --name value, got '{args[index]}'.");
            result[args[index][2..]] = args[++index];
        }
        return result;
    }

    private static string Required(IReadOnlyDictionary<string, string> options, string name)
    {
        return Path.GetFullPath(RequiredValue(options, name));
    }

    private static string RequiredValue(IReadOnlyDictionary<string, string> options, string name)
    {
        if (!options.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{name}.");
        return value;
    }

    private static long RequiredNonNegativeLong(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        var value = RequiredValue(options, name);
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
            throw new ArgumentException($"--{name} must be a non-negative integer, got '{value}'.");
        return parsed;
    }

    private static string RequiredSha256(
        IReadOnlyDictionary<string, string> options,
        string name)
    {
        var value = RequiredValue(options, name).ToLowerInvariant();
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException($"--{name} must be a 64-character SHA-256 hex digest.");
        return value;
    }

    private static void Write(string root, string file, object value)
    {
        var path = Path.Combine(root, file);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions.Write));
        File.Move(temp, path, overwrite: true);
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private sealed class DisposablePayloadMap : IDisposable
    {
        public DisposablePayloadMap(Dictionary<string, PayloadAsset> items) => Items = items;
        public Dictionary<string, PayloadAsset> Items { get; }
        public void Dispose()
        {
            foreach (var item in Items.Values) item.Dispose();
        }
    }
}
