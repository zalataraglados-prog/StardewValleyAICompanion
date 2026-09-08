using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class FriendshipDayTransitionSnapshotAuditRow
    {
        public string NpcName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public bool FriendshipRowExistsBefore { get; set; }

        public bool FriendshipRowExistsAfter { get; set; }

        public int? PointsBefore { get; set; }

        public int? PredictedPointsAfter { get; set; }

        public int? ObservedPointsAfter { get; set; }

        public int? PredictedGiftsTodayAfter { get; set; }

        public int? ObservedGiftsTodayAfter { get; set; }

        public int? PredictedGiftsThisWeekAfter { get; set; }

        public int? ObservedGiftsThisWeekAfter { get; set; }

        public string[] PointTransitionReasons { get; set; } = Array.Empty<string>();

        public string[] Issues { get; set; } = Array.Empty<string>();
    }

    public sealed class FriendshipDayTransitionSnapshotAuditReport
    {
        public string Status { get; set; } = string.Empty;

        public int BeforeTotalDays { get; set; }

        public int AfterTotalDays { get; set; }

        public int NativePopulationRowsBefore { get; set; }

        public int NativePopulationRowsAfter { get; set; }

        public int UniqueNpcCount { get; set; }

        public int VerifiedNpcCount { get; set; }

        public int VerifiedFriendshipRowCount { get; set; }

        public int MismatchCount { get; set; }

        public FriendshipDayTransitionSnapshotAuditRow[] Rows { get; set; } =
            Array.Empty<FriendshipDayTransitionSnapshotAuditRow>();

        public string[] Issues { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "grandpa_native_villager_population_friendshipData_day_transition";
    }

    public sealed class FriendshipDayTransitionSnapshotAuditor
    {
        public FriendshipDayTransitionSnapshotAuditReport Audit(
            JsonElement beforeSnapshot,
            JsonElement afterSnapshot)
        {
            if (!TryReadProgress(beforeSnapshot, out var before) ||
                !TryReadProgress(afterSnapshot, out var after) ||
                !TryReadInt(before, "current_total_days", out var beforeDays) ||
                !TryReadInt(before, "next_total_days", out var predictedAfterDays) ||
                !TryReadInt(after, "current_total_days", out var afterDays) ||
                !TryReadRows(before, out var beforeRows) ||
                !TryReadRows(after, out var afterRows))
            {
                return Blocked("friendship_day_transition_snapshot_fields_missing");
            }
            if (predictedAfterDays != beforeDays + 1 || afterDays != predictedAfterDays)
                return Blocked("friendship_day_transition_not_exactly_one_native_day");

            var beforeGroups = beforeRows
                .GroupBy(row => ReadString(row, "npc_name"), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var afterGroups = afterRows
                .GroupBy(row => ReadString(row, "npc_name"), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            if (beforeGroups.ContainsKey(string.Empty) || afterGroups.ContainsKey(string.Empty))
                return Blocked("friendship_day_transition_npc_name_missing");

            var reportRows = new List<FriendshipDayTransitionSnapshotAuditRow>();
            foreach (var pair in beforeGroups.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (!RowsEquivalent(pair.Value))
                {
                    reportRows.Add(Mismatch(pair.Key, "friendship_day_transition_before_duplicate_conflict"));
                    continue;
                }
                if (!afterGroups.TryGetValue(pair.Key, out var observedRows))
                {
                    reportRows.Add(Mismatch(pair.Key, "friendship_day_transition_after_npc_missing"));
                    continue;
                }
                if (!RowsEquivalent(observedRows))
                {
                    reportRows.Add(Mismatch(pair.Key, "friendship_day_transition_after_duplicate_conflict"));
                    continue;
                }

                var prediction = new FriendshipDayTransitionSimulator().SimulateGrandpaRow(
                    before,
                    pair.Key);
                if (prediction.Status == "blocked")
                {
                    reportRows.Add(new FriendshipDayTransitionSnapshotAuditRow
                    {
                        NpcName = pair.Key,
                        Status = "blocked",
                        Issues = prediction.Issues
                    });
                    continue;
                }

                var observed = observedRows[0];
                var observedExists = ReadBool(observed, "friendship_row_exists");
                var observedPoints = ReadNullableInt(observed, "friendship_points");
                var observedTalked = ReadBool(observed, "talked_to_today");
                var observedGiftsToday = ReadNullableInt(observed, "gifts_today");
                var observedGiftsWeek = ReadNullableInt(observed, "gifts_this_week");
                if (!observedExists.HasValue || !observedTalked.HasValue ||
                    !observedPoints.Readable || !observedGiftsToday.Readable ||
                    !observedGiftsWeek.Readable)
                {
                    reportRows.Add(Mismatch(pair.Key, "friendship_day_transition_after_row_incomplete"));
                    continue;
                }

                var issues = new List<string>();
                if (observedExists.Value != prediction.FriendshipRowExistsAfter)
                    issues.Add("friendship_row_presence_mismatch");
                if (prediction.FriendshipRowExistsAfter)
                {
                    if (observedPoints.Value != prediction.PointsAfter)
                        issues.Add("friendship_points_mismatch");
                    if (observedTalked.Value != prediction.TalkedToTodayAfter)
                        issues.Add("talked_to_today_mismatch");
                    if (observedGiftsToday.Value != prediction.GiftsTodayAfter)
                        issues.Add("gifts_today_mismatch");
                    if (observedGiftsWeek.Value != prediction.GiftsThisWeekAfter)
                        issues.Add("gifts_this_week_mismatch");
                }
                else if (observedPoints.Value.HasValue || observedGiftsToday.Value.HasValue ||
                         observedGiftsWeek.Value.HasValue)
                {
                    issues.Add("absent_friendship_row_has_observed_values");
                }

                reportRows.Add(new FriendshipDayTransitionSnapshotAuditRow
                {
                    NpcName = pair.Key,
                    Status = issues.Count == 0 ? "pass" : "mismatch",
                    FriendshipRowExistsBefore = prediction.PointsBefore.HasValue,
                    FriendshipRowExistsAfter = observedExists.Value,
                    PointsBefore = prediction.PointsBefore,
                    PredictedPointsAfter = prediction.PointsAfter,
                    ObservedPointsAfter = observedPoints.Value,
                    PredictedGiftsTodayAfter = prediction.GiftsTodayAfter,
                    ObservedGiftsTodayAfter = observedGiftsToday.Value,
                    PredictedGiftsThisWeekAfter = prediction.GiftsThisWeekAfter,
                    ObservedGiftsThisWeekAfter = observedGiftsWeek.Value,
                    PointTransitionReasons = prediction.PointTransitions
                        .Select(transition => transition.Reason)
                        .ToArray(),
                    Issues = issues.ToArray()
                });
            }

            foreach (var unexpected in afterGroups.Keys.Except(beforeGroups.Keys, StringComparer.Ordinal))
                reportRows.Add(Mismatch(unexpected, "friendship_day_transition_unexpected_after_npc"));

            var mismatchCount = reportRows.Count(row => row.Status == "mismatch");
            var blockedCount = reportRows.Count(row => row.Status == "blocked");
            var issuesAtRoot = new List<string>();
            if (mismatchCount > 0)
                issuesAtRoot.Add("friendship_day_transition_mismatch_detected");
            if (blockedCount > 0)
                issuesAtRoot.Add("friendship_day_transition_projection_blocked");
            return new FriendshipDayTransitionSnapshotAuditReport
            {
                Status = issuesAtRoot.Count == 0 ? "pass" : "blocked",
                BeforeTotalDays = beforeDays,
                AfterTotalDays = afterDays,
                NativePopulationRowsBefore = beforeRows.Length,
                NativePopulationRowsAfter = afterRows.Length,
                UniqueNpcCount = beforeGroups.Count,
                VerifiedNpcCount = reportRows.Count(row => row.Status == "pass"),
                VerifiedFriendshipRowCount = reportRows.Count(row =>
                    row.Status == "pass" && row.FriendshipRowExistsBefore),
                MismatchCount = mismatchCount,
                Rows = reportRows.ToArray(),
                Issues = issuesAtRoot.ToArray()
            };
        }

        private static bool TryReadProgress(JsonElement snapshot, out JsonElement progress)
        {
            progress = default;
            return snapshot.ValueKind == JsonValueKind.Object &&
                snapshot.TryGetProperty("state", out var state) &&
                state.TryGetProperty("npcs", out var npcs) &&
                npcs.TryGetProperty("grandpa_friendship_progress", out var field) &&
                ReadString(field, "status") is "available" or "derived" &&
                field.TryGetProperty("value", out progress) &&
                progress.ValueKind == JsonValueKind.Object &&
                ReadString(progress, "projection_status") == "complete_live_native_iteration" &&
                ReadString(progress, "day_transition_inputs_status") == "complete_live_native_fields";
        }

        private static bool TryReadRows(JsonElement progress, out JsonElement[] rows)
        {
            rows = Array.Empty<JsonElement>();
            if (!progress.TryGetProperty("eligible_villager_rows", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return false;
            rows = array.EnumerateArray().ToArray();
            return rows.All(row => row.ValueKind == JsonValueKind.Object);
        }

        private static bool RowsEquivalent(IReadOnlyList<JsonElement> rows)
        {
            if (rows.Count == 0)
                return false;
            return rows.Skip(1).All(row => EquivalentObservedRow(rows[0], row));
        }

        private static bool EquivalentObservedRow(JsonElement left, JsonElement right)
        {
            var fields = new[]
            {
                "npc_name", "is_villager", "event_actor", "is_child",
                "friendship_row_exists", "friendship_points", "is_datably_flagged",
                "is_npc_married", "is_player_spouse", "is_dating", "is_divorced",
                "talked_to_today", "gifts_today", "gifts_this_week",
                "last_gift_date_total_days", "last_gift_date_total_sunday_weeks",
                "speaks_dwarvish", "maximum_hearts"
            };
            foreach (var field in fields)
            {
                if (!left.TryGetProperty(field, out var leftValue) ||
                    !right.TryGetProperty(field, out var rightValue) ||
                    !string.Equals(leftValue.GetRawText(), rightValue.GetRawText(), StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private static FriendshipDayTransitionSnapshotAuditRow Mismatch(
            string npcName,
            string issue) => new FriendshipDayTransitionSnapshotAuditRow
        {
            NpcName = npcName,
            Status = "mismatch",
            Issues = new[] { issue }
        };

        private static FriendshipDayTransitionSnapshotAuditReport Blocked(string issue) =>
            new FriendshipDayTransitionSnapshotAuditReport
            {
                Status = "blocked",
                Issues = new[] { issue }
            };

        private static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static bool TryReadInt(JsonElement source, string name, out int value)
        {
            value = 0;
            return source.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out value);
        }

        private static bool? ReadBool(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;

        private static NullableIntRead ReadNullableInt(JsonElement source, string name)
        {
            if (!source.TryGetProperty(name, out var property))
                return new NullableIntRead(false, null);
            if (property.ValueKind == JsonValueKind.Null)
                return new NullableIntRead(true, null);
            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
                ? new NullableIntRead(true, value)
                : new NullableIntRead(false, null);
        }

        private readonly struct NullableIntRead
        {
            public NullableIntRead(bool readable, int? value)
            {
                Readable = readable;
                Value = value;
            }

            public bool Readable { get; }

            public int? Value { get; }
        }
    }
}
