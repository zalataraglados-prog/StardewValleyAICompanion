using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure
{
    public static partial class MasterAnglerWindowIntentValidator
    {
        private const string SchemaVersion =
            "master_angler_stage_one_window_index.v1";
        private const string CompleteStatus =
            "complete_static_windows_dynamic_execution_pending";
        private static readonly ConcurrentDictionary<string, CachedArtifact> Cache =
            new(StringComparer.OrdinalIgnoreCase);

        private static bool TryLoadArtifact(
            string path,
            string expectedHash,
            out ParsedArtifact artifact,
            out string rejectionReason)
        {
            artifact = null!;
            rejectionReason = string.Empty;
            string fullPath;
            FileInfo file;
            try
            {
                fullPath = Path.GetFullPath(path);
                file = new FileInfo(fullPath);
                if (!file.Exists)
                {
                    rejectionReason = "master_angler_window_index_file_missing";
                    return false;
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                rejectionReason = "master_angler_window_index_path_invalid";
                return false;
            }

            if (Cache.TryGetValue(fullPath, out var cached) &&
                cached.Length == file.Length &&
                cached.LastWriteUtcTicks == file.LastWriteTimeUtc.Ticks &&
                string.Equals(cached.Sha256, expectedHash, StringComparison.Ordinal))
            {
                artifact = cached.Artifact;
                return true;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                string actualHash;
                using (var sha256 = SHA256.Create())
                {
                    actualHash = BitConverter.ToString(sha256.ComputeHash(bytes))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
                if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
                {
                    rejectionReason = "master_angler_window_index_sha256_mismatch";
                    return false;
                }
                using var document = JsonDocument.Parse(bytes);
                if (!TryParseArtifact(document.RootElement, out artifact))
                {
                    rejectionReason =
                        "master_angler_window_index_schema_or_coverage_invalid";
                    return false;
                }
                Cache[fullPath] = new CachedArtifact(
                    file.Length,
                    file.LastWriteTimeUtc.Ticks,
                    actualHash,
                    artifact);
                return true;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException or JsonException)
            {
                rejectionReason = "master_angler_window_index_read_failed";
                return false;
            }
        }

        private static bool TryParseArtifact(
            JsonElement root,
            out ParsedArtifact artifact)
        {
            artifact = null!;
            if (root.ValueKind != JsonValueKind.Object ||
                ReadString(root, "schema_version") != SchemaVersion ||
                ReadString(root, "status") != CompleteStatus ||
                ReadBool(root, "static_window_coverage_complete") != true ||
                ReadBool(root, "training_label_eligible") != false ||
                ReadInt(root, "native_denominator_count") != 72 ||
                !root.TryGetProperty("species", out var species) ||
                species.ValueKind != JsonValueKind.Array ||
                species.GetArrayLength() != 72)
            {
                return false;
            }

            var index = new Dictionary<string, ArtifactWindow[]>(StringComparer.Ordinal);
            foreach (var row in species.EnumerateArray())
            {
                var qid = ReadString(row, "qualified_item_id");
                if (string.IsNullOrWhiteSpace(qid) ||
                    !row.TryGetProperty("windows", out var windows) ||
                    windows.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }
                var parsed = windows.EnumerateArray().Select(ParseWindow).ToArray();
                if (parsed.Any(value => value is null) ||
                    !index.TryAdd(qid, parsed.Cast<ArtifactWindow>().ToArray()))
                {
                    return false;
                }
            }
            artifact = new ParsedArtifact(
                ReadString(root, "game_version"),
                ReadInt(root, "deadline_total_day_exclusive"),
                index);
            return artifact.DeadlineTotalDayExclusive > 0;
        }

        private static ArtifactWindow? ParseWindow(JsonElement row)
        {
            if (row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("time_windows", out var timeWindows) ||
                timeWindows.ValueKind != JsonValueKind.Array ||
                !row.TryGetProperty("dynamic_conditions", out var dynamic) ||
                dynamic.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var times = timeWindows.EnumerateArray()
                .Select(value => new ArtifactTimeWindow(
                    ReadInt(value, "start_time"),
                    ReadInt(value, "end_time")))
                .Where(value => value.Start >= 0 && value.End > value.Start)
                .ToArray();
            return times.Length == 0
                ? null
                : new ArtifactWindow(
                    ReadString(row, "source_kind"),
                    ReadString(row, "source_key"),
                    ReadString(row, "location_id"),
                    ReadInt(row, "first_total_day"),
                    ReadInt(row, "last_total_day"),
                    ReadInt(row, "minimum_fishing_level"),
                    ReadBool(row, "require_magic_bait") == true,
                    ReadNullableBool(row, "training_rod_allowed"),
                    dynamic.GetArrayLength(),
                    times);
        }

        private sealed record CachedArtifact(
            long Length,
            long LastWriteUtcTicks,
            string Sha256,
            ParsedArtifact Artifact);

        private sealed record ParsedArtifact(
            string GameVersion,
            int DeadlineTotalDayExclusive,
            IReadOnlyDictionary<string, ArtifactWindow[]> WindowsBySpecies);

        private sealed record ArtifactWindow(
            string SourceKind,
            string SourceKey,
            string LocationId,
            int FirstTotalDay,
            int LastTotalDay,
            int MinimumFishingLevel,
            bool RequireMagicBait,
            bool? TrainingRodAllowed,
            int DynamicConditionCount,
            ArtifactTimeWindow[] TimeWindows);

        private sealed record ArtifactTimeWindow(int Start, int End);
    }
}
