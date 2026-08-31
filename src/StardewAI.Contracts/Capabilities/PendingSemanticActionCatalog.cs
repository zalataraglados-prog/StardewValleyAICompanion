using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Contracts.Capabilities;

public sealed class PendingSemanticActionDeclaration
{
    public string ActionId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SemanticKind { get; set; } = string.Empty;
    public string PrimaryEngineId { get; set; } = string.Empty;
    public string CatalogStatus { get; set; } = "catalogued_blocked";
    public string BlockReason { get; set; } = "option_spec_not_declared";
    public string[] NativeRuntimeTypes { get; set; } = Array.Empty<string>();
}

public static class PendingSemanticActionCatalog
{
    private static readonly IReadOnlyList<PendingSemanticActionDeclaration> Rows =
        new ReadOnlyCollection<PendingSemanticActionDeclaration>(new[]
        {
            C("minigame.play_junimo_kart", "minigame", "composite", "engine.minigame", "MineCart"),
            C("tailoring.dye_item", "tailoring", "composite", "engine.crafting_processing", "DyeMenu")
        });

    private static readonly IReadOnlyDictionary<string, PendingSemanticActionDeclaration> ById =
        new ReadOnlyDictionary<string, PendingSemanticActionDeclaration>(
            Rows.ToDictionary(row => row.ActionId, StringComparer.Ordinal));

    static PendingSemanticActionCatalog()
    {
        var overlap = Rows
            .Where(row => OptionCapabilityRegistrySource.TryGet(row.ActionId, out _))
            .Select(row => row.ActionId)
            .ToArray();
        if (overlap.Length > 0)
            throw new InvalidOperationException(
                "Pending semantic actions overlap registered OptionSpecs: " +
                string.Join(",", overlap));
    }

    public static IReadOnlyList<PendingSemanticActionDeclaration> All => Rows;

    public static bool TryGet(string actionId, out PendingSemanticActionDeclaration declaration) =>
        ById.TryGetValue(actionId, out declaration!);

    private static PendingSemanticActionDeclaration C(
        string id,
        string domain,
        string kind,
        string engine,
        params string[] runtimeTypes) =>
        Create(id, domain, kind, engine, runtimeTypes);

    private static PendingSemanticActionDeclaration P(
        string id,
        string domain,
        string engine,
        params string[] runtimeTypes) =>
        Create(id, domain, "primitive", engine, runtimeTypes);

    private static PendingSemanticActionDeclaration Create(
        string id,
        string domain,
        string kind,
        string engine,
        string[] runtimeTypes)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(engine) ||
            runtimeTypes.Length == 0)
        {
            throw new InvalidOperationException("Pending semantic action declarations must be complete.");
        }

        return new PendingSemanticActionDeclaration
        {
            ActionId = id,
            Domain = domain,
            SemanticKind = kind,
            PrimaryEngineId = engine,
            NativeRuntimeTypes = runtimeTypes
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };
    }
}
