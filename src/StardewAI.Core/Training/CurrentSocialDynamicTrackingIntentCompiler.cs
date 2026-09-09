using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training
{
    public sealed class CurrentSocialDynamicTrackingIntentCompilation
    {
        public string Status { get; set; } = "blocked";

        public CurrentSocialDynamicTrackingIntent[] Intents { get; set; } =
            Array.Empty<CurrentSocialDynamicTrackingIntent>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class CurrentSocialDynamicTrackingIntentCompiler
    {
        public CurrentSocialDynamicTrackingIntentCompilation Compile(
            JsonElement snapshot,
            CurrentSocialDynamicTrackingDirective directive)
        {
            if (directive is null ||
                string.IsNullOrWhiteSpace(directive.NpcName) ||
                !IsValidGameTime(directive.ObservedAtTime) ||
                directive.ObservedLocationName.Length == 0 ||
                directive.ObservedTileX < 0 ||
                directive.ObservedTileY < 0 ||
                directive.TargetBindingMode !=
                    "live_npc_identity_rebind_each_snapshot" ||
                directive.ReplanPolicy !=
                    "refresh_after_each_route_connector_and_before_interaction")
            {
                return Blocked(
                    "current_social_dynamic_tracking_directive_invalid");
            }
            if (!JsonSnapshotEnvelopeAdapter.TryCreate(
                    snapshot,
                    out var envelope))
            {
                return Blocked(
                    "current_social_dynamic_tracking_snapshot_envelope_invalid");
            }
            if (!NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "time", "time", out var timeNode) ||
                !timeNode.TryGetInt32(out var currentTime) ||
                !IsValidGameTime(currentTime))
            {
                return Blocked(
                    "current_social_dynamic_tracking_snapshot_time_invalid");
            }
            if (directive.ObservedAtTime != currentTime)
            {
                return Blocked(
                    "current_social_dynamic_tracking_observed_time_mismatch");
            }

            var intents = new List<CurrentSocialDynamicTrackingIntent>();
            var blockers = new List<string>();
            foreach (var optionId in directive.CandidateFamilies
                .Distinct(StringComparer.Ordinal))
            {
                if (optionId is not
                    ("social.talk_npc" or "social.gift_npc"))
                {
                    blockers.Add(
                        "current_social_dynamic_tracking_option_unsupported:" +
                        optionId);
                    continue;
                }
                var option = new CandidateOptionAvailabilityEvaluator()
                    .Evaluate(envelope, new[] { optionId })
                    .Options
                    .Single();
                var npcCandidates = option.SocialCandidates
                    .Where(candidate => string.Equals(
                            ReadParameter(candidate.Parameters, "npc_name") ??
                                ReadParameter(
                                    candidate.Parameters,
                                    "continuation.npc_name"),
                            directive.NpcName,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var matches = npcCandidates
                    .Where(candidate => candidate.Available)
                    .ToArray();
                if (matches.Length == 0)
                {
                    blockers.Add(
                        "current_social_dynamic_tracking_current_candidate_missing:" +
                        optionId);
                    blockers.AddRange(npcCandidates
                        .SelectMany(candidate => candidate.BlockReasons)
                        .Concat(option.BlockingReasons)
                        .Where(reason => !string.IsNullOrWhiteSpace(reason))
                        .Select(reason =>
                            "current_social_dynamic_tracking_candidate:" +
                            optionId + ":" + reason));
                    continue;
                }
                foreach (var candidate in matches)
                {
                    var slot = ReadIntParameter(
                        candidate.Parameters,
                        "slot_index");
                    intents.Add(new CurrentSocialDynamicTrackingIntent
                    {
                        IntentId = "social.dynamic." +
                            directive.NpcName + "." + optionId +
                            (slot.HasValue ? ".slot." + slot.Value : string.Empty),
                        NpcName = directive.NpcName,
                        OptionId = optionId,
                        CandidateId = candidate.CandidateId,
                        GiftSlotIndex = slot,
                        GiftQualifiedItemId = ReadParameter(
                                candidate.Parameters,
                                "qualified_item_id") ?? string.Empty,
                        TargetBindingMode = directive.TargetBindingMode,
                        ReplanPolicy = directive.ReplanPolicy
                    });
                }
            }

            if (blockers.Count > 0 || intents.Count == 0)
            {
                if (intents.Count == 0 && blockers.Count == 0)
                {
                    blockers.Add(
                        "current_social_dynamic_tracking_no_candidate_family");
                }
                return new CurrentSocialDynamicTrackingIntentCompilation
                {
                    Intents = intents.ToArray(),
                    BlockingReasons = blockers
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()
                };
            }
            return new CurrentSocialDynamicTrackingIntentCompilation
            {
                Status = "pass",
                Intents = intents
                    .OrderBy(value => value.OptionId, StringComparer.Ordinal)
                    .ThenBy(value => value.GiftSlotIndex)
                    .ToArray()
            };
        }

        private static CurrentSocialDynamicTrackingIntentCompilation Blocked(
            string reason) => new()
        {
            BlockingReasons = new[] { reason }
        };

        private static bool IsValidGameTime(int gameTime) =>
            gameTime >= 600 &&
            gameTime <= 2600 &&
            gameTime % 100 < 60 &&
            gameTime % 10 == 0;
    }
}
