using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.LiveTrainingLoop;

public sealed class FormalTrainingDataTransaction
{
    private readonly LiveTrainingOptions options;

    private FormalTrainingDataTransaction(
        LiveTrainingOptions options,
        bool active,
        string stagingRoot)
    {
        this.options = options;
        Active = active;
        StagingRoot = stagingRoot;
        Status = active ? "staging" : "not_required";
    }

    public bool Active { get; }
    public string StagingRoot { get; }
    public string Status { get; private set; }
    public bool CanonicalArtifactsUpdated { get; private set; }

    public static FormalTrainingDataTransaction Begin(LiveTrainingOptions options)
    {
        if (options.SkipTraining || !options.RequireNativeSaveBoundary)
        {
            return new FormalTrainingDataTransaction(options, false, string.Empty);
        }

        var stagingRoot = Path.Combine(options.RunDir, "training-transaction");
        if (Directory.Exists(stagingRoot))
        {
            throw new InvalidOperationException(
                "formal_training_transaction_already_exists:" + stagingRoot);
        }
        if (!File.Exists(options.PolicyCheckpointPath))
        {
            throw new FileNotFoundException(
                "Formal training checkpoint is unavailable.",
                options.PolicyCheckpointPath);
        }

        Directory.CreateDirectory(stagingRoot);
        CopyTreeIfPresent(
            Path.Combine(options.Root, "datasets"),
            Path.Combine(stagingRoot, "datasets"));
        var stagedCheckpoint = Path.Combine(
            stagingRoot,
            "checkpoints",
            Path.GetFileName(options.PolicyCheckpointPath));
        Directory.CreateDirectory(Path.GetDirectoryName(stagedCheckpoint)!);
        File.Copy(options.PolicyCheckpointPath, stagedCheckpoint, overwrite: false);
        options.TrainingDataRootOverride = stagingRoot;
        return new FormalTrainingDataTransaction(options, true, stagingRoot);
    }

    public void Complete(bool verifiedTargetMet)
    {
        if (!Active)
        {
            return;
        }
        if (!verifiedTargetMet)
        {
            Status = "staged_not_committed";
            return;
        }

        var stagedDatasetRoot = Path.Combine(StagingRoot, "datasets");
        var canonicalDatasetRoot = Path.Combine(options.Root, "datasets");
        PromoteTree(stagedDatasetRoot, canonicalDatasetRoot);

        var canonicalManifestPath = Path.Combine(
            canonicalDatasetRoot,
            "formal-policy",
            "policy-dataset-manifest.json");
        RebindDatasetManifest(
            Path.Combine(stagedDatasetRoot, "formal-policy", "policy-dataset-manifest.json"),
            canonicalManifestPath,
            stagedDatasetRoot,
            canonicalDatasetRoot);

        var checkpointStore = new StructuredPolicyCheckpointStore();
        var checkpoint = checkpointStore.Load(options.EffectivePolicyCheckpointPath);
        checkpoint.Dataset.ManifestPath = Path.GetFullPath(canonicalManifestPath);
        checkpoint.Dataset.ManifestSha256 = StructuredPolicyCheckpointStore.HashFile(canonicalManifestPath);
        checkpoint.CheckpointId = StructuredPolicyTrainer.CreateCheckpointId(
            checkpoint.Dataset.ManifestSha256,
            checkpoint.Hyperparameters);
        checkpointStore.Save(options.PolicyCheckpointPath, checkpoint);
        CanonicalArtifactsUpdated = true;
        Status = "committed_after_native_save_boundary";
    }

    private static void RebindDatasetManifest(
        string stagedManifestPath,
        string canonicalManifestPath,
        string stagedDatasetRoot,
        string canonicalDatasetRoot)
    {
        if (!File.Exists(stagedManifestPath))
        {
            throw new FileNotFoundException(
                "Staged policy dataset manifest is unavailable.",
                stagedManifestPath);
        }

        PolicyDatasetManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PolicyDatasetManifest>(
                File.ReadAllText(stagedManifestPath),
                JsonOptions)
                ?? throw new InvalidOperationException("Staged policy dataset manifest is null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Staged policy dataset manifest JSON is invalid.", ex);
        }

        RebindDigest(manifest.Input, stagedDatasetRoot, canonicalDatasetRoot);
        if (manifest.HorizonObservations is not null)
            RebindDigest(manifest.HorizonObservations, stagedDatasetRoot, canonicalDatasetRoot);
        RebindDigest(manifest.Cleaned, stagedDatasetRoot, canonicalDatasetRoot);
        foreach (var partition in manifest.Partitions ?? Array.Empty<PolicyDatasetPartitionDigest>())
            RebindDigest(partition, stagedDatasetRoot, canonicalDatasetRoot);

        WriteJsonAtomically(canonicalManifestPath, manifest);
    }

    private static void RebindDigest(
        PolicyDatasetFileDigest digest,
        string stagedDatasetRoot,
        string canonicalDatasetRoot)
    {
        if (digest is null || string.IsNullOrWhiteSpace(digest.Path))
            throw new InvalidOperationException("Policy dataset manifest contains an empty artifact path.");

        var fullPath = Path.GetFullPath(digest.Path);
        var relative = Path.GetRelativePath(Path.GetFullPath(stagedDatasetRoot), fullPath);
        if (relative == "." ||
            relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            var canonicalRelative = Path.GetRelativePath(
                Path.GetFullPath(canonicalDatasetRoot),
                fullPath);
            if (canonicalRelative == "." ||
                canonicalRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                Path.IsPathRooted(canonicalRelative))
            {
                throw new InvalidOperationException(
                    "Policy dataset artifact path is outside the staged and canonical roots: " + fullPath);
            }
            relative = canonicalRelative;
        }

        digest.Path = Path.GetFullPath(Path.Combine(canonicalDatasetRoot, relative));
    }

    private static void WriteJsonAtomically(string path, PolicyDatasetManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var pending = path + ".pending." + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(
                pending,
                JsonSerializer.Serialize(manifest, IndentedJsonOptions),
                new UTF8Encoding(false));
            File.Move(pending, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(pending))
                File.Delete(pending);
        }
    }

    private static void CopyTreeIfPresent(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (var source in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, source);
            var target = Path.Combine(targetRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: false);
        }
    }

    private static void PromoteTree(string sourceRoot, string targetRoot)
    {
        if (!Directory.Exists(sourceRoot))
        {
            return;
        }

        foreach (var source in Directory.EnumerateFiles(
                     sourceRoot,
                     "*",
                     SearchOption.AllDirectories))
        {
            PromoteFile(
                source,
                Path.Combine(targetRoot, Path.GetRelativePath(sourceRoot, source)));
        }
    }

    private static void PromoteFile(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var pending = destination + ".pending." + Guid.NewGuid().ToString("N");
        try
        {
            File.Copy(source, pending, overwrite: false);
            File.Move(pending, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(pending))
            {
                File.Delete(pending);
            }
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions IndentedJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
