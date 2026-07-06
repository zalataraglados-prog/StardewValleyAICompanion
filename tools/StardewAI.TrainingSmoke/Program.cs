using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

var options = SmokeOptions.Parse(args);
Directory.CreateDirectory(options.Root);
Directory.CreateDirectory(Path.GetDirectoryName(options.DatasetPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.ReportPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.CheckpointPath)!);

if (options.ResetDataset && File.Exists(options.DatasetPath))
{
    File.Delete(options.DatasetPath);
}

var total = Stopwatch.StartNew();
var writer = new JsonlTrainingDatasetWriter();
var dataWatch = Stopwatch.StartNew();
var rows = new List<TrainingFeatureRowEnvelope>();
for (var i = 0; i < options.Samples; i++)
{
    rows.Add(CreateRow(i));
}

writer.AppendMany(options.DatasetPath, rows);
var rowsWritten = rows.Count;

dataWatch.Stop();

var trainer = new BaselineFeatureRowTrainer();
var predictor = new BaselineOptionRanker();
var epochReports = new List<EpochMetric>();
BaselineTrainingReport? finalTrainingReport = null;
PolicyPredictionEnvelope? finalPrediction = null;
var trainWatch = Stopwatch.StartNew();
for (var epoch = 1; epoch <= options.Epochs; epoch++)
{
    var epochWatch = Stopwatch.StartNew();
    finalTrainingReport = trainer.Train(options.DatasetPath);
    finalPrediction = predictor.Rank(finalTrainingReport, Array.Empty<string>());
    epochWatch.Stop();
    epochReports.Add(new EpochMetric
    {
        Epoch = epoch,
        Rows = finalTrainingReport.RowCount,
        BestOptionId = finalPrediction.RankedOptions.FirstOrDefault()?.OptionId ?? string.Empty,
        BestScore = finalPrediction.RankedOptions.FirstOrDefault()?.Score ?? 0,
        DurationMs = epochWatch.ElapsedMilliseconds
    });
}

trainWatch.Stop();
total.Stop();

var report = new SmokeReport
{
    SamplesRequested = options.Samples,
    RowsWritten = rowsWritten,
    Epochs = options.Epochs,
    DatasetPath = options.DatasetPath,
    ReportPath = options.ReportPath,
    CheckpointPath = options.CheckpointPath,
    GameLaunch = "disabled",
    Sound = "disabled",
    DataGenerationMs = dataWatch.ElapsedMilliseconds,
    TrainingMs = trainWatch.ElapsedMilliseconds,
    TotalMs = total.ElapsedMilliseconds,
    WorkingSetMb = Math.Round(Environment.WorkingSet / 1024d / 1024d, 2),
    PrivateMemoryMb = Math.Round(Process.GetCurrentProcess().PrivateMemorySize64 / 1024d / 1024d, 2),
    TrainingReport = finalTrainingReport ?? new BaselineTrainingReport(),
    Prediction = finalPrediction ?? new PolicyPredictionEnvelope(),
    EpochsDetail = epochReports.ToArray()
};

File.WriteAllText(options.ReportPath, JsonSerializer.Serialize(report, JsonOptions));
File.WriteAllText(options.CheckpointPath, JsonSerializer.Serialize(report.TrainingReport, JsonOptions));

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ok",
    samples = report.RowsWritten,
    epochs = report.Epochs,
    best_option = report.Prediction.RankedOptions.FirstOrDefault()?.OptionId ?? string.Empty,
    total_ms = report.TotalMs,
    working_set_mb = report.WorkingSetMb,
    dataset = report.DatasetPath,
    report = report.ReportPath,
    checkpoint = report.CheckpointPath,
    game_launch = report.GameLaunch,
    sound = report.Sound
}, JsonOptions));

