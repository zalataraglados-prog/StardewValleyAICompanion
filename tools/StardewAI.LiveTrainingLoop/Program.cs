using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

var options = LiveTrainingOptions.Parse(args);
Directory.CreateDirectory(options.Root);
Directory.CreateDirectory(options.RunDir);
Directory.CreateDirectory(options.SnapshotDir);
Directory.CreateDirectory(Path.GetDirectoryName(options.DatasetPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.ProgressLogPath)!);

using var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};

AppendProgress(options, "start", 0, string.Empty, string.Empty, "concurrency=1 execution=" + options.ExecutionMode);

var rowsAppended = 0;
var lastQueueId = string.Empty;
var lastStateHash = string.Empty;
JsonObject? lastTrainingReport = null;
JsonObject? lastPrediction = null;

for (var iteration = 1; iteration <= options.Iterations; iteration++)
{
    var snapshotJson = await http.GetStringAsync(options.BridgeSnapshotUrl);
    var snapshotPath = Path.Combine(options.SnapshotDir, "before-snapshot-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(snapshotPath, snapshotJson, Encoding.UTF8);
    var beforeSnapshot = JsonNode.Parse(snapshotJson)?.AsObject() ?? new JsonObject();

    var ingest = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/snapshots", snapshotJson);
    lastStateHash = ReadString(ingest, "state_hash");

    var ready = await http.GetFromJsonAsync<JsonObject>(options.ReadyProbeUrl);
    if (ready is null || ready["ready"]?.GetValue<bool>() != true)
    {
        AppendProgress(options, "blocked", iteration, lastStateHash, string.Empty, "ready_probe_failed");
        continue;
    }

    var mockRequest = JsonSerializer.Serialize(new
    {
        goal = options.Goal,
        state_hash = lastStateHash,
        execution_mode = "training_singleplayer"
    }, JsonOptions);
    var modelOutput = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/mock-model/small-model-action", mockRequest);
    var queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile", modelOutput.ToJsonString(JsonOptions));
    lastQueueId = ReadString(queue, "queue_id");
    var queueStatus = ReadString(queue, "status");
    if (options.RequireExecutorFeedback)
    {
        if (!string.Equals(queueStatus, "pending", StringComparison.Ordinal))
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "queue_not_pending executor_feedback_required");
            continue;
        }

        var execution = options.UseRealRuntimeExecutor
            ? await ExecuteRealRuntimeAsync(http, options, iteration, beforeSnapshot, queue, lastStateHash, lastQueueId)
            : await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/execute-training-sandbox", "{}");
        var feedbackAvailable = execution["feedback_available"]?.GetValue<bool>() == true;
        if (!feedbackAvailable)
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "executor_feedback_unavailable status=" + ReadString(execution, "status"));
            continue;
        }

        if (options.UseRealRuntimeExecutor)
        {
            var realAppend = AppendRealExecutionRow(options, beforeSnapshot, queue, execution, lastStateHash, lastQueueId);
            rowsAppended = realAppend.RowCount;
            AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended + " source=real_runtime_executor");
            var train = await TrainIfNeededAsync(http, options, iteration);
            if (train.TrainingReport is not null)
            {
                lastTrainingReport = train.TrainingReport;
                lastPrediction = train.Prediction;
            }
            continue;
        }
    }

    var appendRequest = JsonSerializer.Serialize(new
    {
        dataset_path = options.DatasetPath
    }, JsonOptions);
    var append = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/training-feature-row/append", appendRequest);
    rowsAppended = append["row_count"]?.GetValue<int>() ?? rowsAppended;
    AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended);

    if (iteration % options.TrainEvery == 0 || iteration == options.Iterations)
    {
        var trainRequest = JsonSerializer.Serialize(new
        {
            dataset_path = options.DatasetPath
        }, JsonOptions);
        lastTrainingReport = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
        lastPrediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
        var bestOption = lastPrediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
        AppendProgress(options, "train", iteration, lastStateHash, lastQueueId, "best_option=" + bestOption);
    }

    if (options.SleepMs > 0 && iteration < options.Iterations)
    {
        await Task.Delay(options.SleepMs);
    }
}

