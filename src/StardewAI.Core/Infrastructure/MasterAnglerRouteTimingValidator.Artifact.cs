using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Infrastructure
{
    public static partial class MasterAnglerRouteTimingValidator
    {
        private static readonly ConcurrentDictionary<string, CachedArtifact>
            ArtifactCache = new(StringComparer.OrdinalIgnoreCase);

        private static bool TryReadArtifact(
            string path,
            string expectedHash,
            out string artifactJson,
            out string rejectionReason)
        {
            artifactJson = string.Empty;
            rejectionReason = string.Empty;
            string fullPath;
            FileInfo file;
            try
            {
                fullPath = Path.GetFullPath(path);
                file = new FileInfo(fullPath);
                if (!file.Exists)
                {
                    rejectionReason =
                        "master_angler_route_timing_calibration_file_missing";
                    return false;
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException or NotSupportedException or
                    PathTooLongException)
            {
                rejectionReason =
                    "master_angler_route_timing_calibration_path_invalid";
                return false;
            }

            if (ArtifactCache.TryGetValue(fullPath, out var cached) &&
                cached.Length == file.Length &&
                cached.LastWriteUtcTicks == file.LastWriteTimeUtc.Ticks &&
                string.Equals(
                    cached.Sha256,
                    expectedHash,
                    StringComparison.Ordinal))
            {
                artifactJson = cached.Json;
                return true;
            }

            try
            {
                var bytes = File.ReadAllBytes(fullPath);
                string actualHash;
                using (var sha256 = SHA256.Create())
                {
                    actualHash = BitConverter.ToString(
                            sha256.ComputeHash(bytes))
                        .Replace("-", string.Empty)
                        .ToLowerInvariant();
                }
                if (!string.Equals(
                        actualHash,
                        expectedHash,
                        StringComparison.Ordinal))
                {
                    rejectionReason =
                        "master_angler_route_timing_calibration_sha256_mismatch";
                    return false;
                }
                artifactJson = System.Text.Encoding.UTF8.GetString(bytes);
                ArtifactCache[fullPath] = new CachedArtifact(
                    file.Length,
                    file.LastWriteTimeUtc.Ticks,
                    actualHash,
                    artifactJson);
                return true;
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                rejectionReason =
                    "master_angler_route_timing_calibration_read_failed";
                return false;
            }
        }

        private static bool TryRead(
            SmallModelActionParameter[] parameters,
            string name,
            out string value)
        {
            var matches = parameters
                .Where(parameter =>
                    parameter.Name == name ||
                    parameter.Name == "continuation." + name)
                .Select(parameter => parameter.Value)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            value = matches.Length == 1 ? matches[0] : string.Empty;
            return matches.Length == 1;
        }

        private static string AppendFirstReason(
            string prefix,
            IReadOnlyList<string> reasons) =>
            reasons.FirstOrDefault() is { Length: > 0 } reason
                ? prefix + ":" + reason
                : prefix;

        private static bool IsSha256(string value) =>
            value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f');

        private sealed record CachedArtifact(
            long Length,
            long LastWriteUtcTicks,
            string Sha256,
            string Json);
    }
}
