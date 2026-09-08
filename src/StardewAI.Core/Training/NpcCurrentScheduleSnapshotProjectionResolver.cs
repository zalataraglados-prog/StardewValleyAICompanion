using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class NpcCurrentScheduleSnapshotProjectionResult
    {
        public string Status { get; set; } = "blocked";

        public string NpcName { get; set; } = string.Empty;

        public string ProjectionStatus { get; set; } = string.Empty;

        public bool RealizedRandomBranch { get; set; }

        public NpcFutureScheduleResolution? Projection { get; set; }

        public NpcCurrentScheduleProjectionVerification? Verification { get; set; }

        public string[] Issues { get; set; } = Array.Empty<string>();
    }

    public sealed class NpcCurrentScheduleSnapshotProjectionResolver
    {
        public NpcCurrentScheduleSnapshotProjectionResult Resolve(
            JsonElement snapshot,
            string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return Blocked(npcName, "current_schedule_projection_npc_missing");
            if (!NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "npcs",
                    "schedule_catalog",
                    out var catalog) ||
                !NpcCurrentScheduleSnapshotAuditor.TryReadFieldValue(
                    snapshot,
                    "npcs",
                    "schedules",
                    out var schedules) ||
                schedules.ValueKind != JsonValueKind.Array ||
                !catalog.TryGetProperty(
                    "current_selection_context",
                    out var context) ||
                context.ValueKind != JsonValueKind.Object)
            {
                return Blocked(
                    npcName,
                    "current_schedule_snapshot_fields_missing");
            }
            if (ReadString(context, "status") !=
                    "complete_live_current_inputs_except_unobserved_rain2_roll" ||
                ReadBool(context, "day_start_capture") != true ||
                ReadInt(context, "game_time") != 600)
            {
                return Blocked(
                    npcName,
                    "current_schedule_audit_requires_day_start_0600");
            }

            var nativeRows = schedules.EnumerateArray().Where(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadString(row, "name") == npcName &&
                    ReadBool(row, "follow_schedule") == true &&
                    ReadBool(row, "ignore_schedule_today") == false &&
                    ReadBool(row, "schedule_loaded") == true &&
                    ReadString(row, "schedule_key").Length > 0)
                .ToArray();
            if (nativeRows.Length != 1)
            {
                return Blocked(
                    npcName,
                    nativeRows.Length == 0
                        ? "current_loaded_native_schedule_missing"
                        : "current_loaded_native_schedule_ambiguous");
            }
            if (!NpcCurrentScheduleSnapshotAuditor.TryBuildScenario(
                    catalog,
                    context,
                    nativeRows[0],
                    npcName,
                    out var scenario,
                    out var issue))
            {
                return Blocked(npcName, issue);
            }

            var projected = new NpcFutureScheduleResolver().Resolve(
                catalog,
                npcName,
                scenario);
            if (projected.Status == NpcFutureScheduleResolutionStatus.Conditional)
            {
                var matches = projected.Alternatives
                    .Select(alternative => new
                    {
                        Projection = alternative,
                        Verification = new NpcCurrentScheduleProjectionVerifier()
                            .Verify(alternative, schedules)
                    })
                    .Where(value => value.Verification.Status == "pass")
                    .ToArray();
                if (matches.Length == 1)
                {
                    return Passed(
                        npcName,
                        projected.Status.ToString(),
                        matches[0].Projection,
                        matches[0].Verification,
                        realizedRandomBranch: true);
                }
                return new NpcCurrentScheduleSnapshotProjectionResult
                {
                    Status = matches.Length == 0 ? "mismatch" : "blocked",
                    NpcName = npcName,
                    ProjectionStatus = projected.Status.ToString(),
                    Issues = new[]
                    {
                        matches.Length == 0
                            ? "no_realized_random_schedule_branch_matches_native"
                            : "multiple_random_schedule_branches_match_native"
                    }
                };
            }
            if (projected.Status != NpcFutureScheduleResolutionStatus.Exact)
            {
                return Blocked(
                    npcName,
                    projected.BlockingReasons.FirstOrDefault() ??
                        "current_schedule_projection_not_exact",
                    projected.Status.ToString());
            }

            var verification = new NpcCurrentScheduleProjectionVerifier()
                .Verify(projected, schedules);
            if (verification.Status != "pass")
            {
                return new NpcCurrentScheduleSnapshotProjectionResult
                {
                    Status = verification.Status,
                    NpcName = npcName,
                    ProjectionStatus = projected.Status.ToString(),
                    Projection = projected,
                    Verification = verification,
                    Issues = verification.Issues
                };
            }
            return Passed(
                npcName,
                projected.Status.ToString(),
                projected,
                verification,
                realizedRandomBranch: false);
        }

        private static NpcCurrentScheduleSnapshotProjectionResult Passed(
            string npcName,
            string projectionStatus,
            NpcFutureScheduleResolution projection,
            NpcCurrentScheduleProjectionVerification verification,
            bool realizedRandomBranch) => new()
        {
            Status = "pass",
            NpcName = npcName,
            ProjectionStatus = projectionStatus,
            RealizedRandomBranch = realizedRandomBranch,
            Projection = projection,
            Verification = verification
        };

        private static NpcCurrentScheduleSnapshotProjectionResult Blocked(
            string npcName,
            string issue,
            string projectionStatus = "Blocked") => new()
        {
            Status = "blocked",
            NpcName = npcName ?? string.Empty,
            ProjectionStatus = projectionStatus,
            Issues = new[] { issue }
        };

        private static string ReadString(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;

        private static int? ReadInt(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out var value)
                ? value
                : null;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.TryGetProperty(name, out var property) &&
            property.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? property.GetBoolean()
                : null;
    }
}
