using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class TrainingEpisodeAdapter
    {
        private readonly TrainingEpisodeRewardCalculator rewardCalculator;

        public TrainingEpisodeAdapter()
            : this(new TrainingEpisodeRewardCalculator())
        {
        }

        public TrainingEpisodeAdapter(TrainingEpisodeRewardCalculator rewardCalculator)
        {
            this.rewardCalculator = rewardCalculator;
        }

        public TrainingEpisodeEnvelope Build(
            ActionQueueEnvelope queue,
            TimeBudgetReport timeBudget,
            SimulatedTransitionResult transition)
        {
            var feasibilityReasons = new List<string>();
            feasibilityReasons.AddRange(queue.CompilerDiagnostics);
            foreach (var item in queue.Items)
            {
                feasibilityReasons.AddRange(item.BlockingReasons);
            }

            feasibilityReasons.AddRange(timeBudget.BlockReasons);
            feasibilityReasons.AddRange(transition.BlockReasons);

            var blocked = queue.Status == "blocked" ||
                transition.Blocked ||
                !timeBudget.FitsRequired;
            var strategyValue = rewardCalculator.Calculate(queue, transition);
            strategyValue.ExcludedExecutorFailures = ExtractExcludedFailures(timeBudget);

            return new TrainingEpisodeEnvelope
            {
                EpisodeId = "episode." + Guid.NewGuid().ToString("N"),
                SourceStateHash = queue.StateHash,
                QueueId = queue.QueueId,
                SourceModel = queue.SourceModel,
                GoalId = queue.GoalId,
                ActionSummary = new EpisodeActionSummary
                {
                    OptionIds = queue.Items.Select(item => item.OptionId).Distinct(StringComparer.Ordinal).ToArray(),
                    ExecutionMode = queue.ExecutionMode,
                    Actor = queue.Actor
                },
                StrategyValue = strategyValue,
                HardFeasibility = new HardFeasibilityFeedback
                {
                    Blocked = blocked,
                    BlockReasons = feasibilityReasons.Distinct(StringComparer.Ordinal).ToArray(),
                    TimeBudget = timeBudget
                },
                ExecutorCalibration = new ExecutorCalibrationFeedback
                {
                    ExecutionProfile = timeBudget.ExecutionProfile,
                    BeforeStateHash = transition.BeforeStateHash,
                    AfterStateHash = transition.AfterStateHash,
                    AppliedOptionIds = transition.AppliedOptionIds,
                    ChangedFacts = transition.ChangedFacts,
                    ResourceCosts = transition.ResourceCosts,
                    DurationItems = timeBudget.Items,
                    CalibrationNotes = ExtractCalibrationNotes(timeBudget)
                },
                CandidateAudit = queue.CandidateAudit
            };
        }

        private static string[] ExtractExcludedFailures(TimeBudgetReport timeBudget)
        {
            return timeBudget.Items
                .SelectMany(item => item.Notes)
                .Where(note => note.StartsWith("preference_penalty_exclusions:", StringComparison.Ordinal))
                .SelectMany(note => note.Substring("preference_penalty_exclusions:".Length).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] ExtractCalibrationNotes(TimeBudgetReport timeBudget)
        {
            return timeBudget.Items
                .SelectMany(item => item.Notes)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
