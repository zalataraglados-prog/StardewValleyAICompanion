using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class GoalMethodPairwiseCheckpointStore
{
    public GoalMethodPairwiseCheckpoint Load(string path)
    {
        var fullPath = RequiredFullPath(path, "Checkpoint path");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException(
                "Goal-method checkpoint does not exist.",
                fullPath);
        GoalMethodPairwiseCheckpoint checkpoint;
        try
        {
            checkpoint = JsonSerializer.Deserialize<
                GoalMethodPairwiseCheckpoint>(
                File.ReadAllText(fullPath),
                JsonDefaults.Compact) ?? throw new InvalidDataException(
                "Goal-method checkpoint is null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Goal-method checkpoint JSON is invalid.",
                exception);
        }
        Validate(checkpoint);
        return checkpoint;
    }

    public string Save(string path, GoalMethodPairwiseCheckpoint checkpoint)
    {
        Validate(checkpoint);
        var fullPath = RequiredFullPath(path, "Checkpoint path");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(checkpoint, JsonDefaults.Options) +
                "\n",
                new UTF8Encoding(false));
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return CurrentTeacherFrontierSupport.HashFile(fullPath);
    }

    public static void ValidateHyperparameters(
        GoalMethodPairwiseHyperparameters value)
    {
        if (value is null ||
            value.Epochs is < 1 or > 100000 ||
            !Finite(value.LearningRate) ||
            value.LearningRate <= 0 ||
            !Finite(value.L2Regularization) ||
            value.L2Regularization < 0)
        {
            throw new InvalidDataException(
                "Goal-method hyperparameters are invalid.");
        }
    }

    public static string HashText(string value)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(
                Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    public void Validate(GoalMethodPairwiseCheckpoint checkpoint)
    {
        if (checkpoint is null ||
            checkpoint.SchemaVersion !=
                GoalMethodPairwiseVersionPins.CheckpointSchema ||
            checkpoint.ModelKind != GoalMethodPairwiseVersionPins.ModelKind ||
            string.IsNullOrWhiteSpace(checkpoint.CheckpointId) ||
            checkpoint.TeacherLabelSource !=
                "explicit_teacher_pairwise_preferences" ||
            checkpoint.UsesCandidateSelectedFlag ||
            checkpoint.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Goal-method checkpoint identity or authority is invalid.");
        }
        ValidateDataset(checkpoint.Dataset);
        ValidateVersions(checkpoint.Versions);
        ValidateHyperparameters(checkpoint.Hyperparameters);
        ValidateModel(checkpoint.Model);
        ValidateSummary(checkpoint.Training, checkpoint.Model.FeatureNames.Length);
        ValidateInitialization(
            checkpoint.Initialization,
            checkpoint.Model.FeatureNames.Length);
        var expectedCheckpointId = GoalMethodPairwiseTrainer.CreateCheckpointId(
            checkpoint.Dataset.CorpusManifestSha256,
            checkpoint.Hyperparameters,
            checkpoint.Initialization?.CheckpointSha256);
        if (checkpoint.CheckpointId != expectedCheckpointId)
        {
            throw new InvalidDataException(
                "Goal-method checkpoint ID does not match its bound inputs.");
        }
    }

    private static void ValidateDataset(
        GoalMethodPairwiseDatasetBinding binding)
    {
        if (binding is null ||
            string.IsNullOrWhiteSpace(binding.CorpusManifestPath) ||
            binding.SourceCount <= 0 ||
            new[]
            {
                binding.CorpusManifestSha256,
                binding.CleanedSha256,
                binding.TrainSha256,
                binding.ValidationSha256,
                binding.TestSha256
            }.Any(value => !IsSha256(value)))
        {
            throw new InvalidDataException(
                "Goal-method checkpoint dataset binding is invalid.");
        }
    }

    private static void ValidateVersions(
        GoalMethodPairwiseVersionBinding versions)
    {
        if (versions is null ||
            versions.FeatureSchema !=
                GoalMethodPairwiseVersionPins.FeatureSchema ||
            versions.CorpusSchema !=
                GoalMethodPairwiseVersionPins.CorpusSchema ||
            versions.SupervisionRowSchema !=
                GoalMethodPairwiseVersionPins.SupervisionRowSchema ||
            string.IsNullOrWhiteSpace(versions.GameVersion) ||
            string.IsNullOrWhiteSpace(versions.BridgeVersion))
        {
            throw new InvalidDataException(
                "Goal-method checkpoint version binding is invalid.");
        }
    }

    private static void ValidateModel(GoalMethodPairwiseLinearModel model)
    {
        var count = model?.FeatureNames?.Length ?? 0;
        if (count == 0 ||
            model!.FeatureMeans?.Length != count ||
            model.FeatureScales?.Length != count ||
            model.Weights?.Length != count ||
            model.FeatureNames.Any(string.IsNullOrWhiteSpace) ||
            model.FeatureNames.Distinct(StringComparer.Ordinal).Count() !=
                count ||
            !model.FeatureNames.SequenceEqual(
                model.FeatureNames.Order(StringComparer.Ordinal)) ||
            model.FeatureMeans.Any(value => !Finite(value)) ||
            model.FeatureScales.Any(value =>
                !Finite(value) || value <= 0) ||
            model.Weights.Any(value => !Finite(value)))
        {
            throw new InvalidDataException(
                "Goal-method checkpoint model is invalid.");
        }
    }

    private static void ValidateSummary(
        GoalMethodPairwiseTrainingSummary summary,
        int featureCount)
    {
        if (summary is null ||
            summary.TrainRows <= 0 || summary.TrainPairs <= 0 ||
            summary.ValidationRows <= 0 || summary.ValidationPairs <= 0 ||
            summary.TestRows <= 0 || summary.TestPairs <= 0 ||
            summary.FeatureCount != featureCount ||
            !Probability(summary.TrainPairAccuracy) ||
            !Probability(summary.ValidationPairAccuracy) ||
            !Probability(summary.TestPairAccuracy))
        {
            throw new InvalidDataException(
                "Goal-method checkpoint training summary is invalid.");
        }
    }

    private static void ValidateInitialization(
        GoalMethodPairwiseInitializationBinding? initialization,
        int featureCount)
    {
        if (initialization is null)
            return;
        if (string.IsNullOrWhiteSpace(initialization.CheckpointId) ||
            !IsSha256(initialization.CheckpointSha256) ||
            initialization.InheritedFeatureCount <= 0 ||
            initialization.NewFeatureCount < 0 ||
            initialization.InheritedFeatureCount +
                initialization.NewFeatureCount != featureCount)
        {
            throw new InvalidDataException(
                "Goal-method checkpoint initialization is invalid.");
        }
    }

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);

    private static bool Finite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool Probability(double value) =>
        Finite(value) && value is >= 0 and <= 1;
}