var report = new LiveTrainingLoopReport
{
    RunId = options.RunId,
    ManifestPath = options.ManifestPath,
    BackendUrl = options.BackendUrl,
    BridgeSnapshotUrl = options.BridgeSnapshotUrl,
    DatasetPath = options.DatasetPath,
    ProgressLogPath = options.ProgressLogPath,
    SnapshotDir = options.SnapshotDir,
    Iterations = options.Iterations,
    RowsAppended = rowsAppended,
    LastStateHash = lastStateHash,
    LastQueueId = lastQueueId,
    Concurrency = 1,
    Execution = options.ExecutionMode,
    ExecutorFeedbackRequired = options.RequireExecutorFeedback,
    TrainingReport = lastTrainingReport,
    Prediction = lastPrediction
};

var reportPath = Path.Combine(options.RunDir, "live-training-loop-report.json");
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
AppendProgress(options, "complete", options.Iterations, lastStateHash, lastQueueId, "report=" + reportPath);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ok",
    run_id = options.RunId,
    iterations = options.Iterations,
    rows_appended = rowsAppended,
    dataset_path = options.DatasetPath,
    report_path = reportPath,
    progress_log_path = options.ProgressLogPath,
    concurrency = 1,
    execution = options.ExecutionMode,
    executor_feedback_required = options.RequireExecutorFeedback
}, JsonOptions));

static async Task<JsonObject> ExecuteRealRuntimeAsync(
    HttpClient http,
    LiveTrainingOptions options,
    int iteration,
    JsonObject beforeSnapshot,
    JsonObject queue,
    string stateHash,
    string queueId)
{
    var item = queue["items"]?.AsArray().FirstOrDefault()?.AsObject();
    var optionId = ReadStringOrEmpty(item, "option_id");
    var queueItemId = ReadStringOrEmpty(item, "queue_item_id");
    if (string.IsNullOrWhiteSpace(queueItemId))
    {
        throw new InvalidOperationException("compiled queue did not include queue_item_id");
    }

    var request = JsonSerializer.Serialize(new TrainingExecutionRequest
    {
        RunId = options.RunId,
        QueueId = queueId,
        QueueItemId = queueItemId,
        BeforeStateHash = stateHash,
        OptionId = optionId,
        ExecutionMode = "training_singleplayer",
        Actor = "training_farmer.main",
        SaveIsolationPath = options.SaveIsolationPath,
        RequestNonce = Guid.NewGuid().ToString("N"),
        CreatedAt = DateTimeOffset.UtcNow.ToString("O"),
        MaxCrops = options.MaxCropsPerExecution
    }, JsonOptions);

    var execution = await PostJsonStringAsync(http, options.ExecutorUrl + "/api/v1/training/execute", request);
    var afterJson = await http.GetStringAsync(options.BridgeSnapshotUrl);
    var afterPath = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(afterPath, afterJson, Encoding.UTF8);
    await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/snapshots", afterJson);
    execution["after_snapshot_path"] = afterPath;
    execution["after_state_hash"] = JsonNode.Parse(afterJson)?["state_hash"]?.GetValue<string>() ?? string.Empty;
    execution["source"] = "real_runtime_executor";
    return execution;
}

