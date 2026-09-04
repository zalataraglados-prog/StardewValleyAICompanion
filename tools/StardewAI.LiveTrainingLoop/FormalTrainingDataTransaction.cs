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

        PromoteTree(
            Path.Combine(StagingRoot, "datasets"),
            Path.Combine(options.Root, "datasets"));
        PromoteFile(
            options.EffectivePolicyCheckpointPath,
            options.PolicyCheckpointPath);
        CanonicalArtifactsUpdated = true;
        Status = "committed_after_native_save_boundary";
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
}
