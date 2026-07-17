using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using StardewAI.RuntimePrimitives;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool TryTickNativeHeavyHitterAction(
        NativeHeavyHitterActionState state,
        Point target,
        int? currentHealth,
        out string blockReason)
    {
        blockReason = string.Empty;
        if (state.ButtonHeld)
        {
            TryApplySmapiButtonOverride(HeavyHitterInputButton(state.Tool), pressed: false, out _);
            state.ButtonHeld = false;
            return true;
        }

        if (state.Progress.ActionIssued)
        {
            if (Game1.player.UsingTool || !Game1.player.CanMove || Game1.player.FarmerSprite.PauseForSingleAnimation)
            {
                return true;
            }

            state.RecordCompletedSwing(currentHealth);
            return true;
        }

        if (Game1.player.UsingTool)
        {
            return true;
        }

        if (!state.Progress.CanIssueAction())
        {
            blockReason = "swing_budget_exceeded";
            return false;
        }

        SelectTool(state.Tool);
        Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, target));
        Game1.player.lastClick = new Vector2(
            target.X * Game1.tileSize + Game1.tileSize / 2,
            target.Y * Game1.tileSize + Game1.tileSize / 2);
        if (!TryApplySmapiButtonOverride(HeavyHitterInputButton(state.Tool), pressed: true, out var inputReason))
        {
            blockReason = inputReason;
            return false;
        }

        state.ButtonHeld = true;
        state.Progress.MarkActionIssued();
        return true;
    }

    private void ReleaseNativeHeavyHitterAction(NativeHeavyHitterActionState state)
    {
        TryApplySmapiButtonOverride(HeavyHitterInputButton(state.Tool), pressed: false, out _);
        state.ButtonHeld = false;
    }

    private static SButton HeavyHitterInputButton(Tool tool)
    {
        return tool is MeleeWeapon ? SButton.MouseLeft : SButton.C;
    }

    private sealed class NativeHeavyHitterActionState
    {
        public NativeHeavyHitterActionState(Tool tool, int healthBefore, int maxSwings)
        {
            Tool = tool;
            Progress = new NativeHeavyHitterProgress(healthBefore, maxSwings);
        }

        public Tool Tool { get; }
        public NativeHeavyHitterProgress Progress { get; }
        public int MaxSwings => Progress.MaxSwings;
        public int SwingCount => Progress.SwingCount;
        public bool ButtonHeld { get; set; }
        public IReadOnlyList<int> ObservedHealth => Progress.ObservedHealth;

        public void RecordCompletedSwing(int? remainingHealth)
        {
            Progress.RecordCompletedSwing(remainingHealth);
        }

        public void RecordRemoval()
        {
            Progress.RecordRemoval();
        }
    }
}
