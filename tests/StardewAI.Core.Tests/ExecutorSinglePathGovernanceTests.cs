using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.Tests;

public sealed class ExecutorSinglePathGovernanceTests
{
    [Fact]
    public void EveryHarnessOptionHasExactlyOneRuntimeDispatchBranch()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));

        foreach (var optionId in RuntimeTestHarnessDispatchCatalog.OptionIds)
        {
            var pattern = Regex.Escape(
                "pending.Request.OptionId == \"" + optionId + "\"");
            Assert.Single(Regex.Matches(source, pattern).Cast<Match>());
        }
    }

    [Fact]
    public void SleepUsesSharedPathAndNativeInputWithoutDirectDialogueMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));

        Assert.Contains("TryAdvanceExecutorPath(", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.Y, pressed: true", source, StringComparison.Ordinal);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.Y, pressed: false", source, StringComparison.Ordinal);
        Assert.Contains("SleepStage.WaitForNativePrompt", source, StringComparison.Ordinal);
        Assert.Contains("prompt.transitioning || prompt.safetyTimer > 0", source, StringComparison.Ordinal);
        Assert.Contains("SleepStage.WaitForPromptClose", source, StringComparison.Ordinal);
        Assert.DoesNotContain("performTouchAction(\"Sleep\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("answerDialogueAction(\"Sleep_Yes\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.activeClickableMenu = null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.dialogueUp = false", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossMapRecoverySmokeUsesHighLevelRecoveryForRouteAndSleep()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-RuntimeRecoveryCrossMapSmoke.ps1"));

        Assert.Contains("--daily-plan-candidate-options \"recovery.stabilize_day\"", source, StringComparison.Ordinal);
        Assert.Contains("Assert-SingleVerifiedOption $routeArtifacts \"executor.traverse_connector\"", source, StringComparison.Ordinal);
        Assert.Contains("Assert-SingleVerifiedOption $sleepArtifacts \"executor.sleep\"", source, StringComparison.Ordinal);
        Assert.Contains("$routeLoopRoot", source, StringComparison.Ordinal);
        Assert.Contains("$sleepLoopRoot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position", source, StringComparison.Ordinal);
        Assert.DoesNotContain("warpFarmer", source, StringComparison.Ordinal);
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

        throw new FileNotFoundException(
            "Unable to locate repository file.",
            Path.Combine(parts));
    }
}
