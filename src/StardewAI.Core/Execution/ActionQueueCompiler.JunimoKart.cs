using System;
using System.Globalization;
using StardewAI.Contracts.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompilePlayJunimoKartStep(
        SmallModelAction action)
    {
        var target = ReadParameter(action, "minigame_target_score");
        var attempts = ReadParameter(action, "minigame_max_attempts");
        if (!int.TryParse(target, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetScore) || targetScore <= 0 ||
            !int.TryParse(attempts, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxAttempts) || maxAttempts <= 0)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "play_junimo_kart",
                "MineCart:endless:target_score=" + targetScore + ":max_attempts=" + maxAttempts,
                "native_score_submitted_and_matching_JKScoreObjective_progress_verified",
                54000)
        };
    }
}
