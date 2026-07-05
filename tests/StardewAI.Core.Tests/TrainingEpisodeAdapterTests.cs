using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests
{
    public sealed class TrainingEpisodeAdapterTests
    {
        [Fact]
        public void BuildSeparatesStrategyFeasibilityAndExecutorCalibration()
        {
            var queue = new ActionQueueEnvelope
            {
                QueueId = "queue.test",
                StateHash = "hash.before",
                SourceModel = "mock-small-model.rule.v1",
                GoalId = "goal.mine",
                Status = "pending",
                ExecutionMode = "training_singleplayer",
                Actor = new ActionActorRef
                {
                    ActorId = "training_farmer.main",
                    ActorType = "training_farmer",
                    ControlSurface = "training_sandbox"
                },
                Items = new[]
                {
                    new ActionQueueItem
                    {
                        QueueItemId = "item.mine",
                        OptionId = "exploration.visit_location",
                        Status = "pending"
                    }
                }
            };

            var timeBudget = new TimeBudgetReport
            {
                StateHash = "hash.before",
                ExecutionProfile = "perfect_human_player",
                FitsRequired = true,
                FitsRequiredPlusOptional = true,
                Items = new[]
                {
                    new TimeBudgetItem
                    {
                        OptionId = "exploration.visit_location",
                        EstimatedMinutes = 85,
                        Estimator = "mining_perfect_executor.v1",
                        Notes = new[]
                        {
                            "assumption_domain:mining_and_combat",
                            "preference_penalty_exclusions:bad_dodging,poor_path_micro",
                            "random_mine_layout_affects_calibration_not_low_level_failure_penalty",
                            "decompile_evidence:MineShaft.mineLevel"
                        }
                    }
                }
            };

            var transition = new SimulatedTransitionResult
            {
                BeforeStateHash = "hash.before",
                AfterStateHash = "hash.after",
                Blocked = false,
                AppliedOptionIds = new[] { "exploration.visit_location" },
                ChangedFacts = Array.Empty<SimulatedFactChange>(),
                ResourceCosts = Array.Empty<SimulatedResourceCost>()
            };

            var episode = new TrainingEpisodeAdapter().Build(queue, timeBudget, transition);

            Assert.Equal("training_episode.v1", episode.SchemaVersion);
            Assert.Equal("queue.test", episode.QueueId);
            Assert.False(episode.HardFeasibility.Blocked);
            Assert.Empty(episode.HardFeasibility.BlockReasons);
            Assert.Equal(0, episode.StrategyValue.GoalProgressDelta);
            Assert.Contains("bad_dodging", episode.StrategyValue.ExcludedExecutorFailures);
            Assert.Contains("poor_path_micro", episode.StrategyValue.ExcludedExecutorFailures);
            Assert.Equal("perfect_human_player", episode.ExecutorCalibration.ExecutionProfile);
            Assert.Equal("hash.before", episode.ExecutorCalibration.BeforeStateHash);
            Assert.Equal("hash.after", episode.ExecutorCalibration.AfterStateHash);
            Assert.Contains("exploration.visit_location", episode.ExecutorCalibration.AppliedOptionIds);
            Assert.Contains(episode.ExecutorCalibration.CalibrationNotes, note => note == "assumption_domain:mining_and_combat");
            Assert.Contains(episode.ExecutorCalibration.CalibrationNotes, note => note == "random_mine_layout_affects_calibration_not_low_level_failure_penalty");
            Assert.Contains(episode.ExecutorCalibration.CalibrationNotes, note => note == "decompile_evidence:MineShaft.mineLevel");
        }

        [Fact]
        public void BuildAddsRewardOnlyFromSimulatedFactChanges()
        {
            var queue = new ActionQueueEnvelope
            {
                QueueId = "queue.farm",
                StateHash = "hash.before",
                SourceModel = "mock-small-model.rule.v1",
                GoalId = "goal.farm",
                Status = "pending",
                ExecutionMode = "training_singleplayer",
                Items = new[]
                {
                    new ActionQueueItem
                    {
                        QueueItemId = "item.farm",
                        OptionId = "farm.maintain_crops",
                        Status = "pending"
                    }
                }
            };
            var timeBudget = new TimeBudgetReport
            {
                StateHash = "hash.before",
                ExecutionProfile = "perfect_human_player",
                FitsRequired = true
            };
            var transition = new SimulatedTransitionResult
            {
                BeforeStateHash = "hash.before",
                AfterStateHash = "hash.after",
                AppliedOptionIds = new[] { "farm.maintain_crops" },
                ChangedFacts = new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[1,2].needs_watering",
                        Before = "true",
                        After = "false"
                    },
                    new SimulatedFactChange
                    {
                        Path = "farm.crops[1,2].watered",
                        Before = "false",
                        After = "true"
                    }
                },
                ResourceCosts = new[]
                {
                    new SimulatedResourceCost { Resource = "player.energy", Amount = 2 }
                }
            };

            var episode = new TrainingEpisodeAdapter().Build(queue, timeBudget, transition);

            Assert.Equal(0.09, episode.StrategyValue.GoalProgressDelta);
            Assert.Contains(episode.StrategyValue.RewardTerms, item =>
                item.Name == "crop_watered" &&
                item.Value == 0.10 &&
                item.Source == "simulated_transition.changed_facts");
            Assert.Contains(episode.StrategyValue.RewardTerms, item =>
                item.Name == "energy_spent" &&
                item.Value == -0.01 &&
                item.Source == "simulated_transition.resource_costs");
        }

        [Fact]
        public void BuildMarksHardFeasibilityBlockedWithoutCreatingStrategyPenalty()
        {
            var queue = new ActionQueueEnvelope
            {
                QueueId = "queue.blocked",
                StateHash = "hash.before",
                SourceModel = "mock-small-model.rule.v1",
                GoalId = "goal.shop",
                Status = "blocked",
                CompilerDiagnostics = new[] { "missing_shop_target" }
            };

            var timeBudget = new TimeBudgetReport
            {
                StateHash = "hash.before",
                ExecutionProfile = "perfect_human_player",
                FitsRequired = false,
                BlockReasons = new[] { "required_time_exceeds_day" }
            };

            var transition = new SimulatedTransitionResult
            {
                BeforeStateHash = "hash.before",
                AfterStateHash = "hash.before",
                Blocked = true,
                BlockReasons = new[] { "unsupported_training_transition:economic_strategic" }
            };

            var episode = new TrainingEpisodeAdapter().Build(queue, timeBudget, transition);

            Assert.True(episode.HardFeasibility.Blocked);
            Assert.Contains("missing_shop_target", episode.HardFeasibility.BlockReasons);
            Assert.Contains("required_time_exceeds_day", episode.HardFeasibility.BlockReasons);
            Assert.Contains("unsupported_training_transition:economic_strategic", episode.HardFeasibility.BlockReasons);
            Assert.Equal(0, episode.StrategyValue.GoalProgressDelta);
            Assert.Empty(episode.StrategyValue.RewardTerms);
        }
    }
}
