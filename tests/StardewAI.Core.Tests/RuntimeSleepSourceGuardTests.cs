namespace StardewAI.Core.Tests;

public sealed class RuntimeSleepSourceGuardTests
{
    [Fact]
    public void SleepConfirmationUsesTheNativeDialogueKeyDispatchPath()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));

        Assert.Contains(
            "prompt.receiveKeyPress(Microsoft.Xna.Framework.Input.Keys.Y);",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "TryApplySmapiButtonOverride(SButton.Y",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HomeSleepRoutesAroundSoftObstaclesButCanCrossThemWhenRequired()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));
        var start = source.IndexOf(
            "private void StartSleep",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private void TickSleep",
            start,
            StringComparison.Ordinal);
        var slice = source[start..end];

        Assert.Contains(
            "avoidSoftObstacles: false",
            slice,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowRemovableObstacles: false",
            slice,
            StringComparison.Ordinal);

        var tickStart = source.IndexOf(
            "private bool TickSleepMoveToStand",
            StringComparison.Ordinal);
        var tickEnd = source.IndexOf(
            "private bool ApplySleepConfirmInput",
            tickStart,
            StringComparison.Ordinal);
        var tickSlice = source[tickStart..tickEnd];
        Assert.Contains(
            "waitForSoftObstacle: true",
            tickSlice,
            StringComparison.Ordinal);

        var follower = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MovementSleep.PathFollower.cs"));
        Assert.Contains(
            "occupiedByCharacter && waitForSoftObstacle",
            follower,
            StringComparison.Ordinal);
        Assert.Contains(
            "soft_obstacle_timeout",
            follower,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PostSleepLevelUpClosesOnlyNonProfessionMenusThroughNativeInput()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));
        var start = source.IndexOf(
            "private void TickPostSleepLevelUpMenu",
            StringComparison.Ordinal);
        var end = source.IndexOf(
            "private bool TickSleepMoveToStand",
            start,
            StringComparison.Ordinal);
        var slice = source[start..end];

        Assert.Contains("levelUpMenu.isProfessionChooser", slice,
            StringComparison.Ordinal);
        Assert.Contains(
            "post_sleep_profession_choice_requires_planned_selection",
            slice,
            StringComparison.Ordinal);
        Assert.Contains("levelUpMenu.CanReceiveInput()", slice,
            StringComparison.Ordinal);
        Assert.Contains(
            "levelUpMenu.receiveGamePadButton(",
            slice,
            StringComparison.Ordinal);
        Assert.Contains(
            "Microsoft.Xna.Framework.Input.Buttons.B",
            slice,
            StringComparison.Ordinal);
        Assert.DoesNotContain("receiveLeftClick", slice,
            StringComparison.Ordinal);
        Assert.DoesNotContain("okButtonClicked", slice,
            StringComparison.Ordinal);
        Assert.DoesNotContain("exitThisMenu", slice,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.activeClickableMenu = null", slice,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
