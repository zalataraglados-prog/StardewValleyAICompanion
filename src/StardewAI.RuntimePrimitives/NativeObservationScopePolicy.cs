using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.RuntimePrimitives
{
    public static class NativeObservationScopePolicy
    {
        public static string? Rejection(string? scope, string executionMode, string optionId, IEnumerable<string> identities)
            => RejectionCore(scope, executionMode, optionId == "executor.move_to_tile" || optionId == "executor.clear_obstacle", identities);

        public static string? TillingRejection(string? scope, string executionMode, string optionId, IEnumerable<string> identities)
            => RejectionCore(scope, executionMode, optionId == "executor.till_soil", identities);

        private static string? RejectionCore(string? scope, string executionMode, bool supportedOption, IEnumerable<string> identities)
        {
            if (scope is null) return null;
            if (string.IsNullOrWhiteSpace(scope) || scope.Length > 128 || scope.Any(char.IsControl))
                return "action_origin_observation_scope_invalid";
            if (executionMode != "training_singleplayer" || !supportedOption)
                return "action_origin_observation_scope_unsupported";
            var rows = identities.ToArray();
            return rows.Length != 6 || rows.Any(string.IsNullOrWhiteSpace) ? "action_origin_observation_identity_required" : null;
        }
    }

}