static TrainingDatasetAppendResult AppendRealExecutionRow(
    LiveTrainingOptions options,
    JsonObject beforeSnapshot,
    JsonObject queue,
    JsonObject execution,
    string stateHash,
    string queueId)
{
    var item = queue["items"]?.AsArray().FirstOrDefault()?.AsObject();
    var optionId = ReadStringOrEmpty(item, "option_id");
    var watered = ReadInt(execution, "watered_count");
    var energyBefore = ReadDouble(execution, "energy_before");
    var energyAfter = ReadDouble(execution, "energy_after");
    var energyCost = Math.Max(0, energyBefore - energyAfter);
    var reward = Math.Round(watered * 0.10 - energyCost * 0.005, 4);
    var blocked = !string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal) &&
        !string.Equals(ReadString(execution, "status"), "no_op", StringComparison.Ordinal);

    var row = new TrainingFeatureRowEnvelope
    {
        RowId = "feature-row." + Guid.NewGuid().ToString("N"),
        EpisodeId = "episode.real." + Guid.NewGuid().ToString("N"),
        SourceStateHash = stateHash,
        QueueId = queueId,
        StateFeatures = BuildStateFeatures(beforeSnapshot),
        ActionFeatures = new ActionFeatureVector
        {
            OptionIds = new[] { optionId },
            Features = new FeatureVector
            {
                Numeric = new[]
                {
                    Number("action.option_count", 1),
                    Number("action.required_minutes", 30),
                    Number("action.optional_minutes", 0)
                },
                Categorical = new[]
                {
                    Category("action.primary_option_id", optionId),
                    Category("action.intent_category", "mechanical"),
                    Category("action.execution_mode", "training_singleplayer"),
                    Category("action.actor_type", "training_farmer"),
                    Category("action.execution_profile", "real_runtime_harness")
                },
                Boolean = new[]
                {
                    Flag("action.hard_blocked", blocked)
                }
            }
        },
        Labels = new TrainingLabelVector
        {
            GoalProgressDelta = reward,
            TotalReward = reward,
            HardBlocked = blocked,
            RequiredMinutes = 30,
            AvailableMinutes = AvailableMinutes(beforeSnapshot),
            RewardTermNames = watered > 0 ? new[] { "real_crop_watered", "real_energy_spent" } : Array.Empty<string>(),
            BlockReasons = ReadArrayStrings(execution, "block_reasons")
        },
        Audit = new TrainingFeatureRowAudit
        {
            Exporter = "StardewAI.LiveTrainingLoop.RealRuntimeExecutor",
            Policy = "Feature row labels are derived from RuntimeTestHarness execution result and before/after transparent snapshots; no simulator endpoint used."
        }
    };

    return AppendJsonl(options.DatasetPath, row);
}

static FeatureVector BuildStateFeatures(JsonObject snapshot)
{
    return new FeatureVector
    {
        Numeric = new[]
        {
            Number("game.time", ReadFieldDouble(snapshot, "time", "time")),
            Number("game.day", ReadFieldDouble(snapshot, "time", "day")),
            Number("game.year", ReadFieldDouble(snapshot, "time", "year")),
            Number("player.money", ReadFieldDouble(snapshot, "player", "money")),
            Number("player.energy", ReadFieldDouble(snapshot, "player", "energy")),
            Number("player.health", ReadFieldDouble(snapshot, "player", "health")),
            Number("player.level", ReadFieldDouble(snapshot, "player", "level")),
            Number("player.total_money_earned", ReadFieldDouble(snapshot, "player", "total_money_earned")),
            Number("farm.crops_needing_watering", CountCropsNeedingWater(snapshot)),
            Number("completeness.unavailable_count", ReadUnavailableCount(snapshot)),
            Number("completeness.required_readable_ratio", 1)
        },
        Categorical = new[]
        {
            Category("game.season", ReadFieldString(snapshot, "time", "season")),
            Category("game.weather", ReadFieldString(snapshot, "time", "weather")),
            Category("player.location_id", ReadFieldString(snapshot, "player", "location_id")),
            Category("world.mode", "training")
        },
        Boolean = new[]
        {
            Flag("completeness.all_required_facts_readable", true),
            Flag("planner_inputs.blocked", false)
        }
    };
}

static TrainingDatasetAppendResult AppendJsonl(string datasetPath, TrainingFeatureRowEnvelope row)
{
    Directory.CreateDirectory(Path.GetDirectoryName(datasetPath)!);
    var payload = JsonSerializer.Serialize(row, JsonlOptions) + Environment.NewLine;
    File.AppendAllText(datasetPath, payload, Encoding.UTF8);
    return new TrainingDatasetAppendResult
    {
        DatasetPath = Path.GetFullPath(datasetPath),
        RowId = row.RowId,
        EpisodeId = row.EpisodeId,
        BytesWritten = Encoding.UTF8.GetByteCount(payload),
        RowCount = File.ReadLines(datasetPath).Count(line => !string.IsNullOrWhiteSpace(line))
    };
}

static async Task<(JsonObject? TrainingReport, JsonObject? Prediction)> TrainIfNeededAsync(HttpClient http, LiveTrainingOptions options, int iteration)
{
    if (iteration % options.TrainEvery != 0 && iteration != options.Iterations)
    {
        return (null, null);
    }

    var trainRequest = JsonSerializer.Serialize(new
    {
        dataset_path = options.DatasetPath
    }, JsonOptions);
    var report = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
    var prediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
    var bestOption = prediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
    AppendProgress(options, "train", iteration, string.Empty, string.Empty, "best_option=" + bestOption + " source=real_runtime_executor");
    return (report, prediction);
}

