using System.Globalization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class GoalMethodPairwiseTrainer
{
    private readonly GoalMethodPairwiseCheckpointStore checkpointStore;

    public GoalMethodPairwiseTrainer()
        : this(new GoalMethodPairwiseCheckpointStore())
    {
    }

    public GoalMethodPairwiseTrainer(
        GoalMethodPairwiseCheckpointStore checkpointStore)
    {
        this.checkpointStore = checkpointStore;
    }

    public GoalMethodPairwiseTrainingResult Train(
        string corpusManifestPath,
        string checkpointPath,
        GoalMethodPairwiseHyperparameters? hyperparameters = null,
        string? initializationCheckpointPath = null)
    {
        var manifestPath = RequiredFullPath(
            corpusManifestPath,
            "Corpus manifest path");
        var parameters = hyperparameters ??
            new GoalMethodPairwiseHyperparameters();
        GoalMethodPairwiseCheckpointStore.ValidateHyperparameters(parameters);
        var corpus = VerifyCorpus(manifestPath);

        GoalMethodPairwiseCheckpoint? initialization = null;
        var initializationHash = string.Empty;
        if (!string.IsNullOrWhiteSpace(initializationCheckpointPath))
        {
            var initializationPath = Path.GetFullPath(
                initializationCheckpointPath);
            initialization = checkpointStore.Load(initializationPath);
            initializationHash = CurrentTeacherFrontierSupport.HashFile(
                initializationPath);
            ValidateInitializationVersions(
                initialization.Versions,
                corpus.Versions);
        }

        var featureNames = GoalMethodPairwiseFeatureEncoder
            .DiscoverFeatureNames(corpus.TrainRows)
            .Concat(initialization?.Model.FeatureNames ??
                Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (featureNames.Length == 0)
            throw new InvalidDataException(
                "Goal-method feature vocabulary is empty.");
        var model = GoalMethodPairwiseFeatureEncoder.FitModelShape(
            corpus.TrainRows,
            featureNames);
        var inheritedFeatureCount = initialization is null
            ? 0
            : GoalMethodPairwiseFeatureEncoder.InitializeWeights(
                model,
                initialization.Model);
        var trainPairs = BuildPairs(corpus.TrainRows, model);
        if (trainPairs.Length == 0)
            throw new InvalidDataException(
                "Goal-method training partition contains no explicit Teacher pairs.");
        Optimize(model.Weights, trainPairs, parameters);

        var manifestSha256 = CurrentTeacherFrontierSupport.HashFile(
            manifestPath);
        var checkpoint = new GoalMethodPairwiseCheckpoint
        {
            CheckpointId = CreateCheckpointId(
                manifestSha256,
                parameters,
                initializationHash),
            Dataset = new GoalMethodPairwiseDatasetBinding
            {
                CorpusManifestPath = manifestPath,
                CorpusManifestSha256 = manifestSha256,
                CleanedSha256 = corpus.Manifest.Cleaned.Sha256,
                TrainSha256 = corpus.TrainDigest.Sha256,
                ValidationSha256 = corpus.ValidationDigest.Sha256,
                TestSha256 = corpus.TestDigest.Sha256,
                SourceCount = corpus.Manifest.Sources.Length
            },
            Versions = corpus.Versions,
            Hyperparameters = Copy(parameters),
            Model = model,
            Training = BuildSummary(
                model,
                corpus.TrainRows,
                corpus.ValidationRows,
                corpus.TestRows,
                trainPairs),
            Initialization = initialization is null
                ? null
                : new GoalMethodPairwiseInitializationBinding
                {
                    CheckpointId = initialization.CheckpointId,
                    CheckpointSha256 = initializationHash,
                    InheritedFeatureCount = inheritedFeatureCount,
                    NewFeatureCount = featureNames.Length -
                        inheritedFeatureCount
                },
            UsesCandidateSelectedFlag = false,
            FormalProductTrainingAuthorized = false
        };
        var fullCheckpointPath = RequiredFullPath(
            checkpointPath,
            "Checkpoint path");
        var checkpointSha256 = checkpointStore.Save(
            fullCheckpointPath,
            checkpoint);
        return new GoalMethodPairwiseTrainingResult
        {
            CheckpointPath = fullCheckpointPath,
            CheckpointSha256 = checkpointSha256,
            Checkpoint = checkpoint
        };
    }

    public static string CreateCheckpointId(
        string manifestSha256,
        GoalMethodPairwiseHyperparameters hyperparameters,
        string? initializationCheckpointSha256 = null)
    {
        if (!IsSha256(manifestSha256))
            throw new ArgumentException(
                "Corpus manifest SHA-256 is invalid.",
                nameof(manifestSha256));
        GoalMethodPairwiseCheckpointStore.ValidateHyperparameters(
            hyperparameters);
        var initialization = string.IsNullOrWhiteSpace(
            initializationCheckpointSha256)
            ? "none"
            : initializationCheckpointSha256.ToLowerInvariant();
        if (initialization != "none" && !IsSha256(initialization))
            throw new ArgumentException(
                "Initialization checkpoint SHA-256 is invalid.",
                nameof(initializationCheckpointSha256));
        var identity = manifestSha256.ToLowerInvariant() + "\n" +
            initialization + "\n" +
            hyperparameters.Epochs.ToString(CultureInfo.InvariantCulture) +
            "|" + hyperparameters.LearningRate.ToString(
                "R",
                CultureInfo.InvariantCulture) + "|" +
            hyperparameters.L2Regularization.ToString(
                "R",
                CultureInfo.InvariantCulture);
        return "goal-method-" + GoalMethodPairwiseCheckpointStore
            .HashText(identity)[..24];
    }

    private static void ValidateInitializationVersions(
        GoalMethodPairwiseVersionBinding prior,
        GoalMethodPairwiseVersionBinding current)
    {
        if (prior.FeatureSchema != current.FeatureSchema ||
            prior.CorpusSchema != current.CorpusSchema ||
            prior.SupervisionRowSchema != current.SupervisionRowSchema ||
            prior.GameVersion != current.GameVersion ||
            prior.BridgeVersion != current.BridgeVersion)
        {
            throw new InvalidDataException(
                "Goal-method initialization version binding differs from the corpus.");
        }
    }

    private static GoalMethodPairwiseHyperparameters Copy(
        GoalMethodPairwiseHyperparameters value) => new()
    {
        Epochs = value.Epochs,
        LearningRate = value.LearningRate,
        L2Regularization = value.L2Regularization
    };

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);

    private static bool IsSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or
            >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
