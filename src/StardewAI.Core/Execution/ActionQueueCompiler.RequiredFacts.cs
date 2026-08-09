using System;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] EffectiveRequiredStateFactors(
            SmallModelAction action,
            OptionSpec option)
        {
            var executorProfile = ReadParameter(action, "required_executor_profile");
            if (string.Equals(action.OptionId, "executor.move_to_tile", StringComparison.Ordinal))
            {
                return ReplaceRequiredFactors(
                    option.RequiredStateFactors,
                    "current_location.map",
                    executorProfile switch
                    {
                        "mining_perfect_executor" => "mining.tiles",
                        "volcano_perfect_executor" => "volcano.tiles",
                        _ => string.Empty
                    });
            }

            if (string.Equals(action.OptionId, "executor.interact", StringComparison.Ordinal) &&
                string.Equals(executorProfile, "mining_perfect_executor", StringComparison.Ordinal))
            {
                var interactionFact = ReadParameter(action, "expected_action_type") switch
                {
                    "GoldenScythe" => "mining.tiles",
                    "SkullKeyChest" => "mining.floor_objectives",
                    _ => string.Empty
                };
                return ReplaceRequiredFactors(
                    option.RequiredStateFactors,
                    new[]
                    {
                        "current_location.route_context",
                        "locations.route_action_branch_coverage"
                    },
                    interactionFact);
            }

            return option.RequiredStateFactors;
        }

        private static string[] ReplaceRequiredFactors(
            string[] requiredFactors,
            string replacedFactor,
            string replacementFactor)
        {
            return ReplaceRequiredFactors(requiredFactors, new[] { replacedFactor }, replacementFactor);
        }

        private static string[] ReplaceRequiredFactors(
            string[] requiredFactors,
            string[] replacedFactors,
            string replacementFactor)
        {
            if (string.IsNullOrWhiteSpace(replacementFactor))
            {
                return requiredFactors;
            }

            return requiredFactors
                .Select(factor => replacedFactors.Contains(factor, StringComparer.Ordinal)
                    ? replacementFactor
                    : factor)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }
}
