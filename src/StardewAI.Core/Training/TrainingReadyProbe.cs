using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class TrainingReadyProbe
    {
        public TrainingReadyProbeResult Check(SnapshotEnvelope? latestSnapshot, bool bridgeReachable)
        {
            return Check(latestSnapshot, bridgeReachable, null, productExecutorReachable: false);
        }

        public TrainingReadyProbeResult Check(SnapshotEnvelope? latestSnapshot, bool bridgeReachable, string? manifestPath)
        {
            return Check(latestSnapshot, bridgeReachable, manifestPath, productExecutorReachable: false);
        }

        public TrainingReadyProbeResult Check(
            SnapshotEnvelope? latestSnapshot,
            bool bridgeReachable,
            string? manifestPath,
            bool productExecutorReachable)
        {
            var snapshotAvailable = latestSnapshot is not null;
            var reasons = new List<string>();
            if (!snapshotAvailable)
            {
                reasons.Add("no_transparent_snapshot_ingested");
            }

            var manifestLoaded = false;
            var expectedRunId = string.Empty;
            TrainingRunManifest? manifest = null;
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
                        manifest = JsonSerializer.Deserialize<TrainingRunManifest>(File.ReadAllText(manifestPath), JsonOptions);
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

            var formalBoundaryRequired = string.Equals(
                manifest?.Mode,
                TrainingSessionMode.FormalProductTraining,
                StringComparison.OrdinalIgnoreCase);
            var datasetManifestVerified = false;
            var checkpointVerified = false;
            var receiptJournalReady = false;
            var gameProcessAlive = false;
            var productExecutorProcessAlive = false;
            var liveTrainingLoopProcessAlive = false;
            var unresolvedProductReceipts = 0;
            if (formalBoundaryRequired && manifest is not null)
            {
                if (!string.Equals(manifest.SchemaVersion, "training_run_manifest.v2", StringComparison.Ordinal))
                    reasons.Add("formal_training_manifest_schema_unsupported");
                if (!string.Equals(manifest.Status, "running", StringComparison.Ordinal))
                    reasons.Add("formal_training_manifest_not_running");

                datasetManifestVerified = VerifyFile(
                    manifest.PolicyDatasetManifestPath,
                    manifest.PolicyDatasetManifestSha256);
                if (!datasetManifestVerified)
                    reasons.Add("formal_policy_dataset_manifest_digest_mismatch");

                checkpointVerified = VerifyCheckpoint(manifest);
                if (!checkpointVerified)
                    reasons.Add("formal_structured_policy_checkpoint_mismatch");

                receiptJournalReady = Directory.Exists(manifest.ProductReceiptRoot);
                if (receiptJournalReady)
                {
                    try
                    {
                        unresolvedProductReceipts = Directory
                            .EnumerateFiles(manifest.ProductReceiptRoot, "*.pending.json", SearchOption.TopDirectoryOnly)
                            .Count();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        receiptJournalReady = false;
                    }
                }
                if (!receiptJournalReady)
                    reasons.Add("formal_product_receipt_journal_unavailable");
                else if (unresolvedProductReceipts > 0)
                    reasons.Add("formal_product_receipt_recovery_required");

                gameProcessAlive = IsProcessAlive(manifest.ProcessId);
                if (!gameProcessAlive)
                    reasons.Add("formal_game_process_not_alive");
                productExecutorProcessAlive = IsProcessAlive(manifest.ProductExecutorProcessId);
                if (!productExecutorProcessAlive)
                    reasons.Add("formal_product_executor_process_not_alive");
                liveTrainingLoopProcessAlive = IsProcessAlive(manifest.LiveTrainingLoopProcessId);
                if (!liveTrainingLoopProcessAlive)
                    reasons.Add("formal_live_training_loop_process_not_alive");
                if (!productExecutorReachable)
                    reasons.Add("formal_product_executor_unreachable");
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
                FormalBoundaryRequired = formalBoundaryRequired,
                DatasetManifestVerified = datasetManifestVerified,
                CheckpointVerified = checkpointVerified,
                ProductExecutorReachable = productExecutorReachable,
                ReceiptJournalReady = receiptJournalReady,
                GameProcessAlive = gameProcessAlive,
                ProductExecutorProcessAlive = productExecutorProcessAlive,
                LiveTrainingLoopProcessAlive = liveTrainingLoopProcessAlive,
                UnresolvedProductReceipts = unresolvedProductReceipts,
                RunId = expectedRunId,
                SnapshotRunId = snapshotRunId,
                SnapshotGameTick = latestSnapshot?.GameTick,
                CheckedAt = DateTimeOffset.UtcNow.ToString("O"),
                BlockReasons = reasons.ToArray()
            };
        }

        private static bool VerifyCheckpoint(TrainingRunManifest manifest)
        {
            if (!VerifyFile(manifest.CheckpointPath, manifest.CheckpointSha256))
                return false;
            try
            {
                var checkpoint = new StructuredPolicyCheckpointStore().Load(manifest.CheckpointPath);
                return string.Equals(
                        Path.GetFullPath(checkpoint.Dataset.ManifestPath),
                        Path.GetFullPath(manifest.PolicyDatasetManifestPath),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        checkpoint.Dataset.ManifestSha256,
                        manifest.PolicyDatasetManifestSha256,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        checkpoint.Versions.Executor,
                        PolicyTrajectoryVersionPins.ProductExecutor,
                        StringComparison.Ordinal) &&
                    VerifyPolicyDatasetFiles(manifest.PolicyDatasetManifestPath, checkpoint);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
            {
                return false;
            }
        }

        private static bool VerifyPolicyDatasetFiles(
            string manifestPath,
            StructuredPolicyCheckpointEnvelope checkpoint)
        {
            try
            {
                var dataset = JsonSerializer.Deserialize<PolicyDatasetManifest>(
                    File.ReadAllText(manifestPath),
                    JsonOptions);
                if (dataset is null ||
                    !string.Equals(dataset.SchemaVersion, "policy_dataset_manifest.v1", StringComparison.Ordinal) ||
                    dataset.Partitions is null ||
                    dataset.Partitions.Length != 3 ||
                    dataset.VersionSets is null ||
                    dataset.VersionSets.Length != 1 ||
                    !string.Equals(
                        dataset.VersionSets[0].Executor,
                        PolicyTrajectoryVersionPins.ProductExecutor,
                        StringComparison.Ordinal) ||
                    !VerifyDigest(dataset.Input) ||
                    (dataset.HorizonObservations is not null && !VerifyDigest(dataset.HorizonObservations)) ||
                    !VerifyDigest(dataset.Cleaned) ||
                    dataset.Partitions.Any(value => !VerifyDigest(value)))
                    return false;

                var train = dataset.Partitions.SingleOrDefault(value => value.Partition == PolicyDatasetPartitions.Train);
                var validation = dataset.Partitions.SingleOrDefault(value => value.Partition == PolicyDatasetPartitions.Validation);
                var test = dataset.Partitions.SingleOrDefault(value => value.Partition == PolicyDatasetPartitions.Test);
                return train is not null && validation is not null && test is not null &&
                    string.Equals(dataset.Cleaned.Sha256, checkpoint.Dataset.CleanedSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(train.Sha256, checkpoint.Dataset.TrainSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(validation.Sha256, checkpoint.Dataset.ValidationSha256, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(test.Sha256, checkpoint.Dataset.TestSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
            {
                return false;
            }
        }

        private static bool VerifyDigest(PolicyDatasetFileDigest digest) =>
            digest is not null && VerifyFile(digest.Path, digest.Sha256);

        private static bool VerifyFile(string path, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(expectedSha256) ||
                !File.Exists(path))
                return false;
            try
            {
                return string.Equals(
                    StructuredPolicyCheckpointStore.HashFile(path),
                    expectedSha256,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool IsProcessAlive(int? processId)
        {
            if (!processId.HasValue || processId <= 0)
                return false;
            try
            {
                using var process = Process.GetProcessById(processId.Value);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
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
