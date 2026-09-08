using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static bool RuntimeFishingCandidateMatchesMasterAngler(
            SnapshotEnvelope snapshot,
            EventCandidate candidate,
            MasterAnglerWindowIntentValidation intent)
        {
            var distributionJson = ReadParameter(
                candidate.Parameters,
                "outcome_distribution_json");
            try
            {
                using var distribution = JsonDocument.Parse(distributionJson);
                if (distribution.RootElement.ValueKind != JsonValueKind.Array)
                    return false;

                if (string.Equals(
                        intent.SourceKind,
                        "location_rule",
                        StringComparison.Ordinal))
                {
                    var separator = intent.SourceKey.LastIndexOf(':');
                    if (separator <= 0 ||
                        !int.TryParse(
                            intent.SourceKey[(separator + 1)..],
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out var sourceIndex))
                    {
                        return false;
                    }
                    var expectedPrefix = "Data/Locations:" +
                        intent.SourceKey[..separator] + "#" +
                        sourceIndex.ToString(CultureInfo.InvariantCulture) + ":";
                    return distribution.RootElement.EnumerateArray().Any(outcome =>
                        string.Equals(
                            ReadString(outcome, "qualified_item_id"),
                            intent.TargetQualifiedItemId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadString(outcome, "source_kind"),
                            "rule",
                            StringComparison.Ordinal) &&
                        (string.Equals(
                             ReadString(outcome, "source_key"),
                             intent.SourceKey,
                             StringComparison.Ordinal) ||
                         ReadString(outcome, "source_key").StartsWith(
                             expectedPrefix,
                             StringComparison.Ordinal)));
                }

                return string.Equals(
                        intent.SourceKind,
                        "mine_override",
                        StringComparison.Ordinal) &&
                    distribution.RootElement.EnumerateArray().Any(outcome =>
                        string.Equals(
                            ReadString(outcome, "qualified_item_id"),
                            intent.TargetQualifiedItemId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadString(outcome, "source_kind"),
                            "special",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadString(outcome, "source_key"),
                            "mine_shaft_fishing",
                            StringComparison.Ordinal)) &&
                    RuntimeMineOverrideMatches(snapshot, candidate, intent);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool RuntimeMineOverrideMatches(
            SnapshotEnvelope snapshot,
            EventCandidate candidate,
            MasterAnglerWindowIntentValidation intent)
        {
            const string prefix = "MineShaft.getFish:area:";
            if (!intent.SourceKey.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(
                    intent.SourceKey[prefix.Length..],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var expectedArea) ||
                !int.TryParse(
                    ReadParameter(candidate.Parameters, "rod_slot_index"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var rodSlot))
            {
                return false;
            }

            var contexts = ReadStateFieldValue(snapshot, "fishing", "rod_contexts");
            if (!contexts.HasValue ||
                contexts.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            return contexts.Value.EnumerateArray().Any(context =>
                ReadInt(context, "rod_slot_index") == rodSlot &&
                context.TryGetProperty("special_catch_sources", out var sources) &&
                sources.ValueKind == JsonValueKind.Object &&
                sources.TryGetProperty(
                    "location_get_fish_override",
                    out var locationOverride) &&
                locationOverride.ValueKind == JsonValueKind.Object &&
                locationOverride.TryGetProperty("handlers", out var handlers) &&
                handlers.ValueKind == JsonValueKind.Array &&
                handlers.EnumerateArray().Any(handler =>
                    string.Equals(
                        ReadString(handler, "handler"),
                        "mine_shaft_fishing",
                        StringComparison.Ordinal) &&
                    ReadInt(handler, "mine_area") == expectedArea &&
                    string.Equals(
                        ReadString(handler, "special_fish_qualified_item_id"),
                        intent.TargetQualifiedItemId,
                        StringComparison.Ordinal)));
        }

        private static EventCandidate BlockedMasterAnglerCandidate(
            string reason,
            SmallModelActionParameter[] parameters) => new()
            {
                CandidateId = "master-angler:blocked",
                Kind = "catch_fish",
                Available = false,
                ExpectedEffect =
                    "master_angler_action_not_compiled;runtime_terminal_validation_required=true",
                AvailabilityClass = "master_angler_fail_closed",
                BlockReasons = new[]
                {
                    string.IsNullOrWhiteSpace(reason)
                        ? "master_angler_intent_validation_failed"
                        : reason
                },
                Parameters = parameters
            };
    }
}
