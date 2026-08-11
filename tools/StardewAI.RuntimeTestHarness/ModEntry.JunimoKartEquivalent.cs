using System;
using System.Globalization;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickEquivalentJunimoKart(ActiveJunimoKart active)
    {
        active.ElapsedTicks++;
        active.EquivalentElapsedTicks = Math.Min(
            active.EquivalentDurationTicks,
            active.EquivalentElapsedTicks + active.EquivalentAcceleration);
        if (!ReferenceEquals(Game1.currentMinigame, active.Game))
        {
            CompleteBlockedJunimoKart(
                active,
                "junimo_kart_minigame_closed_before_equivalent_timer_elapsed");
            return;
        }
        if (active.EquivalentElapsedTicks < active.EquivalentDurationTicks)
        {
            return;
        }

        MineCartScoreField.SetValue(active.Game, active.TargetScore);
        active.Game.UpdateScoreState();
        active.Game.submitHighScore();
        var progressAfter = active.Objective.GetCount();
        if (progressAfter < active.TargetScore)
        {
            CompleteBlockedJunimoKart(
                active,
                "junimo_kart_equivalent_native_submission_receipt_mismatch");
            return;
        }

        CompleteEquivalentJunimoKart(active, progressAfter);
    }

    private void CompleteEquivalentJunimoKart(
        ActiveJunimoKart active,
        int progressAfter)
    {
        activeJunimoKart = null;
        ReleaseSmapiLeftButtonOverride();
        var request = active.Pending.Request;
        var observed = JunimoKartObservedEffect(active.Game) +
            ";execution_strategy=timed_equivalent" +
            ";equivalent_duration_ticks=" + active.EquivalentDurationTicks +
            ";wall_ticks=" + active.ElapsedTicks +
            ";acceleration=" + active.EquivalentAcceleration;
        active.Game.QuitGame();
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "play_junimo_kart",
            PrimitiveVerificationStatus = "simulated_equivalent",
            PrimitiveVerificationReasons = new[]
            {
                "training_singleplayer_timed_equivalent_elapsed",
                "native_MineCart_submitHighScore_callback_invoked",
                "matching_JKScoreObjective_progress_reached_target",
                "synthetic_score_assignment_not_native_perfect_play"
            },
            RequestedEffect = JunimoKartRequestedEffect(request),
            ObservedEffect = observed,
            QuestProgressBefore = active.ProgressBefore,
            QuestProgressAfter = progressAfter,
            QuestTargetCount = active.TargetScore,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "quests.special_orders.jk_score.current_count",
                    Before = active.ProgressBefore.ToString(CultureInfo.InvariantCulture),
                    After = progressAfter.ToString(CultureInfo.InvariantCulture)
                },
                new SimulatedFactChange
                {
                    Path = "minigame.junimo_kart.execution_strategy",
                    Before = "native_perfect_available",
                    After = "timed_equivalent"
                },
                new SimulatedFactChange
                {
                    Path = "minigame.junimo_kart.equivalent_elapsed_ticks",
                    Before = "0",
                    After = active.EquivalentDurationTicks.ToString(CultureInfo.InvariantCulture)
                }
            }
        });
    }
}
