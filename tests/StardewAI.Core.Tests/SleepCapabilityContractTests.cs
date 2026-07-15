namespace StardewAI.Core.Tests;

public sealed class SleepCapabilityContractTests
{
    [Fact]
    public void HomeContextReportsTerminalSleepExecutorEnabled()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.cs"));
        var homeContext = Slice(source, "private static object ReadHomeContext", "private static object ReadRouteContext");

        Assert.Contains("sleep_executor_enabled = true", homeContext, StringComparison.Ordinal);
        Assert.DoesNotContain("sleep_executor_enabled = false", homeContext, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneSleepPromptConfirmationRemainsDisabled()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "MenuReadAdapter.cs"));
        var sleepPromptContext = Slice(source, "private static object ReadSleepPromptContext", "private static object ReadIdentity");

        Assert.Contains("can_confirm_sleep = false", sleepPromptContext, StringComparison.Ordinal);
        Assert.Contains("confirm_executor_enabled = false", sleepPromptContext, StringComparison.Ordinal);
        Assert.Contains("sleep_confirm_executor_disabled", sleepPromptContext, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker: " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker: " + endMarker);
        return source[start..end];
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }
}
