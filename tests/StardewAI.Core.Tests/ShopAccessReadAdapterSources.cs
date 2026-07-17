namespace StardewAI.Core.Tests;

internal static class ShopAccessReadAdapterSources
{
    public static readonly string All = Load();

    private static string Load()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
        var adaptersDirectory = Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters");
        var order = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["ShopAccessReadAdapter.cs"] = 0,
            ["ShopAccessReadAdapter.RouteGraph.cs"] = 10,
            ["ShopAccessReadAdapter.GatesBlockers.cs"] = 20,
            ["ShopAccessReadAdapter.Connectors.cs"] = 30,
            ["ShopAccessReadAdapter.Collision.cs"] = 40,
            ["ShopAccessReadAdapter.ShopProjection.cs"] = 50
        };
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(adaptersDirectory, "ShopAccessReadAdapter*.cs")
                .OrderBy(path => order.TryGetValue(Path.GetFileName(path), out var index) ? index : int.MaxValue)
                .ThenBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
