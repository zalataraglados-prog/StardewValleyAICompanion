using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentScheduleSnapshotAuditRow
    {
        public string NpcName { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public string ProjectionStatus { get; set; } = string.Empty;

        public string SelectedScheduleKey { get; set; } = string.Empty;

        public string NativeScheduleKey { get; set; } = string.Empty;

        public int VerifiedEntryCount { get; set; }

        public int VerifiedArrivalTimeEntryCount { get; set; }

        public int VerifiedTravelTimeEntryCount { get; set; }

        public bool RealizedRandomBranch { get; set; }

        public string[] Issues { get; set; } = Array.Empty<string>();
    }

    public sealed class NpcCurrentScheduleSnapshotAuditReport
    {
        public string Status { get; set; } = string.Empty;

        public int GameTime { get; set; }

        public int LoadedScheduleCount { get; set; }

        public int VerifiedScheduleCount { get; set; }

        public int FailClosedScheduleCount { get; set; }

        public int MismatchCount { get; set; }

        public int VerifiedArrivalTimeEntryCount { get; set; }

        public int VerifiedTravelTimeEntryCount { get; set; }

        public NpcCurrentScheduleSnapshotAuditRow[] Rows { get; set; } =
            Array.Empty<NpcCurrentScheduleSnapshotAuditRow>();

        public string[] Issues { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "day_start_native_schedule_key_static_endpoints_and_native_route_counts";
    }

    public sealed class NpcCurrentScheduleSnapshotAuditor
    {
        public NpcCurrentScheduleSnapshotAuditReport Audit(JsonElement snapshot)
        {
            if (!TryReadFieldValue(snapshot, "npcs", "schedule_catalog", out var catalog) ||
                !TryReadFieldValue(snapshot, "npcs", "schedules", out var schedules) ||
                schedules.ValueKind != JsonValueKind.Array ||
                !catalog.TryGetProperty("current_selection_context", out var context) ||
                context.ValueKind != JsonValueKind.Object)
            {
                return Blocked("current_schedule_snapshot_fields_missing");
            }

            if (ReadString(context, "status") !=
                    "complete_live_current_inputs_except_unobserved_rain2_roll" ||
                ReadBool(context, "day_start_capture") != true ||
                ReadInt(context, "game_time") != 600)
            {
                return Blocked("current_schedule_audit_requires_day_start_0600");
            }

            var nativeRows = schedules.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    ReadBool(row, "follow_schedule") == true &&
                    ReadBool(row, "ignore_schedule_today") == false &&
                    ReadBool(row, "schedule_loaded") == true &&
                    ReadString(row, "schedule_key").Length > 0)
                .ToArray();
            var results = new List<NpcCurrentScheduleSnapshotAuditRow>();
            foreach (var native in nativeRows)
            {
                var npcName = ReadString(native, "name");
                var resolved =
                    new NpcCurrentScheduleSnapshotProjectionResolver()
                        .Resolve(snapshot, npcName);
                results.Add(resolved.Status == "pass" &&
                    resolved.Verification is not null
                    ? Passed(
                        resolved.Verification,
                        resolved.ProjectionStatus,
                        resolved.RealizedRandomBranch)
                    : new NpcCurrentScheduleSnapshotAuditRow
                    {
                        NpcName = npcName,
                        Status = resolved.Status,
                        ProjectionStatus = resolved.ProjectionStatus,
                        SelectedScheduleKey =
                            resolved.Projection?.SelectedScheduleKey ??
                            string.Empty,
                        NativeScheduleKey =
                            resolved.Verification?.NativeScheduleKey ??
                            ReadString(native, "schedule_key"),
                        Issues = resolved.Issues
                    });
            }

            var mismatches = results.Count(row => row.Status == "mismatch");
            var verified = results.Count(row => row.Status == "pass");
            var failClosed = results.Count(row => row.Status == "blocked");
            var issues = new List<string>();
            if (nativeRows.Length == 0)
                issues.Add("no_loaded_native_schedules_at_day_start");
            if (verified == 0)
                issues.Add("no_exact_native_schedule_projection_verified");
            if (mismatches > 0)
                issues.Add("native_schedule_projection_mismatch_detected");

            return new NpcCurrentScheduleSnapshotAuditReport
            {
                Status = issues.Count == 0 ? "pass" : "blocked",
                GameTime = 600,
                LoadedScheduleCount = nativeRows.Length,
                VerifiedScheduleCount = verified,
                FailClosedScheduleCount = failClosed,
                MismatchCount = mismatches,
                VerifiedArrivalTimeEntryCount = results.Sum(row => row.VerifiedArrivalTimeEntryCount),
                VerifiedTravelTimeEntryCount = results.Sum(row => row.VerifiedTravelTimeEntryCount),
                Rows = results.ToArray(),
                Issues = issues.ToArray()
            };
        }

        internal static bool TryBuildScenario(
            JsonElement catalog,
            JsonElement context,
            JsonElement nativeSchedule,
            string npcName,
            out NpcFutureScheduleScenario scenario,
            out string issue)
        {
            scenario = new NpcFutureScheduleScenario();
            issue = string.Empty;
            if (!catalog.TryGetProperty("villagers", out var villagers) ||
                villagers.ValueKind != JsonValueKind.Array)
            {
                issue = "current_schedule_catalog_villagers_missing";
                return false;
            }
            var villagersArray = villagers.EnumerateArray().ToArray();
            var targetRows = villagersArray
                .Where(row => row.ValueKind == JsonValueKind.Object &&
                    ReadString(row, "npc_name") == npcName)
                .ToArray();
            if (targetRows.Length != 1)
            {
                issue = targetRows.Length == 0
                    ? "current_schedule_catalog_target_missing"
                    : "current_schedule_catalog_target_duplicate";
                return false;
            }
            var target = targetRows[0];
            var year = ReadInt(context, "year");
            var day = ReadInt(context, "day_of_month");
            var season = ReadString(context, "season");
            var weekday = ReadString(context, "weekday");
            var greenRain = ReadBool(context, "is_green_rain");
            var valleyRain = ReadBool(context, "valley_is_raining");
            var married = ReadBool(target, "currently_married");
            var allPlayerFriendship = ReadInt(target, "all_player_friendship_points");
            var npcRain = ReadBool(target, "current_location_is_raining");
            if (!year.HasValue || !day.HasValue || season.Length == 0 || weekday.Length == 0 ||
                !greenRain.HasValue || !valleyRain.HasValue || !married.HasValue ||
                !allPlayerFriendship.HasValue || !npcRain.HasValue ||
                ReadBool(target, "current_location_weather_available") != true)
            {
                issue = "current_schedule_selection_input_missing";
                return false;
            }

            if (!TryReadStringSet(context, "current_player_mail_received", out var currentMail) ||
                !TryReadStringSet(context, "master_player_mail_received", out var masterMail) ||
                !TryReadPassiveFestivals(context, out var festivals) ||
                !TryReadConditionSet(context, out var worldStateConditions) ||
                !TryReadLocationAccessibility(context, out var accessibility))
            {
                issue = "current_schedule_collection_input_missing";
                return false;
            }

            var maximumHearts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in villagersArray)
            {
                var name = ReadString(row, "npc_name");
                var hearts = ReadInt(row, "maximum_farmer_friendship_hearts");
                if (name.Length == 0 || !hearts.HasValue)
                {
                    issue = "current_schedule_maximum_friendship_hearts_invalid";
                    return false;
                }
                if (maximumHearts.TryGetValue(name, out var existing))
                {
                    if (existing != hearts.Value)
                    {
                        issue = "current_schedule_duplicate_friendship_hearts_conflict";
                        return false;
                    }
                }
                else
                {
                    maximumHearts.Add(name, hearts.Value);
                }
            }

            scenario = new NpcFutureScheduleScenario
            {
                Year = year,
                Season = season,
                DayOfMonth = day,
                Weekday = weekday,
                IsGreenRain = greenRain,
                IsMarried = married,
                IslandScheduleStateComplete = target.TryGetProperty("island_schedule_name", out var island) &&
                    island.ValueKind == JsonValueKind.String,
                IslandScheduleName = ReadString(target, "island_schedule_name"),
                ActivePassiveFestivals = festivals,
                AllPlayerFriendshipPoints = allPlayerFriendship,
                ValleyIsRaining = valleyRain,
                NpcLocationIsRaining = npcRain,
                Rain2Roll = null,
                CurrentPlayerMailReceived = currentMail,
                MasterPlayerMailReceived = masterMail,
                WorldStateIds = worldStateConditions,
                MaximumFarmerFriendshipHearts = maximumHearts,
                LocationAccessibility = accessibility,
                RealMillisecondsPerGameTenMinutes = ReadInt(
                    context,
                    "real_milliseconds_per_game_ten_minutes"),
                NativePathTimings = ReadNativePathTimings(nativeSchedule),
                NativePathTimingEvidenceComplete = true
            };
            return true;
        }

        private static NpcSchedulePathTimingEvidence[] ReadNativePathTimings(
            JsonElement nativeSchedule)
        {
            var scheduleKey = ReadString(nativeSchedule, "schedule_key");
            if (scheduleKey.Length == 0 ||
                !nativeSchedule.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<NpcSchedulePathTimingEvidence>();
            }

            var result = new List<NpcSchedulePathTimingEvidence>();
            var ordinal = 0;
            foreach (var row in entries.EnumerateArray())
            {
                var location = ReadString(row, "target_location_name");
                var tileX = ReadInt(row, "target_tile_x");
                var tileY = ReadInt(row, "target_tile_y");
                var facing = ReadInt(row, "facing_direction");
                var routePoints = ReadInt(row, "route_count");
                var adjacentPixels = ReadInt(row, "adjacent_route_pixel_distance");
                if (location.Length > 0 && tileX.HasValue && tileY.HasValue &&
                    facing.HasValue && routePoints.HasValue && adjacentPixels.HasValue)
                {
                    result.Add(new NpcSchedulePathTimingEvidence
                    {
                        ScheduleKey = scheduleKey,
                        ScheduleEntryOrdinal = ordinal,
                        TargetLocationName = location,
                        TargetTileX = tileX.Value,
                        TargetTileY = tileY.Value,
                        FacingDirection = facing.Value,
                        NativeRoutePointCount = routePoints.Value,
                        AdjacentRoutePixelDistance = adjacentPixels.Value
                    });
                }
                ordinal++;
            }
            return result.ToArray();
        }

        private static NpcCurrentScheduleSnapshotAuditRow Passed(
            NpcCurrentScheduleProjectionVerification verification,
            string projectionStatus,
            bool realizedRandomBranch) => new NpcCurrentScheduleSnapshotAuditRow
        {
            NpcName = verification.NpcName,
            Status = "pass",
            ProjectionStatus = projectionStatus,
            SelectedScheduleKey = verification.ProjectedSelectedScheduleKey,
            NativeScheduleKey = verification.NativeScheduleKey,
            VerifiedEntryCount = verification.VerifiedEntryCount,
            VerifiedArrivalTimeEntryCount = verification.VerifiedArrivalTimeEntryCount,
            VerifiedTravelTimeEntryCount = verification.VerifiedTravelTimeEntryCount,
            RealizedRandomBranch = realizedRandomBranch
        };

        private static NpcCurrentScheduleSnapshotAuditRow FailClosed(
            string npcName,
            string issue,
            string projectionStatus = "Blocked") => new NpcCurrentScheduleSnapshotAuditRow
        {
            NpcName = npcName,
            Status = "blocked",
            ProjectionStatus = projectionStatus,
            Issues = new[] { issue }
        };

        private static NpcCurrentScheduleSnapshotAuditReport Blocked(string issue) =>
            new NpcCurrentScheduleSnapshotAuditReport
            {
                Status = "blocked",
                Issues = new[] { issue }
            };

        internal static bool TryReadFieldValue(
            JsonElement snapshot,
            string domain,
            string field,
            out JsonElement value)
        {
            value = default;
            return snapshot.ValueKind == JsonValueKind.Object &&
                snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty(domain, out var domainNode) &&
                domainNode.ValueKind == JsonValueKind.Object &&
                domainNode.TryGetProperty(field, out var fieldNode) &&
                fieldNode.ValueKind == JsonValueKind.Object &&
                ReadString(fieldNode, "status") is "available" or "derived" &&
                fieldNode.TryGetProperty("value", out value);
        }

        private static bool TryReadStringSet(
            JsonElement source,
            string name,
            out ISet<string> values)
        {
            values = new HashSet<string>(StringComparer.Ordinal);
            if (!source.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String ||
                    !values.Add(item.GetString() ?? string.Empty))
                    return false;
            }
            return true;
        }

        private static bool TryReadPassiveFestivals(
            JsonElement source,
            out NpcPassiveFestivalScenario[] values)
        {
            values = Array.Empty<NpcPassiveFestivalScenario>();
            if (!source.TryGetProperty("active_passive_festivals", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return false;
            var rows = new List<NpcPassiveFestivalScenario>();
            foreach (var row in array.EnumerateArray())
            {
                var id = ReadString(row, "festival_id");
                var day = ReadInt(row, "day_index");
                if (id.Length == 0 || !day.HasValue || day.Value < 1)
                    return false;
                rows.Add(new NpcPassiveFestivalScenario { FestivalId = id, DayIndex = day.Value });
            }
            values = rows.ToArray();
            return true;
        }

        private static bool TryReadConditionSet(JsonElement source, out ISet<string> values)
        {
            values = new HashSet<string>(StringComparer.Ordinal);
            if (!source.TryGetProperty("mail_or_world_state_conditions", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var row in array.EnumerateArray())
            {
                var id = ReadString(row, "condition_id");
                var met = ReadBool(row, "condition_met");
                if (id.Length == 0 || !met.HasValue)
                    return false;
                if (met.Value)
                    values.Add(id);
            }
            return true;
        }

        private static bool TryReadLocationAccessibility(
            JsonElement source,
            out IReadOnlyDictionary<string, bool> values)
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            values = result;
            if (!source.TryGetProperty("location_accessibility", out var array) ||
                array.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var row in array.EnumerateArray())
            {
                var name = ReadString(row, "location_name");
                var accessible = ReadBool(row, "accessible");
                if (name.Length == 0 || !accessible.HasValue || !result.TryAdd(name, accessible.Value))
                    return false;
            }
            return result.ContainsKey("CommunityCenter") &&
                result.ContainsKey("JojaMart") &&
                result.ContainsKey("Railroad");
        }

        private static string ReadString(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static int? ReadInt(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value)
                ? value
                : null;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;
    }
}
