namespace StardewAI.Core.Tests;

public sealed class RockCrabCombatRecoverySourceGuardTests
{
    [Fact]
    public void NativeShellRecoveryRunsBeforeTheGenericInvulnerabilityWait()
    {
        var combat = ReadHarnessFile("ModEntry.Mining.Combat.cs");
        var recovery = combat.IndexOf("TickRockCrabShellRecovery(active, releasedAttackThisTick)", StringComparison.Ordinal);
        var wait = combat.IndexOf("if (active.Target.isInvincible())", StringComparison.Ordinal);

        Assert.True(recovery >= 0, "A closed rock-crab shell needs native tool recovery before waiting for ordinary hit immunity.");
        Assert.True(wait > recovery);
        Assert.Contains("!active.RockCrabShellHealthBefore.HasValue &&", combat, StringComparison.Ordinal);
    }

    [Fact]
    public void ShellRecoveryUsesNativeToolLifecycleAndRequiresAnObservedShellChange()
    {
        var source = ReadHarnessFile("ModEntry.Mining.Combat.RockCrab.cs");
        Assert.Contains("crab.isStickBug.Value || crab.shellGone.Value", source, StringComparison.Ordinal);
        Assert.Contains("FindTool<Pickaxe>()", source, StringComparison.Ordinal);
        Assert.Contains("releasedAttackThisTick || Game1.player.UsingTool", source, StringComparison.Ordinal);
        Assert.Contains("RockCrabShellLifecycle.Advance(ObserveNativeToolAction())", source, StringComparison.Ordinal);
        Assert.Contains("SButton.C, pressed: true", source, StringComparison.Ordinal);
        Assert.Contains("case NativeToolActionCommand.Release:", source, StringComparison.Ordinal);
        Assert.Contains("if (crab.shellHealth.Value >= before)", source, StringComparison.Ordinal);
        Assert.Contains("rock_crab_shell_native_change_not_observed", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.hitWithTool(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.takeDamage(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.shellGone.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.shellHealth.Value =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.waiter =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("crab.Health =", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("private void TickCombatMonster()", "private void TickManualAutoCombat()")]
    [InlineData("private void CompleteCombatMonster(", "private void CompleteCombatMonsterBlocked(")]
    [InlineData("private void CompleteCombatMonsterBlocked(", "private static string CombatObservedEffect(")]
    public void CombatCompletionFailureAndFoodReleaseTheSeparatePickaxeButton(string start, string end)
    {
        var combat = ReadHarnessFile("ModEntry.Mining.Combat.cs");
        var from = combat.IndexOf(start, StringComparison.Ordinal);
        var to = combat.IndexOf(end, from, StringComparison.Ordinal);
        Assert.True(from >= 0 && to > from);
        Assert.Contains("ReleaseRockCrabShellInput(active);", combat[from..to], StringComparison.Ordinal);
    }

    private static string ReadHarnessFile(string name)
    {
        var root = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "StardewValleyAICompanion.sln")))
            root = root.Parent;
        return File.ReadAllText(Path.Combine(root?.FullName ?? throw new InvalidOperationException("Repository root missing."),
            "tools", "StardewAI.RuntimeTestHarness", name));
    }
}
