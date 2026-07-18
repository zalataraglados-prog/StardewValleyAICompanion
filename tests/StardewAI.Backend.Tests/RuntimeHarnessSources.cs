namespace StardewAI.Backend.Tests;

internal static class RuntimeHarnessSources
{
    public static readonly string All = Load();

    public static string File(string fileName)
    {
        return System.IO.File.ReadAllText(Path.Combine(FindHarnessDirectory(), fileName));
    }

    public static string RepositoryFile(params string[] segments)
    {
        return System.IO.File.ReadAllText(Path.Combine(new[] { FindRepositoryRoot() }.Concat(segments).ToArray()));
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
        return Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Cannot find repository root.");
    }
}
