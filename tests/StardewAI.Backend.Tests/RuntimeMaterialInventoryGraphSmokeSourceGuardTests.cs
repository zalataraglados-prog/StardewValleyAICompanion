namespace StardewAI.Backend.Tests;

public sealed class RuntimeMaterialInventoryGraphSmokeSourceGuardTests
{
    [Fact]
    public void HarnessBuildsNativeContainerAndMachineStateMatrix()
    {
        var dispatch = RuntimeHarnessSources.File("ModEntry.cs");
        var fixture = RuntimeHarnessSources.File("ModEntry.MaterialInventoryGraph.cs");
        var allowlist = RuntimeHarnessSources.File("ModEntry.SupportedOptions.cs");

        Assert.Contains("debug.setup_material_inventory_graph", dispatch, StringComparison.Ordinal);
        Assert.Contains("debug.setup_material_inventory_graph", allowlist, StringComparison.Ordinal);
        Assert.Contains("FarmerTeam.GlobalInventoryId_JunimoChest", fixture, StringComparison.Ordinal);
        Assert.Equal(2, Count(fixture, "CreateOwnedChest(junimo"));
        Assert.Contains("new Workbench(workbenchTile)", fixture, StringComparison.Ordinal);
        Assert.Contains("CreateFixtureChest(bigTile, \"BigChest\"", fixture, StringComparison.Ordinal);
        Assert.Contains("new Chest(\"216\", tile, 217, 2)", fixture, StringComparison.Ordinal);
        Assert.Contains("autoGrabber.heldObject.Value = autoGrabberChest", fixture, StringComparison.Ordinal);
        Assert.Contains("CreateFixtureMachine(readyMachineTile, ready: true)", fixture, StringComparison.Ordinal);
        Assert.Contains("CreateFixtureMachine(processingMachineTile, ready: false)", fixture, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSmokeRequiresJunimoDeduplicationAndSupplyStateSeparation()
    {
        var script = RuntimeHarnessSources.RepositoryFile("scripts", "Invoke-RuntimeMaterialInventoryGraphSmoke.ps1");

        Assert.Contains("deduplicated_access_point_count", script, StringComparison.Ordinal);
        Assert.Contains("JunimoChests", script, StringComparison.Ordinal);
        Assert.Contains("ready_output_quantity", script, StringComparison.Ordinal);
        Assert.Contains("in_process_quantity", script, StringComparison.Ordinal);
        Assert.Contains("workbench_links", script, StringComparison.Ordinal);
        Assert.Contains("WindowStyle Hidden", script, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) =>
        source.Split(value, StringSplitOptions.None).Length - 1;
}
