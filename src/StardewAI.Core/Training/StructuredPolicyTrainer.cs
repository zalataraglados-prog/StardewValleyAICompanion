using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class StructuredPolicyTrainer
{
    private readonly StructuredPolicyCheckpointStore checkpointStore;
    private readonly PolicyTrajectoryDatasetValidator trajectoryValidator = new();

    public StructuredPolicyTrainer()
        : this(new StructuredPolicyCheckpointStore())
    {
    }

    public StructuredPolicyTrainer(StructuredPolicyCheckpointStore checkpointStore)
    {
        this.checkpointStore = checkpointStore;
    }

    public StructuredPolicyTrainingResult Train(
        string datasetManifestPath,
        string checkpointPath,
        StructuredPolicyHyperparameters? hyperparameters = null)
    {
        var manifestPath = Path.GetFullPath(Required(datasetManifestPath, "Dataset manifest path"));
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Policy dataset manifest does not exist.", manifestPath);
        var parameters = hyperparameters ?? new StructuredPolicyHyperparameters();
        StructuredPolicyCheckpointStore.ValidateHyperparameters(parameters);
        var manifest = ReadManifest(manifestPath);
        var version = ValidateManifest(manifest);
        VerifyDigest(manifest.Cleaned);
        var trainDigest = Partition(manifest, PolicyDatasetPartitions.Train);
        var validationDigest = Partition(manifest, PolicyDatasetPartitions.Validation);
        var testDigest = Partition(manifest, PolicyDatasetPartitions.Test);
        var trainRows = ReadPartition(trainDigest, PolicyDatasetPartitions.Train);
        var validationRows = ReadPartition(validationDigest, PolicyDatasetPartitions.Validation);
        var testRows = ReadPartition(testDigest, PolicyDatasetPartitions.Test);
        var allRows = trainRows.Concat(validationRows).Concat(testRows).ToArray();
        if (allRows.Length != manifest.Counts.AcceptedRows ||
            manifest.Cleaned.Rows != manifest.Counts.AcceptedRows ||
            manifest.Partitions.Sum(item => item.Rows) != manifest.Counts.AcceptedRows)
            throw new InvalidOperationException("Policy dataset row counts do not agree across the manifest.");
        if (allRows.Select(row => row.TrajectoryId).Distinct(StringComparer.Ordinal).Count() != allRows.Length)
            throw new InvalidOperationException("Policy dataset trajectory IDs are not globally unique.");
        if (trainRows.Length == 0)
            throw new InvalidOperationException("Structured policy training partition is empty.");

        var featureNames = StructuredPolicyFeatureEncoder.DiscoverFeatureNames(trainRows);
        if (featureNames.Length == 0)
            throw new InvalidOperationException("Structured policy feature vocabulary is empty.");
        var model = StructuredPolicyFeatureEncoder.FitModelShape(trainRows, featureNames);
        var trainPairs = BuildPairs(trainRows, model);
        if (trainPairs.Count == 0)
            throw new InvalidOperationException("Structured policy training partition contains no admitted comparison pairs.");
        Optimize(model.Weights, trainPairs, parameters);

        var manifestHash = StructuredPolicyCheckpointStore.HashFile(manifestPath);
        var checkpoint = new StructuredPolicyCheckpointEnvelope
        {
            CheckpointId = "structured-policy-" + StructuredPolicyCheckpointStore.HashText(
                manifestHash + "\n" + CanonicalHyperparameters(parameters)).Substring(0, 24),
            Dataset = new StructuredPolicyDatasetBinding
            {
                ManifestPath = manifestPath,
                ManifestSha256 = manifestHash,
                CleanedSha256 = manifest.Cleaned.Sha256,
                TrainSha256 = trainDigest.Sha256,
                ValidationSha256 = validationDigest.Sha256,
                TestSha256 = testDigest.Sha256
            },
            Versions = CopyVersion(version),
            Hyperparameters = CopyHyperparameters(parameters),
            Model = model,
            Training = BuildSummary(model, trainRows, validationRows, testRows, trainPairs)
        };
        var fullCheckpointPath = Path.GetFullPath(Required(checkpointPath, "Checkpoint path"));
        var checkpointHash = checkpointStore.Save(fullCheckpointPath, checkpoint);
        return new StructuredPolicyTrainingResult
        {
            CheckpointPath = fullCheckpointPath,
            CheckpointSha256 = checkpointHash,
            Checkpoint = checkpoint
        };
    }

    private PolicyDatasetManifest ReadManifest(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<PolicyDatasetManifest>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException("Policy dataset manifest is null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Policy dataset manifest JSON is invalid.", ex);
        }
    }

    private static PolicyDatasetVersionSet ValidateManifest(PolicyDatasetManifest manifest)
    {
        if (!string.Equals(manifest.SchemaVersion, "policy_dataset_manifest.v1", StringComparison.Ordinal) ||
            manifest.Cleaned is null || manifest.Counts is null ||
            manifest.Partitions is null || manifest.VersionSets is null)
            throw new InvalidOperationException("Policy dataset manifest is incomplete or unsupported.");
        if (manifest.VersionSets.Length != 1)
            throw new InvalidOperationException("Structured policy training requires exactly one immutable version set.");
        var version = manifest.VersionSets[0];
        if (!string.Equals(version.FeatureSchema, PolicyTrajectoryVersionPins.FeatureSchema, StringComparison.Ordinal) ||
            !string.Equals(version.CandidateVocabulary, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(version.CapabilityRegistry, OptionCapabilityRegistrySource.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(version.KnowledgeDictionary, PolicyTrajectoryVersionPins.KnowledgeDictionary, StringComparison.Ordinal) ||
            !string.Equals(version.Compiler, "action_queue.v1", StringComparison.Ordinal) ||
            !string.Equals(version.Executor, "runtime_test_harness_executor.v1", StringComparison.Ordinal))
            throw new InvalidOperationException("Policy dataset version binding is stale or unsupported.");
        if (version.RowCount != manifest.Counts.AcceptedRows)
            throw new InvalidOperationException("Policy dataset version row count does not match the manifest.");
        return version;
    }

    private PolicyDecisionTrajectoryEnvelope[] ReadPartition(
        PolicyDatasetPartitionDigest digest,
        string expectedPartition)
    {
        VerifyDigest(digest);
        var rows = new List<PolicyDecisionTrajectoryEnvelope>();
        var lines = File.ReadAllLines(Path.GetFullPath(digest.Path));
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
                throw new InvalidOperationException("Policy partition contains a blank row at line " + (index + 1) + ".");
            PolicyDecisionTrajectoryEnvelope row;
            try
            {
                row = JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(lines[index], JsonOptions)
                    ?? throw new InvalidOperationException("Policy partition row is null at line " + (index + 1) + ".");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Policy partition JSON is invalid at line " + (index + 1) + ".", ex);
            }
            var rejection = trajectoryValidator.Validate(row);
            if (rejection is not null)
                throw new InvalidOperationException("Policy partition row failed validation: " + rejection + ".");
            if (!string.Equals(row.Context.DatasetPartition, expectedPartition, StringComparison.Ordinal) ||
                !string.Equals(PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey), expectedPartition, StringComparison.Ordinal))
                throw new InvalidOperationException("Policy partition row crossed its deterministic split boundary.");
            rows.Add(row);
        }
        if (rows.Count != digest.Rows)
            throw new InvalidOperationException("Policy partition row count does not match the manifest.");
        return rows.ToArray();
    }

    private static void VerifyDigest(PolicyDatasetFileDigest digest)
    {
        if (digest is null || string.IsNullOrWhiteSpace(digest.Path) || !File.Exists(Path.GetFullPath(digest.Path)))
            throw new InvalidOperationException("Policy dataset artifact is missing.");
        var info = new FileInfo(Path.GetFullPath(digest.Path));
        if (info.Length != digest.Bytes ||
            !string.Equals(StructuredPolicyCheckpointStore.HashFile(info.FullName), digest.Sha256, StringComparison.Ordinal))
            throw new InvalidOperationException("Policy dataset artifact digest does not match the manifest.");
    }

    private static PolicyDatasetPartitionDigest Partition(PolicyDatasetManifest manifest, string name)
    {
        var matches = manifest.Partitions.Where(item => string.Equals(item.Partition, name, StringComparison.Ordinal)).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException("Policy dataset manifest must contain exactly one '" + name + "' partition.");
    }

    private static List<TrainingPair> BuildPairs(
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows,
        StructuredPolicyLinearModel model)
    {
        var pairs = new List<TrainingPair>();
        foreach (var row in rows)
        {
            var selected = row.Candidates.Single(candidate => candidate.Selected);
            var positive = StructuredPolicyFeatureEncoder.Encode(row.StateFeatures, selected, model);
            foreach (var negative in row.Candidates.Where(candidate =>
                         !candidate.Selected && candidate.Available && candidate.AdmittedForPolicy))
            {
                var encodedNegative = StructuredPolicyFeatureEncoder.Encode(row.StateFeatures, negative, model);
                var difference = new double[positive.Length];
                for (var index = 0; index < difference.Length; index++)
                    difference[index] = positive[index] - encodedNegative[index];
                pairs.Add(new TrainingPair(difference, ReturnWeight(row.Returns)));
            }
        }
        return pairs;
    }

    private static double ReturnWeight(PolicyTrajectoryReturns returns)
    {
        var observed = returns.Grandpa21 ?? returns.Year ?? returns.Season ?? returns.Day ?? returns.Immediate;
        return observed;
    }

    private static void Optimize(
        double[] weights,
        IReadOnlyList<TrainingPair> pairs,
        StructuredPolicyHyperparameters parameters)
    {
        var effectivePairs = pairs.Select(pair => new TrainingPair(
            pair.Difference,
            1 + Math.Min(parameters.MaxReturnWeight - 1, Math.Max(0, pair.Weight)))).ToArray();
        var gradient = new double[weights.Length];
        for (var epoch = 0; epoch < parameters.Epochs; epoch++)
        {
            Array.Clear(gradient, 0, gradient.Length);
            foreach (var pair in effectivePairs)
            {
                var margin = Dot(weights, pair.Difference);
                var negativeProbability = margin >= 0
                    ? Math.Exp(-margin) / (1 + Math.Exp(-margin))
                    : 1 / (1 + Math.Exp(margin));
                var factor = -pair.Weight * negativeProbability;
                for (var index = 0; index < gradient.Length; index++)
                    gradient[index] += factor * pair.Difference[index];
            }
            for (var index = 0; index < gradient.Length; index++)
            {
                gradient[index] = gradient[index] / effectivePairs.Length +
                    parameters.L2Regularization * weights[index];
                weights[index] -= parameters.LearningRate * gradient[index];
            }
        }
    }

    private static StructuredPolicyTrainingSummary BuildSummary(
        StructuredPolicyLinearModel model,
        PolicyDecisionTrajectoryEnvelope[] trainRows,
        PolicyDecisionTrajectoryEnvelope[] validationRows,
        PolicyDecisionTrajectoryEnvelope[] testRows,
        IReadOnlyList<TrainingPair> trainPairs)
    {
        var validationPairs = BuildPairs(validationRows, model);
        var testPairs = BuildPairs(testRows, model);
        return new StructuredPolicyTrainingSummary
        {
            TrainRows = trainRows.Length,
            TrainPairs = trainPairs.Count,
            FeatureCount = model.FeatureNames.Length,
            TrainPairAccuracy = Accuracy(model.Weights, trainPairs) ?? 0,
            ValidationRows = validationRows.Length,
            ValidationPairs = validationPairs.Count,
            ValidationPairAccuracy = Accuracy(model.Weights, validationPairs),
            TestRows = testRows.Length,
            TestPairs = testPairs.Count,
            TestPairAccuracy = Accuracy(model.Weights, testPairs)
        };
    }

    private static double? Accuracy(double[] weights, IReadOnlyList<TrainingPair> pairs)
    {
        if (pairs.Count == 0)
            return null;
        return Math.Round((double)pairs.Count(pair => Dot(weights, pair.Difference) > 0) / pairs.Count, 6);
    }

    private static double Dot(double[] left, double[] right)
    {
        var result = 0d;
        for (var index = 0; index < left.Length; index++)
            result += left[index] * right[index];
        return result;
    }

    private static string CanonicalHyperparameters(StructuredPolicyHyperparameters value) =>
        value.Epochs.ToString(CultureInfo.InvariantCulture) + "|" +
        value.LearningRate.ToString("R", CultureInfo.InvariantCulture) + "|" +
        value.L2Regularization.ToString("R", CultureInfo.InvariantCulture) + "|" +
        value.MaxReturnWeight.ToString("R", CultureInfo.InvariantCulture);

    private static PolicyDatasetVersionSet CopyVersion(PolicyDatasetVersionSet value) => new()
    {
        FeatureSchema = value.FeatureSchema,
        CandidateVocabulary = value.CandidateVocabulary,
        CapabilityRegistry = value.CapabilityRegistry,
        KnowledgeDictionary = value.KnowledgeDictionary,
        Compiler = value.Compiler,
        Executor = value.Executor,
        RowCount = value.RowCount
    };

    private static StructuredPolicyHyperparameters CopyHyperparameters(StructuredPolicyHyperparameters value) => new()
    {
        Epochs = value.Epochs,
        LearningRate = value.LearningRate,
        L2Regularization = value.L2Regularization,
        MaxReturnWeight = value.MaxReturnWeight
    };

    private static string Required(string value, string label) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException(label + " is required.")
        : value;

    private sealed record TrainingPair(double[] Difference, double Weight);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
