using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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

AppendProgress(options, "start", 0, string.Empty, string.Empty, "concurrency=1 execution=disabled");

var rowsAppended = 0;
var lastQueueId = string.Empty;
var lastStateHash = string.Empty;
JsonObject? lastTrainingReport = null;
JsonObject? lastPrediction = null;

for (var iteration = 1; iteration <= options.Iterations; iteration++)
{
    var snapshotJson = await http.GetStringAsync(options.BridgeSnapshotUrl);
    var snapshotPath = Path.Combine(options.SnapshotDir, "snapshot-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(snapshotPath, snapshotJson, Encoding.UTF8);

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
    Execution = "disabled",
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
    execution = "disabled"
}, JsonOptions));

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
    public string ManifestPath { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-manifest-path.txt");
    public string RunId { get; set; } = ReadTextOrEmpty(@"E:\StardewAITraining\last-run-id.txt");
    public string Goal { get; set; } = "grandpa_four_candles_year3";
    public int Iterations { get; set; } = 3;
    public int TrainEvery { get; set; } = 1;
    public int SleepMs { get; set; } = 1000;

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
}
