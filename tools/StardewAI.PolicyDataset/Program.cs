using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

var options = PolicyDatasetOptions.Parse(args);
var result = new PolicyTrajectoryDatasetBuilder().Build(
    options.InputPath,
    options.OutputRoot,
    options.HorizonObservationPath,
    options.ExpectedKnowledgeDictionary);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ok",
    manifest_path = result.ManifestPath,
    rejections_path = result.RejectionsPath,
    input_lines = result.Manifest.Counts.InputLines,
    accepted_rows = result.Manifest.Counts.AcceptedRows,
    rejected_rows = result.Manifest.Counts.RejectedRows,
    duplicate_rows = result.Manifest.Counts.DuplicateRows,
    conflicting_duplicate_rows = result.Manifest.Counts.ConflictingDuplicateRows,
    train_rows = result.Manifest.Partitions.Single(row => row.Partition == "train").Rows,
    validation_rows = result.Manifest.Partitions.Single(row => row.Partition == "validation").Rows,
    test_rows = result.Manifest.Partitions.Single(row => row.Partition == "test").Rows,
    day_returns = result.Manifest.Returns.DayComplete,
    season_returns = result.Manifest.Returns.SeasonComplete,
    year_returns = result.Manifest.Returns.YearComplete,
    grandpa_21_returns = result.Manifest.Returns.Grandpa21Complete
}, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
}));

internal sealed class PolicyDatasetOptions
{
    public string InputPath { get; private set; } = @"E:\StardewAITraining\datasets\policy-decision-trajectories.jsonl";
    public string OutputRoot { get; private set; } = @"E:\StardewAITraining\datasets\formal-policy";
    public string? HorizonObservationPath { get; private set; } = @"E:\StardewAITraining\datasets\policy-horizon-observations.jsonl";
    public string ExpectedKnowledgeDictionary { get; private set; } = PolicyTrajectoryVersionPins.KnowledgeDictionary;

    public static PolicyDatasetOptions Parse(string[] args)
    {
        var result = new PolicyDatasetOptions();
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == "--input" && index + 1 < args.Length)
                result.InputPath = args[++index];
            else if (args[index] == "--output-root" && index + 1 < args.Length)
                result.OutputRoot = args[++index];
            else if (args[index] == "--horizon-observations" && index + 1 < args.Length)
                result.HorizonObservationPath = args[++index];
            else if (args[index] == "--no-horizon-observations")
                result.HorizonObservationPath = null;
            else if (args[index] == "--knowledge-dictionary-version" && index + 1 < args.Length)
                result.ExpectedKnowledgeDictionary = args[++index];
            else
                throw new ArgumentException("Unknown or incomplete argument: " + args[index]);
        }
        return result;
    }
}
