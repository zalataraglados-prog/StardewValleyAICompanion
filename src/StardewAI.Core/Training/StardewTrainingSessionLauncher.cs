using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var blockReasons = Validate(request, manifest, requireLaunchPermission: false);
            manifest.Status = blockReasons.Count == 0 ? "prepared" : "blocked";
            manifest.Audit.Notes = blockReasons.ToArray();

            WriteManifest(manifest);

            return new TrainingLaunchResult
            {
                Started = false,
                LaunchAttempted = false,
                Blocked = blockReasons.Count > 0,
                BlockReasons = blockReasons.ToArray(),
                Manifest = manifest
            };
        }

        public TrainingLaunchResult Launch(TrainingLaunchRequest request)
        {
            var rootPath = FullPathOrDefault(request.RootPath, @"E:\StardewAITraining");
            var manifest = BuildManifest(request, rootPath);
            var blockReasons = Validate(request, manifest, requireLaunchPermission: true);
            if (blockReasons.Count > 0)
            {
                manifest.Status = "blocked";
                manifest.Audit.Notes = blockReasons.ToArray();
                WriteManifest(manifest);
                return new TrainingLaunchResult
                {
                    Started = false,
                    LaunchAttempted = false,
                    Blocked = true,
                    BlockReasons = blockReasons.ToArray(),
                    Manifest = manifest
                };
            }

            try
            {
                var process = StartTrainingProcess(manifest);
                manifest.ProcessId = process.Id;
                manifest.Status = "running";
                manifest.GameLaunch = "started";
                WriteManifest(manifest);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                manifest.Status = "blocked";
                manifest.Audit.Notes = new[] { "process_start_failed: " + ex.Message };
                WriteManifest(manifest);
                return new TrainingLaunchResult
                {
                    Started = false,
                    LaunchAttempted = true,
                    Blocked = true,
                    BlockReasons = manifest.Audit.Notes,
                    Manifest = manifest
                };
            }

            return new TrainingLaunchResult
            {
                Started = true,
                LaunchAttempted = true,
                Blocked = false,
                Manifest = manifest
            };
        }

        private static TrainingRunManifest BuildManifest(TrainingLaunchRequest request, string rootPath)
        {
            var runId = "train." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "." + Guid.NewGuid().ToString("N").Substring(0, 8);
            var mode = string.IsNullOrWhiteSpace(request.Mode) ? TrainingSessionMode.OfflineSmoke : request.Mode.Trim();
            var manifestPath = FullPathOrDefault(request.ManifestPath, Path.Combine(rootPath, "runs", runId, "training-run-manifest.json"));
            var gameExecutablePath = FullPathOrEmpty(request.GameExecutablePath);
            var gameWorkingDirectory = FullPathOrDefault(request.GameWorkingDirectory, string.IsNullOrWhiteSpace(gameExecutablePath)
                ? rootPath
                : Path.GetDirectoryName(gameExecutablePath)!);

            return new TrainingRunManifest
            {
                RunId = runId,
                Mode = mode,
                RootPath = rootPath,
                DatasetPath = FullPathOrDefault(request.DatasetPath, Path.Combine(rootPath, "datasets", "training-feature-rows.jsonl")),
                ReportPath = FullPathOrDefault(request.ReportPath, Path.Combine(rootPath, "reports", "training-report.json")),
                CheckpointPath = FullPathOrDefault(request.CheckpointPath, Path.Combine(rootPath, "checkpoints", "baseline-latest.json")),
                ManifestPath = manifestPath,
                GameExecutablePath = gameExecutablePath,
                GameWorkingDirectory = gameWorkingDirectory,
                SaveIsolationPath = FullPathOrEmpty(request.SaveIsolationPath),
                BridgeUrl = request.BridgeUrl,
                BackendUrl = request.BackendUrl,
                GameLaunch = request.AllowGameLaunch ? "requested" : "disabled",
                Sound = request.SoundEnabled ? "enabled" : "disabled",
                WindowStyle = string.IsNullOrWhiteSpace(request.WindowStyle) ? "minimized" : request.WindowStyle.Trim(),
                ExecutableKind = ClassifyExecutable(gameExecutablePath),
                EnvironmentOverrides = BuildEnvironmentOverrides(request),
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static List<string> Validate(TrainingLaunchRequest request, TrainingRunManifest manifest, bool requireLaunchPermission)
        {
            var reasons = new List<string>();
            var realGameMode = string.Equals(manifest.Mode, TrainingSessionMode.StardewWindowed, StringComparison.OrdinalIgnoreCase);

            if (request.SoundEnabled)
            {
                reasons.Add("sound_must_be_disabled_for_background_training");
            }

            if (realGameMode && requireLaunchPermission && !request.AllowGameLaunch)
            {
                reasons.Add("real_game_launch_requires_allow_game_launch_true");
            }

            if (realGameMode && string.IsNullOrWhiteSpace(manifest.GameExecutablePath))
            {
                reasons.Add("game_executable_path_required_for_real_game_mode");
            }

            if (realGameMode && string.IsNullOrWhiteSpace(manifest.GameWorkingDirectory))
            {
                reasons.Add("game_working_directory_required_for_real_game_mode");
            }

            if (realGameMode && !string.IsNullOrWhiteSpace(manifest.GameExecutablePath) && !File.Exists(manifest.GameExecutablePath))
            {
                reasons.Add("game_executable_path_not_found");
            }

            if (realGameMode &&
                !string.IsNullOrWhiteSpace(manifest.GameExecutablePath) &&
                !string.IsNullOrWhiteSpace(manifest.GameWorkingDirectory) &&
                !IsSameOrChildPath(manifest.GameWorkingDirectory, manifest.GameExecutablePath))
            {
                reasons.Add("game_executable_must_be_inside_training_working_directory");
            }

            if (realGameMode && manifest.ExecutableKind != "smapi")
            {
                reasons.Add("smapi_executable_required_for_transparent_bridge_training");
            }

            if (realGameMode && string.IsNullOrWhiteSpace(manifest.SaveIsolationPath))
            {
                reasons.Add("save_isolation_path_required_for_real_game_training");
            }

            return reasons;
        }

        private static Process StartTrainingProcess(TrainingRunManifest manifest)
        {
            Directory.CreateDirectory(manifest.SaveIsolationPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = manifest.GameExecutablePath,
                WorkingDirectory = manifest.GameWorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Minimized
            };

            foreach (var item in manifest.EnvironmentOverrides)
            {
                startInfo.Environment[item.Name] = item.Value;
            }

            var process = Process.Start(startInfo);
            if (process is null)
            {
                throw new InvalidOperationException("failed_to_start_training_game_process");
            }

            return process;
        }

        private static void WriteManifest(TrainingRunManifest manifest)
        {
            Directory.CreateDirectory(manifest.RootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.DatasetPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ReportPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.CheckpointPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ManifestPath)!);
            if (!string.IsNullOrWhiteSpace(manifest.SaveIsolationPath))
            {
                Directory.CreateDirectory(manifest.SaveIsolationPath);
            }

            File.WriteAllText(manifest.ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        }

        private static TrainingEnvironmentOverride[] BuildEnvironmentOverrides(TrainingLaunchRequest request)
        {
            return new[]
            {
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TRAINING_MODE", Value = "1" },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_BACKEND_URL", Value = request.BackendUrl },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_BRIDGE_URL", Value = request.BridgeUrl },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_SAVE_ISOLATION_PATH", Value = request.SaveIsolationPath ?? string.Empty },
                new TrainingEnvironmentOverride { Name = "SDL_AUDIODRIVER", Value = "dummy" },
                new TrainingEnvironmentOverride { Name = "ALSOFT_DRIVERS", Value = "null" }
            };
        }

        private static string ClassifyExecutable(string gameExecutablePath)
        {
            var fileName = Path.GetFileName(gameExecutablePath);
            if (string.Equals(fileName, "StardewModdingAPI.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "smapi";
            }

            if (string.Equals(fileName, "Stardew Valley.exe", StringComparison.OrdinalIgnoreCase))
            {
                return "vanilla";
            }

            return string.IsNullOrWhiteSpace(fileName) ? string.Empty : "unknown";
        }

        private static bool IsSameOrChildPath(string parentPath, string childPath)
        {
            var parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var child = Path.GetFullPath(childPath);
            return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
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
