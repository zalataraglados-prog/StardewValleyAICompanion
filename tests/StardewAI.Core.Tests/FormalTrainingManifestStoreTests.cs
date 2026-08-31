using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class FormalTrainingManifestStoreTests
{
    [Fact]
    public void UpdateArtifactsAtomicallyRefreshesFrozenDigests()
    {
        var paths = CreateFixture();
        var checkpointHash = StructuredPolicyCheckpointStore.HashFile(paths.CheckpointPath);

        var updated = new FormalTrainingManifestStore().UpdateArtifacts(
            paths.ManifestPath,
            paths.RunId,
            paths.DatasetManifestPath,
            paths.CheckpointPath,
            checkpointHash);

        Assert.Equal(checkpointHash, updated.CheckpointSha256);
        Assert.Equal(
            StructuredPolicyCheckpointStore.HashFile(paths.DatasetManifestPath),
            updated.PolicyDatasetManifestSha256);
        var persisted = JsonSerializer.Deserialize<TrainingRunManifest>(
            File.ReadAllText(paths.ManifestPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(persisted);
        Assert.Equal(updated.CheckpointSha256, persisted!.CheckpointSha256);
        Assert.Empty(Directory.EnumerateFiles(paths.Root, "*.tmp.*", SearchOption.AllDirectories));
    }

    [Fact]
    public void UpdateArtifactsRejectsRunIdentityDriftWithoutChangingManifest()
    {
        var paths = CreateFixture();
        var before = File.ReadAllText(paths.ManifestPath);
        var checkpointHash = StructuredPolicyCheckpointStore.HashFile(paths.CheckpointPath);

        var error = Assert.Throws<InvalidOperationException>(() =>
            new FormalTrainingManifestStore().UpdateArtifacts(
                paths.ManifestPath,
                "different-run",
                paths.DatasetManifestPath,
                paths.CheckpointPath,
                checkpointHash));

        Assert.Contains("identity changed", error.Message);
        Assert.Equal(before, File.ReadAllText(paths.ManifestPath));
    }

    private static FixturePaths CreateFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-formal-manifest-tests", Guid.NewGuid().ToString("N"));
        var runId = "formal.test." + Guid.NewGuid().ToString("N");
        var runRoot = Path.Combine(root, "runs", runId);
        var datasetManifestPath = Path.Combine(root, "datasets", "formal-policy", "policy-dataset-manifest.json");
        var checkpointPath = Path.Combine(root, "checkpoints", "structured-policy-latest.json");
        var manifestPath = Path.Combine(runRoot, "training-run-manifest.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datasetManifestPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(checkpointPath)!);
        Directory.CreateDirectory(runRoot);
        File.WriteAllText(datasetManifestPath, "{\"schema_version\":\"policy_dataset_manifest.v1\"}");
        File.WriteAllText(checkpointPath, "{\"checkpoint\":\"test\"}");
        var manifest = new TrainingRunManifest
        {
            RunId = runId,
            Mode = TrainingSessionMode.FormalProductTraining,
            RootPath = root,
            ManifestPath = manifestPath,
            PolicyDatasetManifestPath = datasetManifestPath,
            CheckpointPath = checkpointPath,
            Status = "running"
        };
        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return new FixturePaths(root, runId, manifestPath, datasetManifestPath, checkpointPath);
    }

    private sealed record FixturePaths(
        string Root,
        string RunId,
        string ManifestPath,
        string DatasetManifestPath,
        string CheckpointPath);
}
