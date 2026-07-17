namespace StardewAI.Core.Tests;

internal static class DailyPlanCompilerSources
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
        var trainingDirectory = Path.Combine(root, "src", "StardewAI.Core", "Training");
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(trainingDirectory, "DailyPlanCompiler*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
