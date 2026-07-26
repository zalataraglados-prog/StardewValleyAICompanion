namespace StardewAI.Core.Tests;

public sealed class StrategicMachineRelocationRuntimeSmokeSourceGuardTests
{
    [Fact]
    public void TrainingMachineProfileIncludesRouteFactsNeededByRelocation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs"));
        var blockStart = source.IndexOf(
            "if (profile is \"daily\" or \"training_machine\")",
            StringComparison.Ordinal);
        var blockEnd = source.IndexOf(
            "if (profile is \"fishing\")",
            blockStart,
            StringComparison.Ordinal);
        var block = source[blockStart..blockEnd];

        Assert.Contains(
            "domains.Add(\"locations\")",
            block,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeUsesPlannerLedgerFreshSnapshotAndHiddenIsolation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeStrategicMachineRelocationSmoke.ps1"));

        Assert.Contains(
            "?profile=training_machine",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"relocate_machine_item\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"place_machine_item\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"route_connector_tile\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "continuation.relocation_intent_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TargetLocationId",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return if (",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/strategy/commitments/latest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "exact_target_machine_observed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "-WindowStyle Hidden",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "STARDEWAI_STRATEGY_LEDGER_DIR",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RelocationProjectionLimitsRemoteTargetsToOwnedMachineClusters()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "Adapters",
            "PlayerReadAdapter.MachinePlacement.cs"));

        Assert.Contains(
            "location.IsPlayerControlled",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "location.Location.objects.Pairs.Any",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "current_source_plus_player_controlled_existing_machine_clusters",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                new[] { current.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate repository file.",
            Path.Combine(parts));
    }
}
