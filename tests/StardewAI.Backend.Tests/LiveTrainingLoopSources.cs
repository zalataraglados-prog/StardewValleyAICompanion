namespace StardewAI.Backend.Tests;

internal static class LiveTrainingLoopSources
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
        var loopDirectory = Path.Combine(root, "tools", "StardewAI.LiveTrainingLoop");
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(loopDirectory, "*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
