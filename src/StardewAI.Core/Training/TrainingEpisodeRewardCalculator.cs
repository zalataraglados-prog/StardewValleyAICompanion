using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class TrainingEpisodeRewardCalculator
    {
        private const double WateredCropReward = 0.10;
        private const double EnergyCostWeight = 0.005;

        public StrategyValueFeedback Calculate(ActionQueueEnvelope queue, SimulatedTransitionResult transition)
        {
            if (queue.Status == "blocked" || transition.Blocked)
            {
                return new StrategyValueFeedback();
            }

            var terms = new List<EpisodeRewardTerm>();
            AddMaintainCropTerms(queue, transition, terms);

            return new StrategyValueFeedback
            {
                GoalProgressDelta = Math.Round(terms.Sum(item => item.Value), 4),
                RewardTerms = terms.ToArray()
            };
        }

        private static void AddMaintainCropTerms(
            ActionQueueEnvelope queue,
            SimulatedTransitionResult transition,
            List<EpisodeRewardTerm> terms)
        {
            if (!queue.Items.Any(item => item.OptionId == "executor.water_crop"))
            {
                return;
            }

            var wateredCrops = transition.ChangedFacts.Count(item =>
                item.Path.StartsWith("current_location.crops[", StringComparison.Ordinal) &&
                item.Path.EndsWith("].needs_watering", StringComparison.Ordinal) &&
                item.Before == "true" &&
                item.After == "false");
            if (wateredCrops > 0)
            {
                terms.Add(new EpisodeRewardTerm
                {
                    Name = "crop_watered",
                    Value = Math.Round(wateredCrops * WateredCropReward, 4),
                    Source = "simulated_transition.changed_facts"
                });
            }

            var energyCost = transition.ResourceCosts
                .Where(item => item.Resource == "player.energy")
                .Sum(item => item.Amount);
            if (energyCost > 0)
            {
                terms.Add(new EpisodeRewardTerm
                {
                    Name = "energy_spent",
                    Value = Math.Round(-energyCost * EnergyCostWeight, 4),
                    Source = "simulated_transition.resource_costs"
                });
            }
        }
    }
}
