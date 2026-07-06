using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class TrainingReadyProbe
    {
        public TrainingReadyProbeResult Check(SnapshotEnvelope? latestSnapshot, bool bridgeReachable)
        {
            return Check(latestSnapshot, bridgeReachable, null);
        }

        public TrainingReadyProbeResult Check(SnapshotEnvelope? latestSnapshot, bool bridgeReachable, string? manifestPath)
        {
            var snapshotAvailable = latestSnapshot is not null;
            var reasons = new List<string>();
            if (!snapshotAvailable)
            {
                reasons.Add("no_transparent_snapshot_ingested");
            }

            var manifestLoaded = false;
            var expectedRunId = string.Empty;
            if (!string.IsNullOrWhiteSpace(manifestPath))
            {
                if (!File.Exists(manifestPath))
                {
                    reasons.Add("training_manifest_not_found");
                }
                else
                {
                    try
                    {
                        var manifest = JsonSerializer.Deserialize<TrainingRunManifest>(File.ReadAllText(manifestPath), JsonOptions);
                        expectedRunId = manifest?.RunId ?? string.Empty;
                        manifestLoaded = !string.IsNullOrWhiteSpace(expectedRunId);
                        if (!manifestLoaded)
                        {
                            reasons.Add("training_manifest_missing_run_id");
                        }
                    }
                    catch (JsonException)
                    {
                        reasons.Add("training_manifest_invalid_json");
                    }
                }
            }

            var snapshotRunId = latestSnapshot is null ? string.Empty : ReadStringField(latestSnapshot, "environment", "training_run_id");
            var snapshotTrainingMode = latestSnapshot is null ? string.Empty : ReadStringField(latestSnapshot, "environment", "training_mode");
            if (manifestLoaded)
            {
                if (!string.Equals(snapshotTrainingMode, "1", StringComparison.Ordinal))
                {
                    reasons.Add("snapshot_not_from_training_mode");
                }

                if (!string.Equals(snapshotRunId, expectedRunId, StringComparison.Ordinal))
                {
                    reasons.Add("snapshot_run_id_does_not_match_manifest");
                }
            }

            return new TrainingReadyProbeResult
            {
                Ready = snapshotAvailable && bridgeReachable && reasons.Count == 0,
                BackendReachable = true,
                BridgeReachable = bridgeReachable,
                LatestSnapshotAvailable = snapshotAvailable,
                LatestStateHash = latestSnapshot?.StateHash ?? string.Empty,
                ManifestLoaded = manifestLoaded,
                RunId = expectedRunId,
                SnapshotRunId = snapshotRunId,
                SnapshotGameTick = latestSnapshot?.GameTick,
                CheckedAt = DateTimeOffset.UtcNow.ToString("O"),
                BlockReasons = reasons.ToArray()
            };
        }

        private static string ReadStringField(SnapshotEnvelope snapshot, string section, string name)
        {
            if (!snapshot.State.TryGetValue(section, out var sectionElement) ||
                sectionElement.ValueKind != JsonValueKind.Object ||
                !sectionElement.TryGetProperty(name, out var fieldElement) ||
                fieldElement.ValueKind != JsonValueKind.Object ||
                !fieldElement.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.String)
            {
                return string.Empty;
            }

            return valueElement.GetString() ?? string.Empty;
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
