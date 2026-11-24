using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool TickRockCrabShellRecovery(ActiveCombatMonster active, bool releasedAttackThisTick)
    {
        if (active.Target is not RockCrab crab)
            return false;

        if (!active.RockCrabShellHealthBefore.HasValue)
        {
            if (crab.isStickBug.Value || crab.shellGone.Value || crab.shellHealth.Value <= 0 ||
                crab.Sprite.currentFrame % 4 != 0 || !crab.isInvincible() ||
                !IsMonsterWithinCombatReach(crab, active.Weapon))
                return false;

            if (releasedAttackThisTick || Game1.player.UsingTool ||
                Game1.activeClickableMenu is not null || Game1.eventUp)
                return true;

            var pickaxe = FindTool<Pickaxe>();
            if (pickaxe is null)
            {
                CompleteCombatMonsterBlocked(active, "rock_crab_shell_requires_native_pickaxe");
                return true;
            }

            // RockCrab.hitWithTool wakes a stationary waiter and reduces shell
            // health. Waiting for ordinary hit immunity cannot cause that change.
            active.RockCrabShellPickaxe = pickaxe;
            active.RockCrabShellHealthBefore = crab.shellHealth.Value;
            active.RockCrabWaiterBefore = crab.waiter;
            active.RockCrabShellLifecycle.Reset();
        }

        StopAllMovement();
        if (releasedAttackThisTick || Game1.activeClickableMenu is not null || Game1.eventUp)
            return true;
        if (active.RockCrabShellPickaxe is not { } tool || !Game1.player.Items.Contains(tool))
        {
            CompleteCombatMonsterBlocked(active, "rock_crab_shell_pickaxe_no_longer_held");
            return true;
        }

        SelectTool(tool);
        var decision = active.RockCrabShellLifecycle.Advance(ObserveNativeToolAction());
        switch (decision.Command)
        {
            case NativeToolActionCommand.Press:
                var target = crab.GetBoundingBox().Center;
                Game1.player.faceDirection(DirectionToPixel(Game1.player.GetBoundingBox().Center, target, Game1.player.FacingDirection));
                Game1.player.lastClick = new Vector2(target.X, target.Y);
                if (!TryApplySmapiButtonOverride(SButton.C, pressed: true, out var pressReason))
                    CompleteCombatMonsterBlocked(active, "rock_crab_shell_input:" + pressReason);
                break;
            case NativeToolActionCommand.Release:
                if (!TryApplySmapiButtonOverride(SButton.C, pressed: false, out var releaseReason))
                    CompleteCombatMonsterBlocked(active, "rock_crab_shell_release:" + releaseReason);
                break;
            case NativeToolActionCommand.Block:
                TryApplySmapiButtonOverride(SButton.C, pressed: false, out _);
                CompleteCombatMonsterBlocked(active, "rock_crab_shell_native_cycle:" + decision.Reason);
                break;
            case NativeToolActionCommand.CycleCompleted:
                if (!TryApplySmapiButtonOverride(SButton.C, pressed: false, out var completionReleaseReason))
                {
                    CompleteCombatMonsterBlocked(active, "rock_crab_shell_release:" + completionReleaseReason);
                    break;
                }
                var before = active.RockCrabShellHealthBefore.Value;
                if (crab.shellHealth.Value >= before)
                {
                    CompleteCombatMonsterBlocked(active, "rock_crab_shell_native_change_not_observed");
                    break;
                }
                active.Pending.ChangedFacts.Add(new SimulatedFactChange
                {
                    Path = "mining.monsters[" + active.TargetRuntimeIdentity + "].shell_health",
                    Before = before.ToString(),
                    After = crab.shellHealth.Value.ToString()
                });
                active.Pending.ChangedFacts.Add(new SimulatedFactChange
                {
                    Path = "mining.monsters[" + active.TargetRuntimeIdentity + "].waiter",
                    Before = active.RockCrabWaiterBefore.ToString(),
                    After = crab.waiter.ToString()
                });
                Monitor.Log($"Rock crab native shell recovery [{active.TargetRuntimeIdentity}]: shell={before}->{crab.shellHealth.Value}, waiter={active.RockCrabWaiterBefore}->{crab.waiter}.", LogLevel.Info);
                active.RockCrabShellHealthBefore = null;
                active.RockCrabShellPickaxe = null;
                active.Path.Clear();
                active.PathIndex = 0;
                active.NoProgressTicks = 0;
                break;
        }
        return true;
    }

    private void ReleaseRockCrabShellInput(ActiveCombatMonster active)
    {
        if (active.RockCrabShellHealthBefore.HasValue &&
            TryApplySmapiButtonOverride(SButton.C, pressed: false, out _))
        {
            active.RockCrabShellLifecycle.NotifyInputReleased();
        }
    }
}
