using System;
using System.IO;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class FormalTrainingManifestStore
{
    public TrainingRunManifest UpdateArtifacts(
        string manifestPath,
        string runId,
        string datasetManifestPath,
        string checkpointPath,
        string checkpointSha256)
    {
        var fullManifestPath = Path.GetFullPath(Required(manifestPath, "Manifest path"));
        var fullDatasetManifestPath = Path.GetFullPath(Required(datasetManifestPath, "Dataset manifest path"));
        var fullCheckpointPath = Path.GetFullPath(Required(checkpointPath, "Checkpoint path"));
        var manifest = JsonSerializer.Deserialize<TrainingRunManifest>(
            File.ReadAllText(fullManifestPath),
            JsonOptions) ?? throw new InvalidOperationException("Formal training manifest is empty.");
        if (!string.Equals(manifest.SchemaVersion, "training_run_manifest.v2", StringComparison.Ordinal) ||
            !string.Equals(manifest.Mode, TrainingSessionMode.FormalProductTraining, StringComparison.Ordinal) ||
            !string.Equals(manifest.RunId, runId, StringComparison.Ordinal) ||
            !PathsEqual(manifest.ManifestPath, fullManifestPath) ||
            !PathsEqual(manifest.PolicyDatasetManifestPath, fullDatasetManifestPath) ||
            !PathsEqual(manifest.CheckpointPath, fullCheckpointPath))
        {
            throw new InvalidOperationException("Formal training manifest identity changed during structured training.");
        }

        var actualCheckpointHash = StructuredPolicyCheckpointStore.HashFile(fullCheckpointPath);
        if (!string.Equals(actualCheckpointHash, checkpointSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Structured policy checkpoint digest does not match the training result.");
        manifest.PolicyDatasetManifestSha256 = StructuredPolicyCheckpointStore.HashFile(fullDatasetManifestPath);
        manifest.CheckpointSha256 = actualCheckpointHash;
        WriteAtomic(fullManifestPath, manifest);
        return manifest;
    }

    private static void WriteAtomic(string path, TrainingRunManifest manifest)
    {
        var temporaryPath = path + ".tmp." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(manifest, JsonOptions),
                new UTF8Encoding(false));
            File.Replace(temporaryPath, path, null);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(name + " is required.", nameof(value))
            : value;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
