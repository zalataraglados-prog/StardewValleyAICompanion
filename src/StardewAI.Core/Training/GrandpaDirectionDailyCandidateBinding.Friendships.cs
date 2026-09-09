using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed partial class GrandpaDirectionDailyCandidateBinding
    {
        private const int GrandpaFriendshipThreshold = 1975;
        private const int GrandpaFriendshipTargetCount = 10;
        private static readonly string[] ReservedPortfolioParameterNames =
        {
            "grandpa_friendship_native_population_member",
            "grandpa_friendship_threshold_points",
            "grandpa_friendship_deficit_before",
            "grandpa_friendship_deficit_after",
            "grandpa_friendship_portfolio_slots_remaining"
        };

        private static bool TryBuildFriendshipPortfolioEvidence(
            SnapshotEnvelope snapshot,
            PolicyEventCandidatePrediction candidate,
            out SmallModelActionParameter[] evidence,
            out string rejectionReason)
        {
            evidence = Array.Empty<SmallModelActionParameter>();
            rejectionReason = string.Empty;
            if ((candidate.Parameters ?? Array.Empty<SmallModelActionParameter>()).Any(parameter =>
                    ReservedPortfolioParameterNames.Contains(parameter.Name, StringComparer.Ordinal)))
            {
                rejectionReason = "friendship_candidate_contains_reserved_portfolio_evidence";
                return false;
            }

            var progress = ReadStateFieldValue(snapshot, "npcs", "grandpa_friendship_progress");
            if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
                !TryReadObjectInt(progress.Value, "threshold_points", out var threshold) ||
                threshold != GrandpaFriendshipThreshold ||
                !TryReadObjectInt(progress.Value, "maximum_points", out var maximum) ||
                maximum != 999999 ||
                !TryReadObjectBool(progress.Value, "romance_only", out var romanceOnly) ||
                romanceOnly ||
                !TryReadObjectString(progress.Value, "projection_status", out var status) ||
                !string.Equals(status, "complete_live_native_iteration", StringComparison.Ordinal) ||
                !TryReadObjectInt(progress.Value, "qualifying_count", out var qualifyingCount) ||
                qualifyingCount < 0 ||
                !progress.Value.TryGetProperty("eligible_villager_rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                rejectionReason = "grandpa_friendship_projection_incomplete";
                return false;
            }

            if (!TryReadUniqueParameter(candidate, "npc_name", out var npcName) ||
                !TryReadUniqueIntParameter(candidate, "friendship_points_before", out var pointsBefore) ||
                !TryReadUniqueIntParameter(candidate, "expected_friendship_delta", out var expectedDelta) ||
                !TryReadUniqueIntParameter(candidate, "expected_friendship_points_after", out var pointsAfter) ||
                !TryReadUniqueBoolParameter(candidate, "friendship_row_exists_before", out var rowExistsBefore))
            {
                rejectionReason = "friendship_transition_evidence_missing_or_invalid";
                return false;
            }

            if (string.Equals(candidate.Kind, "route_connector_tile", StringComparison.Ordinal) &&
                !HasExactSocialRouteContinuation(candidate, npcName, out rejectionReason))
            {
                return false;
            }

            var matchingRows = rows.EnumerateArray()
                .Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    TryReadObjectString(row, "npc_name", out var rowName) &&
                    string.Equals(rowName, npcName, StringComparison.Ordinal))
                .ToArray();
            if (matchingRows.Length != 1)
            {
                rejectionReason = matchingRows.Length == 0
                    ? "friendship_target_not_in_native_grandpa_population"
                    : "friendship_target_native_population_duplicate";
                return false;
            }

            var row = matchingRows[0];
            if (!TryReadObjectBool(row, "is_villager", out var isVillager) || !isVillager ||
                !TryReadObjectBool(row, "event_actor", out var eventActor) || eventActor ||
                !TryReadObjectBool(row, "qualifies", out var qualifies))
            {
                rejectionReason = "friendship_target_native_population_evidence_invalid";
                return false;
            }
            if (qualifies)
            {
                rejectionReason = "friendship_target_already_qualifies_for_grandpa";
                return false;
            }

            var nativePoints = 0;
            var nativeRowHasPoints = row.TryGetProperty("friendship_points", out var nativePointsValue) &&
                nativePointsValue.ValueKind == JsonValueKind.Number &&
                nativePointsValue.TryGetInt32(out nativePoints);
            if (nativePointsValue.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
            {
                rejectionReason = "friendship_target_native_points_invalid";
                return false;
            }
            if (rowExistsBefore != nativeRowHasPoints || pointsBefore != (nativeRowHasPoints ? nativePoints : 0))
            {
                rejectionReason = "friendship_candidate_before_state_mismatch";
                return false;
            }
            if (pointsBefore < 0 || pointsBefore >= GrandpaFriendshipThreshold ||
                expectedDelta <= 0 || pointsAfter != pointsBefore + expectedDelta)
            {
                rejectionReason = "friendship_candidate_transition_not_positive_or_consistent";
                return false;
            }

            evidence = new[]
            {
                Parameter("grandpa_friendship_native_population_member", "true"),
                Parameter("grandpa_friendship_threshold_points", GrandpaFriendshipThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Parameter("grandpa_friendship_deficit_before", Math.Max(0, GrandpaFriendshipThreshold - pointsBefore).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Parameter("grandpa_friendship_deficit_after", Math.Max(0, GrandpaFriendshipThreshold - pointsAfter).ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Parameter("grandpa_friendship_portfolio_slots_remaining", Math.Max(0, GrandpaFriendshipTargetCount - qualifyingCount).ToString(System.Globalization.CultureInfo.InvariantCulture))
            };
            return true;
        }

        private static bool HasExactSocialRouteContinuation(
            PolicyEventCandidatePrediction candidate,
            string npcName,
            out string rejectionReason)
        {
            rejectionReason = string.Empty;
            if (!TryReadUniqueParameter(candidate, "continuation.option_id", out var continuationOptionId) ||
                !string.Equals(continuationOptionId, candidate.OptionId, StringComparison.Ordinal) ||
                continuationOptionId is not ("social.talk_npc" or "social.gift_npc"))
            {
                rejectionReason = "friendship_route_continuation_option_mismatch";
                return false;
            }
            if (!TryReadUniqueParameter(candidate, "continuation.npc_name", out var continuationNpc) ||
                !string.Equals(continuationNpc, npcName, StringComparison.Ordinal))
            {
                rejectionReason = "friendship_route_continuation_npc_mismatch";
                return false;
            }
            if (!TryReadUniqueParameter(candidate, "continuation.target_location", out _))
            {
                rejectionReason = "friendship_route_continuation_target_missing";
                return false;
            }
            return true;
        }

        private static bool TryReadUniqueBoolParameter(
            PolicyEventCandidatePrediction candidate,
            string name,
            out bool value)
        {
            value = false;
            return TryReadUniqueParameter(candidate, name, out var text) && bool.TryParse(text, out value);
        }

        private static bool TryReadObjectInt(JsonElement source, string name, out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool TryReadObjectBool(JsonElement source, string name, out bool value)
        {
            value = false;
            if (!source.TryGetProperty(name, out var property) ||
                property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }

            value = property.GetBoolean();
            return true;
        }

        private static bool TryReadObjectString(JsonElement source, string name, out string value)
        {
            value = string.Empty;
            if (!source.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
