using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

var options = PolicyModelOptions.Parse(args);
var result = new StructuredPolicyTrainer().Train(
    options.DatasetManifestPath,
    options.CheckpointPath,
    options.Hyperparameters);
Console.WriteLine(JsonSerializer.Serialize(new
{
    status = "ok",
    checkpoint_path = result.CheckpointPath,
    checkpoint_sha256 = result.CheckpointSha256,
    checkpoint_id = result.Checkpoint.CheckpointId,
    model_kind = result.Checkpoint.ModelKind,
    training = result.Checkpoint.Training
}, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

internal sealed class PolicyModelOptions
{
    public string DatasetManifestPath { get; private set; } =
        @"E:\StardewAITraining\datasets\formal-policy\policy-dataset-manifest.json";
    public string CheckpointPath { get; private set; } =
        @"E:\StardewAITraining\checkpoints\structured-policy-v1.json";
    public StructuredPolicyHyperparameters Hyperparameters { get; } = new();

    public static PolicyModelOptions Parse(string[] args)
    {
        var result = new PolicyModelOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            if (current == "--dataset-manifest" && index + 1 < args.Length)
                result.DatasetManifestPath = args[++index];
            else if (current == "--checkpoint" && index + 1 < args.Length)
                result.CheckpointPath = args[++index];
            else if (current == "--epochs" && index + 1 < args.Length)
                result.Hyperparameters.Epochs = ParseInt(args[++index], current);
            else if (current == "--learning-rate" && index + 1 < args.Length)
                result.Hyperparameters.LearningRate = ParseDouble(args[++index], current);
            else if (current == "--l2" && index + 1 < args.Length)
                result.Hyperparameters.L2Regularization = ParseDouble(args[++index], current);
            else if (current == "--max-return-weight" && index + 1 < args.Length)
                result.Hyperparameters.MaxReturnWeight = ParseDouble(args[++index], current);
            else
                throw new ArgumentException("Unknown or incomplete argument: " + current);
        }
        return result;
    }

    private static int ParseInt(string value, string option) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException(option + " requires an integer.");

    private static double ParseDouble(string value, string option) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new ArgumentException(option + " requires a number.");
}
