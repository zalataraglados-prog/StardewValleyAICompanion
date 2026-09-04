using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StardewAI.Contracts.Execution;
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
            var formalTraining = string.Equals(
                request.Mode,
                TrainingSessionMode.FormalProductTraining,
                StringComparison.OrdinalIgnoreCase);
            var loadReasons = new List<string>();
            var manifest = formalTraining
                ? LoadPreparedManifest(request, rootPath, loadReasons)
                : BuildManifest(request, rootPath);
            var blockReasons = Validate(request, manifest, requireLaunchPermission: true);
            blockReasons.InsertRange(0, loadReasons);
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

            Process? productExecutorProcess = null;
            Process? gameProcess = null;
            Process? liveTrainingLoopProcess = null;
            var ownsGameProcess = !string.Equals(
                manifest.GameProcessOwnership,
                "external",
                StringComparison.Ordinal);
            try
            {
                if (formalTraining)
                {
                    productExecutorProcess = StartProductExecutorProcess(manifest);
                    manifest.ProductExecutorProcessId = productExecutorProcess.Id;
                    WaitForProductExecutor(manifest, productExecutorProcess, TimeSpan.FromSeconds(60));
                }
                gameProcess = ownsGameProcess
                    ? StartTrainingProcess(manifest)
                    : AttachTrainingProcess(manifest);
                manifest.ProcessId = gameProcess.Id;
                if (formalTraining)
                {
                    WaitForTrainingWorld(manifest, gameProcess, TimeSpan.FromSeconds(180));
                    liveTrainingLoopProcess = StartLiveTrainingLoopProcess(manifest);
                    manifest.LiveTrainingLoopProcessId = liveTrainingLoopProcess.Id;
                }
                manifest.Status = "running";
                manifest.GameLaunch = "started";
                WriteManifest(manifest);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is IOException)
            {
                StopStartedProcess(liveTrainingLoopProcess);
                if (ownsGameProcess)
                    StopStartedProcess(gameProcess);
                else
                    gameProcess?.Dispose();
                StopStartedProcess(productExecutorProcess);
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

        private static TrainingRunManifest LoadPreparedManifest(
            TrainingLaunchRequest request,
            string rootPath,
            List<string> reasons)
        {
            if (string.IsNullOrWhiteSpace(request.ManifestPath))
            {
                reasons.Add("formal_launch_requires_prepared_manifest");
                return BuildManifest(request, rootPath);
            }

            var path = Path.GetFullPath(request.ManifestPath);
            if (!File.Exists(path))
            {
                reasons.Add("formal_prepared_manifest_not_found");
                return BuildManifest(request, rootPath);
            }

            try
            {
                var manifest = JsonSerializer.Deserialize<TrainingRunManifest>(File.ReadAllText(path), JsonOptions);
                if (manifest is null ||
                    !string.Equals(manifest.SchemaVersion, "training_run_manifest.v2", StringComparison.Ordinal) ||
                    !string.Equals(manifest.Mode, TrainingSessionMode.FormalProductTraining, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(manifest.Status, "prepared", StringComparison.Ordinal) ||
                    !string.Equals(Path.GetFullPath(manifest.RootPath), rootPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(Path.GetFullPath(manifest.ManifestPath), path, StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("formal_prepared_manifest_identity_mismatch");
                    return BuildManifest(request, rootPath);
                }
                return manifest;
            }
            catch (Exception ex) when (ex is IOException or JsonException or ArgumentException)
            {
                reasons.Add("formal_prepared_manifest_invalid");
                return BuildManifest(request, rootPath);
            }
        }

        private static TrainingRunManifest BuildManifest(TrainingLaunchRequest request, string rootPath)
        {
            var runId = string.IsNullOrWhiteSpace(request.RunId)
                ? "train." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "." + Guid.NewGuid().ToString("N").Substring(0, 8)
                : request.RunId.Trim();
            var mode = string.IsNullOrWhiteSpace(request.Mode) ? TrainingSessionMode.OfflineSmoke : request.Mode.Trim();
            var formalTraining = string.Equals(mode, TrainingSessionMode.FormalProductTraining, StringComparison.OrdinalIgnoreCase);
            var manifestPath = FullPathOrDefault(request.ManifestPath, Path.Combine(rootPath, "runs", runId, "training-run-manifest.json"));
            var gameExecutablePath = FullPathOrEmpty(request.GameExecutablePath);
            var gameWorkingDirectory = FullPathOrDefault(request.GameWorkingDirectory, string.IsNullOrWhiteSpace(gameExecutablePath)
                ? rootPath
                : Path.GetDirectoryName(gameExecutablePath)!);
            var checkpointPath = FullPathOrDefault(
                request.CheckpointPath,
                Path.Combine(rootPath, "checkpoints", formalTraining ? "structured-policy-latest.json" : "baseline-latest.json"));
            var policyDatasetManifestPath = FullPathOrDefault(
                request.PolicyDatasetManifestPath,
                Path.Combine(rootPath, "datasets", "formal-policy", "policy-dataset-manifest.json"));

            return new TrainingRunManifest
            {
                RunId = runId,
                Mode = mode,
                RootPath = rootPath,
                DatasetPath = FullPathOrDefault(request.DatasetPath, Path.Combine(rootPath, "datasets", "training-feature-rows.jsonl")),
                ReportPath = FullPathOrDefault(request.ReportPath, Path.Combine(rootPath, "reports", "training-report.json")),
                CheckpointPath = checkpointPath,
                CheckpointSha256 = HashExistingFile(checkpointPath),
                PolicyTrajectoryPath = FullPathOrDefault(
                    request.PolicyTrajectoryPath,
                    Path.Combine(rootPath, "datasets", "policy-decision-trajectories.jsonl")),
                PolicyDatasetManifestPath = policyDatasetManifestPath,
                PolicyDatasetManifestSha256 = HashExistingFile(policyDatasetManifestPath),
                ProductReceiptRoot = FullPathOrDefault(
                    request.ProductReceiptRoot,
                    Path.Combine(rootPath, "product-executor", runId)),
                ProductExecutorUrl = request.ProductExecutorUrl,
                NativeExecutorUrl = request.NativeExecutorUrl,
                ProductExecutorExecutablePath = FullPathOrEmpty(request.ProductExecutorExecutablePath),
                LiveTrainingLoopExecutablePath = FullPathOrEmpty(request.LiveTrainingLoopExecutablePath),
                MaxAttempts = Math.Max(1, request.MaxAttempts),
                MaxPersistedIterations = Math.Clamp(
                    request.MaxPersistedIterations,
                    1,
                    64),
                RequiredVerifiedActions = Math.Max(0, request.RequiredVerifiedActions),
                RequireNativeSaveBoundary = formalTraining && request.RequireNativeSaveBoundary,
                SaveBoundaryMaxAttempts = Math.Clamp(
                    request.SaveBoundaryMaxAttempts,
                    1,
                    128),
                MinFreeSpaceMb = Math.Clamp(
                    request.MinFreeSpaceMb,
                    1024,
                    1024 * 1024),
                TargetExecutionMode = string.IsNullOrWhiteSpace(request.TargetExecutionMode)
                    ? ExecutionTargetProfiles.TrainingSingleplayer
                    : request.TargetExecutionMode.Trim(),
                CompilerVersion = formalTraining ? PolicyTrajectoryVersionPins.Compiler : string.Empty,
                ExecutorVersion = formalTraining ? PolicyTrajectoryVersionPins.ProductExecutor : string.Empty,
                StructuredPolicyRequired = formalTraining,
                ManifestPath = manifestPath,
                GameExecutablePath = gameExecutablePath,
                GameWorkingDirectory = gameWorkingDirectory,
                SaveIsolationPath = FullPathOrEmpty(request.SaveIsolationPath),
                SaveSlot = request.SaveSlot?.Trim() ?? string.Empty,
                BridgeUrl = request.BridgeUrl,
                BackendUrl = request.BackendUrl,
                GameLaunch = request.AttachExistingGame
                    ? "attach_requested"
                    : request.AllowGameLaunch ? "requested" : "disabled",
                GameProcessOwnership = request.AttachExistingGame ? "external" : "launcher",
                ProcessId = request.AttachExistingGame ? request.ExistingGameProcessId : null,
                Sound = request.SoundEnabled ? "enabled" : "disabled",
                WindowStyle = string.IsNullOrWhiteSpace(request.WindowStyle) ? "minimized" : request.WindowStyle.Trim(),
                ExecutableKind = ClassifyExecutable(gameExecutablePath),
                EnvironmentOverrides = BuildEnvironmentOverrides(request, runId),
                CreatedAt = DateTimeOffset.UtcNow.ToString("O")
            };
        }

        private static List<string> Validate(TrainingLaunchRequest request, TrainingRunManifest manifest, bool requireLaunchPermission)
        {
            var reasons = new List<string>();
            var formalTraining = string.Equals(manifest.Mode, TrainingSessionMode.FormalProductTraining, StringComparison.OrdinalIgnoreCase);
            var realGameMode = formalTraining ||
                string.Equals(manifest.Mode, TrainingSessionMode.StardewWindowed, StringComparison.OrdinalIgnoreCase);

            if (!IsKnownMode(manifest.Mode))
            {
                reasons.Add("training_mode_unsupported");
            }

            if (!ExecutionTargetProfiles.IsSupported(manifest.TargetExecutionMode))
            {
                reasons.Add("training_target_execution_mode_unsupported");
            }

            if (!IsValidRunId(manifest.RunId))
            {
                reasons.Add("training_run_id_invalid");
            }

            if (!string.IsNullOrWhiteSpace(request.RunId) &&
                !string.Equals(request.RunId.Trim(), manifest.RunId, StringComparison.Ordinal))
            {
                reasons.Add("training_run_id_does_not_match_prepared_manifest");
            }

            if (request.SoundEnabled)
            {
                reasons.Add("sound_must_be_disabled_for_background_training");
            }

            var attachExistingGame = string.Equals(
                manifest.GameProcessOwnership,
                "external",
                StringComparison.Ordinal);

            if (attachExistingGame && !formalTraining)
            {
                reasons.Add("existing_game_attachment_requires_formal_training");
            }

            if (attachExistingGame && request.AllowGameLaunch)
            {
                reasons.Add("existing_game_attachment_disallows_game_launch");
            }

            if (realGameMode && requireLaunchPermission && !attachExistingGame && !request.AllowGameLaunch)
            {
                reasons.Add("real_game_launch_requires_allow_game_launch_true");
            }

            if (attachExistingGame)
            {
                ValidateAttachedGameProcess(manifest, reasons);
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

            if (formalTraining)
            {
                ValidateFormalSaveSlot(manifest, reasons);
            }

            if (formalTraining)
            {
                ValidateFormalBoundary(manifest, reasons);
            }

            return reasons;
        }

        private static void ValidateFormalSaveSlot(TrainingRunManifest manifest, List<string> reasons)
        {
            if (string.IsNullOrWhiteSpace(manifest.SaveSlot))
            {
                reasons.Add("formal_save_slot_required");
                return;
            }

            if (manifest.SaveSlot.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) >= 0 ||
                string.Equals(manifest.SaveSlot, ".", StringComparison.Ordinal) ||
                string.Equals(manifest.SaveSlot, "..", StringComparison.Ordinal))
            {
                reasons.Add("formal_save_slot_invalid");
                return;
            }

            var slotDirectory = Path.Combine(manifest.SaveIsolationPath, manifest.SaveSlot);
            var saveFile = Path.Combine(slotDirectory, manifest.SaveSlot);
            if (!IsSameOrChildPath(manifest.SaveIsolationPath, slotDirectory) || !File.Exists(saveFile))
            {
                reasons.Add("formal_save_slot_not_found");
            }
        }

        private static void ValidateFormalBoundary(TrainingRunManifest manifest, List<string> reasons)
        {
            if (!string.Equals(manifest.WindowStyle, "hidden", StringComparison.OrdinalIgnoreCase))
                reasons.Add("formal_training_window_style_must_be_hidden");
            if (!IsLoopbackHttpUrl(manifest.ProductExecutorUrl))
                reasons.Add("formal_product_executor_url_must_be_loopback");
            if (!IsLoopbackHttpUrl(manifest.NativeExecutorUrl))
                reasons.Add("formal_native_executor_url_must_be_loopback");
            if (string.IsNullOrWhiteSpace(manifest.ProductExecutorExecutablePath) ||
                !File.Exists(manifest.ProductExecutorExecutablePath))
                reasons.Add("formal_product_executor_executable_not_found");
            if (string.IsNullOrWhiteSpace(manifest.LiveTrainingLoopExecutablePath) ||
                !File.Exists(manifest.LiveTrainingLoopExecutablePath))
                reasons.Add("formal_live_training_loop_executable_not_found");

            foreach (var path in new[]
                     {
                         manifest.DatasetPath,
                         manifest.ReportPath,
                         manifest.CheckpointPath,
                         manifest.PolicyTrajectoryPath,
                         manifest.PolicyDatasetManifestPath,
                         manifest.ProductReceiptRoot,
                         manifest.ManifestPath
                     })
            {
                if (string.IsNullOrWhiteSpace(path) || !IsSameOrChildPath(manifest.RootPath, path))
                {
                    reasons.Add("formal_artifact_path_outside_training_root");
                    break;
                }
            }

            if (!File.Exists(manifest.PolicyTrajectoryPath))
                reasons.Add("formal_policy_trajectory_not_found");
            if (!File.Exists(manifest.PolicyDatasetManifestPath))
                reasons.Add("formal_policy_dataset_manifest_not_found");
            if (!File.Exists(manifest.CheckpointPath))
                reasons.Add("formal_structured_policy_checkpoint_not_found");

            if (File.Exists(manifest.PolicyDatasetManifestPath) &&
                !string.Equals(
                    HashExistingFile(manifest.PolicyDatasetManifestPath),
                    manifest.PolicyDatasetManifestSha256,
                    StringComparison.OrdinalIgnoreCase))
                reasons.Add("formal_policy_dataset_manifest_changed_after_prepare");
            if (File.Exists(manifest.CheckpointPath) &&
                !string.Equals(
                    HashExistingFile(manifest.CheckpointPath),
                    manifest.CheckpointSha256,
                    StringComparison.OrdinalIgnoreCase))
                reasons.Add("formal_structured_policy_checkpoint_changed_after_prepare");

            if (!File.Exists(manifest.CheckpointPath) || !File.Exists(manifest.PolicyDatasetManifestPath))
                return;

            try
            {
                var checkpoint = new StructuredPolicyCheckpointStore().Load(manifest.CheckpointPath);
                if (!string.Equals(
                        Path.GetFullPath(checkpoint.Dataset.ManifestPath),
                        Path.GetFullPath(manifest.PolicyDatasetManifestPath),
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        checkpoint.Dataset.ManifestSha256,
                        manifest.PolicyDatasetManifestSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("formal_checkpoint_dataset_binding_mismatch");
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
            {
                reasons.Add("formal_structured_policy_checkpoint_invalid");
            }
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
                WindowStyle = ResolveWindowStyle(manifest.WindowStyle)
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

        private static Process AttachTrainingProcess(TrainingRunManifest manifest)
        {
            if (!manifest.ProcessId.HasValue || manifest.ProcessId.Value <= 0)
                throw new InvalidOperationException("attached_game_process_id_required");
            try
            {
                var process = Process.GetProcessById(manifest.ProcessId.Value);
                if (process.HasExited)
                {
                    process.Dispose();
                    throw new InvalidOperationException("attached_game_process_not_alive");
                }
                return process;
            }
            catch (ArgumentException)
            {
                throw new InvalidOperationException("attached_game_process_not_alive");
            }
        }

        private static Process StartProductExecutorProcess(TrainingRunManifest manifest)
        {
            var startInfo = CreateToolStartInfo(manifest.ProductExecutorExecutablePath);
            startInfo.Environment["STARDEWAI_PRODUCT_EXECUTOR_URL"] = manifest.ProductExecutorUrl;
            startInfo.Environment["STARDEWAI_NATIVE_EXECUTOR_URL"] = manifest.NativeExecutorUrl;
            startInfo.Environment["STARDEWAI_BRIDGE_SNAPSHOT_URL"] = SnapshotUrl(manifest.BridgeUrl);
            startInfo.Environment["STARDEWAI_PRODUCT_JOURNAL_ROOT"] = manifest.ProductReceiptRoot;
            startInfo.Environment["STARDEWAI_PRODUCT_ALLOWED_SAVE_ROOT"] = manifest.SaveIsolationPath;
            startInfo.Environment["STARDEWAI_PRODUCT_RUN_ID"] = manifest.RunId;
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("failed_to_start_product_executor_process");
        }

        private static Process StartLiveTrainingLoopProcess(TrainingRunManifest manifest)
        {
            var startInfo = CreateToolStartInfo(manifest.LiveTrainingLoopExecutablePath);
            AddArguments(
                startInfo,
                "--root", manifest.RootPath,
                "--backend-url", manifest.BackendUrl,
                "--bridge-snapshot-url", SnapshotUrl(manifest.BridgeUrl),
                "--executor-url", manifest.ProductExecutorUrl,
                "--manifest-path", manifest.ManifestPath,
                "--run-id", manifest.RunId,
                "--save-isolation-path", manifest.SaveIsolationPath,
                "--max-attempts", manifest.MaxAttempts.ToString(),
                "--required-verified-actions", manifest.RequiredVerifiedActions.ToString(),
                "--save-slot", manifest.SaveSlot,
                "--save-boundary-max-attempts", manifest.SaveBoundaryMaxAttempts.ToString(),
                "--min-free-space-mb", Math.Max(
                    1024,
                    manifest.MinFreeSpaceMb).ToString(),
                "--target-execution-mode", manifest.TargetExecutionMode,
                "--policy-checkpoint-path", manifest.CheckpointPath,
                "--artifact-retention-mode", "rolling",
                "--max-persisted-iterations", manifest.MaxPersistedIterations.ToString(),
                "--continue-after-blocked-queue-items",
                "--no-progress-backoff-ms", "5000",
                "--no-progress-max-backoff-ms", "60000",
                "--use-product-executor",
                "--use-daily-plan",
                "--require-structured-policy");
            if (manifest.RequireNativeSaveBoundary)
            {
                startInfo.ArgumentList.Add("--require-native-save-boundary");
            }
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException("failed_to_start_live_training_loop_process");
        }

        private static void WaitForProductExecutor(
            TrainingRunManifest manifest,
            Process process,
            TimeSpan timeout)
        {
            WaitForHttpJson(
                manifest.ProductExecutorUrl.TrimEnd('/') + "/health",
                process,
                timeout,
                document =>
                {
                    var root = document.RootElement;
                    return root.TryGetProperty("status", out var status) &&
                        string.Equals(status.GetString(), "ready", StringComparison.Ordinal) &&
                        root.TryGetProperty("product_executor_count", out var count) &&
                        count.TryGetInt32(out var value) && value > 0;
                },
                "product_executor_startup_probe_failed");
        }

        private static void WaitForTrainingWorld(
            TrainingRunManifest manifest,
            Process process,
            TimeSpan timeout)
        {
            WaitForHttpJson(
                SnapshotUrl(manifest.BridgeUrl),
                process,
                timeout,
                document => SnapshotMatchesTrainingRun(document.RootElement, manifest.RunId),
                "training_world_startup_probe_failed");
        }

        private static void WaitForHttpJson(
            string url,
            Process process,
            TimeSpan timeout,
            Func<JsonDocument, bool> accept,
            string failureCode)
        {
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            string lastError = "not_ready";
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (process.HasExited)
                    throw new InvalidOperationException(failureCode + ": process_exited");
                try
                {
                    var json = client.GetStringAsync(url).GetAwaiter().GetResult();
                    using var document = JsonDocument.Parse(json);
                    if (accept(document))
                        return;
                    lastError = "response_not_ready";
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
                {
                    lastError = ex.GetType().Name;
                }
                Thread.Sleep(500);
            }
            throw new InvalidOperationException(failureCode + ": " + lastError);
        }

        private static bool SnapshotMatchesTrainingRun(JsonElement root, string runId)
        {
            if (!root.TryGetProperty("state", out var state) ||
                !state.TryGetProperty("environment", out var environment) ||
                !environment.TryGetProperty("training_mode", out var trainingMode) ||
                !trainingMode.TryGetProperty("value", out var trainingModeValue) ||
                !string.Equals(trainingModeValue.GetString(), "1", StringComparison.Ordinal) ||
                !environment.TryGetProperty("training_run_id", out var trainingRunId) ||
                !trainingRunId.TryGetProperty("value", out var trainingRunIdValue) ||
                !string.Equals(trainingRunIdValue.GetString(), runId, StringComparison.Ordinal) ||
                !state.TryGetProperty("identity", out var identity) ||
                !identity.TryGetProperty("save_id", out var saveId) ||
                !saveId.TryGetProperty("value", out var saveIdValue))
                return false;
            return !string.IsNullOrWhiteSpace(saveIdValue.GetString());
        }

        private static ProcessStartInfo CreateToolStartInfo(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new InvalidOperationException("formal_training_tool_executable_not_found");
            var fullPath = Path.GetFullPath(executablePath);
            var managedAssembly = string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase);
            var startInfo = new ProcessStartInfo
            {
                FileName = managedAssembly ? "dotnet" : fullPath,
                WorkingDirectory = Path.GetDirectoryName(fullPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            if (managedAssembly)
                startInfo.ArgumentList.Add(fullPath);
            return startInfo;
        }

        private static void AddArguments(ProcessStartInfo startInfo, params string[] arguments)
        {
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
        }

        private static string SnapshotUrl(string bridgeUrl)
        {
            var value = bridgeUrl.TrimEnd('/');
            return value.IndexOf("/api/v1/snapshot", StringComparison.OrdinalIgnoreCase) >= 0
                ? value
                : value + "/api/v1/snapshot?profile=full";
        }

        private static void StopStartedProcess(Process? process)
        {
            if (process is null)
                return;
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                process.Dispose();
            }
        }

        private static ProcessWindowStyle ResolveWindowStyle(string? value)
        {
            return string.Equals(value, "hidden", StringComparison.OrdinalIgnoreCase)
                ? ProcessWindowStyle.Hidden
                : ProcessWindowStyle.Minimized;
        }

        private static void WriteManifest(TrainingRunManifest manifest)
        {
            Directory.CreateDirectory(manifest.RootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.DatasetPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ReportPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.CheckpointPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.PolicyTrajectoryPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.PolicyDatasetManifestPath)!);
            Directory.CreateDirectory(manifest.ProductReceiptRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(manifest.ManifestPath)!);
            if (!string.IsNullOrWhiteSpace(manifest.SaveIsolationPath))
            {
                Directory.CreateDirectory(manifest.SaveIsolationPath);
            }

            File.WriteAllText(manifest.ManifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
        }

        private static TrainingEnvironmentOverride[] BuildEnvironmentOverrides(TrainingLaunchRequest request, string runId)
        {
            return new[]
            {
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TRAINING_MODE", Value = "1" },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TRAINING_RUN_ID", Value = runId },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_BACKEND_URL", Value = request.BackendUrl },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_BRIDGE_URL", Value = request.BridgeUrl },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_SAVE_ISOLATION_PATH", Value = request.SaveIsolationPath ?? string.Empty },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TEST_SAVES", Value = request.SaveIsolationPath ?? string.Empty },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TEST_SLOT", Value = request.SaveSlot?.Trim() ?? string.Empty },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_TEST_AUTO_LOAD", Value = string.IsNullOrWhiteSpace(request.SaveSlot) ? "false" : "true" },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_PRODUCT_EXECUTOR_URL", Value = request.ProductExecutorUrl },
                new TrainingEnvironmentOverride { Name = "STARDEWAI_NATIVE_EXECUTOR_URL", Value = request.NativeExecutorUrl },
                new TrainingEnvironmentOverride { Name = "SDL_AUDIODRIVER", Value = "dummy" },
                new TrainingEnvironmentOverride { Name = "ALSOFT_DRIVERS", Value = "null" }
            };
        }

        private static string ClassifyExecutable(string gameExecutablePath)
        {
            var fileName = Path.GetFileName(gameExecutablePath);
            if (string.Equals(fileName, "StardewModdingAPI.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "StardewModdingAPI", StringComparison.OrdinalIgnoreCase))
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

        private static bool IsKnownMode(string mode) =>
            string.Equals(mode, TrainingSessionMode.OfflineSmoke, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TrainingSessionMode.SimulatedTransition, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TrainingSessionMode.StardewWindowed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, TrainingSessionMode.FormalProductTraining, StringComparison.OrdinalIgnoreCase);

        private static void ValidateAttachedGameProcess(
            TrainingRunManifest manifest,
            List<string> reasons)
        {
            if (!manifest.ProcessId.HasValue || manifest.ProcessId.Value <= 0)
            {
                reasons.Add("attached_game_process_id_required");
                return;
            }

            try
            {
                using var process = Process.GetProcessById(manifest.ProcessId.Value);
                if (process.HasExited)
                {
                    reasons.Add("attached_game_process_not_alive");
                    return;
                }

                var actualExecutablePath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(actualExecutablePath) ||
                    !string.Equals(
                        Path.GetFullPath(actualExecutablePath),
                        Path.GetFullPath(manifest.GameExecutablePath),
                        StringComparison.OrdinalIgnoreCase))
                {
                    reasons.Add("attached_game_process_executable_mismatch");
                }
            }
            catch (Exception ex) when (
                ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                reasons.Add("attached_game_process_not_alive");
            }
        }

        private static bool IsValidRunId(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId) || runId.Length > 128)
                return false;
            foreach (var value in runId)
            {
                if (!(char.IsLetterOrDigit(value) || value is '.' or '-' or '_'))
                    return false;
            }
            return true;
        }

        private static bool IsLoopbackHttpUrl(string value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https" &&
            uri.IsLoopback;

        private static string HashExistingFile(string path)
        {
            if (!File.Exists(path))
                return string.Empty;
            try
            {
                return StructuredPolicyCheckpointStore.HashFile(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return string.Empty;
            }
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
