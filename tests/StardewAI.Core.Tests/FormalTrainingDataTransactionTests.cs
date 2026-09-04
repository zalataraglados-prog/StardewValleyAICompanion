using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class FormalTrainingDataTransactionTests
{
    [Fact]
    public void FailedNativeBoundaryLeavesCanonicalArtifactsUnchanged()
    {
        var fixture = CreateFixture("failed");
        var baselineCheckpoint = File.ReadAllText(fixture.CanonicalCheckpoint);
        var transaction = FormalTrainingDataTransaction.Begin(fixture.Options);

        File.AppendAllText(fixture.Options.DatasetPath, "staged\n");
        File.WriteAllText(fixture.Options.EffectivePolicyCheckpointPath, "staged-checkpoint");
        transaction.Complete(verifiedTargetMet: false);

        Assert.Equal("baseline\n", File.ReadAllText(fixture.CanonicalDataset));
        Assert.Equal(baselineCheckpoint, File.ReadAllText(fixture.CanonicalCheckpoint));
        Assert.Equal("staged_not_committed", transaction.Status);
        Assert.False(transaction.CanonicalArtifactsUpdated);
        Assert.True(File.Exists(fixture.Options.DatasetPath));
    }

    [Fact]
    public void VerifiedNativeBoundaryPromotesStagedArtifacts()
    {
        var fixture = CreateFixture("verified");
        var transaction = FormalTrainingDataTransaction.Begin(fixture.Options);

        File.AppendAllText(fixture.Options.DatasetPath, "staged\n");
        WriteStagedModelArtifacts(fixture, transaction.StagingRoot);
        transaction.Complete(verifiedTargetMet: true);

        Assert.Equal("baseline\nstaged\n", File.ReadAllText(fixture.CanonicalDataset));
        Assert.Equal("committed_after_native_save_boundary", transaction.Status);
        Assert.True(transaction.CanonicalArtifactsUpdated);

        var canonicalManifestPath = Path.Combine(
            fixture.Options.Root,
            "datasets",
            "formal-policy",
            "policy-dataset-manifest.json");
        var canonicalManifest = JsonSerializer.Deserialize<PolicyDatasetManifest>(
            File.ReadAllText(canonicalManifestPath),
            JsonOptions)!;
        var canonicalDatasetRoot = Path.Combine(fixture.Options.Root, "datasets");
        Assert.All(ManifestDigests(canonicalManifest), digest =>
        {
            Assert.StartsWith(Path.GetFullPath(canonicalDatasetRoot), Path.GetFullPath(digest.Path));
            Assert.DoesNotContain(transaction.StagingRoot, digest.Path, StringComparison.OrdinalIgnoreCase);
        });

        var checkpoint = new StructuredPolicyCheckpointStore().Load(fixture.CanonicalCheckpoint);
        Assert.Equal(Path.GetFullPath(canonicalManifestPath), checkpoint.Dataset.ManifestPath);
        Assert.Equal(
            StructuredPolicyCheckpointStore.HashFile(canonicalManifestPath),
            checkpoint.Dataset.ManifestSha256);
        Assert.Equal(
            StructuredPolicyTrainer.CreateCheckpointId(
                checkpoint.Dataset.ManifestSha256,
                checkpoint.Hyperparameters),
            checkpoint.CheckpointId);
    }

    [Fact]
    public void PromotedArtifactsPassTheNextFormalCheckpointBindingGate()
    {
        var fixture = CreateFixture("next-round");
        var transaction = FormalTrainingDataTransaction.Begin(fixture.Options);
        WriteStagedModelArtifacts(fixture, transaction.StagingRoot);
        transaction.Complete(verifiedTargetMet: true);

        var canonicalManifestPath = Path.Combine(
            fixture.Options.Root,
            "datasets",
            "formal-policy",
            "policy-dataset-manifest.json");
        var checkpoint = new StructuredPolicyCheckpointStore().Load(fixture.CanonicalCheckpoint);

        Assert.Equal(Path.GetFullPath(canonicalManifestPath), checkpoint.Dataset.ManifestPath);
        Assert.Equal(
            StructuredPolicyCheckpointStore.HashFile(canonicalManifestPath),
            checkpoint.Dataset.ManifestSha256);
        Assert.All(ManifestDigests(
            JsonSerializer.Deserialize<PolicyDatasetManifest>(
                File.ReadAllText(canonicalManifestPath),
                JsonOptions)!),
            digest =>
            {
                Assert.True(File.Exists(digest.Path));
                Assert.Equal(
                    StructuredPolicyCheckpointStore.HashFile(digest.Path),
                    digest.Sha256);
            });
    }

    private static Fixture CreateFixture(string name)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-training-transaction-tests",
            name + "." + Guid.NewGuid().ToString("N"));
        var runDir = Path.Combine(root, "runs", "run." + name);
        var manifest = Path.Combine(runDir, "training-run-manifest.json");
        var dataset = Path.Combine(root, "datasets", "live-training-feature-rows.jsonl");
        var checkpoint = Path.Combine(root, "checkpoints", "structured-policy-latest.json");
        Directory.CreateDirectory(runDir);
        Directory.CreateDirectory(Path.GetDirectoryName(dataset)!);
        Directory.CreateDirectory(Path.GetDirectoryName(checkpoint)!);
        File.WriteAllText(manifest, "{}");
        File.WriteAllText(dataset, "baseline\n");
        WriteCanonicalModelArtifacts(root, checkpoint);
        var options = new LiveTrainingOptions
        {
            Root = root,
            RunId = "run." + name,
            ManifestPath = manifest,
            PolicyCheckpointPath = checkpoint,
            RequireNativeSaveBoundary = true,
            RequireStructuredPolicy = true,
            UseProductExecutor = true,
            UseDailyPlan = true
        };
        return new Fixture(options, dataset, checkpoint);
    }

    private static void WriteCanonicalModelArtifacts(string root, string checkpointPath)
    {
        var datasetRoot = Path.Combine(root, "datasets");
        var manifestPath = Path.Combine(datasetRoot, "formal-policy", "policy-dataset-manifest.json");
        var manifest = CreateManifest(datasetRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, IndentedJsonOptions));
        WriteCheckpoint(checkpointPath, manifestPath, manifest);
    }

    private static void WriteStagedModelArtifacts(Fixture fixture, string stagingRoot)
    {
        var stagedDatasetRoot = Path.Combine(stagingRoot, "datasets");
        var manifestPath = Path.Combine(stagedDatasetRoot, "formal-policy", "policy-dataset-manifest.json");
        var manifest = CreateManifest(stagedDatasetRoot);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, IndentedJsonOptions));
        WriteCheckpoint(fixture.Options.EffectivePolicyCheckpointPath, manifestPath, manifest);
    }

    private static PolicyDatasetManifest CreateManifest(string datasetRoot)
    {
        var input = WriteDatasetFile(datasetRoot, "policy-decision-trajectories.jsonl", "input\n", 1);
        var horizon = WriteDatasetFile(datasetRoot, "policy-horizon-observations.jsonl", "horizon\n", 1);
        var formalRoot = Path.Combine(datasetRoot, "formal-policy");
        var cleaned = WriteDatasetFile(formalRoot, "policy-trajectories.cleaned.jsonl", "cleaned\n", 1);
        var train = WritePartition(formalRoot, "policy-trajectories.train.jsonl", "train", "train\n", 1);
        var validation = WritePartition(formalRoot, "policy-trajectories.validation.jsonl", "validation", string.Empty, 0);
        var test = WritePartition(formalRoot, "policy-trajectories.test.jsonl", "test", string.Empty, 0);
        WriteDatasetFile(formalRoot, "policy-trajectories.rejections.jsonl", string.Empty, 0);
        return new PolicyDatasetManifest
        {
            Input = input,
            HorizonObservations = horizon,
            Cleaned = cleaned,
            Counts = new PolicyDatasetCounts { InputLines = 1, AcceptedRows = 1 },
            Partitions = new[] { train, validation, test },
            VersionSets = new[]
            {
                new PolicyDatasetVersionSet
                {
                    FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                    CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                    CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                    KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                    Compiler = PolicyTrajectoryVersionPins.Compiler,
                    Executor = PolicyTrajectoryVersionPins.ProductExecutor,
                    RowCount = 1
                }
            }
        };
    }

    private static PolicyDatasetFileDigest WriteDatasetFile(
        string root,
        string name,
        string content,
        int rows)
    {
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        return new PolicyDatasetFileDigest
        {
            Path = path,
            Sha256 = StructuredPolicyCheckpointStore.HashFile(path),
            Bytes = new FileInfo(path).Length,
            Rows = rows
        };
    }

    private static PolicyDatasetPartitionDigest WritePartition(
        string root,
        string name,
        string partition,
        string content,
        int rows)
    {
        var digest = WriteDatasetFile(root, name, content, rows);
        return new PolicyDatasetPartitionDigest
        {
            Partition = partition,
            Path = digest.Path,
            Sha256 = digest.Sha256,
            Bytes = digest.Bytes,
            Rows = digest.Rows
        };
    }

    private static void WriteCheckpoint(
        string checkpointPath,
        string manifestPath,
        PolicyDatasetManifest manifest)
    {
        var manifestSha256 = StructuredPolicyCheckpointStore.HashFile(manifestPath);
        var hyperparameters = new StructuredPolicyHyperparameters();
        var checkpoint = new StructuredPolicyCheckpointEnvelope
        {
            CheckpointId = StructuredPolicyTrainer.CreateCheckpointId(manifestSha256, hyperparameters),
            Dataset = new StructuredPolicyDatasetBinding
            {
                ManifestPath = Path.GetFullPath(manifestPath),
                ManifestSha256 = manifestSha256,
                CleanedSha256 = manifest.Cleaned.Sha256,
                TrainSha256 = manifest.Partitions.Single(value => value.Partition == "train").Sha256,
                ValidationSha256 = manifest.Partitions.Single(value => value.Partition == "validation").Sha256,
                TestSha256 = manifest.Partitions.Single(value => value.Partition == "test").Sha256
            },
            Versions = manifest.VersionSets.Single(),
            Hyperparameters = hyperparameters,
            Model = new StructuredPolicyLinearModel
            {
                FeatureNames = new[] { "candidate.boolean:available" },
                FeatureMeans = new[] { 0d },
                FeatureScales = new[] { 1d },
                Weights = new[] { 0d }
            },
            Training = new StructuredPolicyTrainingSummary
            {
                TrainRows = 1,
                TrainPairs = 1,
                FeatureCount = 1,
                TrainPairAccuracy = 1
            }
        };
        new StructuredPolicyCheckpointStore().Save(checkpointPath, checkpoint);
    }

    private static IEnumerable<PolicyDatasetFileDigest> ManifestDigests(PolicyDatasetManifest manifest)
    {
        yield return manifest.Input;
        if (manifest.HorizonObservations is not null)
            yield return manifest.HorizonObservations;
        yield return manifest.Cleaned;
        foreach (var partition in manifest.Partitions)
            yield return partition;
    }

    private sealed record Fixture(
        LiveTrainingOptions Options,
        string CanonicalDataset,
        string CanonicalCheckpoint);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
