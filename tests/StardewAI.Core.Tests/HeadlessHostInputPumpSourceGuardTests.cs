namespace StardewAI.Core.Tests;

public sealed class HeadlessHostInputPumpSourceGuardTests
{
    [Fact]
    public void HiddenOrdinaryDialogueReusesNativeClickOnlyBehindTheOriginalSafetyGates()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Dialogue.cs"));
        var start = source.IndexOf("private void TickDialogueAdvanceCore", StringComparison.Ordinal);
        var end = source.IndexOf("private static TrainingExecutionResult DialogueAdvanceResult", start, StringComparison.Ordinal);
        var tick = source[start..end];
        var guard = tick.IndexOf("!CanAdvanceOrdinaryDialogue(", StringComparison.Ordinal);
        var press = tick.IndexOf("case DialogueAdvanceStage.Press:", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < press);
        var input = tick[press..tick.IndexOf("case DialogueAdvanceStage.WaitAdvanceEffect:", press, StringComparison.Ordinal)];
        Assert.Contains("ShouldSuppressLocalRendering() && Program.releaseBuild &&", input, StringComparison.Ordinal);
        Assert.Contains("!Program.gamePtr.IsActiveNoOverlay", input, StringComparison.Ordinal);
        Assert.Contains("advance.UseNativeMenuClick = true;", input, StringComparison.Ordinal);
        Assert.Contains("currentBox.receiveLeftClick(0, 0);", input, StringComparison.Ordinal);
        Assert.Contains("else if (!TryApplySmapiLeftButtonOverride(pressed: true", input, StringComparison.Ordinal);
        Assert.Contains("!advance.UseNativeMenuClick && !TryApplySmapiLeftButtonOverride(pressed: false", input, StringComparison.Ordinal);
        Assert.DoesNotContain("exitThisMenu", tick, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.dialogueUp =", tick, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.CanMove =", tick, StringComparison.Ordinal);
        Assert.Contains("dialogue_native_menu_click=", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadlessDrawSuppressionPreservesNativeFarmEventProgress()
    {
        var entry = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.cs"));
        var start = entry.IndexOf("internal static class HostLocalDrawPatch", StringComparison.Ordinal);
        var end = entry.IndexOf("internal static class HeadlessSaveGameMenuLifecyclePatch", start, StringComparison.Ordinal);
        var draw = entry[start..end];
        var nativeEvent = draw.IndexOf("if (Game1.farmEvent is not null)", StringComparison.Ordinal);
        Assert.True(nativeEvent >= 0);
        Assert.True(nativeEvent < draw.IndexOf("if (interval <= 0)", StringComparison.Ordinal));
        var nativeBranch = draw[nativeEvent..draw.IndexOf("var interval =", nativeEvent, StringComparison.Ordinal)];
        Assert.Contains("return true;", nativeBranch, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.farmEvent =", draw, StringComparison.Ordinal);
        Assert.DoesNotContain("SetValue", draw, StringComparison.Ordinal);
    }

    [Fact]
    public void HeadlessHostKeepsNativeControlInputActiveWithoutSynthesizingActions()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.HeadlessInput.cs"));
        var entry = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var calibration = File.ReadAllText(FindRepositoryFile(
            "scripts",
            "Invoke-ProductBootstrapCalibration.ps1"));

        Assert.Contains("HeadlessMovementInputSimulator", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_SUPPRESS_LOCAL_RENDER", source, StringComparison.Ordinal);
        Assert.Contains("STARDEWAI_TRAINING_MODE", source, StringComparison.Ordinal);
        Assert.Contains("IsHeadlessTrainingRuntime()", source, StringComparison.Ordinal);
        Assert.Contains("OnHeadlessTrainingSaveLoaded", source, StringComparison.Ordinal);
        Assert.Contains("IsHeadlessTrainingRuntime()", entry, StringComparison.Ordinal);
        Assert.Contains("ShouldSuppressLocalRendering()", entry, StringComparison.Ordinal);
        Assert.Contains("IsVanillaAiHostMode() || IsHeadlessTrainingRuntime()", source, StringComparison.Ordinal);
        Assert.Contains("OnHeadlessTrainingSaveLoaded", entry, StringComparison.Ordinal);
        Assert.Contains("GameInputSimulatorField.SetValue(null, headlessInputPump)", source, StringComparison.Ordinal);
        Assert.Contains("EnsureHeadlessInputPump();", entry, StringComparison.Ordinal);
        Assert.Contains("executorMovementLease.LastTransitionTick", source, StringComparison.Ordinal);
        Assert.Contains("MovementInputEdgeTracker", source, StringComparison.Ordinal);
        Assert.Contains("moveLeftHeld = edges.HeldDirection == 3", source, StringComparison.Ordinal);
        Assert.Contains("moveLeftReleased = edges.ReleasedDirection == 3", source, StringComparison.Ordinal);
        Assert.DoesNotContain("actionButtonPressed =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Position =", source, StringComparison.Ordinal);
        Assert.Contains(
            "$env:STARDEWAI_SUPPRESS_LOCAL_RENDER = \"1\"",
            calibration,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:SDL_AUDIODRIVER = \"dummy\"",
            calibration,
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
