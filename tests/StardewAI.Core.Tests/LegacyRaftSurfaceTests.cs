using System;
using System.IO;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Tests;

public sealed class LegacyRaftSurfaceTests
{
    [Fact]
    public void UnreachableRaftTypeIsNotAPlayerSemanticAction()
    {
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.use_raft", out _));
        Assert.False(OptionCapabilityRegistrySource.TryGet("executor.use_raft", out _));

        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.KnowledgeCompiler", "NativeActionSurfaceCatalogBuilder.cs"));

        Assert.Contains("runtimeType is \"Raft\"", source, StringComparison.Ordinal);
        Assert.Contains("\"legacy_unreachable\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Raft\" => new[] { \"executor.use_raft\" }", source,
            StringComparison.Ordinal);
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