static async Task<JsonObject> PostJsonStringAsync(HttpClient http, string url, string json)
{
    using var content = new StringContent(json, Encoding.UTF8, "application/json");
    using var response = await http.PostAsync(url, content);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(url + " failed with " + (int)response.StatusCode + ": " + body);
    }

    return JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
}

static string ReadString(JsonObject value, string property)
{
    return value[property]?.GetValue<string>() ?? string.Empty;
}

static string ReadStringOrEmpty(JsonObject? value, string property)
{
    return value?[property]?.GetValue<string>() ?? string.Empty;
}

static int ReadInt(JsonObject value, string property)
{
    return value[property]?.GetValue<int>() ?? 0;
}

static double ReadDouble(JsonObject value, string property)
{
    return value[property]?.GetValue<double>() ?? 0;
}

static double ReadFieldDouble(JsonObject snapshot, string section, string name)
{
    var value = snapshot["state"]?[section]?[name]?["value"];
    if (value is null)
    {
        return 0;
    }

    return value.GetValueKind() == JsonValueKind.Number ? value.GetValue<double>() : 0;
}

static string ReadFieldString(JsonObject snapshot, string section, string name)
{
    var value = snapshot["state"]?[section]?[name]?["value"];
    if (value is null || value.GetValueKind() != JsonValueKind.String)
    {
        return "unknown";
    }

    return value.GetValue<string>() ?? "unknown";
}

static int CountCropsNeedingWater(JsonObject snapshot)
{
    var crops = snapshot["state"]?["farm"]?["crops"]?["value"]?.AsArray();
    if (crops is null)
    {
        return 0;
    }

    return crops.Count(item => item?["needs_watering"]?.GetValue<bool>() == true);
}

static int ReadUnavailableCount(JsonObject snapshot)
{
    return snapshot["unavailable_fields"]?.AsArray().Count ?? 0;
}

static int AvailableMinutes(JsonObject snapshot)
{
    var time = (int)ReadFieldDouble(snapshot, "time", "time");
    if (time <= 0)
    {
        return 0;
    }

    var hour = time / 100;
    var minute = time % 100;
    var current = hour * 60 + minute;
    return Math.Max(0, 26 * 60 - current);
}

static string[] ReadArrayStrings(JsonObject value, string property)
{
    var array = value[property]?.AsArray();
    if (array is null)
    {
        return Array.Empty<string>();
    }

    return array
        .Select(item => item?.GetValue<string>() ?? string.Empty)
        .Where(item => item.Length > 0)
        .ToArray();
}

static NumericFeature Number(string name, double value)
{
    return new NumericFeature { Name = name, Value = value };
}

static CategoricalFeature Category(string name, string value)
{
    return new CategoricalFeature { Name = name, Value = string.IsNullOrWhiteSpace(value) ? "unknown" : value };
}

static BooleanFeature Flag(string name, bool value)
{
    return new BooleanFeature { Name = name, Value = value };
}

static void AppendProgress(LiveTrainingOptions options, string stage, int iteration, string stateHash, string queueId, string detail)
{
    var line = string.Join(" ", new[]
    {
        DateTimeOffset.Now.ToString("O"),
        "stage=" + stage,
        "iteration=" + iteration,
        "run_id=" + options.RunId,
        "state_hash=" + stateHash,
        "queue_id=" + queueId,
        detail
    });
    File.AppendAllText(options.ProgressLogPath, line + Environment.NewLine);
}

