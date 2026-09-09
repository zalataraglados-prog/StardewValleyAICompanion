using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public sealed class FutureRouteTimingCalibration
    {
        internal FutureRouteTimingCalibration(
            int totalDays,
            int captureTotalDays,
            int gameMinuteNumeratorPerTile,
            int gameMinuteDenominatorPerTile,
            int connectorTransitionGameMinutes,
            string gameVersion,
            string evidenceId,
            string evidenceSha256)
        {
            TotalDays = totalDays;
            CalibrationCaptureTotalDays = captureTotalDays;
            GameMinuteNumeratorPerTile = gameMinuteNumeratorPerTile;
            GameMinuteDenominatorPerTile = gameMinuteDenominatorPerTile;
            ConnectorTransitionGameMinutes = connectorTransitionGameMinutes;
            GameVersion = gameVersion;
            EvidenceId = evidenceId;
            EvidenceSha256 = evidenceSha256;
        }

        public int TotalDays { get; }

        public int CalibrationCaptureTotalDays { get; }

        public FutureRouteTravelTimingEvidenceKind EvidenceKind =>
            FutureRouteTravelTimingEvidenceKind.ConservativeUpperBound;

        public int GameMinuteNumeratorPerTile { get; }

        public int GameMinuteDenominatorPerTile { get; }

        public int ConnectorTransitionGameMinutes { get; }

        public bool StateComplete => true;

        public string GameVersion { get; }

        public string EvidenceId { get; }

        public string EvidenceSha256 { get; }

        public string Scope =>
            "current_date_route_bound_to_versioned_runtime_calibration_and_current_native_movement_context";
    }

    public enum FutureRouteTimingCalibrationLoadStatus
    {
        Loaded,
        Blocked
    }

    public sealed class FutureRouteTimingCalibrationLoadResult
    {
        public FutureRouteTimingCalibrationLoadStatus Status { get; set; }

        public FutureRouteTimingCalibration? Calibration { get; set; }

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();
    }

    public sealed class FutureRouteTimingCalibrationLoader
    {
        private const string Schema =
            "stardewai.runtime_movement_timing_calibration.v1";
        private const string MovementProjection =
            "exact_current_player_native_cardinal_movement_context";
        private const string MovementScope =
            "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay";

        public FutureRouteTimingCalibrationLoadResult Load(
            string artifactJson,
            JsonElement movementTimingContextField,
            string expectedGameVersion,
            int targetTotalDays)
        {
            if (string.IsNullOrWhiteSpace(artifactJson) ||
                string.IsNullOrWhiteSpace(expectedGameVersion) ||
                targetTotalDays < 0)
            {
                return Blocked("future_route_timing_calibration_input_invalid");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(artifactJson);
            }
            catch (JsonException)
            {
                return Blocked("future_route_timing_calibration_json_invalid");
            }

            using (document)
            {
                var artifact = document.RootElement;
                if (artifact.ValueKind != JsonValueKind.Object)
                    return Blocked("future_route_timing_calibration_artifact_invalid");
                if (!TryUnwrapField(
                        movementTimingContextField,
                        out var movementContext))
                {
                    return Blocked(
                        "future_route_movement_timing_context_unavailable");
                }

                var reasons = ValidateArtifact(
                    artifact,
                    movementContext,
                    expectedGameVersion);
                if (reasons.Count > 0)
                    return Blocked(reasons.ToArray());

                var sha256 = Sha256(artifactJson);
                var gameVersion = ReadString(artifact, "game_version")!;
                return new FutureRouteTimingCalibrationLoadResult
                {
                    Status = FutureRouteTimingCalibrationLoadStatus.Loaded,
                    Calibration = new FutureRouteTimingCalibration(
                        targetTotalDays,
                        ReadInt(artifact, "capture_total_days")!.Value,
                        ReadInt(
                            artifact,
                            "conservative_game_minute_numerator_per_tile")!.Value,
                        ReadInt(
                            artifact,
                            "conservative_game_minute_denominator_per_tile")!.Value,
                        ReadInt(
                            artifact,
                            "conservative_connector_transition_game_minutes")!.Value,
                        gameVersion,
                        "runtime_movement_timing:" + gameVersion + ":" + sha256,
                        sha256)
                };
            }
        }

        private static List<string> ValidateArtifact(
            JsonElement artifact,
            JsonElement movement,
            string expectedGameVersion)
        {
            var reasons = new List<string>();
            Require(
                string.Equals(
                    ReadString(artifact, "schema_version"),
                    Schema,
                    StringComparison.Ordinal),
                "future_route_timing_calibration_schema_mismatch",
                reasons);
            Require(
                string.Equals(
                    ReadString(artifact, "status"),
                    "passed",
                    StringComparison.Ordinal),
                "future_route_timing_calibration_not_passed",
                reasons);
            Require(
                string.Equals(
                    ReadString(artifact, "game_version"),
                    expectedGameVersion,
                    StringComparison.Ordinal),
                "future_route_timing_calibration_game_version_mismatch",
                reasons);
            Require(
                ReadInt(artifact, "capture_total_days") >= 0,
                "future_route_timing_calibration_capture_date_invalid",
                reasons);

            var numerator = ReadInt(
                artifact,
                "conservative_game_minute_numerator_per_tile");
            var denominator = ReadInt(
                artifact,
                "conservative_game_minute_denominator_per_tile");
            var movementUpperBound =
                numerator > 0 && denominator > 0
                    ? numerator.Value / (double)denominator.Value
                    : (double?)null;
            var maximumObserved = ReadDouble(
                artifact,
                "maximum_observed_game_minutes_per_tile");
            Require(
                ReadInt(artifact, "sample_count") >= 3 &&
                ReadInt(artifact, "total_manhattan_tiles") > 0 &&
                maximumObserved > 0d &&
                movementUpperBound > 0d &&
                maximumObserved <= movementUpperBound,
                "future_route_timing_calibration_movement_samples_invalid",
                reasons);

            var connectorBound = ReadInt(
                artifact,
                "conservative_connector_transition_game_minutes");
            var maximumConnector = ReadDouble(
                artifact,
                "maximum_observed_connector_game_minutes");
            var connectorKinds = ReadStringArray(artifact, "connector_kinds");
            Require(
                ReadInt(artifact, "connector_sample_count") >= 2 &&
                connectorBound > 0 &&
                maximumConnector >= 0d &&
                maximumConnector <= connectorBound &&
                connectorKinds.Contains("building_door", StringComparer.Ordinal) &&
                connectorKinds.Contains("warp", StringComparer.Ordinal) &&
                string.Equals(
                    ReadString(
                        artifact,
                        "connector_transition_game_minutes_status"),
                    "runtime_proven_building_door_and_step_warp",
                    StringComparison.Ordinal),
                "future_route_timing_calibration_connector_samples_invalid",
                reasons);

            var before = ReadString(
                artifact,
                "source_save_fingerprint_before");
            var after = ReadString(
                artifact,
                "source_save_fingerprint_after");
            Require(
                ReadBool(artifact, "source_save_untouched") == true &&
                !string.IsNullOrWhiteSpace(before) &&
                string.Equals(before, after, StringComparison.Ordinal),
                "future_route_timing_calibration_source_save_changed",
                reasons);
            Require(
                string.Equals(
                    ReadString(artifact, "calibration_evidence_kind"),
                    "conservative_upper_bound",
                    StringComparison.Ordinal),
                "future_route_timing_calibration_evidence_kind_invalid",
                reasons);
            Require(
                string.Equals(
                    ReadString(artifact, "movement_context_projection_status"),
                    MovementProjection,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(artifact, "movement_context_scope"),
                    MovementScope,
                    StringComparison.Ordinal),
                "future_route_timing_calibration_movement_scope_invalid",
                reasons);

            var contextUpperBound = ReadDouble(
                movement,
                "theoretical_upper_bound_game_minutes_per_tile");
            Require(
                string.Equals(
                    ReadString(movement, "projection_status"),
                    MovementProjection,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(movement, "scope"),
                    MovementScope,
                    StringComparison.Ordinal) &&
                ReadBool(movement, "runtime_calibration_compatible") == true &&
                ReadBool(movement, "route_timing_ready_now") == true &&
                contextUpperBound > 0d &&
                movementUpperBound.HasValue &&
                contextUpperBound <= movementUpperBound,
                "future_route_movement_timing_context_outside_calibration_scope",
                reasons);
            Require(
                ReadDouble(artifact, "native_milliseconds_per_game_minute") > 0d &&
                ReadDouble(artifact, "native_milliseconds_per_game_minute") ==
                    ReadDouble(movement, "real_milliseconds_per_game_minute"),
                "future_route_movement_clock_constant_mismatch",
                reasons);
            return reasons;
        }

        private static bool TryUnwrapField(
            JsonElement source,
            out JsonElement value)
        {
            value = default;
            if (source.ValueKind != JsonValueKind.Object)
                return false;
            if (!source.TryGetProperty("status", out var status))
            {
                value = source;
                return true;
            }
            if (status.ValueKind != JsonValueKind.String ||
                status.GetString() is not ("available" or "derived") ||
                !source.TryGetProperty("value", out value) ||
                value.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }
            return true;
        }

        private static void Require(
            bool condition,
            string reason,
            ICollection<string> reasons)
        {
            if (!condition)
                reasons.Add(reason);
        }

        private static string? ReadString(JsonElement source, string name) =>
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static int? ReadInt(JsonElement source, string name) =>
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var parsed)
                ? parsed
                : null;

        private static double? ReadDouble(JsonElement source, string name) =>
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var parsed)
                ? parsed
                : null;

        private static bool? ReadBool(JsonElement source, string name) =>
            source.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;

        private static string[] ReadStringArray(
            JsonElement source,
            string name) =>
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Array
                ? value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .ToArray()
                : Array.Empty<string>();

        private static string Sha256(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static FutureRouteTimingCalibrationLoadResult Blocked(
            string reason) => Blocked(new[] { reason });

        private static FutureRouteTimingCalibrationLoadResult Blocked(
            string[] reasons) => new()
        {
            Status = FutureRouteTimingCalibrationLoadStatus.Blocked,
            BlockingReasons = reasons
        };
    }
}
