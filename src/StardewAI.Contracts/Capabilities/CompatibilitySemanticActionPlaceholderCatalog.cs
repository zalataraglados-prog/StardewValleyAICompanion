using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Contracts.Capabilities;

public sealed class CompatibilitySemanticActionPlaceholderDeclaration
{
    public string ActionId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SemanticKind { get; set; } = "primitive";
    public string PrimaryEngineId { get; set; } = string.Empty;
    public string VanillaDisposition { get; set; } = "cut_content_unreachable";
    public string CompatibilityStatus { get; set; } = "placeholder_requires_reachable_adapter";
    public string[] NativeRuntimeTypes { get; set; } = Array.Empty<string>();
}

public static class CompatibilitySemanticActionPlaceholderCatalog
{
    private static readonly IReadOnlyList<CompatibilitySemanticActionPlaceholderDeclaration> Rows =
        new ReadOnlyCollection<CompatibilitySemanticActionPlaceholderDeclaration>(new[]
        {
            P("executor.toggle_lantern", "tool", "engine.tool_harvest", "Lantern"),
            P("executor.use_raft", "movement", "engine.movement_navigation", "Raft")
        });

    private static readonly IReadOnlyDictionary<string, CompatibilitySemanticActionPlaceholderDeclaration> ById =
        new ReadOnlyDictionary<string, CompatibilitySemanticActionPlaceholderDeclaration>(
            Rows.ToDictionary(row => row.ActionId, StringComparer.Ordinal));

    static CompatibilitySemanticActionPlaceholderCatalog()
    {
        var overlap = Rows
            .Where(row => OptionCapabilityRegistrySource.TryGet(row.ActionId, out _) ||
                PendingSemanticActionCatalog.TryGet(row.ActionId, out _))
            .Select(row => row.ActionId)
            .ToArray();
        if (overlap.Length > 0)
        {
            throw new InvalidOperationException(
                "Compatibility placeholders overlap vanilla semantic actions: " +
                string.Join(",", overlap));
        }
    }

    public static IReadOnlyList<CompatibilitySemanticActionPlaceholderDeclaration> All => Rows;

    public static bool TryGet(
        string actionId,
        out CompatibilitySemanticActionPlaceholderDeclaration declaration) =>
        ById.TryGetValue(actionId, out declaration!);

    private static CompatibilitySemanticActionPlaceholderDeclaration P(
        string id,
        string domain,
        string engine,
        params string[] runtimeTypes) =>
        new()
        {
            ActionId = id,
            Domain = domain,
            PrimaryEngineId = engine,
            NativeRuntimeTypes = runtimeTypes
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };
}
