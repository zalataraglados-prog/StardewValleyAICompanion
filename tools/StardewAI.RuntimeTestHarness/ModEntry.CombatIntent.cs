using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Monsters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool TryResolveCombatIntent(
        TrainingExecutionRequest request,
        out string combatIntent)
    {
        combatIntent = TrainingCombatIntentRules.Normalize(
            request.CombatIntent);
        return TrainingCombatIntentRules.IsSupported(combatIntent);
    }

    private static bool ShouldDisengageCombatIntent(
        string combatIntent,
        Point initialTargetTile,
        Monster target)
    {
        return TrainingCombatIntentRules.ShouldDisengage(
            combatIntent,
            ManhattanDistance(
                Game1.player.TilePoint,
                target.TilePoint),
            ManhattanDistance(
                initialTargetTile,
                target.TilePoint));
    }
}
