using System;
using System.IO;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Tests;

public sealed class LegacyRaftSurfaceTests
{
    [Fact]
    public void CutToolsAreCompatibilityPlaceholdersNotVanillaSemanticActions()
    {
        var ids = new[] { "executor.toggle_lantern", "executor.use_raft" };
        Assert.All(ids, id =>
        {
            Assert.False(PendingSemanticActionCatalog.TryGet(id, out _));
            Assert.False(OptionCapabilityRegistrySource.TryGet(id, out _));
            var placeholder = Assert.Single(
                CompatibilitySemanticActionPlaceholderCatalog.All,
                row => row.ActionId == id);
            Assert.Equal("cut_content_unreachable", placeholder.VanillaDisposition);
            Assert.Equal("placeholder_requires_reachable_adapter", placeholder.CompatibilityStatus);
        });

        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.KnowledgeCompiler", "NativeActionSurfaceCatalogBuilder.cs"));

        Assert.Contains("runtimeType is \"Raft\"", source, StringComparison.Ordinal);
        Assert.Contains("runtimeType is \"Lantern\"", source, StringComparison.Ordinal);
        Assert.Contains("\"cut_content_unreachable\"", source, StringComparison.Ordinal);
        Assert.Contains("\"mapped_to_compatibility_placeholder\"", source, StringComparison.Ordinal);

        using var semanticCatalog = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "semantic-action-catalog.json")));
        var semanticRoot = semanticCatalog.RootElement;
        Assert.Equal(2, semanticRoot.GetProperty("compatibility_placeholder_count").GetInt32());
        Assert.Equal(
            ids,
            semanticRoot.GetProperty("compatibility_placeholders").EnumerateArray()
                .Select(row => row.GetProperty("actionId").GetString())
                .OrderBy(id => id, StringComparer.Ordinal));
        var denominatorIds = semanticRoot.GetProperty("actions").EnumerateArray()
            .Select(row => row.GetProperty("action_id").GetString())
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(ids, id => Assert.DoesNotContain(id, denominatorIds));

        using var nativeCatalog = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile(
            "catalogs", "vanilla-1.6.15", "native-action-surface-inventory.json")));
        var placeholderSurfaces = nativeCatalog.RootElement.GetProperty("surfaces").EnumerateArray()
            .Where(row => row.GetProperty("mappedOptionIds").EnumerateArray()
                .Any(id => ids.Contains(id.GetString(), StringComparer.Ordinal)))
            .ToArray();
        Assert.Equal(2, placeholderSurfaces.Length);
        Assert.All(placeholderSurfaces, row =>
        {
            Assert.Equal("mapped_to_compatibility_placeholder", row.GetProperty("semanticCoverageStatus").GetString());
            Assert.Equal("cut_content_unreachable", row.GetProperty("scopeDisposition").GetString());
        });
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Repository file not found.", Path.Combine(segments));
    }
}