public sealed class LiveTrainingOptions
{
    public string Root { get; set; } = @"E:\StardewAITraining";
    public string BackendUrl { get; set; } = "http://localhost:5108";
    public string BridgeSnapshotUrl { get; set; } = "http://127.0.0.1:8765/api/v1/snapshot";
    public string ExecutorUrl { get; set; } = "http://127.0.0.1:8767";
    public string ManifestPath { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-manifest-path.txt");
    public string RunId { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-run-id.txt");
    public string SaveIsolationPath { get; set; } = @"E:\StardewValleyAICompanion-runtime\saves";
    public string Goal { get; set; } = "grandpa_four_candles_year3";
    public int Iterations { get; set; } = 3;
    public int TrainEvery { get; set; } = 1;
    public int SleepMs { get; set; } = 1000;
    public int MaxCropsPerExecution { get; set; } = 16;
    public bool RequireExecutorFeedback { get; set; } = true;
    public bool UseRealRuntimeExecutor { get; set; } = true;
    public string ExecutionMode => RequireExecutorFeedback
        ? UseRealRuntimeExecutor ? "real_runtime_executor" : "training_sandbox_feedback_gate"
        : "disabled";

    public string RunDir => string.IsNullOrWhiteSpace(ManifestPath)
        ? Path.Combine(Root, "runs", string.IsNullOrWhiteSpace(RunId) ? "live-training" : RunId)
        : Path.GetDirectoryName(ManifestPath) ?? Path.Combine(Root, "runs", RunId);
    public string SnapshotDir => Path.Combine(RunDir, "live-snapshots");
    public string DatasetPath => Path.Combine(Root, "datasets", "live-training-feature-rows.jsonl");
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
            else if (current == "--executor-url" && i + 1 < args.Length)
            {
                options.ExecutorUrl = args[++i].TrimEnd('/');
            }
            else if (current == "--save-isolation-path" && i + 1 < args.Length)
            {
                options.SaveIsolationPath = args[++i];
            }
            else if (current == "--manifest-path" && i + 1 < args.Length)
            {
                options.ManifestPath = args[++i];
            }
            else if (current == "--run-id" && i + 1 < args.Length)
            {
                options.RunId = args[++i];
            }
            else if (current == "--goal" && i + 1 < args.Length)
            {
                options.Goal = args[++i];
            }
            else if (current == "--iterations" && i + 1 < args.Length && int.TryParse(args[++i], out var iterations))
            {
                options.Iterations = Math.Max(1, iterations);
            }
            else if (current == "--train-every" && i + 1 < args.Length && int.TryParse(args[++i], out var trainEvery))
            {
                options.TrainEvery = Math.Max(1, trainEvery);
            }
            else if (current == "--sleep-ms" && i + 1 < args.Length && int.TryParse(args[++i], out var sleepMs))
            {
                options.SleepMs = Math.Max(0, sleepMs);
            }
            else if (current == "--max-crops" && i + 1 < args.Length && int.TryParse(args[++i], out var maxCrops))
            {
                options.MaxCropsPerExecution = Math.Max(1, maxCrops);
            }
            else if (current == "--no-executor-feedback-required")
            {
                options.RequireExecutorFeedback = false;
            }
            else if (current == "--use-sandbox-executor")
            {
                options.UseRealRuntimeExecutor = false;
            }
        }

        if (string.IsNullOrWhiteSpace(options.RunId))
        {
            options.RunId = "live." + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        }

        return options;
    }

    private static string ReadTextOrEmpty(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
    }
}

public sealed class LiveTrainingLoopReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "live_training_loop_report.v1";

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("manifest_path")]
    public string ManifestPath { get; set; } = string.Empty;

    [JsonPropertyName("backend_url")]
    public string BackendUrl { get; set; } = string.Empty;

    [JsonPropertyName("bridge_snapshot_url")]
    public string BridgeSnapshotUrl { get; set; } = string.Empty;

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("progress_log_path")]
    public string ProgressLogPath { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_dir")]
    public string SnapshotDir { get; set; } = string.Empty;

    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    [JsonPropertyName("rows_appended")]
    public int RowsAppended { get; set; }

    [JsonPropertyName("last_state_hash")]
    public string LastStateHash { get; set; } = string.Empty;

    [JsonPropertyName("last_queue_id")]
    public string LastQueueId { get; set; } = string.Empty;

    [JsonPropertyName("concurrency")]
    public int Concurrency { get; set; }

    [JsonPropertyName("execution")]
    public string Execution { get; set; } = "disabled";

    [JsonPropertyName("executor_feedback_required")]
    public bool ExecutorFeedbackRequired { get; set; }

    [JsonPropertyName("training_report")]
    public JsonObject? TrainingReport { get; set; }

    [JsonPropertyName("prediction")]
    public JsonObject? Prediction { get; set; }
}

static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonlOptions = new(JsonSerializerDefaults.Web);
}
