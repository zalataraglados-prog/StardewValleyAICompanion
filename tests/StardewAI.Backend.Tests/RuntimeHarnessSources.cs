namespace StardewAI.Backend.Tests;

internal static class RuntimeHarnessSources
{
    public static readonly string All = Load();

    public static string File(string fileName)
    {
        return System.IO.File.ReadAllText(Path.Combine(FindHarnessDirectory(), fileName));
    }

    private static string Load()
    {
        var harness = FindHarnessDirectory();
        return string.Join(
            "\n// --- FILE BOUNDARY ---\n",
            Directory.GetFiles(harness, "ModEntry*.cs")
                .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                .Select(System.IO.File.ReadAllText));
    }

    private static string FindHarnessDirectory()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        var root = directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
        return Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness");
    }
}
