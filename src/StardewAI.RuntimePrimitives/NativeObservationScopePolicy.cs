using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.RuntimePrimitives
{
    public static class NativeObservationScopePolicy
    {
        public static string? Rejection(string? scope, string executionMode, string optionId, IEnumerable<string> identities)
        {
            if (scope is null) return null;
            if (string.IsNullOrWhiteSpace(scope) || scope.Length > 128 || scope.Any(char.IsControl))
                return "action_origin_observation_scope_invalid";
            if (executionMode != "training_singleplayer" || (optionId != "executor.move_to_tile" && optionId != "executor.clear_obstacle"))
                return "action_origin_observation_scope_unsupported";
            var rows = identities.ToArray();
            return rows.Length != 6 || rows.Any(string.IsNullOrWhiteSpace) ? "action_origin_observation_identity_required" : null;
        }
    }

}
