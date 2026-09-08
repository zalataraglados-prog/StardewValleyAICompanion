using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public enum FutureNpcContactEligibilityProductionStatus
    {
        Exact,
        Blocked
    }

    public sealed class FutureNpcContactEligibilityProduction
    {
        public FutureNpcContactEligibilityProductionStatus Status { get; set; }

        public string NpcName { get; set; } = string.Empty;

        public FutureNpcContactEligibilityEvidence[] Evidence { get; set; } =
            Array.Empty<FutureNpcContactEligibilityEvidence>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "unconditional_vanilla_talk_and_ordinary_non_stardrop_tea_gift_state";
    }

    public sealed class FutureNpcContactEligibilityProducer
    {
        public FutureNpcContactEligibilityProduction Produce(
            JsonElement scheduleCatalog,
            int totalDays,
            NpcFuturePresenceWindowResolution presence)
        {
            if (presence is null ||
                presence.Status != NpcFuturePresenceWindowResolutionStatus.Exact ||
                string.IsNullOrWhiteSpace(presence.NpcName))
            {
                return Blocked(
                    presence?.NpcName ?? string.Empty,
                    "future_contact_eligibility_presence_not_exact");
            }
            if (!TryUnwrap(scheduleCatalog, out var catalog) ||
                !catalog.TryGetProperty("current_selection_context", out var context) ||
                ReadInt(context, "capture_total_days", -1) != totalDays ||
                !TryReadBool(context, "is_green_rain", out var greenRain) ||
                !TryReadInt(context, "year", out var year) ||
                !catalog.TryGetProperty("villagers", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_eligibility_catalog_or_date_incomplete");
            }

            var matches = rows.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "npc_name"),
                    presence.NpcName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                return Blocked(
                    presence.NpcName,
                    matches.Length == 0
                        ? "future_contact_eligibility_npc_missing"
                        : "future_contact_eligibility_npc_ambiguous");
            }

            var row = matches[0];
            if (!TryReadBool(row, "vanilla_social_query_supported", out var supported) ||
                !supported ||
                !TryReadBool(row, "character_master_data_present", out var masterData) ||
                !masterData ||
                !TryReadBool(row, "is_villager", out var isVillager) ||
                !isVillager)
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_eligibility_runtime_type_or_master_data_unsupported");
            }
            if (!TryReadBool(row, "can_socialize_now", out var canSocializeNow) ||
                !TryReadBool(row, "can_receive_gifts_now", out var canReceiveGiftsNow))
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_native_social_query_result_missing");
            }
            var socialCondition = ReadString(row, "can_socialize_condition");
            var socialQueryEvidenceKind = "unconditional_native_data";
            if (!string.IsNullOrWhiteSpace(socialCondition))
            {
                if (!canSocializeNow)
                {
                    return Blocked(
                        presence.NpcName,
                        "future_contact_conditional_social_query_false_on_capture_date");
                }
                if (!IsMonotonicSeenEventCondition(socialCondition))
                {
                    return Blocked(
                        presence.NpcName,
                        "future_contact_conditional_social_query_day_stability_unproven");
                }
                socialQueryEvidenceKind =
                    "native_true_monotonic_player_seen_event_condition";
            }
            else if (!canSocializeNow)
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_unconditional_social_query_runtime_mismatch");
            }
            if (!TryReadBool(row, "simple_non_villager_npc", out var simpleNpc) ||
                !TryReadBool(row, "gift_taste_master_data_present", out var giftTastePresent) ||
                !TryReadBool(row, "is_child", out var isChild) ||
                !TryReadBool(row, "is_player_spouse", out var isPlayerSpouse) ||
                !TryReadBool(row, "currently_married", out var currentlyMarried) ||
                !TryReadBool(row, "is_birthday_on_capture_date", out var isBirthday) ||
                !TryReadBool(row, "friendship_row_exists", out var friendshipRowExists) ||
                !TryReadBool(row, "talked_to_today", out var talkedToToday) ||
                !TryReadBool(row, "friendship_is_divorced", out var divorced) ||
                !TryReadNullableBool(row, "can_receive_gifts_data", out var canReceiveGiftsData) ||
                !TryReadNullableInt(row, "gifts_today", out var giftsToday) ||
                !TryReadNullableInt(row, "gifts_this_week", out var giftsThisWeek))
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_eligibility_native_state_incomplete");
            }

            var talkAllowed = !talkedToToday;
            var baseGiftAllowed =
                canReceiveGiftsNow &&
                !simpleNpc &&
                giftTastePresent &&
                canReceiveGiftsData != false &&
                !divorced &&
                !(greenRain && year == 1 && !currentlyMarried);
            var weeklyGiftAllowed =
                (friendshipRowExists &&
                 giftsThisWeek.HasValue &&
                 giftsThisWeek.Value < 2) ||
                isPlayerSpouse ||
                isChild ||
                isBirthday;
            var dailyGiftAllowed =
                !friendshipRowExists ||
                (giftsToday.HasValue && giftsToday.Value != 1);
            var giftAllowed =
                baseGiftAllowed &&
                weeklyGiftAllowed &&
                dailyGiftAllowed;

            var evidence = presence.Windows
                .Where(window =>
                    window.HasStableInterval &&
                    window.EndpointBehaviorComplete)
                .Select(window => new FutureNpcContactEligibilityEvidence
                {
                    TotalDays = totalDays,
                    NpcName = presence.NpcName,
                    SelectedScheduleKey = presence.SelectedScheduleKey,
                    ScheduleEntryOrdinal = window.ScheduleEntryOrdinal,
                    LocationName = window.LocationName,
                    TileX = window.TileX,
                    TileY = window.TileY,
                    EligibleFromTime = window.WindowStartTime,
                    EligibleUntilTimeExclusive = window.WindowEndTimeExclusive,
                    StateComplete = true,
                    TalkAllowed = talkAllowed,
                    GiftAllowed = giftAllowed,
                    SocialQueryCondition = socialCondition,
                    SocialQueryValueOnCaptureDate = canSocializeNow,
                    SocialQueryStableThroughDay = true,
                    SocialQueryEvidenceKind = socialQueryEvidenceKind
                })
                .ToArray();
            if (evidence.Length == 0)
            {
                return Blocked(
                    presence.NpcName,
                    "future_contact_eligibility_stable_window_missing");
            }

            return new FutureNpcContactEligibilityProduction
            {
                Status = FutureNpcContactEligibilityProductionStatus.Exact,
                NpcName = presence.NpcName,
                Evidence = evidence
            };
        }

        private static bool TryUnwrap(JsonElement source, out JsonElement value)
        {
            value = source;
            if (source.ValueKind != JsonValueKind.Object)
                return false;
            if (!source.TryGetProperty("value", out var wrapped))
                return true;
            if (!string.Equals(ReadString(source, "status"), "available", StringComparison.Ordinal) ||
                wrapped.ValueKind != JsonValueKind.Object)
            {
                return false;
            }
            value = wrapped;
            return true;
        }

        private static FutureNpcContactEligibilityProduction Blocked(
            string npcName,
            string reason) => new()
        {
            Status = FutureNpcContactEligibilityProductionStatus.Blocked,
            NpcName = npcName,
            BlockingReasons = new[] { reason }
        };

        private static bool IsMonotonicSeenEventCondition(string condition)
        {
            var tokens = condition.Split(
                new[] { ' ', '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3 ||
                !string.Equals(
                    tokens[0],
                    "PLAYER_HAS_SEEN_EVENT",
                    StringComparison.OrdinalIgnoreCase) ||
                !(string.Equals(tokens[1], "Any", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(tokens[1], "All", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            return tokens.Skip(2).All(token =>
                int.TryParse(token, out var eventId) && eventId >= 0);
        }

        private static string ReadString(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static int ReadInt(JsonElement source, string name, int fallback) =>
            TryReadInt(source, name, out var value) ? value : fallback;

        private static bool TryReadInt(
            JsonElement source,
            string name,
            out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool TryReadBool(
            JsonElement source,
            string name,
            out bool value)
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

        private static bool TryReadNullableBool(
            JsonElement source,
            string name,
            out bool? value)
        {
            value = null;
            if (!source.TryGetProperty(name, out var property))
                return false;
            if (property.ValueKind == JsonValueKind.Null)
                return true;
            if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = property.GetBoolean();
                return true;
            }
            return false;
        }

        private static bool TryReadNullableInt(
            JsonElement source,
            string name,
            out int? value)
        {
            value = null;
            if (!source.TryGetProperty(name, out var property))
                return false;
            if (property.ValueKind == JsonValueKind.Null)
                return true;
            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out var parsed))
            {
                value = parsed;
                return true;
            }
            return false;
        }
    }
}
