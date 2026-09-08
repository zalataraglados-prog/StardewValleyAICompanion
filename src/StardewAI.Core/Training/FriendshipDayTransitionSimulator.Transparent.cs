using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed partial class FriendshipDayTransitionSimulator
    {
        public FriendshipDayTransitionResult SimulateGrandpaRow(
            JsonElement grandpaFriendshipProgress,
            string npcName)
        {
            if (grandpaFriendshipProgress.ValueKind != JsonValueKind.Object ||
                ReadString(grandpaFriendshipProgress, "projection_status") != "complete_live_native_iteration" ||
                ReadString(grandpaFriendshipProgress, "day_transition_inputs_status") != "complete_live_native_fields" ||
                !TryReadInt(grandpaFriendshipProgress, "next_total_days", out var transitionTotalDays) ||
                !TryReadInt(grandpaFriendshipProgress, "next_total_sunday_weeks", out var transitionSundayWeeks) ||
                !TryReadBool(grandpaFriendshipProgress, "player_has_friendship_book", out var hasBook) ||
                !TryReadBool(grandpaFriendshipProgress, "player_can_understand_dwarves", out var understandsDwarves) ||
                !grandpaFriendshipProgress.TryGetProperty("eligible_villager_rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return Blocked(npcName, "grandpa_friendship_day_transition_projection_incomplete");
            }

            var matches = rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    ReadString(row, "npc_name") == npcName)
                .ToArray();
            if (matches.Length == 0)
                return Blocked(npcName, "grandpa_friendship_day_transition_npc_missing");

            var row = matches[0];
            if (matches.Skip(1).Any(candidate => !EquivalentTransitionRow(row, candidate)))
                return Blocked(npcName, "grandpa_friendship_day_transition_duplicate_npc_conflict");
            if (!TryReadBool(row, "is_villager", out var isVillager) || !isVillager ||
                !TryReadBool(row, "event_actor", out var eventActor) || eventActor ||
                !TryReadBool(row, "is_child", out var isChild) ||
                !TryReadBool(row, "friendship_row_exists", out var rowExists) ||
                !TryReadBool(row, "is_datably_flagged", out var isDatable) ||
                !TryReadBool(row, "is_npc_married", out var isNpcMarried) ||
                !TryReadBool(row, "is_player_spouse", out var isPlayerSpouse) ||
                !TryReadBool(row, "is_dating", out var isDating) ||
                !TryReadBool(row, "is_divorced", out var isDivorced) ||
                !TryReadBool(row, "talked_to_today", out var talkedToday) ||
                !TryReadBool(row, "speaks_dwarvish", out var speaksDwarvish) ||
                !TryReadInt(row, "maximum_hearts", out var maximumHearts) ||
                !TryReadNullableInt(row, "friendship_points", out var points) ||
                !TryReadNullableInt(row, "gifts_today", out var giftsToday) ||
                !TryReadNullableInt(row, "gifts_this_week", out var giftsThisWeek) ||
                !TryReadNullableInt(row, "last_gift_date_total_days", out var lastGiftDay) ||
                !TryReadNullableInt(row, "last_gift_date_total_sunday_weeks", out var lastGiftWeek))
            {
                return Blocked(npcName, "grandpa_friendship_day_transition_row_incomplete");
            }

            return Simulate(new FriendshipDayTransitionInput
            {
                NpcName = npcName,
                FriendshipRowExists = rowExists,
                CharacterExists = true,
                IsVillager = isVillager,
                IsChild = isChild,
                IsDatable = isDatable,
                IsNpcMarried = isNpcMarried,
                IsPlayerSpouse = isPlayerSpouse,
                IsDating = isDating,
                IsDivorced = isDivorced,
                TalkedToToday = talkedToday,
                SpeaksDwarvish = speaksDwarvish,
                PlayerCanUnderstandDwarves = understandsDwarves,
                PlayerHasFriendshipBook = hasBook,
                MaximumHearts = maximumHearts,
                Points = points,
                GiftsToday = giftsToday,
                GiftsThisWeek = giftsThisWeek,
                LastGiftDateStateComplete = true,
                LastGiftDateTotalDays = lastGiftDay,
                LastGiftDateTotalSundayWeeks = lastGiftWeek,
                TransitionDateTotalDays = transitionTotalDays,
                TransitionDateTotalSundayWeeks = transitionSundayWeeks
            });
        }

        private static bool EquivalentTransitionRow(JsonElement left, JsonElement right)
        {
            var fields = new[]
            {
                "npc_name",
                "is_villager",
                "event_actor",
                "is_child",
                "friendship_row_exists",
                "friendship_points",
                "is_datably_flagged",
                "is_npc_married",
                "is_player_spouse",
                "is_dating",
                "is_divorced",
                "talked_to_today",
                "gifts_today",
                "gifts_this_week",
                "last_gift_date_total_days",
                "last_gift_date_total_sunday_weeks",
                "speaks_dwarvish",
                "maximum_hearts"
            };
            foreach (var field in fields)
            {
                if (!left.TryGetProperty(field, out var leftValue) ||
                    !right.TryGetProperty(field, out var rightValue) ||
                    !string.Equals(leftValue.GetRawText(), rightValue.GetRawText(), StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private static string ReadString(JsonElement source, string name)
        {
            return source.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static bool TryReadInt(JsonElement source, string name, out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool TryReadNullableInt(JsonElement source, string name, out int? value)
        {
            value = null;
            if (!source.TryGetProperty(name, out var property))
                return false;
            if (property.ValueKind == JsonValueKind.Null)
                return true;
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var parsed))
            {
                value = parsed;
                return true;
            }
            return false;
        }

        private static bool TryReadBool(JsonElement source, string name, out bool value)
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
    }
}
