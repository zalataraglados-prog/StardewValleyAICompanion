using System.Text.Json;

namespace StardewAI.Core.Tests;

public sealed class ActionCatalogProvenanceGovernanceTests
{
    [Fact]
    public void Native_and_planning_fingerprints_are_independent_and_cross_catalog_consistent()
    {
        using var native = ReadJson("catalogs", "vanilla-1.6.15", "native-action-denominator-fingerprint.json");
        using var freeze = ReadJson("catalogs", "vanilla-1.6.15", "native-action-denominator-freeze.json");
        using var semantic = ReadJson("catalogs", "vanilla-1.6.15", "semantic-action-catalog.json");
        using var dashboard = ReadJson("catalogs", "vanilla-1.6.15", "action-progress-dashboard.json");

        var nativeRoot = native.RootElement;
        var freezeRoot = freeze.RootElement;
        var semanticRoot = semantic.RootElement;
        var dashboardRoot = dashboard.RootElement;
        var nativeFingerprint = nativeRoot.GetProperty("fingerprint_sha256").GetString();
        var planningFingerprint = semanticRoot
            .GetProperty("planning_semantic_catalog_fingerprint_sha256")
            .GetString();

        Assert.Equal("stardewai.native_action_denominator_fingerprint.v2", nativeRoot.GetProperty("schema_version").GetString());
        Assert.Equal("stardewai.native_action_denominator_freeze.v2", freezeRoot.GetProperty("schema_version").GetString());
        Assert.Equal("stardewai.semantic_action_catalog.v2", semanticRoot.GetProperty("schema_version").GetString());
        Assert.Equal("stardewai.action_progress_dashboard.v2", dashboardRoot.GetProperty("schema_version").GetString());
        Assert.Equal("native_game_action_evidence_only", nativeRoot.GetProperty("fingerprint_scope").GetString());
        Assert.False(nativeRoot.TryGetProperty("semantic_action_count", out _));
        Assert.False(freezeRoot.TryGetProperty("semantic_action_count", out _));
        Assert.Equal(nativeFingerprint, freezeRoot.GetProperty("fingerprint_sha256").GetString());
        Assert.Equal(nativeFingerprint, semanticRoot.GetProperty("native_action_surface_fingerprint_sha256").GetString());
        Assert.Equal(nativeFingerprint, dashboardRoot.GetProperty("native_action_surface_fingerprint_sha256").GetString());
        Assert.Equal(planningFingerprint, dashboardRoot.GetProperty("planning_semantic_catalog_fingerprint_sha256").GetString());
        Assert.NotEqual(nativeFingerprint, planningFingerprint);
        Assert.Matches("^[0-9a-f]{40}$", dashboardRoot.GetProperty("catalog_source_commit_sha").GetString()!);
        Assert.Matches("^EVD-[0-9]+$", dashboardRoot.GetProperty("latest_evidence_id").GetString()!);
    }

    [Fact]
    public void Native_fingerprint_builder_excludes_semantic_and_implementation_mappings()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.KnowledgeCompiler", "ActionDenominatorFingerprintBuilder.cs"));

        Assert.Contains("stardewai.native_action_surface.v1", source, StringComparison.Ordinal);
        Assert.Contains("stardewai.planning_semantic_catalog.v1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("row.MappedOptionIds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("row.MappedActionIds", source, StringComparison.Ordinal);
        Assert.DoesNotContain("semanticActionIds", source, StringComparison.Ordinal);
    }

    private static JsonDocument ReadJson(params string[] parts) =>
        JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(parts)));

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
