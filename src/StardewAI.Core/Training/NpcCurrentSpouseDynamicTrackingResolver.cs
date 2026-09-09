using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentSpouseDynamicTrackingResolution
    {
        public string Status { get; set; } = "blocked";

        public string ProjectionStatus { get; set; } = "Blocked";

        public CurrentSocialDynamicTrackingDirective? Directive { get; set; }

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class NpcCurrentSpouseDynamicTrackingResolver
    {
        public NpcCurrentSpouseDynamicTrackingResolution Resolve(
            JsonElement snapshot,
            string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "time", "time", out var timeNode) ||
                !timeNode.TryGetInt32(out var gameTime) ||
                gameTime < 600 || gameTime > 2600 ||
                gameTime % 100 >= 60 || gameTime % 10 != 0 ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "player", "spouse", out var spouseNode) ||
                spouseNode.ValueKind != JsonValueKind.String ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "player", "married_or_roommate", out var marriedNode) ||
                marriedNode.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "npcs", "schedule_catalog", out var catalog) ||
                !catalog.TryGetProperty("villagers", out var catalogRows) ||
                catalogRows.ValueKind != JsonValueKind.Array ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "npcs", "schedules", out var schedules) ||
                schedules.ValueKind != JsonValueKind.Array ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot, "npcs", "positions", out var positions) ||
                positions.ValueKind != JsonValueKind.Array)
            {
                return Blocked("current_spouse_dynamic_snapshot_inputs_incomplete");
            }
            if (!marriedNode.GetBoolean() ||
                !string.Equals(spouseNode.GetString(), npcName, StringComparison.Ordinal))
            {
                return Blocked("current_spouse_dynamic_player_identity_mismatch");
            }

            var catalogMatches = catalogRows.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(row, "npc_name"), npcName, StringComparison.Ordinal))
                .ToArray();
            if (catalogMatches.Length != 1)
            {
                return Blocked(catalogMatches.Length == 0
                    ? "current_spouse_dynamic_catalog_row_missing"
                    : "current_spouse_dynamic_catalog_row_ambiguous");
            }
            var catalogRow = catalogMatches[0];
            if (ReadBool(catalogRow, "is_villager") != true ||
                ReadBool(catalogRow, "event_actor") == true ||
                ReadBool(catalogRow, "is_player_spouse") != true ||
                ReadBool(catalogRow, "currently_married") != true)
            {
                return Blocked("current_spouse_dynamic_native_identity_contract_not_met");
            }

            var scheduleMatches = schedules.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(row, "name"), npcName, StringComparison.Ordinal))
                .ToArray();
            if (scheduleMatches.Length != 1)
            {
                return Blocked(scheduleMatches.Length == 0
                    ? "current_spouse_dynamic_schedule_row_missing"
                    : "current_spouse_dynamic_schedule_row_ambiguous");
            }
            var schedule = scheduleMatches[0];
            if (ReadBool(schedule, "follow_schedule") != false ||
                ReadBool(schedule, "schedule_loaded") != false ||
                ReadString(schedule, "schedule_key").Length != 0 ||
                !schedule.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array ||
                entries.GetArrayLength() != 0)
            {
                return Blocked("current_spouse_dynamic_runtime_schedule_not_empty");
            }

            var positionMatches = positions.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(row, "name"), npcName, StringComparison.Ordinal))
                .ToArray();
            if (positionMatches.Length != 1)
            {
                return Blocked(positionMatches.Length == 0
                    ? "current_spouse_dynamic_position_missing"
                    : "current_spouse_dynamic_position_ambiguous");
            }
            var position = positionMatches[0];
            var location = ReadString(position, "location_id");
            var x = ReadInt(position, "tile_x", -1);
            var y = ReadInt(position, "tile_y", -1);
            if (location.Length == 0 || x < 0 || y < 0 ||
                !string.Equals(
                    location,
                    ReadString(catalogRow, "current_location_name"),
                    StringComparison.Ordinal))
            {
                return Blocked("current_spouse_dynamic_observed_position_invalid");
            }

            var candidateFamilies = new[]
            {
                ReadBool(catalogRow, "can_socialize_now") == true
                    ? "social.talk_npc"
                    : string.Empty,
                ReadBool(catalogRow, "can_receive_gifts_now") == true
                    ? "social.gift_npc"
                    : string.Empty
            }.Where(value => value.Length > 0).ToArray();
            return new NpcCurrentSpouseDynamicTrackingResolution
            {
                Status = "pass",
                ProjectionStatus = "ExactCurrentNativeSpouseDynamicTrackingRequired",
                Directive = new CurrentSocialDynamicTrackingDirective
                {
                    NpcName = npcName,
                    ObservedLocationName = location,
                    ObservedTileX = x,
                    ObservedTileY = y,
                    ObservedAtTime = gameTime,
                    CandidateFamilies = candidateFamilies,
                    EvidenceKind =
                        "native_player_spouse_identity_empty_schedule_and_exact_current_position"
                }
            };
        }

        private static NpcCurrentSpouseDynamicTrackingResolution Blocked(string reason) => new()
        {
            BlockingReasons = new[] { reason }
        };

        private static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;

        private static int ReadInt(JsonElement source, string name, int fallback) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value)
                ? value
                : fallback;
    }
}
