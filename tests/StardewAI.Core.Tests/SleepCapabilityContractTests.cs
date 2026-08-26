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
    public void SleepPromptConfirmationIsExposedOnlyForExactOpenPrompt()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "MenuReadAdapter.cs"));
        var sleepPromptContext = Slice(source, "private static object ReadSleepPromptContext", "private static object ReadIdentity");

        Assert.Contains("promptOpen = menu is DialogueBox", sleepPromptContext, StringComparison.Ordinal);
        Assert.Contains("can_confirm_sleep = promptOpen", sleepPromptContext, StringComparison.Ordinal);
        Assert.Contains("confirm_executor_enabled = promptOpen", sleepPromptContext, StringComparison.Ordinal);
        Assert.Contains("confirm_action_key = \"Sleep_Yes\"", sleepPromptContext, StringComparison.Ordinal);
        Assert.DoesNotContain("sleep_confirm_executor_disabled", sleepPromptContext, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepResumeDoesNotEnableGenericDialogueConfirmation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.Core", "Infrastructure", "SleepPromptResumeProjection.cs"));

        Assert.Contains("existing_exact_prompt", source, StringComparison.Ordinal);
        Assert.Contains("sleep_resume_active_menu_not_dialogue_box", source, StringComparison.Ordinal);
        Assert.Contains("sleep_resume_question_key_not_sleep", source, StringComparison.Ordinal);
        Assert.Contains("sleep_resume_player_not_at_or_adjacent_to_bed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NativeSleepPrefersTheCurrentStandAndWaitsForAStableNewDay()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Sleep.cs"));

        Assert.Contains(".OrderBy(tile => tile == startTile ? 0 : 1)", source, StringComparison.Ordinal);
        Assert.Contains("NativeNewDayWorldStable(sleep)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.timeOfDay >= 600 && Game1.timeOfDay < 700", source, StringComparison.Ordinal);
        Assert.Contains("native_new_day_world_stable", source, StringComparison.Ordinal);
        Assert.Contains("post_sleep_world_not_stable", source, StringComparison.Ordinal);
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
