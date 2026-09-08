using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentStaticPresenceProjectionResolution
    {
        public string Status { get; set; } = "blocked";

        public string ProjectionStatus { get; set; } = "Blocked";

        public NpcFuturePresenceWindowResolution? Presence { get; set; }

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class NpcCurrentStaticPresenceProjectionResolver
    {
        public NpcCurrentStaticPresenceProjectionResolution Resolve(
            JsonElement snapshot,
            string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName) ||
                !TryReadField(snapshot, "time", "time", out var timeNode) ||
                !timeNode.TryGetInt32(out var gameTime) ||
                gameTime < 600 || gameTime > 2600 ||
                gameTime % 100 >= 60 || gameTime % 10 != 0 ||
                !TryReadField(snapshot, "npcs", "schedule_catalog", out var catalog) ||
                !catalog.TryGetProperty("villagers", out var catalogRows) ||
                catalogRows.ValueKind != JsonValueKind.Array ||
                !TryReadField(snapshot, "npcs", "schedules", out var schedules) ||
                schedules.ValueKind != JsonValueKind.Array ||
                !TryReadField(snapshot, "npcs", "positions", out var positions) ||
                positions.ValueKind != JsonValueKind.Array)
            {
                return Blocked("current_static_presence_snapshot_inputs_incomplete");
            }

            var catalogMatches = catalogRows.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "npc_name"),
                    npcName,
                    StringComparison.Ordinal))
                .ToArray();
            if (catalogMatches.Length != 1)
            {
                return Blocked(catalogMatches.Length == 0
                    ? "current_static_presence_catalog_row_missing"
                    : "current_static_presence_catalog_row_ambiguous");
            }
            var catalogRow = catalogMatches[0];
            if (ReadBool(catalogRow, "is_villager") != true ||
                ReadBool(catalogRow, "event_actor") == true ||
                ReadBool(catalogRow, "is_player_spouse") != false ||
                ReadBool(catalogRow, "currently_married") != false ||
                ReadBool(catalogRow, "master_schedule_present") != false ||
                ReadInt(catalogRow, "master_schedule_entry_count", -1) != 0)
            {
                return Blocked(
                    "current_static_presence_native_no_schedule_contract_not_met");
            }

            var scheduleMatches = schedules.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "name"),
                    npcName,
                    StringComparison.Ordinal))
                .ToArray();
            if (scheduleMatches.Length != 1)
            {
                return Blocked(scheduleMatches.Length == 0
                    ? "current_static_presence_schedule_row_missing"
                    : "current_static_presence_schedule_row_ambiguous");
            }
            var schedule = scheduleMatches[0];
            if (ReadBool(schedule, "follow_schedule") != false ||
                ReadBool(schedule, "schedule_loaded") != false ||
                !schedule.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array ||
                entries.GetArrayLength() != 0)
            {
                return Blocked(
                    "current_static_presence_runtime_schedule_not_empty");
            }

            var positionMatches = positions.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "name"),
                    npcName,
                    StringComparison.Ordinal))
                .ToArray();
            if (positionMatches.Length != 1)
            {
                return Blocked(positionMatches.Length == 0
                    ? "current_static_presence_position_missing"
                    : "current_static_presence_position_ambiguous");
            }
            var position = positionMatches[0];
            var location = ReadString(position, "location_id");
            var currentCatalogLocation = ReadString(
                catalogRow,
                "current_location_name");
            var defaultLocation = ReadString(catalogRow, "default_map");
            var x = ReadInt(position, "tile_x", -1);
            var y = ReadInt(position, "tile_y", -1);
            var defaultX = ReadInt(catalogRow, "default_tile_x", -1);
            var defaultY = ReadInt(catalogRow, "default_tile_y", -1);
            if (location.Length == 0 ||
                !string.Equals(location, currentCatalogLocation, StringComparison.Ordinal) ||
                !string.Equals(location, defaultLocation, StringComparison.Ordinal) ||
                x < 0 || y < 0 || x != defaultX || y != defaultY)
            {
                return Blocked(
                    "current_static_presence_default_position_mismatch");
            }

            return new NpcCurrentStaticPresenceProjectionResolution
            {
                Status = "pass",
                ProjectionStatus = "ExactNativeStaticNoMasterSchedule",
                Presence = new NpcFuturePresenceWindowResolution
                {
                    Status = NpcFuturePresenceWindowResolutionStatus.Exact,
                    NpcName = npcName,
                    SelectedScheduleKey = "native_static_no_master_schedule",
                    ResolvedScheduleKey = "native_static_no_master_schedule",
                    Windows = new[]
                    {
                        new NpcFuturePresenceWindow
                        {
                            ScheduleEntryOrdinal = 0,
                            LocationName = location,
                            TileX = x,
                            TileY = y,
                            WindowStartTime = 600,
                            WindowEndTimeExclusive = 2600,
                            HasStableInterval = true,
                            EndpointBehaviorComplete = true
                        }
                    }
                }
            };
        }

        private static NpcCurrentStaticPresenceProjectionResolution Blocked(
            string reason) => new()
        {
            BlockingReasons = new[] { reason }
        };

        private static bool TryReadField(
            JsonElement snapshot,
            string domain,
            string name,
            out JsonElement value)
        {
            value = default;
            return snapshot.ValueKind == JsonValueKind.Object &&
                snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty(domain, out var domainNode) &&
                domainNode.ValueKind == JsonValueKind.Object &&
                domainNode.TryGetProperty(name, out var field) &&
                field.ValueKind == JsonValueKind.Object &&
                ReadString(field, "status") is "available" or "derived" &&
                field.TryGetProperty("value", out value);
        }

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

        private static int ReadInt(
            JsonElement source,
            string name,
            int fallback) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value)
                ? value
                : fallback;
    }
}
