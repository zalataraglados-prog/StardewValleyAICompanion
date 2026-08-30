namespace StardewAI.Core.Tests;

internal static class RuntimeHarnessSources
{
    public static readonly string All = Load();

    public static string LoadFile(string fileName)
    {
        var directory = Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness");
        return System.IO.File.ReadAllText(Path.Combine(directory, fileName));
    }

    private static string Load()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness");
        var order = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ModEntry.cs"] = 0,
            ["ModEntry.MovementSleep.cs"] = 10,
            ["ModEntry.MovementSleep.ObstacleClearance.cs"] = 11,
            ["ModEntry.MovementSleep.ResultsConnector.cs"] = 12,
            ["ModEntry.MovementSleep.PathingInput.cs"] = 13,
            ["ModEntry.Sleep.cs"] = 20,
            ["ModEntry.Farming.cs"] = 30,
            ["ModEntry.Dialogue.cs"] = 40,
            ["ModEntry.SkullKey.cs"] = 50,
            ["ModEntry.Interact.cs"] = 60,
            ["ModEntry.Social.cs"] = 70,
            ["ModEntry.DialogueChoice.cs"] = 80,
            ["ModEntry.Shop.cs"] = 90,
            ["ModEntry.ExecutionCommon.cs"] = 100,
            ["ModEntry.MiningResources.cs"] = 110,
            ["ModEntry.Volcano.cs"] = 120,
            ["ModEntry.Volcano.Obstacle.cs"] = 121,
            ["ModEntry.Volcano.Combat.cs"] = 122,
            ["ModEntry.Mining.Container.cs"] = 130,
            ["ModEntry.Mining.Traversal.cs"] = 140,
            ["ModEntry.Mining.Consumable.cs"] = 150,
            ["ModEntry.Mining.Ranged.cs"] = 160,
            ["ModEntry.Mining.Bomb.cs"] = 170,
            ["ModEntry.Mining.Combat.cs"] = 180,
            ["ModEntry.Mining.Fixtures.cs"] = 190,
            ["ModEntry.FarmFixtures.cs"] = 200,
            ["ModEntry.MachinesAndPickup.cs"] = 210,
            ["ModEntry.Fishing.cs"] = 220,
            ["ModEntry.FixtureInventory.cs"] = 230,
            ["ModEntry.Shipping.cs"] = 240,
            ["ModEntry.Shipping.Execution.cs"] = 241,
            ["ModEntry.Shipping.Utilities.cs"] = 242,
            ["ModEntry.State.cs"] = 250,
            ["ModEntry.State.Mining.cs"] = 251,
            ["ModEntry.State.Volcano.cs"] = 252,
            ["ModEntry.State.Combat.cs"] = 253,
            ["ModEntry.State.Setup.cs"] = 254,
            ["ModEntry.State.RecoveryShipping.cs"] = 255
        };

        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(directory, "ModEntry*.cs")
                .OrderBy(path => order.TryGetValue(Path.GetFileName(path), out var index) ? index : int.MaxValue)
                .ThenBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