static TrainingFeatureRowEnvelope CreateRow(int index)
{
    var optionId = (index % 4) switch
    {
        0 => "farm.maintain_crops",
        1 => "farm.maintain_crops",
        2 => "exploration.visit_location",
        _ => "social.gift_npc"
    };
    var blocked = optionId == "social.gift_npc" && index % 2 == 1;
    var reward = optionId switch
    {
        "farm.maintain_crops" => 0.09,
        "exploration.visit_location" => 0.04,
        "social.gift_npc" => blocked ? -0.02 : 0.03,
        _ => 0
    };

    return new TrainingFeatureRowEnvelope
    {
        RowId = "smoke-row." + index.ToString("D5"),
        EpisodeId = "smoke-episode." + index.ToString("D5"),
        SourceStateHash = "smoke.hash",
        QueueId = "smoke.queue." + index.ToString("D5"),
        StateFeatures = new FeatureVector
        {
            Numeric = new[]
            {
                new NumericFeature { Name = "game.time", Value = 610 + index % 12 * 10 },
                new NumericFeature { Name = "player.energy", Value = 270 - index % 20 },
                new NumericFeature { Name = "farm.crops_needing_watering", Value = index % 5 }
            },
            Categorical = new[]
            {
                new CategoricalFeature { Name = "game.season", Value = "spring" },
                new CategoricalFeature { Name = "player.location_id", Value = "Farm" }
            },
            Boolean = new[]
            {
                new BooleanFeature { Name = "completeness.all_required_facts_readable", Value = true }
            }
        },
        ActionFeatures = new ActionFeatureVector
        {
            OptionIds = new[] { optionId }
        },
        Labels = new TrainingLabelVector
        {
            GoalProgressDelta = reward,
            TotalReward = reward,
            HardBlocked = blocked,
            RequiredMinutes = optionId == "farm.maintain_crops" ? 30 : 90,
            AvailableMinutes = 1070,
            RewardTermNames = reward >= 0 ? new[] { "smoke_positive" } : new[] { "smoke_blocked" },
            BlockReasons = blocked ? new[] { "smoke_blocked_option" } : Array.Empty<string>()
        }
    };
}

public sealed class SmokeOptions
{
    public string Root { get; set; } = @"E:\StardewAITraining";
    public int Samples { get; set; } = 64;
    public int Epochs { get; set; } = 1;
    public bool ResetDataset { get; set; } = true;

    public string DatasetPath => Path.Combine(Root, "datasets", "training-feature-rows.jsonl");
    public string ReportPath => Path.Combine(Root, "reports", "training-smoke-report.json");
    public string CheckpointPath => Path.Combine(Root, "checkpoints", "baseline-latest.json");

    public static SmokeOptions Parse(string[] args)
    {
        var options = new SmokeOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            if (current == "--root" && i + 1 < args.Length)
            {
                options.Root = args[++i];
            }
            else if (current == "--samples" && i + 1 < args.Length && int.TryParse(args[++i], out var samples))
            {
                options.Samples = Math.Max(1, samples);
            }
            else if (current == "--epochs" && i + 1 < args.Length && int.TryParse(args[++i], out var epochs))
            {
                options.Epochs = Math.Max(1, epochs);
            }
            else if (current == "--append")
            {
                options.ResetDataset = false;
            }
        }

        return options;
    }
}

public sealed class SmokeReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "training_smoke_report.v1";

    [JsonPropertyName("samples_requested")]
    public int SamplesRequested { get; set; }

    [JsonPropertyName("rows_written")]
    public int RowsWritten { get; set; }

    [JsonPropertyName("epochs")]
    public int Epochs { get; set; }

    [JsonPropertyName("dataset_path")]
    public string DatasetPath { get; set; } = string.Empty;

    [JsonPropertyName("report_path")]
    public string ReportPath { get; set; } = string.Empty;

    [JsonPropertyName("checkpoint_path")]
    public string CheckpointPath { get; set; } = string.Empty;

    [JsonPropertyName("game_launch")]
    public string GameLaunch { get; set; } = "disabled";

    [JsonPropertyName("sound")]
    public string Sound { get; set; } = "disabled";

    [JsonPropertyName("data_generation_ms")]
    public long DataGenerationMs { get; set; }

    [JsonPropertyName("training_ms")]
    public long TrainingMs { get; set; }

    [JsonPropertyName("total_ms")]
    public long TotalMs { get; set; }

    [JsonPropertyName("working_set_mb")]
    public double WorkingSetMb { get; set; }

    [JsonPropertyName("private_memory_mb")]
    public double PrivateMemoryMb { get; set; }

    [JsonPropertyName("training_report")]
    public BaselineTrainingReport TrainingReport { get; set; } = new();

    [JsonPropertyName("prediction")]
    public PolicyPredictionEnvelope Prediction { get; set; } = new();

    [JsonPropertyName("epochs_detail")]
    public EpochMetric[] EpochsDetail { get; set; } = Array.Empty<EpochMetric>();
}

public sealed class EpochMetric
{
    [JsonPropertyName("epoch")]
    public int Epoch { get; set; }

    [JsonPropertyName("rows")]
    public int Rows { get; set; }

    [JsonPropertyName("best_option_id")]
    public string BestOptionId { get; set; } = string.Empty;

    [JsonPropertyName("best_score")]
    public double BestScore { get; set; }

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}

static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
