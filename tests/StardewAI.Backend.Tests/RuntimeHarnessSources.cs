namespace StardewAI.Backend.Tests;

internal static class RuntimeHarnessSources
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
        var harness = Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness");
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(harness, "ModEntry*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }
}
