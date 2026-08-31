using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class StructuredPolicyCheckpointStore
{
    public StructuredPolicyCheckpointEnvelope Load(string path)
    {
        var fullPath = RequiredFullPath(path, "Checkpoint path");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Structured policy checkpoint does not exist.", fullPath);
        StructuredPolicyCheckpointEnvelope checkpoint;
        try
        {
            checkpoint = JsonSerializer.Deserialize<StructuredPolicyCheckpointEnvelope>(
                File.ReadAllText(fullPath), JsonOptions)
                ?? throw new InvalidOperationException("Structured policy checkpoint is null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Structured policy checkpoint JSON is invalid.", ex);
        }
        Validate(checkpoint);
        return checkpoint;
    }

    public string Save(string path, StructuredPolicyCheckpointEnvelope checkpoint)
    {
        Validate(checkpoint);
        var fullPath = RequiredFullPath(path, "Checkpoint path");
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Checkpoint directory is unavailable.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(checkpoint, IndentedJsonOptions),
                new UTF8Encoding(false));
            if (File.Exists(fullPath))
                File.Replace(temporaryPath, fullPath, null);
            else
                File.Move(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return HashFile(fullPath);
    }

    public void Validate(StructuredPolicyCheckpointEnvelope checkpoint)
    {
        if (checkpoint is null)
            throw new ArgumentNullException(nameof(checkpoint));
        if (!string.Equals(checkpoint.SchemaVersion, StructuredPolicyVersionPins.CheckpointSchema, StringComparison.Ordinal) ||
            !string.Equals(checkpoint.ModelKind, StructuredPolicyVersionPins.ModelKind, StringComparison.Ordinal))
            throw new InvalidOperationException("Structured policy checkpoint schema or model kind is unsupported.");
        if (string.IsNullOrWhiteSpace(checkpoint.CheckpointId))
            throw new InvalidOperationException("Structured policy checkpoint ID is missing.");
        ValidateDataset(checkpoint.Dataset);
        ValidateVersions(checkpoint.Versions);
        ValidateHyperparameters(checkpoint.Hyperparameters);
        ValidateModel(checkpoint.Model);
        if (checkpoint.Training is null || checkpoint.Audit is null)
            throw new InvalidOperationException("Structured policy checkpoint metadata is incomplete.");
        ValidateTrainingSummary(checkpoint.Training, checkpoint.Model.FeatureNames.Length);
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(Path.GetFullPath(path));
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(stream));
    }

    public static string HashText(string value)
    {
        using var sha = SHA256.Create();
        return ToHex(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static void ValidateDataset(StructuredPolicyDatasetBinding dataset)
    {
        if (dataset is null || string.IsNullOrWhiteSpace(dataset.ManifestPath))
            throw new InvalidOperationException("Structured policy dataset binding is missing.");
        foreach (var digest in new[]
                 {
                     dataset.ManifestSha256, dataset.CleanedSha256, dataset.TrainSha256,
                     dataset.ValidationSha256, dataset.TestSha256
                 })
        {
            if (!IsSha256(digest))
                throw new InvalidOperationException("Structured policy dataset digest is invalid.");
        }
    }

    private static void ValidateVersions(PolicyDatasetVersionSet versions)
    {
        if (versions is null ||
            !string.Equals(versions.FeatureSchema, PolicyTrajectoryVersionPins.FeatureSchema, StringComparison.Ordinal) ||
            !string.Equals(versions.CandidateVocabulary, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(versions.CapabilityRegistry, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(versions.KnowledgeDictionary, PolicyTrajectoryVersionPins.KnowledgeDictionary, StringComparison.Ordinal) ||
            !string.Equals(versions.Compiler, PolicyTrajectoryVersionPins.Compiler, StringComparison.Ordinal) ||
            !string.Equals(versions.Executor, PolicyTrajectoryVersionPins.ProductExecutor, StringComparison.Ordinal))
            throw new InvalidOperationException("Structured policy checkpoint version binding is stale or incomplete.");
    }

    internal static void ValidateHyperparameters(StructuredPolicyHyperparameters value)
    {
        if (value is null || value.Epochs <= 0 || value.Epochs > 100000 ||
            !Finite(value.LearningRate) || value.LearningRate <= 0 ||
            !Finite(value.L2Regularization) || value.L2Regularization < 0 ||
            !Finite(value.MaxReturnWeight) || value.MaxReturnWeight < 1)
            throw new InvalidOperationException("Structured policy hyperparameters are invalid.");
    }

    private static void ValidateModel(StructuredPolicyLinearModel model)
    {
        if (model is null)
            throw new InvalidOperationException("Structured policy model is missing.");
        var count = model.FeatureNames?.Length ?? 0;
        if (count == 0 || model.FeatureMeans?.Length != count ||
            model.FeatureScales?.Length != count || model.Weights?.Length != count)
            throw new InvalidOperationException("Structured policy model arrays are empty or misaligned.");
        if (model.FeatureNames.Any(string.IsNullOrWhiteSpace) ||
            model.FeatureNames.Distinct(StringComparer.Ordinal).Count() != count ||
            !model.FeatureNames.SequenceEqual(model.FeatureNames.OrderBy(name => name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Structured policy feature vocabulary is not canonical.");
        if (model.FeatureMeans.Any(value => !Finite(value)) ||
            model.FeatureScales.Any(value => !Finite(value) || value <= 0) ||
            model.Weights.Any(value => !Finite(value)))
            throw new InvalidOperationException("Structured policy model contains non-finite parameters.");
    }

    private static void ValidateTrainingSummary(StructuredPolicyTrainingSummary summary, int featureCount)
    {
        if (summary.TrainRows <= 0 || summary.TrainPairs <= 0 ||
            summary.ValidationRows < 0 || summary.ValidationPairs < 0 ||
            summary.TestRows < 0 || summary.TestPairs < 0 ||
            summary.FeatureCount != featureCount ||
            !Probability(summary.TrainPairAccuracy) ||
            !NullableProbability(summary.ValidationPairAccuracy) ||
            !NullableProbability(summary.TestPairAccuracy))
            throw new InvalidOperationException("Structured policy training summary is invalid.");
    }

    private static bool IsSha256(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Probability(double value) => Finite(value) && value is >= 0 and <= 1;
    private static bool NullableProbability(double? value) => !value.HasValue || Probability(value.Value);
    private static string RequiredFullPath(string value, string label) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException(label + " is required.")
        : Path.GetFullPath(value);
    private static string ToHex(byte[] bytes) => string.Concat(bytes.Select(value => value.ToString("x2")));

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
