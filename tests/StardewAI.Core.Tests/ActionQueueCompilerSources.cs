namespace StardewAI.Core.Tests;

internal static class ActionQueueCompilerSources
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
        var executionDirectory = Path.Combine(root, "src", "StardewAI.Core", "Execution");
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(executionDirectory, "ActionQueueCompiler*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
