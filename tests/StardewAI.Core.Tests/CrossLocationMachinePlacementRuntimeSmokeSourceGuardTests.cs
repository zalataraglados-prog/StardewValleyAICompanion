namespace StardewAI.Core.Tests;

public sealed class CrossLocationMachinePlacementRuntimeSmokeSourceGuardTests
{
    [Fact]
    public void SmokeUsesTypedContinuationFreshSnapshotsAndHiddenIsolation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeCrossLocationMachinePlacementSmoke.ps1"));

        Assert.Contains(
            "?profile=training_machine",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "--daily-plan-candidate-id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"continuation.machine_location_id\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"continuation.machine_inventory_slot_index\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"continuation.machine_qualified_item_id\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "return if (",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "objective_continuation_completed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.traverse_connector",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.place_machine",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "-WindowStyle Hidden",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SDL_AUDIODRIVER",
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
