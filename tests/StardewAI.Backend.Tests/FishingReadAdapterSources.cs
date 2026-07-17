namespace StardewAI.Backend.Tests;

internal static class FishingReadAdapterSources
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
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(adaptersDirectory, "FishingReadAdapter*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
