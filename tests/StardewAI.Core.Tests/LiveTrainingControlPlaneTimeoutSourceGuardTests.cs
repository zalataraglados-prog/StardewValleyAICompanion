namespace StardewAI.Core.Tests;

public sealed class LiveTrainingControlPlaneTimeoutSourceGuardTests
{
    [Fact]
    public void RuntimeReplanningCannotTimeOutBeforeTheExecutorBudget()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs"));

        Assert.Contains(
            "Timeout = TimeSpan.FromSeconds(Math.Max(180, options.ExecutorTimeoutSeconds))",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(segments));
    }
}
