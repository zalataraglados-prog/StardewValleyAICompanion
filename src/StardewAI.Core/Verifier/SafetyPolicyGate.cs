using System;
using System.Collections.Generic;
using StardewAI.Contracts.Options;

namespace StardewAI.Core.Verifier
{
    internal sealed class SafetyPolicyResult
    {
        public string ExecutionAuthorization { get; init; } = "denied";
        public string[] BlockingReasons { get; init; } = Array.Empty<string>();
    }

    internal static class SafetyPolicyGate
    {
        public static SafetyPolicyResult Evaluate(
            OptionSpec option,
            OptionAvailabilityCandidate candidate)
        {
            var reasons = new List<string>();
            if (option.HostPolicy == OptionHostPolicy.HostOnly && !candidate.ActorIsHost)
            {
                reasons.Add("host_only_option_requires_host_actor");
            }

            if (!candidate.OwnershipAuthorized)
            {
                reasons.Add("option_ownership_not_authorized");
            }

            if (option.ModAdapterPolicy == OptionModAdapterPolicy.VanillaNativeOnly &&
                !string.Equals(candidate.AdapterId, "vanilla_native", StringComparison.Ordinal))
            {
                reasons.Add("option_adapter_not_authorized");
            }

            if (option.AutonomousCandidatePolicy == AutonomousCandidatePolicy.Forbidden)
            {
                reasons.Add("autonomous_candidate_forbidden");
            }

            if (option.InvocationPolicy == OptionInvocationPolicy.PlayerCommandOnly &&
                candidate.InvocationSource != OptionInvocationSource.PlayerCommand)
            {
                reasons.Add("player_command_only_option_requires_player_command_source");
            }

            if (reasons.Count > 0)
            {
                return new SafetyPolicyResult
                {
                    ExecutionAuthorization = "denied",
                    BlockingReasons = reasons.ToArray()
                };
            }

            if (option.ConfirmationPolicy == OptionConfirmationPolicy.ExplicitUserConfirmationRequired &&
                !candidate.ExplicitConfirmationGranted)
            {
                return new SafetyPolicyResult
                {
                    ExecutionAuthorization = "confirmation_required",
                    BlockingReasons = new[] { "explicit_user_confirmation_required" }
                };
            }

            return new SafetyPolicyResult
            {
                ExecutionAuthorization = "authorized"
            };
        }
    }
}
