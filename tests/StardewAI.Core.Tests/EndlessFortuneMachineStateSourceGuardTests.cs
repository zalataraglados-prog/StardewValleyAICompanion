namespace StardewAI.Core.Tests;

public sealed class
    EndlessFortuneMachineStateSourceGuardTests
{
    [Fact]
    public void EndlessFortuneSeparatesExactAndSharedRngBranches()
    {
        var stateSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.EndlessFortuneState.cs"));
        var dispatchSource = File.ReadAllText(
            FindRepositoryFile(
                "src",
                "StardewAI.TransparentBridge",
                "Adapters",
                "FarmReadAdapter.SpecialMachinePrediction.cs"));

        Assert.Contains(
            "statue_endless_fortune_daily_output.v1",
            stateSource);
        Assert.Contains("(BC)127", stateSource);
        Assert.Contains(
            ": OutputStatueOfEndlessFortune",
            stateSource);
        Assert.Contains(
            "Utility.getTodaysBirthdayNPC()",
            stateSource);
        Assert.Contains(
            "birthdayNpc?.getFavoriteItem()",
            stateSource);
        Assert.Contains("(O)72", stateSource);
        Assert.Contains("(O)337", stateSource);
        Assert.Contains("(O)749", stateSource);
        Assert.Contains("(O)336", stateSource);
        Assert.Contains(
            "conditional_probability = 0.25",
            stateSource);
        Assert.Contains(
            "clears_previous_contents_overnight = true",
            stateSource);
        Assert.Contains(
            "blocked_shared_rng_actual_identity",
            stateSource);
        Assert.Contains(
            "complete_for_vanilla_current_date_branch",
            stateSource);
        Assert.DoesNotContain(
            "Game1.random.",
            stateSource);
        Assert.DoesNotContain(
            "OutputStatueOfEndlessFortune(",
            stateSource);

        Assert.Contains(
            "ReadEndlessFortuneSpecialState(",
            dispatchSource);
        Assert.Contains(
            "IsVettedEndlessFortuneOutputMethod(",
            dispatchSource);
    }

    private static string FindRepositoryFile(
        params string[] segments)
    {
        var current = new DirectoryInfo(
            AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(
                new[] { current.FullName }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Repository file not found: " +
            Path.Combine(segments));
    }
}
