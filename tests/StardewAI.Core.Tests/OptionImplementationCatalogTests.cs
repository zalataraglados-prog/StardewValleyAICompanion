using StardewAI.Contracts.Capabilities;
using StardewAI.Core.OptionRegistry;
using System.Text.Json;

namespace StardewAI.Core.Tests;

public sealed class OptionImplementationCatalogTests
{
    [Fact]
    public void Every_registered_option_has_exactly_one_primary_engine()
    {
        var expected = OptionCapabilityRegistrySource.RegisteredIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var actual = OptionImplementationCatalog.All
            .Select(row => row.OptionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(actual.Length, actual.Distinct(StringComparer.Ordinal).Count());
        Assert.All(OptionImplementationCatalog.All, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.PrimaryEngineId));
            Assert.False(string.IsNullOrWhiteSpace(row.AdapterId));
            Assert.False(string.IsNullOrWhiteSpace(row.CandidateBinding));
            Assert.False(string.IsNullOrWhiteSpace(row.CompilerBinding));
            Assert.False(string.IsNullOrWhiteSpace(row.RuntimeBinding));
            Assert.False(string.IsNullOrWhiteSpace(row.VerifierBinding));
            Assert.False(string.IsNullOrWhiteSpace(row.EvidenceBinding));
        });
    }

    [Fact]
    public void Executor_options_cannot_fall_back_to_strategy_engine()
    {
        Assert.All(
            OptionImplementationCatalog.All.Where(row =>
                row.OptionId.StartsWith("executor.", StringComparison.Ordinal)),
            row => Assert.NotEqual(
                ImplementationEngineIds.StrategyOrchestration,
                row.PrimaryEngineId));
    }

    [Fact]
    public void Shared_engine_catalog_has_no_orphan_compiler_or_runtime_ids()
    {
        var registered = OptionCapabilityRegistrySource.RegisteredIds
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            StardewAI.Core.Execution.ActionQueueCompiler.StepCompilerOptionIds,
            id => Assert.Contains(id, registered));
        Assert.All(
            StardewAI.Core.Execution.ActionQueueCompiler.ParameterCompilerOptionIds,
            id => Assert.Contains(id, registered));
        Assert.All(
            RuntimeTestHarnessDispatchCatalog.OptionIds,
            id => Assert.Contains(id, registered));
        Assert.All(
            ProductExecutorCapabilityCatalog.OptionIds,
            id => Assert.Contains(id, registered));
    }

    [Fact]
    public void Committed_reconciliation_catalog_matches_live_registration_and_ownership()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "action-implementation-reconciliation.json")));
        var root = document.RootElement;
        Assert.Equal(
            OptionCapabilityRegistrySource.RegisteredIds.Count,
            root.GetProperty("registered_option_count").GetInt32());
        Assert.Equal(0, root.GetProperty("orphan_compiler_count").GetInt32());
        Assert.Equal(0, root.GetProperty("orphan_runtime_count").GetInt32());

        var generated = root.GetProperty("options")
            .EnumerateArray()
            .ToDictionary(
                row => row.GetProperty("optionId").GetString()!,
                row => row.GetProperty("primaryEngineId").GetString()!,
                StringComparer.Ordinal);
        Assert.Equal(OptionImplementationCatalog.All.Count, generated.Count);
        Assert.All(OptionImplementationCatalog.All, row =>
            Assert.Equal(row.PrimaryEngineId, generated[row.OptionId]));
    }

    [Fact]
    public void Native_surface_inventory_cannot_be_mistaken_for_a_frozen_denominator()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-action-surface-inventory.json")));
        var root = document.RootElement;

        Assert.Equal("native_decompile_scanned", root.GetProperty("source_status").GetString());
        Assert.True(root.GetProperty("surface_count").GetInt32() >= 50);
        Assert.True(root.GetProperty("branch_decompilation_required_count").GetInt32() > 0);
        Assert.Equal(
            root.GetProperty("branch_decompilation_required_count").GetInt32(),
            root.GetProperty("branch_inventory_generated_count").GetInt32());
        Assert.Equal(0, root.GetProperty("branch_inventory_missing_count").GetInt32());
        Assert.Equal(0, root.GetProperty("unclassified_count").GetInt32());
        Assert.Equal(
            PendingSemanticActionCatalog.All.Count,
            root.GetProperty("catalogued_blocked_action_count").GetInt32());
        Assert.Equal(0, root.GetProperty("missing_semantic_action_count").GetInt32());

        var gameLocationPerformActionOverloads = root.GetProperty("surfaces")
            .EnumerateArray()
            .Where(row =>
                row.GetProperty("runtimeType").GetString() == "GameLocation" &&
                row.GetProperty("member").GetString() == "performAction")
            .Select(row => row.GetProperty("signature").GetString())
            .ToArray();
        Assert.Equal(2, gameLocationPerformActionOverloads.Length);
        Assert.Equal(
            gameLocationPerformActionOverloads.Length,
            gameLocationPerformActionOverloads.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Native_branch_and_runtime_map_interaction_catalogs_are_semantically_closed()
    {
        using var branchDocument = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-action-branch-inventory.json")));
        var branches = branchDocument.RootElement;
        Assert.Equal("native_branch_syntax_scanned", branches.GetProperty("source_status").GetString());
        Assert.True(branches.GetProperty("branch_count").GetInt32() > 0);
        Assert.Equal(0, branches.GetProperty("missing_surface_count").GetInt32());
        Assert.Equal(0, branches.GetProperty("semantic_review_required_count").GetInt32());
        Assert.Equal(0, branches.GetProperty("missing_registration_branch_count").GetInt32());
        Assert.Equal(
            branches.GetProperty("broad_surface_count").GetInt32(),
            branches.GetProperty("covered_surface_count").GetInt32());

        using var mapDocument = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-map-interaction-coverage.json")));
        var maps = mapDocument.RootElement;
        Assert.True(maps.GetProperty("interaction_token_count").GetInt32() > 0);
        Assert.True(maps.GetProperty("occurrence_count").GetInt32() > 0);
        Assert.Equal(0, maps.GetProperty("semantic_review_required_count").GetInt32());
        Assert.Equal(
            maps.GetProperty("interaction_token_count").GetInt32(),
            maps.GetProperty("mapped_token_count").GetInt32() +
            maps.GetProperty("classified_non_semantic_token_count").GetInt32());
    }

    [Fact]
    public void Pending_semantic_actions_are_unique_blocked_and_source_attributed()
    {
        Assert.Equal(
            PendingSemanticActionCatalog.All.Count,
            PendingSemanticActionCatalog.All
                .Select(row => row.ActionId)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(PendingSemanticActionCatalog.All, row =>
        {
            Assert.Equal("catalogued_blocked", row.CatalogStatus);
            Assert.Equal("option_spec_not_declared", row.BlockReason);
            Assert.NotEmpty(row.NativeRuntimeTypes);
            Assert.False(OptionCapabilityRegistrySource.TryGet(row.ActionId, out _));
            Assert.False(string.IsNullOrWhiteSpace(row.PrimaryEngineId));
        });
    }

    [Fact]
    public void Committed_semantic_catalog_joins_option_specs_and_blocked_actions_without_gaps()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "semantic-action-catalog.json")));
        var root = document.RootElement;
        var expected = OptionCapabilityRegistrySource.RegisteredIds
            .Concat(PendingSemanticActionCatalog.All.Select(row => row.ActionId))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var actual = root.GetProperty("actions")
            .EnumerateArray()
            .Select(row => row.GetProperty("action_id").GetString()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, root.GetProperty("action_count").GetInt32());
        Assert.Equal(
            "native_action_denominator_frozen",
            root.GetProperty("denominator_status").GetString());
        Assert.Equal(0, root.GetProperty("uncatalogued_native_action_count").GetInt32());
        Assert.Equal(0, root.GetProperty("pending_catalog_without_surface_count").GetInt32());
    }

    [Fact]
    public void Committed_native_action_denominator_matches_independent_freeze_approval()
    {
        using var fingerprint = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-action-denominator-fingerprint.json")));
        using var approval = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-action-denominator-freeze.json")));
        var actual = fingerprint.RootElement;
        var expected = approval.RootElement;

        Assert.Equal("frozen", actual.GetProperty("freeze_status").GetString());
        Assert.Equal(
            expected.GetProperty("fingerprint_sha256").GetString(),
            actual.GetProperty("fingerprint_sha256").GetString());
        Assert.Equal(expected.GetProperty("surface_count").GetInt32(), actual.GetProperty("surface_count").GetInt32());
        Assert.Equal(expected.GetProperty("branch_count").GetInt32(), actual.GetProperty("branch_count").GetInt32());
        Assert.Equal(expected.GetProperty("map_token_count").GetInt32(), actual.GetProperty("map_token_count").GetInt32());
        Assert.Equal(expected.GetProperty("semantic_action_count").GetInt32(), actual.GetProperty("semantic_action_count").GetInt32());
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Unable to locate repository file.", Path.Combine(parts));
    }
}
