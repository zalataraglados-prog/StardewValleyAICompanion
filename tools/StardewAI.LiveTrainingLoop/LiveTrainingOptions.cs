using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

public sealed class LiveTrainingOptions
{
    public string Root { get; set; } = @"E:\StardewAITraining";
    public string BackendUrl { get; set; } = "http://localhost:5108";
    public string BridgeSnapshotUrl { get; set; } = "http://127.0.0.1:8765/api/v1/snapshot";
    public string SnapshotFile { get; set; } = string.Empty;
    public string ExecutorUrl { get; set; } = "http://127.0.0.1:8767";
    public int ExecutorTimeoutSeconds { get; set; } = 600;
    public string ManifestPath { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-manifest-path.txt");
    public string RunId { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-run-id.txt");
    public string ArtifactRunId { get; set; } = string.Empty;
    public string SaveIsolationPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";
    public string Goal { get; set; } = StardewAI.Contracts.Goals.GrandpaEvaluationGoalDefinition.StrategicGoal;
    public int MaxAttempts { get; set; } = 3;
    public int RequiredVerifiedActions { get; set; }
    public int TrainEvery { get; set; } = 1;
    public bool SkipTraining { get; set; }
    public int SleepMs { get; set; } = 1000;
    public int NoProgressBackoffMs { get; set; }
    public int NoProgressMaxBackoffMs { get; set; }
    public int MaxCropsPerExecution { get; set; } = 16;
    public string ExecutorOptionId { get; set; } = string.Empty;
    public int? TargetTileX { get; set; }
    public int? TargetTileY { get; set; }
    public int? Direction { get; set; }
    public int? WaitTicks { get; set; }
    public string PlanStepKind { get; set; } = "move_to_tile";
    public int AfterSnapshotWaitMs { get; set; } = 2500;
    public int AfterSnapshotPollMs { get; set; } = 100;
    public bool RequireExecutorFeedback { get; set; } = true;
    public bool UseRuntimeTestHarnessExecutor { get; set; } = true;
    public bool UsePlanOutput { get; set; }
    public bool UseDailyPlan { get; set; }
    public bool UseParameterizedAction { get; set; }
    public string ActionOptionId { get; set; } = string.Empty;
    public List<SmallModelActionParameter> ActionParameters { get; } = new();
    public bool ContinueAfterBlockedQueueItems { get; set; }
    public int MaxQueueItemAttempts { get; set; } = 24;
    public int DailyPlanMaxCandidates { get; set; } = 4;
    public int MaxPersistedIterations { get; set; } = 64;
    public string ArtifactRetentionMode { get; set; } = "stop";
    public int MinFreeSpaceMb { get; set; } = 8192;
    public int MaxConsecutiveErrors { get; set; } = 5;
    public string[] DailyPlanCandidateOptionIds { get; set; } = Array.Empty<string>();
    public string KnowledgeDictionaryVersion { get; set; } = "game-1.6.15-20260723T093543Z-linux-v24";
    public List<SmallModelActionParameter> DailyPlanCandidateParameters { get; } = new();
    public string DailyPlanCandidateKind { get; set; } = string.Empty;
    public string DailyPlanCandidateId { get; set; } = string.Empty;
    public bool StopAfterSocialObjectiveComplete { get; set; }
    public string TargetExecutionMode { get; set; } = ExecutionTargetProfiles.TrainingSingleplayer;
    public ActionActorRef TargetActor => ExecutionTargetProfiles.CreateActor(TargetExecutionMode);
    public string FeedbackMode => RequireExecutorFeedback
        ? UseRuntimeTestHarnessExecutor ? "runtime_test_harness_executor" : "training_sandbox_feedback_gate"
        : "disabled";

    public string RunDir => string.IsNullOrWhiteSpace(ManifestPath)
        ? Path.Combine(Root, "runs", string.IsNullOrWhiteSpace(ArtifactRunId)
            ? string.IsNullOrWhiteSpace(RunId) ? "live-training" : RunId
            : ArtifactRunId)
        : !string.IsNullOrWhiteSpace(Path.GetDirectoryName(ManifestPath))
            ? Path.GetDirectoryName(ManifestPath)!
            : Path.Combine(Root, "runs", string.IsNullOrWhiteSpace(ArtifactRunId)
                ? string.IsNullOrWhiteSpace(RunId) ? "live-training" : RunId
                : ArtifactRunId);
    public string SnapshotDir => Path.Combine(RunDir, "live-snapshots");
    public string DatasetPath => Path.Combine(Root, "datasets", "live-training-feature-rows.jsonl");
    public string PolicyTrajectoryDatasetPath => Path.Combine(Root, "datasets", "policy-decision-trajectories.jsonl");
    public string ProgressLogPath => Path.Combine(Root, "logs", "live-training-progress.log");
    public string ReadyProbeUrl => BackendUrl + "/api/v1/training/session/ready-probe?manifest_path=" + Uri.EscapeDataString(ManifestPath);

    public static LiveTrainingOptions Parse(string[] args)
    {
        var options = new LiveTrainingOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (current == "--root" && i + 1 < args.Length)
            {
                options.Root = args[++i];
            }
            else if (current == "--backend-url" && i + 1 < args.Length)
            {
                options.BackendUrl = args[++i].TrimEnd('/');
            }
            else if (current == "--bridge-snapshot-url" && i + 1 < args.Length)
            {
                options.BridgeSnapshotUrl = args[++i];
            }
            else if (current == "--snapshot-file" && i + 1 < args.Length)
            {
                options.SnapshotFile = args[++i];
            }
            else if (current == "--executor-url" && i + 1 < args.Length)
            {
                options.ExecutorUrl = args[++i].TrimEnd('/');
            }
            else if (current == "--executor-timeout-seconds" &&
                i + 1 < args.Length &&
                int.TryParse(args[++i], out var executorTimeoutSeconds))
            {
                options.ExecutorTimeoutSeconds = Math.Clamp(
                    executorTimeoutSeconds,
                    30,
                    3600);
            }
            else if (current == "--save-isolation-path" && i + 1 < args.Length)
            {
                options.SaveIsolationPath = args[++i];
            }
            else if (current == "--target-execution-mode" && i + 1 < args.Length)
            {
                options.TargetExecutionMode = args[++i];
            }
            else if (current == "--manifest-path" && i + 1 < args.Length)
            {
                options.ManifestPath = args[++i];
            }
            else if (current == "--no-manifest")
            {
                options.ManifestPath = string.Empty;
            }
            else if (current == "--run-id" && i + 1 < args.Length)
            {
                options.RunId = args[++i];
            }
            else if (current == "--artifact-run-id" && i + 1 < args.Length)
            {
                options.ArtifactRunId = args[++i];
            }
            else if (current == "--goal" && i + 1 < args.Length)
            {
                options.Goal = args[++i];
            }
            else if (current == "--knowledge-dictionary-version" && i + 1 < args.Length)
            {
                options.KnowledgeDictionaryVersion = args[++i];
            }
            else if (current == "--iterations" && i + 1 < args.Length && int.TryParse(args[++i], out var iterations))
            {
                options.MaxAttempts = Math.Max(1, iterations);
            }
            else if (current == "--max-attempts" && i + 1 < args.Length && int.TryParse(args[++i], out var maxAttempts))
            {
                options.MaxAttempts = Math.Max(1, maxAttempts);
            }
            else if (current == "--required-verified-actions" && i + 1 < args.Length && int.TryParse(args[++i], out var requiredVerifiedActions))
            {
                options.RequiredVerifiedActions = Math.Max(0, requiredVerifiedActions);
            }
            else if (current == "--train-every" && i + 1 < args.Length && int.TryParse(args[++i], out var trainEvery))
            {
                options.TrainEvery = Math.Max(1, trainEvery);
            }
            else if (current == "--skip-training")
            {
                options.SkipTraining = true;
            }
            else if (current == "--sleep-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var sleepMs))
            {
                options.SleepMs = Math.Max(0, sleepMs);
            }
            else if (current == "--no-progress-backoff-ms" &&
                i + 1 < args.Length &&
                int.TryParse(
                    args[++i],
                    out var noProgressBackoffMs))
            {
                options.NoProgressBackoffMs = Math.Max(
                    0,
                    noProgressBackoffMs);
            }
            else if (current == "--no-progress-max-backoff-ms" &&
                i + 1 < args.Length &&
                int.TryParse(
                    args[++i],
                    out var noProgressMaxBackoffMs))
            {
                options.NoProgressMaxBackoffMs = Math.Max(
                    0,
                    noProgressMaxBackoffMs);
            }
            else if (current == "--max-crops" && i + 1 < args.Length && int.TryParse(args[++i], out var maxCrops))
            {
                options.MaxCropsPerExecution = Math.Max(1, maxCrops);
            }
            else if (current == "--executor-option-id" && i + 1 < args.Length)
            {
                options.ExecutorOptionId = args[++i];
            }
            else if (current == "--target-tile-x" && i + 1 < args.Length && int.TryParse(args[++i], out var targetTileX))
            {
                options.TargetTileX = targetTileX;
            }
            else if (current == "--target-tile-y" && i + 1 < args.Length && int.TryParse(args[++i], out var targetTileY))
            {
                options.TargetTileY = targetTileY;
            }
            else if (current == "--direction" && i + 1 < args.Length && int.TryParse(args[++i], out var direction))
            {
                options.Direction = direction;
            }
            else if (current == "--wait-ticks" && i + 1 < args.Length && int.TryParse(args[++i], out var waitTicks))
            {
                options.WaitTicks = waitTicks;
            }
            else if (current == "--after-snapshot-wait-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var afterSnapshotWaitMs))
            {
                options.AfterSnapshotWaitMs = Math.Max(0, afterSnapshotWaitMs);
            }
            else if (current == "--after-snapshot-poll-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var afterSnapshotPollMs))
            {
                options.AfterSnapshotPollMs = Math.Max(1, afterSnapshotPollMs);
            }
            else if (current == "--plan-step-kind" && i + 1 < args.Length)
            {
                options.PlanStepKind = args[++i];
            }
            else if (current == "--no-executor-feedback-required")
            {
                options.RequireExecutorFeedback = false;
            }
            else if (current == "--use-sandbox-executor")
            {
                options.UseRuntimeTestHarnessExecutor = false;
            }
            else if (current == "--use-plan-output")
            {
                options.UsePlanOutput = true;
            }
            else if (current == "--use-daily-plan")
            {
                options.UseDailyPlan = true;
            }
            else if (current == "--use-parameterized-action")
            {
                options.UseParameterizedAction = true;
            }
            else if (current == "--action-option-id" && i + 1 < args.Length)
            {
                options.ActionOptionId = args[++i];
            }
            else if (current == "--action-parameter" && i + 1 < args.Length)
            {
                var pair = args[++i];
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException("--action-parameter must be formatted as name=value.");
                }
                options.ActionParameters.Add(new SmallModelActionParameter
                {
                    Name = pair[..separator],
                    Value = pair[(separator + 1)..]
                });
            }
            else if (current == "--continue-after-blocked-queue-items")
            {
                options.ContinueAfterBlockedQueueItems = true;
            }
            else if (current == "--max-queue-item-attempts" && i + 1 < args.Length && int.TryParse(args[++i], out var maxQueueItemAttempts))
            {
                options.MaxQueueItemAttempts = Math.Max(1, maxQueueItemAttempts);
            }
            else if (current == "--artifact-retention-mode" && i + 1 < args.Length)
            {
                options.ArtifactRetentionMode = args[++i] switch
                {
                    "stop" => "stop",
                    "rolling" => "rolling",
                    var value => throw new ArgumentException(
                        "--artifact-retention-mode must be stop or rolling, not '" + value + "'.")
                };
            }
            else if (current == "--daily-plan-max-candidates" && i + 1 < args.Length && int.TryParse(args[++i], out var dailyPlanMaxCandidates))
            {
                options.DailyPlanMaxCandidates = Math.Max(1, dailyPlanMaxCandidates);
            }
            else if (current == "--max-persisted-iterations" && i + 1 < args.Length && int.TryParse(args[++i], out var maxPersistedIterations))
            {
                options.MaxPersistedIterations = Math.Max(1, maxPersistedIterations);
            }
            else if (current == "--min-free-space-mb" && i + 1 < args.Length && int.TryParse(args[++i], out var minFreeSpaceMb))
            {
                options.MinFreeSpaceMb = Math.Max(1, minFreeSpaceMb);
            }
            else if (current == "--max-consecutive-errors" && i + 1 < args.Length && int.TryParse(args[++i], out var maxConsecutiveErrors))
            {
                options.MaxConsecutiveErrors = Math.Max(1, maxConsecutiveErrors);
            }
            else if (current == "--daily-plan-candidate-options" && i + 1 < args.Length)
            {
                options.DailyPlanCandidateOptionIds = args[++i]
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();
            }
            else if (current == "--daily-plan-candidate-parameter" && i + 1 < args.Length)
            {
                var pair = args[++i];
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    throw new ArgumentException(
                        "--daily-plan-candidate-parameter must be formatted as name=value.");
                }
                options.DailyPlanCandidateParameters.Add(new SmallModelActionParameter
                {
                    Name = pair[..separator],
                    Value = pair[(separator + 1)..]
                });
            }
            else if (current == "--daily-plan-candidate-kind" &&
                i + 1 < args.Length)
            {
                options.DailyPlanCandidateKind = args[++i].Trim();
            }
            else if (current == "--daily-plan-candidate-id" &&
                i + 1 < args.Length)
            {
                options.DailyPlanCandidateId = args[++i].Trim();
            }
            else if (current == "--stop-after-social-objective-complete")
            {
                options.StopAfterSocialObjectiveComplete = true;
            }
        }

        if (string.IsNullOrWhiteSpace(options.RunId))
        {
            options.RunId = "live." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        }

        if (options.NoProgressBackoffMs > 0)
        {
            options.NoProgressMaxBackoffMs = Math.Max(
                options.NoProgressBackoffMs,
                options.NoProgressMaxBackoffMs);
        }

        if (!ExecutionTargetProfiles.IsSupported(options.TargetExecutionMode))
        {
            throw new ArgumentException("Unsupported --target-execution-mode: " + options.TargetExecutionMode);
        }

        if (options.DailyPlanCandidateParameters.Count > 0 &&
            options.DailyPlanCandidateOptionIds.Length != 1)
        {
            throw new ArgumentException(
                "Explicit daily-plan candidate parameters require exactly one " +
                "--daily-plan-candidate-options value.");
        }

        return options;
    }

    private static string ReadTextOrEmpty(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }
}
