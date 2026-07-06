using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class StardewTrainingSessionLauncher
    {
        public TrainingLaunchResult Prepare(TrainingLaunchRequest request)
        {
            var rootPath = FullPathOrDefault(request.RootPath, @"E:\StardewAITraining");
            var manifest = BuildManifest(request, rootPath);
            var blockReasons = Validate(request, manifest);
            manifest.Status = blockReasons.Count == 0 ? "prepared" : "blocked";
            manifest.Audit.Notes = blockReasons.ToArray();

            Directory.CreateDirectory(rootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.DatasetPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ReportPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.CheckpointPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ManifestPath)!);
            File.WriteAllText(manifest.ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));

            return new TrainingLaunchResult
            {
                Started = false,
                Blocked = blockReasons.Count > 0,
                BlockReasons = blockReasons.ToArray(),
                Manifest = manifest
            };
        }

        private static TrainingRunManifest BuildManifest(TrainingLaunchRequest request, string rootPath)
        {
            var runId = "train." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "." + Guid.NewGuid().ToString("N").Substring(0, 8);
            var mode = string.IsNullOrWhiteSpace(request.Mode) ? TrainingSessionMode.OfflineSmoke : request.Mode.Trim();
            var manifestPath = FullPathOrDefault(request.ManifestPath, Path.Combine(rootPath, "runs", runId, "training-run-manifest.json"));

            return new TrainingRunManifest
            {
                RunId = runId,
                Mode = mode,
                RootPath = rootPath,
                DatasetPath = FullPathOrDefault(request.DatasetPath, Path.Combine(rootPath, "datasets", "training-feature-rows.jsonl")),
                ReportPath = FullPathOrDefault(request.ReportPath, Path.Combine(rootPath, "reports", "training-report.json")),
                CheckpointPath = FullPathOrDefault(request.CheckpointPath, Path.Combine(rootPath, "checkpoints", "baseline-latest.json")),
                ManifestPath = manifestPath,
                GameExecutablePath = FullPathOrEmpty(request.GameExecutablePath),
                GameWorkingDirectory = FullPathOrEmpty(request.GameWorkingDirectory),
                SaveIsolationPath = FullPathOrEmpty(request.SaveIsolationPath),
                BridgeUrl = request.BridgeUrl,
                BackendUrl = request.BackendUrl,
                GameLaunch = request.AllowGameLaunch ? "requested" : "disabled",
                Sound = request.SoundEnabled ? "enabled" : "disabled",
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static List<string> Validate(TrainingLaunchRequest request, TrainingRunManifest manifest)
        {
            var reasons = new List<string>();
            var realGameMode = string.Equals(manifest.Mode, TrainingSessionMode.StardewWindowed, StringComparison.OrdinalIgnoreCase);

            if (request.SoundEnabled)
            {
                reasons.Add("sound_must_be_disabled_for_background_training");
            }

            if (realGameMode && !request.AllowGameLaunch)
            {
                reasons.Add("real_game_launch_requires_allow_game_launch_true");
            }

            if (realGameMode && string.IsNullOrWhiteSpace(manifest.GameExecutablePath))
            {
                reasons.Add("game_executable_path_required_for_real_game_mode");
            }

            if (realGameMode && !string.IsNullOrWhiteSpace(manifest.GameExecutablePath) && !File.Exists(manifest.GameExecutablePath))
            {
                reasons.Add("game_executable_path_not_found");
            }

            if (realGameMode && string.IsNullOrWhiteSpace(manifest.SaveIsolationPath))
            {
                reasons.Add("save_isolation_path_required_for_real_game_training");
            }

            return reasons;
        }

        private static string FullPathOrDefault(string? value, string defaultPath)
        {
            return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? defaultPath : value);
        }

        private static string FullPathOrEmpty(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Path.GetFullPath(value);
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
    }
}
