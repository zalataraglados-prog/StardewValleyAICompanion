namespace StardewAI.Core.Tests;

public sealed class GoalConditionedBootstrapDebugExceptionSourceGuardTests
{
    [Fact]
    public void ExplicitDebugModeRethrowsBeforeStructuredErrorSerialization()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "Program.cs"));

        var debugGuard = source.IndexOf(
            "Environment.GetEnvironmentVariable(\"STARDEWAI_BOOTSTRAP_DEBUG_EXCEPTIONS\")",
            StringComparison.Ordinal);
        var rethrow = source.IndexOf("throw;", debugGuard, StringComparison.Ordinal);
        var structuredError = source.IndexOf(
            "Console.Error.WriteLine(JsonSerializer.Serialize",
            rethrow,
            StringComparison.Ordinal);

        Assert.True(debugGuard >= 0, "The explicit bootstrap debug switch is missing.");
        Assert.True(rethrow > debugGuard, "Debug mode must rethrow the original exception.");
        Assert.True(
            structuredError > rethrow,
            "Debug rethrow must occur before the default structured error serialization.");
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
